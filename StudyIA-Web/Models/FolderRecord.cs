namespace StudyIA_Web.Models;

public class FolderRecord
{
    public int      Id         { get; set; }
    public string   Name       { get; set; } = string.Empty;
    public string   FolderPath { get; set; } = string.Empty;
    public DateTime CreatedAt  { get; set; }
}
