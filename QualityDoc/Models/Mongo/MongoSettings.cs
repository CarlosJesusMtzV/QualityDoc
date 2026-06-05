namespace QualityDoc.Models.Mongo;

public class MongoSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public string Database { get; set; } = "qualitydoc_meta";
}
