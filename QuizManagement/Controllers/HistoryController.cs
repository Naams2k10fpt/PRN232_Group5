using BusinessObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizManagement.ViewModels.Quiz;
using Services;
using System.Security.Claims;

namespace QuizManagement.Controllers
{
    [Authorize]
    public class HistoryController : Controller
    {
        private readonly IQuizService _quizService;

        public HistoryController(IQuizService quizService)
        {
            _quizService = quizService;
        }

        /// <summary>
        /// GET: Danh sách lịch sử làm bài của user hiện tại
        /// </summary>
        public async Task<IActionResult> Index(int page = 1)
        {
            const int pageSize = 50;
            var result = await _quizService.GetTestHistoryPageAsync(CurrentUserId(), page, pageSize);
            ViewBag.Page = result.Page;
            ViewBag.PageSize = result.PageSize;
            ViewBag.TotalCount = result.TotalCount;
            return View(result.Items);
        }

        /// <summary>
        /// GET: Chi tiết một lần làm bài (xem lại câu hỏi + đáp án)
        /// </summary>
        public IActionResult Details(int id)
        {
            var history = _quizService.GetTestHistoryById(id, CurrentUserId());
            if (history is null)
            {
                return NotFound();
            }

            var resultModel = QuizResultViewModel.FromHistory(history);
            return View(resultModel);
        }

        private string CurrentUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new InvalidOperationException("Không tìm thấy UserId trong phiên đăng nhập.");
        }
    }
}
