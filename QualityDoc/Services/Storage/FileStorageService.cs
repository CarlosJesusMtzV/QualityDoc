using System.Security.Cryptography;

namespace QualityDoc.Services.Storage;

public class FileStorageService : IFileStorageService
{
    private readonly string _basePath;

    public FileStorageService(IConfiguration config, IWebHostEnvironment env)
    {
        var configured = config["Storage:Path"] ?? "storage";
        _basePath = Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(env.ContentRootPath, configured);
        Directory.CreateDirectory(_basePath);
    }

    public async Task<StoredFile> GuardarAsync(string empresaSlug, int documentoId, string versionTag, IFormFile file, string nombreBase)
    {
        var ext = Path.GetExtension(file.FileName);                 // incluye el punto, p.ej ".pdf"
        var baseSan = Sanitizar(nombreBase);
        if (string.IsNullOrWhiteSpace(baseSan))
            baseSan = Path.GetFileNameWithoutExtension(file.FileName);
        var safeName = baseSan + ext;

        var relDir = Path.Combine(empresaSlug, documentoId.ToString(), versionTag);
        var absDir = Path.Combine(_basePath, relDir);
        Directory.CreateDirectory(absDir);

        var absPath = Path.Combine(absDir, safeName);
        await using (var fs = new FileStream(absPath, FileMode.Create))
            await file.CopyToAsync(fs);

        string hash;
        await using (var read = new FileStream(absPath, FileMode.Open, FileAccess.Read))
        using (var sha = SHA256.Create())
            hash = Convert.ToHexString(await sha.ComputeHashAsync(read)).ToLowerInvariant();

        var tipo = Path.GetExtension(safeName).TrimStart('.').ToLowerInvariant();
        var rel = Path.Combine(relDir, safeName).Replace('\\', '/');
        return new StoredFile(rel, safeName, tipo, hash, file.Length);
    }

    public (Stream Stream, string Nombre)? Abrir(string rutaRelativa, string nombre)
    {
        var abs = Path.Combine(_basePath, rutaRelativa.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(abs)) return null;
        Stream stream = new FileStream(abs, FileMode.Open, FileAccess.Read);
        return (stream, nombre);
    }

    private static string Sanitizar(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, ' ');
        s = s.Replace('/', ' ').Replace('\\', ' ').Trim();
        while (s.Contains("  ")) s = s.Replace("  ", " ");
        return s;
    }
}
