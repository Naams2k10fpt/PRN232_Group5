namespace BusinessObjects;

public sealed class QuizResultSnapshot
{
    public string DeckName { get; set; } = string.Empty;

    public string SubjectName { get; set; } = string.Empty;

    public List<QuizResultQuestionSnapshot> Questions { get; set; } = [];
}

public sealed class QuizResultQuestionSnapshot
{
    public int QuestionId { get; set; }

    public string Content { get; set; } = string.Empty;

    public string? Explanation { get; set; }

    public int QuestionType { get; set; }

    public bool IsCorrect { get; set; }

    public List<QuizResultAnswerSnapshot> Answers { get; set; } = [];
}

public sealed class QuizResultAnswerSnapshot
{
    public int AnswerId { get; set; }

    public string Content { get; set; } = string.Empty;

    public bool IsCorrectAnswer { get; set; }

    public bool WasSelected { get; set; }
}
