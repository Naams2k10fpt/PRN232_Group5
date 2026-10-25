using BusinessObjects;
using Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Services;

public class KahootService : IKahootService
{
    private readonly IKahootRepository _repository;
    private static readonly Random _random = new();

    public KahootService(IKahootRepository repository)
    {
        _repository = repository;
    }

    public Task<KahootRoom> CreateRoomAsync(int deckId, string title, string hostUserId)
    {
        var questions = _repository.GetQuestionsForDeck(deckId);
        
        // Sinh mã PIN 6 số ngẫu nhiên không trùng lặp
        string roomCode;
        do
        {
            roomCode = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        } while (_repository.GetRoomByCode(roomCode) != null);

        var room = new KahootRoom
        {
            RoomCode = roomCode,
            Title = string.IsNullOrWhiteSpace(title) ? $"Kahoot Room {roomCode}" : title.Trim(),
            HostUserId = hostUserId,
            DeckId = deckId,
            Status = "Waiting",
            CurrentQuestionIndex = 0,
            TotalQuestions = questions.Count,
            CreatedAt = DateTime.UtcNow
        };

        var createdRoom = _repository.CreateRoom(room);
        return Task.FromResult(createdRoom);
    }

    public Task<KahootRoom?> GetRoomByCodeAsync(string? roomCode)
    {
        if (string.IsNullOrWhiteSpace(roomCode))
        {
            return Task.FromResult<KahootRoom?>(null);
        }

        var room = _repository.GetRoomByCode(roomCode.Trim());
        return Task.FromResult(room);
    }

    public Task<KahootParticipant> JoinRoomAsync(string? roomCode, string nickname, string? userId, string? connectionId)
    {
        if (string.IsNullOrWhiteSpace(roomCode))
        {
            throw new InvalidOperationException("Mã phòng không được để trống.");
        }

        var room = _repository.GetRoomByCode(roomCode.Trim());
        if (room == null)
        {
            throw new InvalidOperationException($"Phòng chơi với mã {roomCode} không tồn tại.");
        }

        if (room.Status == "Finished")
        {
            throw new InvalidOperationException("Phòng chơi này đã kết thúc.");
        }

        var participant = new KahootParticipant
        {
            RoomId = room.Id,
            Nickname = string.IsNullOrWhiteSpace(nickname) ? $"Player_{RandomNumberGenerator.GetInt32(1000, 9999)}" : nickname.Trim(),
            UserId = string.IsNullOrWhiteSpace(userId) ? null : userId.Trim(),
            ConnectionId = connectionId,
            TotalScore = 0,
            CurrentRank = 0,
            JoinedAt = DateTime.UtcNow
        };

        var savedParticipant = _repository.AddParticipant(participant);
        return Task.FromResult(savedParticipant);
    }

    public Task<List<Question>> GetRoomQuestionsAsync(int roomId)
    {
        var room = _repository.GetRoomById(roomId);
        if (room == null) return Task.FromResult(new List<Question>());

        var questions = _repository.GetQuestionsForDeck(room.DeckId);
        return Task.FromResult(questions);
    }

    public Task<KahootAnswerResult> SubmitAnswerAsync(
        string roomCode,
        int participantId,
        int questionId,
        int selectedAnswerId,
        decimal elapsedSeconds,
        int timeLimitSeconds)
    {
        var participant = _repository.GetParticipant(participantId);
        if (participant == null || participant.Room == null)
        {
            throw new InvalidOperationException("Không tìm thấy người tham gia.");
        }

        var questions = _repository.GetQuestionsForDeck(participant.Room.DeckId);
        var question = questions.FirstOrDefault(q => q.Id == questionId);
        if (question == null)
        {
            throw new InvalidOperationException("Không tìm thấy câu hỏi.");
        }

        var selectedAnswer = question.Answers.FirstOrDefault(a => a.Id == selectedAnswerId);
        var correctAnswer = question.Answers.FirstOrDefault(a => a.IsCorrect);
        bool isCorrect = selectedAnswer?.IsCorrect ?? false;

        // Nếu thí sinh đã nộp câu hỏi này rồi, trả về kết quả đã lưu mà không cộng dồn điểm tiếp
        var existingAnswer = _repository.GetAnswer(participantId, questionId);
        if (existingAnswer != null)
        {
            return Task.FromResult(new KahootAnswerResult
            {
                IsCorrect = existingAnswer.IsCorrect,
                ScoreEarned = existingAnswer.ScoreEarned,
                TotalScore = participant.TotalScore,
                CorrectAnswerId = correctAnswer?.Id ?? 0
            });
        }

        int scoreEarned = 0;
        if (isCorrect)
        {
            int safeTimeLimit = timeLimitSeconds > 0 ? timeLimitSeconds : 20;
            double elapsed = (double)Math.Max(0, elapsedSeconds);
            // Công thức Kahoot tiêu chuẩn: Trả lời càng nhanh điểm càng cao (từ 500 đến 1000 điểm)
            double factor = 1.0 - (elapsed / (2.0 * safeTimeLimit));
            if (factor < 0.5) factor = 0.5;
            if (factor > 1.0) factor = 1.0;
            scoreEarned = (int)Math.Round(1000.0 * factor);
        }

        var answer = new KahootAnswer
        {
            ParticipantId = participantId,
            QuestionId = questionId,
            SelectedAnswerId = selectedAnswerId > 0 ? selectedAnswerId : null,
            IsCorrect = isCorrect,
            AnswerTimeSeconds = elapsedSeconds,
            ScoreEarned = scoreEarned
        };

        _repository.SaveAnswer(answer);

        return Task.FromResult(new KahootAnswerResult
        {
            IsCorrect = isCorrect,
            ScoreEarned = scoreEarned,
            TotalScore = participant.TotalScore,
            CorrectAnswerId = correctAnswer?.Id ?? 0
        });
    }

    public Task<List<KahootLeaderboardEntry>> GetLeaderboardAsync(string? roomCode)
    {
        if (string.IsNullOrWhiteSpace(roomCode))
        {
            return Task.FromResult(new List<KahootLeaderboardEntry>());
        }

        var participants = _repository.GetParticipantsByRoom(roomCode.Trim());
        var leaderboard = participants
            .Select((p, index) => new KahootLeaderboardEntry
            {
                ParticipantId = p.Id,
                Nickname = p.Nickname,
                Score = p.TotalScore,
                Rank = index + 1
            })
            .ToList();

        return Task.FromResult(leaderboard);
    }

    public Task<bool> UpdateRoomStatusAsync(string? roomCode, string status, int currentQuestionIndex)
    {
        if (string.IsNullOrWhiteSpace(roomCode))
        {
            return Task.FromResult(false);
        }

        _repository.UpdateRoomStatus(roomCode.Trim(), status, currentQuestionIndex);
        return Task.FromResult(true);
    }

    public Task<List<KahootParticipant>> GetParticipantsAsync(string? roomCode)
    {
        if (string.IsNullOrWhiteSpace(roomCode))
        {
            return Task.FromResult(new List<KahootParticipant>());
        }

        var participants = _repository.GetParticipantsByRoom(roomCode.Trim());
        return Task.FromResult(participants);
    }

    public Task<List<Deck>> GetAvailableDecksAsync()
    {
        var decks = _repository.GetAvailableDecks();
        return Task.FromResult(decks);
    }
}
