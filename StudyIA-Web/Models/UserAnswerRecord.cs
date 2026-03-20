namespace StudyIA_Web.Models;

public class UserAnswerRecord
{
    public int      Id         { get; set; }
    public int      QuestionId { get; set; }
    public string   UserAnswer { get; set; } = string.Empty;
    public double   Score      { get; set; }
    public string   Feedback   { get; set; } = string.Empty;
    public DateTime AnsweredAt { get; set; }
}
