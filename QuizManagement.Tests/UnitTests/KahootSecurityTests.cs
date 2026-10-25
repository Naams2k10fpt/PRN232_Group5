using BusinessObjects;
using Repositories;
using Services;
using Xunit;

namespace QuizManagement.Tests.UnitTests;

public class KahootSecurityTests
{
    private sealed class FakeKahootRepository : IKahootRepository
    {
        public List<KahootRoom> Rooms { get; } = new();
        public List<KahootParticipant> Participants { get; } = new();
        public List<KahootAnswer> Answers { get; } = new();
        public List<Question> Questions { get; } = new();
        public int SaveAnswerCallCount { get; private set; }

        public KahootRoom CreateRoom(KahootRoom room)
        {
            room.Id = Rooms.Count + 1;
            Rooms.Add(room);
            return room;
        }

        public KahootRoom? GetRoomByCode(string roomCode)
            => Rooms.FirstOrDefault(r => string.Equals(r.RoomCode, roomCode, StringComparison.OrdinalIgnoreCase));

        public KahootRoom? GetRoomById(int id) => Rooms.FirstOrDefault(r => r.Id == id);

        public KahootParticipant AddParticipant(KahootParticipant participant)
        {
            participant.Id = Participants.Count + 1;
            participant.Room ??= Rooms.FirstOrDefault(r => r.Id == participant.RoomId)!;
            Participants.Add(participant);
            return participant;
        }

        public KahootParticipant? GetParticipant(int participantId)
            => Participants.FirstOrDefault(p => p.Id == participantId);

        public List<KahootParticipant> GetParticipantsByRoom(string roomCode)
            => Participants.Where(p => p.Room?.RoomCode == roomCode).ToList();

        public KahootAnswer? GetAnswer(int participantId, int questionId)
            => Answers.FirstOrDefault(a => a.ParticipantId == participantId && a.QuestionId == questionId);

        public void SaveAnswer(KahootAnswer answer)
        {
            SaveAnswerCallCount++;
            var already = Answers.Any(a => a.ParticipantId == answer.ParticipantId && a.QuestionId == answer.QuestionId);
            if (already) return;

            Answers.Add(answer);
            var participant = GetParticipant(answer.ParticipantId);
            if (participant != null)
            {
                participant.TotalScore += answer.ScoreEarned;
            }
        }

        public void UpdateRoomStatus(string roomCode, string status, int currentQuestionIndex) { }

        public List<Question> GetQuestionsForDeck(int deckId) => Questions;

        public List<Deck> GetAvailableDecks() => new();
    }

    private static (KahootService service, FakeKahootRepository repo, KahootParticipant participant, Question question) CreateSetup()
    {
        var repo = new FakeKahootRepository();
        var room = repo.CreateRoom(new KahootRoom
        {
            RoomCode = "123456",
            DeckId = 1,
            Title = "Test Room",
            Status = "InProgress"
        });

        var participant = repo.AddParticipant(new KahootParticipant
        {
            RoomId = room.Id,
            Room = room,
            Nickname = "Player1",
            TotalScore = 0
        });

        var question = new Question
        {
            Id = 101,
            DeckId = 1,
            Content = "What is 2 + 2?",
            Answers = new List<Answer>
            {
                new() { Id = 1, Content = "3", IsCorrect = false },
                new() { Id = 2, Content = "4", IsCorrect = true }
            }
        };
        repo.Questions.Add(question);

        var service = new KahootService(repo);
        return (service, repo, participant, question);
    }

    [Fact]
    public async Task SubmitAnswerAsync_WhenSubmittingTwice_IsIdempotentAndDoesNotDuplicateScore()
    {
        var (service, repo, participant, question) = CreateSetup();

        // Lần 1: nộp đáp án đúng trong 2 giây
        var firstResult = await service.SubmitAnswerAsync(
            roomCode: "123456",
            participantId: participant.Id,
            questionId: question.Id,
            selectedAnswerId: 2,
            elapsedSeconds: 2.0m,
            timeLimitSeconds: 20);

        Assert.True(firstResult.IsCorrect);
        Assert.True(firstResult.ScoreEarned > 500);
        var expectedScore = firstResult.ScoreEarned;
        Assert.Equal(expectedScore, participant.TotalScore);
        Assert.Equal(1, repo.SaveAnswerCallCount);

        // Lần 2: nộp lại câu hỏi đó (giả lập click đúp hoặc spam mạng)
        var secondResult = await service.SubmitAnswerAsync(
            roomCode: "123456",
            participantId: participant.Id,
            questionId: question.Id,
            selectedAnswerId: 2,
            elapsedSeconds: 2.0m,
            timeLimitSeconds: 20);

        Assert.True(secondResult.IsCorrect);
        Assert.Equal(expectedScore, secondResult.ScoreEarned);
        // Điểm số của participant KHÔNG bị nhân đôi
        Assert.Equal(expectedScore, participant.TotalScore);
        // SaveAnswer không bị gọi lại
        Assert.Equal(1, repo.SaveAnswerCallCount);
    }

    [Fact]
    public async Task SubmitAnswerAsync_FasterAnswer_EarnsMorePointsThanSlowerAnswer()
    {
        var (service, repo, participant, question) = CreateSetup();

        // Thí sinh 1 trả lời cực nhanh (1 giây)
        var fastResult = await service.SubmitAnswerAsync("123456", participant.Id, question.Id, 2, 1.0m, 20);

        // Thí sinh 2 trả lời chậm (18 giây)
        var participant2 = repo.AddParticipant(new KahootParticipant
        {
            RoomId = 1,
            Nickname = "Player2",
            TotalScore = 0
        });
        var slowResult = await service.SubmitAnswerAsync("123456", participant2.Id, question.Id, 2, 18.0m, 20);

        Assert.True(fastResult.ScoreEarned > slowResult.ScoreEarned);
        Assert.True(fastResult.ScoreEarned <= 1000);
        Assert.True(slowResult.ScoreEarned >= 500);
    }

    [Fact]
    public async Task SubmitAnswerAsync_WrongAnswer_EarnsZeroScore()
    {
        var (service, _, participant, question) = CreateSetup();

        var result = await service.SubmitAnswerAsync("123456", participant.Id, question.Id, 1, 1.0m, 20);

        Assert.False(result.IsCorrect);
        Assert.Equal(0, result.ScoreEarned);
        Assert.Equal(0, participant.TotalScore);
    }
}
