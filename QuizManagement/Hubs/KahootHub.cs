using BusinessObjects;
using Microsoft.AspNetCore.SignalR;
using Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QuizManagement.Hubs
{
    public class RoomGameState
    {
        public string RoomCode { get; set; } = null!;
        public int DeckId { get; set; }
        public List<Question> Questions { get; set; } = new();
        public int CurrentQuestionIndex { get; set; } = 0;
        public int CurrentAnswerCount { get; set; } = 0;
        public int TotalParticipants { get; set; } = 0;
        public int TimeLimitSeconds { get; set; } = 20;
    }

    public class KahootHub : Hub
    {
        private readonly IKahootService _kahootService;
        private static readonly ConcurrentDictionary<string, RoomGameState> _roomStates = new();
        private static readonly ConcurrentDictionary<string, (string RoomCode, int ParticipantId, string Nickname)> _connectionMap = new();

        public KahootHub(IKahootService kahootService)
        {
            _kahootService = kahootService;
        }

        // Host hoặc Player tham gia nhóm SignalR theo RoomCode
        public async Task JoinRoom(string roomCode, string nickname, string? userId = null)
        {
            roomCode = roomCode.Trim().ToUpperInvariant();
            await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);

            // Kiểm tra và tạo participant
            var participant = await _kahootService.JoinRoomAsync(roomCode, nickname, userId, Context.ConnectionId);

            _connectionMap[Context.ConnectionId] = (roomCode, participant.Id, participant.Nickname);

            var participants = await _kahootService.GetParticipantsAsync(roomCode);
            var state = _roomStates.GetOrAdd(roomCode, _ => new RoomGameState { RoomCode = roomCode });
            state.TotalParticipants = participants.Count;

            // Báo lại cho chính người chơi biết đã join thành công kèm ParticipantId
            await Clients.Caller.SendAsync("JoinedSuccess", new
            {
                participantId = participant.Id,
                nickname = participant.Nickname,
                roomCode = roomCode
            });

            // Báo cho cả phòng (đặc biệt là Host) cập nhật danh sách người tham gia
            await Clients.Group(roomCode).SendAsync("PlayerJoined", new
            {
                participantId = participant.Id,
                nickname = participant.Nickname,
                totalPlayers = participants.Count,
                players = participants.Select(p => new { p.Id, p.Nickname, p.TotalScore })
            });
        }

        // Host đăng ký quản lý phòng (không tính là người chơi)
        public async Task RegisterHost(string roomCode)
        {
            roomCode = roomCode.Trim().ToUpperInvariant();
            await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);

            var participants = await _kahootService.GetParticipantsAsync(roomCode);
            await Clients.Caller.SendAsync("HostRegistered", new
            {
                roomCode = roomCode,
                totalPlayers = participants.Count,
                players = participants.Select(p => new { p.Id, p.Nickname, p.TotalScore })
            });
        }

        // Host bấm Start game
        public async Task StartGame(string roomCode)
        {
            roomCode = roomCode.Trim().ToUpperInvariant();
            var room = await _kahootService.GetRoomByCodeAsync(roomCode);
            if (room == null) return;

            var questions = await _kahootService.GetRoomQuestionsAsync(room.Id);
            if (questions.Count == 0)
            {
                await Clients.Caller.SendAsync("Error", "Bộ đề này chưa có câu hỏi nào!");
                return;
            }

            var state = _roomStates.GetOrAdd(roomCode, _ => new RoomGameState { RoomCode = roomCode });
            state.DeckId = room.DeckId;
            state.Questions = questions;
            state.CurrentQuestionIndex = 0;
            state.CurrentAnswerCount = 0;

            await _kahootService.UpdateRoomStatusAsync(roomCode, "InProgress", 0);

            // Bắn tín hiệu bắt đầu game đến tất cả mọi người
            await Clients.Group(roomCode).SendAsync("GameStarted", new
            {
                totalQuestions = questions.Count,
                roomCode = roomCode
            });

            // Gửi câu hỏi đầu tiên
            await PushCurrentQuestion(roomCode);
        }

        // Host chuyển sang câu hỏi tiếp theo
        public async Task NextQuestion(string roomCode)
        {
            roomCode = roomCode.Trim().ToUpperInvariant();
            if (!_roomStates.TryGetValue(roomCode, out var state)) return;

            state.CurrentQuestionIndex++;
            state.CurrentAnswerCount = 0;

            if (state.CurrentQuestionIndex >= state.Questions.Count)
            {
                await EndGame(roomCode);
                return;
            }

            await _kahootService.UpdateRoomStatusAsync(roomCode, "InProgress", state.CurrentQuestionIndex);
            await PushCurrentQuestion(roomCode);
        }

        private async Task PushCurrentQuestion(string roomCode)
        {
            if (!_roomStates.TryGetValue(roomCode, out var state)) return;
            if (state.CurrentQuestionIndex >= state.Questions.Count) return;

            var currentQ = state.Questions[state.CurrentQuestionIndex];
            state.CurrentAnswerCount = 0;

            // Tạo payload câu hỏi (không gửi IsCorrect về client để chống gian lận)
            var questionPayload = new
            {
                questionIndex = state.CurrentQuestionIndex + 1,
                totalQuestions = state.Questions.Count,
                questionId = currentQ.Id,
                content = currentQ.Content,
                timeLimit = state.TimeLimitSeconds,
                answers = currentQ.Answers.Select((a, idx) => new
                {
                    id = a.Id,
                    text = a.Content,
                    colorIndex = idx // 0: Đỏ, 1: Xanh dương, 2: Vàng, 3: Xanh lá
                }).ToList()
            };

            await Clients.Group(roomCode).SendAsync("ReceiveQuestion", questionPayload);
        }

        // Thí sinh nộp câu trả lời
        public async Task SubmitAnswer(string roomCode, int participantId, int questionId, int selectedAnswerId, decimal elapsedSeconds)
        {
            roomCode = roomCode.Trim().ToUpperInvariant();
            var state = _roomStates.GetOrAdd(roomCode, _ => new RoomGameState { RoomCode = roomCode });

            var result = await _kahootService.SubmitAnswerAsync(
                roomCode,
                participantId,
                questionId,
                selectedAnswerId,
                elapsedSeconds,
                state.TimeLimitSeconds);

            state.CurrentAnswerCount++;

            // Trả riêng kết quả cho thí sinh vừa nộp
            await Clients.Caller.SendAsync("AnswerResult", new
            {
                isCorrect = result.IsCorrect,
                scoreEarned = result.ScoreEarned,
                totalScore = result.TotalScore
            });

            // Báo cho Host biết số lượng người đã trả lời
            var participants = await _kahootService.GetParticipantsAsync(roomCode);
            await Clients.Group(roomCode).SendAsync("AnswerSubmitted", new
            {
                currentAnswerCount = state.CurrentAnswerCount,
                totalPlayers = participants.Count
            });
        }

        // Hết giờ câu hỏi: Host gọi hoặc Server tự kết thúc câu hỏi
        public async Task EndQuestion(string roomCode)
        {
            roomCode = roomCode.Trim().ToUpperInvariant();
            if (!_roomStates.TryGetValue(roomCode, out var state)) return;

            if (state.CurrentQuestionIndex < state.Questions.Count)
            {
                var currentQ = state.Questions[state.CurrentQuestionIndex];
                var correctAnswer = currentQ.Answers.FirstOrDefault(a => a.IsCorrect);
                var leaderboard = await _kahootService.GetLeaderboardAsync(roomCode);

                await Clients.Group(roomCode).SendAsync("QuestionEnded", new
                {
                    correctAnswerId = correctAnswer?.Id ?? 0,
                    leaderboard = leaderboard.Take(5).ToList(),
                    allScores = leaderboard
                });
            }
        }

        // Kết thúc trận đấu
        public async Task EndGame(string roomCode)
        {
            roomCode = roomCode.Trim().ToUpperInvariant();
            await _kahootService.UpdateRoomStatusAsync(roomCode, "Finished", -1);
            var leaderboard = await _kahootService.GetLeaderboardAsync(roomCode);

            await Clients.Group(roomCode).SendAsync("GameOver", new
            {
                podium = leaderboard.Take(3).ToList(),
                fullLeaderboard = leaderboard
            });

            _roomStates.TryRemove(roomCode, out _);
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (_connectionMap.TryRemove(Context.ConnectionId, out var info))
            {
                var participants = await _kahootService.GetParticipantsAsync(info.RoomCode);
                await Clients.Group(info.RoomCode).SendAsync("PlayerLeft", new
                {
                    nickname = info.Nickname,
                    totalPlayers = participants.Count
                });
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}
