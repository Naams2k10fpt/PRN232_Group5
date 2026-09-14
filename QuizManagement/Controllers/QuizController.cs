using BusinessObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using QuizManagement.ViewModels.Quiz;
using Services;
using System.Security.Claims;
using System.Text.Json;

namespace QuizManagement.Controllers
{
    [Authorize(Policy = "StudyContent")]
    public class QuizController : Controller
    {
        private readonly IDeckService _deckService;
        private readonly IQuizService _quizService;
        private readonly IDataProtector _quizAttemptProtector;

        public QuizController(
            IDeckService deckService,
            IQuizService quizService,
            IDataProtectionProvider dataProtectionProvider)
        {
            _deckService = deckService;
            _quizService = quizService;
            _quizAttemptProtector = dataProtectionProvider.CreateProtector("QuizManagement.QuizAttempt.v2");
        }

        public IActionResult Config(int deckId)
        {
            var deck = _deckService.GetDeckForStudy(deckId);
            if (deck is null)
            {
                return NotFound();
            }

            var availableCount = _quizService.GetAvailableQuestionCount(deckId);
            if (availableCount == 0)
            {
                TempData["ErrorMessage"] = "Bộ đề này chưa có câu hỏi nào để làm bài.";
                return RedirectToAction("Index", "Questions", new { deckId });
            }

            return View(new QuizConfigViewModel
            {
                DeckId = deck.Id,
                DeckName = deck.Name,
                SubjectName = deck.Subject.Name,
                AvailableQuestionCount = availableCount,
                QuestionCount = Math.Min(10, availableCount),
                TimeLimitMinutes = deck.TimeLimitMinutes
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Take(QuizConfigViewModel config)
        {
            var deck = _deckService.GetDeckForStudy(config.DeckId);
            if (deck is null)
            {
                return NotFound();
            }

            var availableCount = _quizService.GetAvailableQuestionCount(config.DeckId);
            config.AvailableQuestionCount = availableCount;
            config.DeckName = deck.Name;
            config.SubjectName = deck.Subject.Name;
            config.TimeLimitMinutes = deck.TimeLimitMinutes;

            if (deck.TimeLimitMinutes is < 0 or > 180)
            {
                ModelState.AddModelError(string.Empty,
                    "Cấu hình thời gian của bộ đề không hợp lệ. Vui lòng liên hệ người quản lý.");
                return View("Config", config);
            }

            if (config.QuestionCount < 1 || config.QuestionCount > availableCount)
            {
                ModelState.AddModelError(nameof(config.QuestionCount),
                    $"Số câu hỏi phải từ 1 đến {availableCount}.");
                return View("Config", config);
            }

            if (!ModelState.IsValid)
            {
                return View("Config", config);
            }

            var questions = _quizService.GetQuestionsForQuiz(config.DeckId, config.QuestionCount);
            var questionIds = questions.Select(q => q.Id).ToList();
            if (questionIds.Count == 0)
            {
                TempData["ErrorMessage"] = "Bộ đề hiện không còn câu hỏi để làm bài.";
                return RedirectToAction(nameof(Config), new { deckId = config.DeckId });
            }

            var attempt = _quizService.StartQuizAttempt(
                config.DeckId,
                CurrentUserId(),
                questionIds,
                deck.TimeLimitMinutes);

            return RedirectToAction(nameof(Take), new
            {
                deckId = config.DeckId,
                attemptId = attempt.Id
            });
        }

        [HttpGet]
        public IActionResult Take(int deckId, Guid attemptId)
        {
            var deck = _deckService.GetDeckForStudy(deckId);
            if (deck is null)
            {
                return NotFound();
            }

            var currentUserId = CurrentUserId();
            var attempt = attemptId == Guid.Empty
                ? null
                : _quizService.GetValidQuizAttempt(attemptId, deckId, currentUserId);
            if (attempt is null)
            {
                TempData["ErrorMessage"] = "Phiên làm bài không hợp lệ hoặc đã hết hạn. Vui lòng bắt đầu lại bài quiz.";
                return RedirectToAction(nameof(Config), new { deckId });
            }

            var questions = _quizService.GetQuestionsForAttempt(deckId, attempt.QuestionIds);
            if (questions.Count != attempt.QuestionIds.Count)
            {
                TempData["ErrorMessage"] = "Bài quiz đã thay đổi và không thể tiếp tục. Vui lòng bắt đầu lại.";
                return RedirectToAction(nameof(Config), new { deckId });
            }

            var model = new QuizTakeViewModel
            {
                DeckId = deckId,
                DeckName = deck.Name,
                SubjectName = deck.Subject.Name,
                AttemptToken = ProtectQuizAttempt(attempt.Id, deckId, currentUserId),
                TimeRemainingSeconds = attempt.RemainingSeconds,
                Questions = questions.Select(q => new QuizQuestionViewModel
                {
                    QuestionId = q.Id,
                    Content = q.Content,
                    QuestionType = q.QuestionType,
                    Answers = q.Answers.Select(a => new QuizAnswerOptionViewModel
                    {
                        AnswerId = a.Id,
                        Content = a.Content
                    }).ToList()
                }).ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Submit(QuizSubmitViewModel model)
        {
            var deck = _deckService.GetDeckForStudy(model.DeckId);
            if (deck is null)
            {
                return NotFound();
            }

            var currentUserId = CurrentUserId();
            var attempt = ReadQuizAttempt(model.AttemptToken);
            if (attempt is null
                || attempt.AttemptId == Guid.Empty
                || attempt.DeckId != model.DeckId
                || attempt.UserId != currentUserId)
            {
                TempData["ErrorMessage"] = "Phiên làm bài không hợp lệ hoặc đã hết hạn. Vui lòng bắt đầu lại bài quiz.";
                return RedirectToAction(nameof(Config), new { deckId = model.DeckId });
            }

            var selectedAnswersByQuestion = model.Questions
                .GroupBy(q => q.QuestionId)
                .ToDictionary(
                    group => group.Key,
                    group => group.SelectMany(GetSelectedAnswerIds).Distinct().ToList());

            var history = _quizService.SubmitQuizAttempt(
                attempt.AttemptId,
                model.DeckId,
                currentUserId,
                selectedAnswersByQuestion);
            if (history is null)
            {
                TempData["ErrorMessage"] = "Phiên làm bài không hợp lệ hoặc đã hết hạn. Vui lòng bắt đầu lại bài quiz.";
                return RedirectToAction(nameof(Config), new { deckId = model.DeckId });
            }

            TempData["SuccessMessage"] = "Đã nộp bài thành công!";
            return RedirectToAction(nameof(Result), new { id = history.Id });
        }

        public IActionResult Result(int id)
        {
            var history = _quizService.GetTestHistoryById(id, CurrentUserId());
            if (history is null)
            {
                return NotFound();
            }

            var resultModel = QuizResultViewModel.FromHistory(history);
            return View(resultModel);
        }

        private string ProtectQuizAttempt(Guid attemptId, int deckId, string userId)
        {
            var payload = new QuizAttemptPayload
            {
                AttemptId = attemptId,
                DeckId = deckId,
                UserId = userId
            };

            return _quizAttemptProtector.Protect(JsonSerializer.Serialize(payload));
        }

        private QuizAttemptPayload? ReadQuizAttempt(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            try
            {
                var json = _quizAttemptProtector.Unprotect(token);
                return JsonSerializer.Deserialize<QuizAttemptPayload>(json);
            }
            catch
            {
                return null;
            }
        }

        private static IEnumerable<int> GetSelectedAnswerIds(QuizQuestionSubmitItem question)
        {
            if (question.SelectedAnswerId.HasValue)
            {
                yield return question.SelectedAnswerId.Value;
            }

            foreach (var answerId in question.SelectedAnswerIds)
            {
                yield return answerId;
            }
        }

        private string CurrentUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new InvalidOperationException("Không tìm thấy UserId trong phiên đăng nhập.");
        }

        private sealed class QuizAttemptPayload
        {
            public Guid AttemptId { get; set; }

            public int DeckId { get; set; }

            public string UserId { get; set; } = string.Empty;
        }
    }
}
