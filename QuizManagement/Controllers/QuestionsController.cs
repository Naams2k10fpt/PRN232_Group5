using BusinessObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizManagement.ViewModels.Questions;
using Services;
using System.Security.Claims;

namespace QuizManagement.Controllers
{
    [Authorize(Policy = "StudyContent")]
    public class QuestionsController : Controller
    {
        private readonly IDeckService _deckService;
        private readonly IQuestionService _questionService;

        public QuestionsController(IDeckService deckService, IQuestionService questionService)
        {
            _deckService = deckService;
            _questionService = questionService;
        }

        public IActionResult Index(int deckId)
        {
            var deck = _deckService.GetDeckForStudy(deckId);
            if (deck is null)
            {
                return NotFound();
            }

            ViewBag.Deck = deck;
            ViewBag.CurrentUserId = CurrentUserId();
            var questions = _questionService.GetQuestionsByDeckForStudy(deckId);
            return View(questions);
        }

        [Authorize(Policy = "ManageContent")]
        public IActionResult Create(int deckId)
        {
            var deck = _deckService.GetDeckById(deckId, CurrentUserId(), IsAdmin());
            if (deck is null)
            {
                return NotFound();
            }

            return View(BuildFormModel(deck));
        }

        [HttpPost]
        [Authorize(Policy = "ManageContent")]
        [ValidateAntiForgeryToken]
        public IActionResult Create(QuestionFormViewModel model)
        {
            var deck = _deckService.GetDeckById(model.DeckId, CurrentUserId(), IsAdmin());
            if (deck is null)
            {
                return NotFound();
            }

            NormalizeAndValidate(model);
            FillDeckInfo(model, deck);

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            _questionService.AddQuestion(ToQuestion(model, User.Identity?.Name));

            TempData["SuccessMessage"] = "Đã tạo câu hỏi.";
            return RedirectToAction(nameof(Index), new { deckId = model.DeckId });
        }

        [Authorize(Policy = "ManageContent")]
        public IActionResult Edit(int id)
        {
            var question = _questionService.GetQuestionById(id, CurrentUserId(), IsAdmin());
            if (question is null)
            {
                return NotFound();
            }

            return View(ToFormModel(question));
        }

        [HttpPost]
        [Authorize(Policy = "ManageContent")]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, QuestionFormViewModel model)
        {
            if (id != model.Id)
            {
                return BadRequest();
            }

            var question = _questionService.GetQuestionById(id, CurrentUserId(), IsAdmin());
            if (question is null)
            {
                return NotFound();
            }

            model.DeckId = question.DeckId;
            byte[]? rowVersion = null;
            try
            {
                rowVersion = string.IsNullOrWhiteSpace(model.RowVersion)
                    ? null
                    : Convert.FromBase64String(model.RowVersion);
            }
            catch (FormatException)
            {
                // The generic validation error below avoids exposing concurrency internals.
            }
            if (rowVersion is not { Length: 8 })
            {
                ModelState.AddModelError(string.Empty,
                    "Phiên bản câu hỏi không hợp lệ. Vui lòng tải lại trang.");
            }
            NormalizeAndValidate(model);
            FillDeckInfo(model, question.Deck);

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var updatedQuestion = ToQuestion(model, User.Identity?.Name);
            updatedQuestion.Id = id;
            updatedQuestion.RowVersion = rowVersion!;
            var updateResult = _questionService.TryUpdateQuestion(updatedQuestion);
            if (updateResult != QuestionUpdateResult.Updated)
            {
                ModelState.Clear();
                if (updateResult == QuestionUpdateResult.ReferencedAnswer)
                {
                    ModelState.AddModelError(string.Empty,
                        "Không thể xóa đáp án đã xuất hiện trong lịch sử làm bài. Vui lòng giữ lại đáp án đó.");
                    return View(ToFormModel(question));
                }

                ModelState.AddModelError(string.Empty,
                    "Câu hỏi đã được người khác cập nhật. Dữ liệu mới nhất đã được tải lại; vui lòng kiểm tra và sửa lại.");
                var latest = _questionService.GetQuestionById(id, CurrentUserId(), IsAdmin());
                return latest is null ? NotFound() : View(ToFormModel(latest));
            }

            TempData["SuccessMessage"] = "Đã cập nhật câu hỏi.";
            return RedirectToAction(nameof(Index), new { deckId = question.DeckId });
        }

