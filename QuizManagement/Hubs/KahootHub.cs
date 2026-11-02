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
        public DateTime QuestionStartedAtUtc { get; set; } = DateTime.UtcNow;
        public HashSet<int> AnsweredParticipantIds { get; } = new();
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

            string? validUserId = string.IsNullOrWhiteSpace(userId) ? null : userId.Trim();
            // Kiểm tra và tạo participant
            var participant = await _kahootService.JoinRoomAsync(roomCode, nickname, validUserId, Context.ConnectionId);

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

            // Nếu phòng đã bắt đầu chơi, gửi câu hỏi hiện tại ngay cho người chơi vừa vào/reconnect
            var room = await _kahootService.GetRoomByCodeAsync(roomCode);
            if (room != null && room.Status == "InProgress")
            {
                var restoredState = await EnsureRoomStateAsync(roomCode);
                if (restoredState != null && restoredState.CurrentQuestionIndex < restoredState.Questions.Count)
                {
                    var questionPayload = BuildQuestionPayload(restoredState);
                    await Clients.Caller.SendAsync("ReceiveQuestion", questionPayload);
                }
            }
        }

        // Host đăng ký quản lý phòng (không tính là người chơi)
        public async Task RegisterHost(string roomCode)
        {
            roomCode = roomCode.Trim().ToUpperInvariant();
            await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);

            var participants = await _kahootService.GetParticipantsAsync(roomCode);
            var room = await _kahootService.GetRoomByCodeAsync(roomCode);

            await Clients.Caller.SendAsync("HostRegistered", new
            {
                roomCode = roomCode,
                status = room?.Status ?? "Waiting",
                totalPlayers = participants.Count,
                players = participants.Select(p => new { p.Id, p.Nickname, p.TotalScore })
            });

            // NẾU PHÒNG ĐANG IN PROGRESS: Gửi ngay câu hỏi hiện tại cho Host
            if (room != null && room.Status == "InProgress")
            {
                var state = await EnsureRoomStateAsync(roomCode);
                if (state != null && state.CurrentQuestionIndex < state.Questions.Count)
                {
                    // Đồng bộ lại mốc thời gian bắt đầu câu 1 khi Host vừa tải xong trang Host.cshtml
                    if (state.CurrentQuestionIndex == 0 && (DateTime.UtcNow - state.QuestionStartedAtUtc).TotalSeconds < 15.0)
                    {
                        state.QuestionStartedAtUtc = DateTime.UtcNow;
                    }

                    var questionPayload = BuildQuestionPayload(state);
                    await Clients.Caller.SendAsync("ReceiveQuestion", questionPayload);

                    await Clients.Caller.SendAsync("AnswerSubmitted", new
                    {
                        currentAnswerCount = state.CurrentAnswerCount,
                        totalPlayers = participants.Count
                    });
                }
            }
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

        public async Task GetCurrentQuestion(string roomCode)
        {
            roomCode = roomCode.Trim().ToUpperInvariant();
            var state = await EnsureRoomStateAsync(roomCode);
            if (state != null && state.CurrentQuestionIndex < state.Questions.Count)
            {
                var questionPayload = BuildQuestionPayload(state);
                await Clients.Caller.SendAsync("ReceiveQuestion", questionPayload);
            }
        }

        private async Task<RoomGameState?> EnsureRoomStateAsync(string roomCode)
        {
            if (_roomStates.TryGetValue(roomCode, out var state))
            {
                return state;
            }

            var room = await _kahootService.GetRoomByCodeAsync(roomCode);
            if (room == null) return null;

            var questions = await _kahootService.GetRoomQuestionsAsync(room.Id);
            if (questions.Count == 0) return null;

            var participants = await _kahootService.GetParticipantsAsync(roomCode);

            state = new RoomGameState
            {
                RoomCode = roomCode,
                DeckId = room.DeckId,
                Questions = questions,
                CurrentQuestionIndex = Math.Max(0, room.CurrentQuestionIndex),
                TotalParticipants = participants.Count,
                CurrentAnswerCount = 0,
                TimeLimitSeconds = 20
            };

            _roomStates[roomCode] = state;
            return state;
        }

        private object BuildQuestionPayload(RoomGameState state)
        {
            var currentQ = state.Questions[state.CurrentQuestionIndex];
            return new
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
                    colorIndex = idx,
                    optionLetter = idx switch { 0 => "A", 1 => "B", 2 => "C", 3 => "D", _ => "?" }
                }).ToList()
            };
        }

        // Host chuyển sang câu hỏi tiếp theo
        public async Task NextQuestion(string roomCode)
        {
            roomCode = roomCode.Trim().ToUpperInvariant();
            var state = await EnsureRoomStateAsync(roomCode);
            if (state == null) return;

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
            var state = await EnsureRoomStateAsync(roomCode);
            if (state == null) return;
            if (state.CurrentQuestionIndex >= state.Questions.Count) return;

            state.CurrentAnswerCount = 0;
            state.QuestionStartedAtUtc = DateTime.UtcNow;
            lock (state.AnsweredParticipantIds)
            {
                state.AnsweredParticipantIds.Clear();
            }

            var questionPayload = BuildQuestionPayload(state);
            await Clients.Group(roomCode).SendAsync("ReceiveQuestion", questionPayload);
        }

        // Thí sinh nộp câu trả lời (nhận double elapsedSeconds để tương thích JSON float từ Flutter & Web)
        public async Task SubmitAnswer(string roomCode, int participantId, int questionId, int selectedAnswerId, double elapsedSeconds)
        {
            roomCode = roomCode.Trim().ToUpperInvariant();

            // 1. Chống IDOR: Xác thực danh tính caller qua connection session đã lưu
            if (!_connectionMap.TryGetValue(Context.ConnectionId, out var session) ||
                !string.Equals(session.RoomCode, roomCode, StringComparison.OrdinalIgnoreCase))
            {
                await Clients.Caller.SendAsync("Error", "Phiên kết nối phòng không hợp lệ.");
                return;
            }

            // Đảm bảo participantId thực tế thuộc về ConnectionId này
            int verifiedParticipantId = session.ParticipantId;
            if (participantId != 0 && participantId != verifiedParticipantId)
            {
                await Clients.Caller.SendAsync("Error", "Bạn không có quyền nộp bài cho người chơi khác.");
                return;
            }

            var state = await EnsureRoomStateAsync(roomCode);
            if (state == null) return;

            // Kiểm tra câu hỏi có khớp với câu hỏi hiện tại không
            if (state.CurrentQuestionIndex >= state.Questions.Count ||
                state.Questions[state.CurrentQuestionIndex].Id != questionId)
            {
                return;
            }

            // 2. Chống gian lận thời gian: Sử dụng server clock làm thước đo chuẩn
            var serverElapsed = (decimal)Math.Max(0.0, (DateTime.UtcNow - state.QuestionStartedAtUtc).TotalSeconds);
            int safeLimit = state.TimeLimitSeconds > 0 ? state.TimeLimitSeconds : 20;

            // Nếu câu hỏi đã kết thúc (quá thời gian quy định + 5.0s dung sai độ trễ mạng và render client)
            decimal gracePeriod = 5.0m;
            if (serverElapsed > safeLimit + gracePeriod)
            {
                var participantsExpired = await _kahootService.GetParticipantsAsync(roomCode);
                var currentP = participantsExpired.FirstOrDefault(p => p.Id == verifiedParticipantId);
                await Clients.Caller.SendAsync("AnswerResult", new
                {
                    isCorrect = false,
                    scoreEarned = 0,
                    totalScore = currentP?.TotalScore ?? 0
                });
                return;
            }

            // Client không được khai báo thời gian thấp hơn (serverElapsed - 2.0s)
            // Nếu client báo thời gian quá cao (lệch đồng hồ local), tự động đồng bộ theo serverElapsed
            decimal clientElapsed = (decimal)Math.Max(0.0, elapsedSeconds);
            decimal minAllowedElapsed = Math.Max(0.0m, serverElapsed - 2.0m);
            decimal validatedElapsed = (clientElapsed < minAllowedElapsed || clientElapsed > serverElapsed + 1.0m)
                ? serverElapsed
                : clientElapsed;

            var result = await _kahootService.SubmitAnswerAsync(
                roomCode,
                verifiedParticipantId,
                questionId,
                selectedAnswerId,
                validatedElapsed,
                safeLimit);

            // 3. Chống double-submit lặp điểm và cập nhật số lượng trả lời chính xác
            bool isFirstSubmit;
            lock (state.AnsweredParticipantIds)
            {
                isFirstSubmit = state.AnsweredParticipantIds.Add(verifiedParticipantId);
                if (isFirstSubmit)
                {
                    state.CurrentAnswerCount = state.AnsweredParticipantIds.Count;
                }
            }

            // Trả riêng kết quả cho thí sinh vừa nộp
            await Clients.Caller.SendAsync("AnswerResult", new
            {
                isCorrect = result.IsCorrect,
                scoreEarned = result.ScoreEarned,
                totalScore = result.TotalScore
            });

            // Chỉ phát tín hiệu AnswerSubmitted tới Host và nhóm khi đây là lần nộp đầu tiên của câu hỏi này
            if (isFirstSubmit)
            {
                var participants = await _kahootService.GetParticipantsAsync(roomCode);
                await Clients.Group(roomCode).SendAsync("AnswerSubmitted", new
                {
                    currentAnswerCount = state.CurrentAnswerCount,
                    totalPlayers = participants.Count
                });
            }
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
