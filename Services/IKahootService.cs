using System.Collections.Generic;
using System.Threading.Tasks;
using BusinessObjects;

namespace Services;

public class KahootLeaderboardEntry
{
    public int ParticipantId { get; set; }
    public string Nickname { get; set; } = null!;
    public int Score { get; set; }
    public int Rank { get; set; }
}

public class KahootAnswerResult
{
    public bool IsCorrect { get; set; }
    public int ScoreEarned { get; set; }
    public int TotalScore { get; set; }
    public int CorrectAnswerId { get; set; }
}

public interface IKahootService
{
    Task<KahootRoom> CreateRoomAsync(int deckId, string title, string hostUserId);
    Task<KahootRoom?> GetRoomByCodeAsync(string? roomCode);
    Task<KahootParticipant> JoinRoomAsync(string? roomCode, string nickname, string? userId, string? connectionId);
    Task<List<Question>> GetRoomQuestionsAsync(int roomId);
    Task<KahootAnswerResult> SubmitAnswerAsync(string roomCode, int participantId, int questionId, int selectedAnswerId, decimal elapsedSeconds, int timeLimitSeconds);
    Task<List<KahootLeaderboardEntry>> GetLeaderboardAsync(string? roomCode);
    Task<bool> UpdateRoomStatusAsync(string? roomCode, string status, int currentQuestionIndex);
    Task<List<KahootParticipant>> GetParticipantsAsync(string? roomCode);
    Task<List<Deck>> GetAvailableDecksAsync();
}