        [HttpPost]
        [Authorize(Policy = "ManageContent")]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            var question = _questionService.GetQuestionById(id, CurrentUserId(), IsAdmin());
            if (question is null)
            {
                return NotFound();
            }

            var deckId = question.DeckId;
            question.UpdatedBy = User.Identity?.Name;
            _questionService.DeleteQuestion(question);

            TempData["SuccessMessage"] = "Đã xóa câu hỏi.";
            return RedirectToAction(nameof(Index), new { deckId });
        }

        private QuestionFormViewModel BuildFormModel(Deck deck)
        {
            return new QuestionFormViewModel
            {
                DeckId = deck.Id,
                DeckName = deck.Name,
                SubjectId = deck.SubjectId,
                SubjectName = deck.Subject.Name,
                Answers =
                [
                    new AnswerFormViewModel(),
                    new AnswerFormViewModel(),
                    new AnswerFormViewModel(),
                    new AnswerFormViewModel()
                ]
            };
        }

        private QuestionFormViewModel ToFormModel(Question question)
        {
            var model = new QuestionFormViewModel
            {
                Id = question.Id,
                RowVersion = Convert.ToBase64String(question.RowVersion),
                DeckId = question.DeckId,
                DeckName = question.Deck.Name,
                SubjectId = question.Deck.SubjectId,
                SubjectName = question.Deck.Subject.Name,
                Content = question.Content,
                Explanation = question.Explanation,
                QuestionType = question.QuestionType,
                Answers = question.Answers
                    .OrderBy(a => a.Id)
                    .Select(a => new AnswerFormViewModel
                    {
                        Id = a.Id,
                        Content = a.Content,
                        IsCorrect = a.IsCorrect
                    })
                    .ToList()
            };

            while (model.Answers.Count < 4)
            {
                model.Answers.Add(new AnswerFormViewModel());
            }

            return model;
        }

        private Question ToQuestion(QuestionFormViewModel model, string? username)
        {
            return new Question
            {
                Id = model.Id,
                DeckId = model.DeckId,
                Content = model.Content.Trim(),
                Explanation = string.IsNullOrWhiteSpace(model.Explanation) ? null : model.Explanation.Trim(),
                QuestionType = model.QuestionType,
                CreatedBy = username,
                UpdatedBy = username,
                Answers = model.Answers
                    .Where(a => !string.IsNullOrWhiteSpace(a.Content))
                    .Select(a => new Answer
                    {
                        Id = a.Id,
                        Content = a.Content!.Trim(),
                        IsCorrect = a.IsCorrect
                    })
                    .ToList()
            };
        }

        private void NormalizeAndValidate(QuestionFormViewModel model)
        {
            model.Content = model.Content.Trim();
            model.Explanation = string.IsNullOrWhiteSpace(model.Explanation) ? null : model.Explanation.Trim();
            model.Answers = model.Answers
                .Where(a => !string.IsNullOrWhiteSpace(a.Content))
                .Select(a =>
                {
                    a.Content = a.Content!.Trim();
                    return a;
                })
                .ToList();

            if (model.Answers.Count < 2)
            {
                ModelState.AddModelError(nameof(model.Answers), "Mỗi câu hỏi cần ít nhất 2 đáp án.");
            }

            var correctCount = model.Answers.Count(a => a.IsCorrect);
            if (model.QuestionType == 1 && correctCount != 1)
            {
                ModelState.AddModelError(nameof(model.Answers), "Câu hỏi một đáp án đúng phải có đúng 1 đáp án được đánh dấu đúng.");
            }

            if (model.QuestionType == 2 && correctCount < 1)
            {
                ModelState.AddModelError(nameof(model.Answers), "Câu hỏi nhiều đáp án đúng phải có ít nhất 1 đáp án được đánh dấu đúng.");
            }

            while (model.Answers.Count < 4)
            {
                model.Answers.Add(new AnswerFormViewModel());
            }
        }

        private static void FillDeckInfo(QuestionFormViewModel model, Deck deck)
        {
            model.DeckName = deck.Name;
            model.SubjectId = deck.SubjectId;
            model.SubjectName = deck.Subject.Name;
        }

        private string CurrentUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new InvalidOperationException("Không tìm thấy UserId trong phiên đăng nhập.");
        }

        private bool IsAdmin() => User.IsInRole(AppRoles.Admin);
    }
}
