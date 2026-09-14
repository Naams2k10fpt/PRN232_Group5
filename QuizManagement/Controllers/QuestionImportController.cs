using BusinessObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizManagement.ViewModels.Import;
using Services;
using System.Security.Claims;

namespace QuizManagement.Controllers
{
    [Authorize(Policy = "ManageContent")]
    public class QuestionImportController : Controller
    {
        private readonly IDeckService _deckService;
        private readonly IQuestionService _questionService;
        private readonly IQuestionImportService _questionImportService;
        private readonly ILogger<QuestionImportController> _logger;

        public QuestionImportController(
            IDeckService deckService,
            IQuestionService questionService,
            IQuestionImportService questionImportService,
            ILogger<QuestionImportController> logger)
        {
            _deckService = deckService;
            _questionService = questionService;
            _questionImportService = questionImportService;
            _logger = logger;
        }

        public IActionResult Import(int deckId)
        {
            var deck = _deckService.GetDeckById(deckId, CurrentUserId(), IsAdmin());
            if (deck is null)
            {
                return NotFound();
            }

            return View("~/Views/Questions/Import.cshtml", BuildInputModel(deck));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(QuestionImportLimits.MaxRequestBytes)]
        [RequestFormLimits(
            MultipartBodyLengthLimit = QuestionImportLimits.MaxRequestBytes,
            ValueLengthLimit = QuestionImportLimits.MaxTextCharacters,
            ValueCountLimit = QuestionImportLimits.MaxFormValues)]
        public IActionResult Preview(QuestionImportInputViewModel model)
        {
            var deck = _deckService.GetDeckById(model.DeckId, CurrentUserId(), IsAdmin());
            if (deck is null)
            {
                return NotFound();
            }

            QuestionImportPreview preview;
            var sourceName = "Text";

            try
            {
                if (model.ExcelFile is { Length: > 0 })
                {
                    sourceName = Path.GetFileName(model.ExcelFile.FileName);
                    if (model.ExcelFile.Length > QuestionImportLimits.MaxUploadBytes)
                    {
                        ModelState.AddModelError(nameof(model.ExcelFile),
                            $"File Excel không được vượt quá {QuestionImportLimits.MaxUploadBytes / 1024 / 1024} MB.");
                        return View("~/Views/Questions/Import.cshtml", BuildInputModel(deck, model.RawText));
                    }

                    if (!model.ExcelFile.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                    {
                        ModelState.AddModelError(nameof(model.ExcelFile), "Chỉ hỗ trợ file .xlsx.");
                        return View("~/Views/Questions/Import.cshtml", BuildInputModel(deck, model.RawText));
                    }

                    using var stream = model.ExcelFile.OpenReadStream();
                    preview = _questionImportService.ParseExcel(stream);
                }
                else if (!string.IsNullOrWhiteSpace(model.RawText))
                {
                    if (model.RawText.Length > QuestionImportLimits.MaxTextCharacters)
                    {
                        ModelState.AddModelError(nameof(model.RawText), "Nội dung text vượt quá giới hạn cho phép.");
                        return View("~/Views/Questions/Import.cshtml", BuildInputModel(deck));
                    }

                    preview = _questionImportService.ParseText(model.RawText);
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Vui lòng tải file Excel hoặc nhập nội dung text.");
                    return View("~/Views/Questions/Import.cshtml", BuildInputModel(deck, model.RawText));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Question import preview was rejected for deck {DeckId} and source {SourceName}.",
                    deck.Id,
                    sourceName);
                ModelState.AddModelError(string.Empty,
                    "Không đọc được dữ liệu import. Vui lòng kiểm tra định dạng và giới hạn file.");
                return View("~/Views/Questions/Import.cshtml", BuildInputModel(deck, model.RawText));
            }

            var previewModel = BuildPreviewModel(deck, preview, sourceName);
            return View("~/Views/Questions/ImportPreview.cshtml", previewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(QuestionImportLimits.MaxRequestBytes)]
        [RequestFormLimits(
            MultipartBodyLengthLimit = QuestionImportLimits.MaxRequestBytes,
            ValueLengthLimit = QuestionImportLimits.MaxCellCharacters,
            ValueCountLimit = QuestionImportLimits.MaxFormValues)]
        public IActionResult Commit(QuestionImportPreviewViewModel model)
        {
            var deck = _deckService.GetDeckById(model.DeckId, CurrentUserId(), IsAdmin());
            if (deck is null)
            {
                return NotFound();
            }

            if (model.ValidRows.Count > QuestionImportLimits.MaxRows)
            {
                ModelState.AddModelError(string.Empty,
                    $"Chỉ hỗ trợ tối đa {QuestionImportLimits.MaxRows:N0} dòng mỗi lần import.");
                return View("~/Views/Questions/ImportPreview.cshtml", model);
            }

            var validation = _questionImportService.ValidateRows(model.ValidRows.Select(ToServiceRow));
            if (validation.Errors.Count > 0)
            {
                var previewModel = BuildPreviewModel(deck, validation, model.SourceName);
                ModelState.AddModelError(string.Empty, "Dữ liệu import còn lỗi. Không có câu hỏi nào được lưu.");
                return View("~/Views/Questions/ImportPreview.cshtml", previewModel);
            }

            if (validation.ValidRows.Count == 0)
            {
                var previewModel = BuildPreviewModel(deck, validation, model.SourceName);
                ModelState.AddModelError(string.Empty, "Không có câu hỏi hợp lệ để import.");
                return View("~/Views/Questions/ImportPreview.cshtml", previewModel);
            }

            _questionService.AddQuestions(validation.ValidRows.Select(row => new Question
            {
                DeckId = deck.Id,
                Content = row.Content,
                Explanation = row.Explanation,
                QuestionType = row.QuestionType,
                CreatedBy = User.Identity?.Name,
                UpdatedBy = User.Identity?.Name,
                Answers = row.Answers.Select(answer => new Answer
                {
                    Content = answer.Content,
                    IsCorrect = answer.IsCorrect
                }).ToList()
            }));

            TempData["SuccessMessage"] = $"Đã import {validation.ValidRows.Count} câu hỏi.";
            return RedirectToAction("Index", "Questions", new { deckId = deck.Id });
        }

        private QuestionImportInputViewModel BuildInputModel(Deck deck, string? rawText = null)
        {
            return new QuestionImportInputViewModel
            {
                DeckId = deck.Id,
                DeckName = deck.Name,
                SubjectName = deck.Subject.Name,
                RawText = rawText,
                TextTemplate = _questionImportService.GetTextTemplate()
            };
        }

        private static QuestionImportPreviewViewModel BuildPreviewModel(Deck deck, QuestionImportPreview preview, string sourceName)
        {
            return new QuestionImportPreviewViewModel
            {
                DeckId = deck.Id,
                DeckName = deck.Name,
                SubjectName = deck.Subject.Name,
                SourceName = sourceName,
                ValidRows = preview.ValidRows.Select(row => new QuestionImportRowViewModel
                {
                    RowNumber = row.RowNumber,
                    Content = row.Content,
                    QuestionType = row.QuestionType,
                    Explanation = row.Explanation,
                    Answers = row.Answers.Select(answer => new QuestionImportAnswerViewModel
                    {
                        Content = answer.Content,
                        IsCorrect = answer.IsCorrect
                    }).ToList()
                }).ToList(),
                Errors = preview.Errors.Select(error => new QuestionImportErrorViewModel
                {
                    RowNumber = error.RowNumber,
                    Message = error.Message
                }).ToList()
            };
        }

        private static QuestionImportRow ToServiceRow(QuestionImportRowViewModel row)
        {
            return new QuestionImportRow
            {
                RowNumber = row.RowNumber,
                Content = row.Content,
                QuestionType = row.QuestionType,
                Explanation = row.Explanation,
                Answers = row.Answers.Select(answer => new QuestionImportAnswer
                {
                    Content = answer.Content,
                    IsCorrect = answer.IsCorrect
                }).ToList()
            };
        }

        private string CurrentUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new InvalidOperationException("Không tìm thấy UserId trong phiên đăng nhập.");
        }

        private bool IsAdmin() => User.IsInRole(AppRoles.Admin);
    }
}
