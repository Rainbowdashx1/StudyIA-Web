namespace StudyIA_Web.Models;

public class PdfSection
{
    public int    Id        { get; set; }
    public int    PdfFileId { get; set; }
    public string Title     { get; set; } = string.Empty;
    public int    StartPage { get; set; }
    public int    EndPage   { get; set; }

    public string PageRange =>
        StartPage == EndPage ? $"p.{StartPage}" : $"p.{StartPage}–{EndPage}";
}
