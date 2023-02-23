namespace SlothImport.Models;

public class ImportOptions
{
    public string BaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public FileInfo CsvFile { get; set; } = null!;
}
