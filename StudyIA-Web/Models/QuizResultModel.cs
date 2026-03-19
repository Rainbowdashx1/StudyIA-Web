namespace StudyIA_Web.Models;

public class QuizResultModel
{
    public int Number { get; set; }
    public string Question { get; set; } = string.Empty;
    public string UserAnswer { get; set; } = string.Empty;
    public int Score { get; set; }
    public string ScoreDisplay { get; set; } = string.Empty;
    public string Feedback { get; set; } = string.Empty;
}
