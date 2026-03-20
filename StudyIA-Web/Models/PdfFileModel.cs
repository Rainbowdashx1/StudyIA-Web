namespace StudyIA_Web.Models;

public enum PdfStatus { New, Unchanged, Modified }

public class PdfFileModel
{
    public string    FileName        { get; }
    public string    FileSizeDisplay { get; }
    public string    HashShort       { get; }
    public string    Status          { get; }
    public string    LastSeenDisplay { get; }
    public PdfStatus RawStatus       { get; }

    public PdfFileModel(string fileName, long fileSize, string hash,
                        PdfStatus status, string? error = null)
    {
        FileName        = fileName;
        FileSizeDisplay = FormatSize(fileSize);
        HashShort       = hash.Length >= 8 ? hash[..8] + "..." : hash;
        RawStatus       = status;
        LastSeenDisplay = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        Status = error is not null            ? "Error"
               : status == PdfStatus.New      ? "Nuevo"
               : status == PdfStatus.Modified ? "Modificado"
                                              : "Sin cambios";
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F1} GB",
        >= 1_048_576     => $"{bytes / 1_048_576.0:F1} MB",
        >= 1_024         => $"{bytes / 1_024.0:F1} KB",
        _                => $"{bytes} B"
    };
}
