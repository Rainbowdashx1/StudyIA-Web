namespace StudyIA_Web.Models;

public class PdfFileModel
{
    public string FileName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string FileSizeDisplay { get; set; } = string.Empty;
    public string HashShort { get; set; } = string.Empty;
    public string LastSeenDisplay { get; set; } = string.Empty;
}
