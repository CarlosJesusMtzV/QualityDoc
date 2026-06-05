using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace QualityDoc.Services.Extraction;

/// <summary>
/// Extrae metadatos sin librerías externas:
///  - PDF: diccionario Info (Título, Autor, Asunto, PalabrasClave, fechas) y nº de páginas.
///  - OOXML (docx/xlsx/pptx): docProps/core.xml y app.xml (es un ZIP).
///  - Imágenes (png/jpg/gif/bmp): dimensiones.
/// </summary>
public class MetadataExtractor : IMetadataExtractor
{
    public ExtraccionResultado Extraer(Stream contenido, string nombreArchivo, string? tipo)
    {
        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            contenido.CopyTo(ms);
            bytes = ms.ToArray();
        }

        var props = new Dictionary<string, string>
        {
            ["Nombre"] = nombreArchivo,
            ["TamanioBytes"] = bytes.Length.ToString()
        };
        var ext = (tipo ?? Path.GetExtension(nombreArchivo).TrimStart('.')).ToLowerInvariant();
        if (!string.IsNullOrEmpty(ext)) props["Extension"] = ext;

        try
        {
            switch (ext)
            {
                case "pdf": ExtraerPdf(bytes, props); break;
                case "docx": case "docm":
                case "xlsx": case "xlsm":
                case "pptx": case "pptm": ExtraerOoxml(bytes, props); break;
                case "png": case "jpg": case "jpeg": case "gif": case "bmp":
                    ExtraerImagen(bytes, ext, props); break;
            }
        }
        catch { /* extracción best-effort: nunca rompe la subida */ }

        // Texto para búsqueda = todos los valores de metadatos (así son buscables en Mongo).
        var texto = string.Join(" ", props.Values);
        return new ExtraccionResultado(props, texto);
    }

    private static void ExtraerPdf(byte[] bytes, Dictionary<string, string> props)
    {
        var text = Encoding.Latin1.GetString(bytes);
        Add(props, "Título",        Match(text, @"/Title\s*\(([^)]*)\)"));
        Add(props, "Autor",         Match(text, @"/Author\s*\(([^)]*)\)"));
        Add(props, "Asunto",        Match(text, @"/Subject\s*\(([^)]*)\)"));
        Add(props, "PalabrasClave", Match(text, @"/Keywords\s*\(([^)]*)\)"));
        Add(props, "Creado",        Match(text, @"/CreationDate\s*\(([^)]*)\)"));
        Add(props, "Productor",     Match(text, @"/Producer\s*\(([^)]*)\)"));

        var paginas = Regex.Matches(text, @"/Type\s*/Page[^s]").Count;
        if (paginas > 0) props["Paginas"] = paginas.ToString();
        props["Formato"] = "PDF";
    }

    private static void ExtraerOoxml(byte[] bytes, Dictionary<string, string> props)
    {
        using var ms = new MemoryStream(bytes);
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);

        XNamespace dc = "http://purl.org/dc/elements/1.1/";
        XNamespace cp = "http://schemas.openxmlformats.org/package/2006/metadata/core-properties";
        XNamespace dcterms = "http://purl.org/dc/terms/";

        var core = zip.GetEntry("docProps/core.xml");
        if (core is not null)
        {
            using var s = core.Open();
            var xml = XDocument.Load(s);
            Add(props, "Título",        xml.Descendants(dc + "title").FirstOrDefault()?.Value);
            Add(props, "Autor",         xml.Descendants(dc + "creator").FirstOrDefault()?.Value);
            Add(props, "Asunto",        xml.Descendants(dc + "subject").FirstOrDefault()?.Value);
            Add(props, "PalabrasClave", xml.Descendants(cp + "keywords").FirstOrDefault()?.Value);
            Add(props, "ModificadoPor", xml.Descendants(cp + "lastModifiedBy").FirstOrDefault()?.Value);
            Add(props, "Creado",        xml.Descendants(dcterms + "created").FirstOrDefault()?.Value);
            Add(props, "Modificado",    xml.Descendants(dcterms + "modified").FirstOrDefault()?.Value);
        }

        XNamespace ep = "http://schemas.openxmlformats.org/officeDocument/2006/extended-properties";
        var app = zip.GetEntry("docProps/app.xml");
        if (app is not null)
        {
            using var s = app.Open();
            var xml = XDocument.Load(s);
            Add(props, "Aplicacion", xml.Descendants(ep + "Application").FirstOrDefault()?.Value);
            Add(props, "Paginas",    xml.Descendants(ep + "Pages").FirstOrDefault()?.Value);
            Add(props, "Palabras",   xml.Descendants(ep + "Words").FirstOrDefault()?.Value);
            Add(props, "Empresa",    xml.Descendants(ep + "Company").FirstOrDefault()?.Value);
        }
    }

    private static void ExtraerImagen(byte[] b, string ext, Dictionary<string, string> props)
    {
        int w = 0, h = 0;
        if (ext == "png" && b.Length > 24)
        {
            w = (b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19];
            h = (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23];
        }
        else if (ext == "gif" && b.Length > 10)
        {
            w = b[6] | (b[7] << 8);
            h = b[8] | (b[9] << 8);
        }
        else if (ext == "bmp" && b.Length > 26)
        {
            w = b[18] | (b[19] << 8) | (b[20] << 16) | (b[21] << 24);
            h = b[22] | (b[23] << 8) | (b[24] << 16) | (b[25] << 24);
        }
        else if (ext is "jpg" or "jpeg")
        {
            int i = 2;
            while (i + 9 < b.Length)
            {
                if (b[i] != 0xFF) { i++; continue; }
                int marker = b[i + 1];
                // SOF markers con dimensiones (excepto DHT/DAC/DRI/RST/SOI/EOI)
                if (marker is >= 0xC0 and <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC)
                {
                    h = (b[i + 5] << 8) | b[i + 6];
                    w = (b[i + 7] << 8) | b[i + 8];
                    break;
                }
                int len = (b[i + 2] << 8) | b[i + 3];
                i += 2 + len;
            }
        }
        if (w > 0 && h > 0)
        {
            props["Ancho"] = w.ToString();
            props["Alto"] = h.ToString();
            props["Dimensiones"] = $"{w}x{h}";
        }
        props["Formato"] = ext.ToUpperInvariant();
    }

    private static string? Match(string text, string pattern)
    {
        var m = Regex.Match(text, pattern);
        return m.Success ? m.Groups[1].Value : null;
    }

    private static void Add(Dictionary<string, string> props, string clave, string? valor)
    {
        if (!string.IsNullOrWhiteSpace(valor)) props[clave] = valor.Trim();
    }
}
