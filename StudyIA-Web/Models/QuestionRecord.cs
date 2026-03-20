namespace StudyIA_Web.Models;

public class QuestionRecord
{
    public int      Id             { get; set; }
    public int      PdfFileId      { get; set; }
    public int      PageNumber     { get; set; }
    public string   Context        { get; set; } = string.Empty;
    public string   QuestionText   { get; set; } = string.Empty;
    public string   ExpectedAnswer { get; set; } = string.Empty;
    public DateTime CreatedAt      { get; set; }
    public int?     PdfSectionId   { get; set; }
    public string   FileName       { get; set; } = string.Empty;

    /// <summary>Devuelve una copia con el FileName indicado, sin mutar el objeto original.</summary>
    public QuestionRecord WithFileName(string fileName) => new()
    {
        Id             = Id,
        PdfFileId      = PdfFileId,
        PageNumber     = PageNumber,
        Context        = Context,
        QuestionText   = QuestionText,
        ExpectedAnswer = ExpectedAnswer,
        CreatedAt      = CreatedAt,
        PdfSectionId   = PdfSectionId,
        FileName       = fileName
    };
}
