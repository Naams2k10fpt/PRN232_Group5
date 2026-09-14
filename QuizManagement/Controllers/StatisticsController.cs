using BusinessObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizManagement.ViewModels.Statistics;
using QuizManagement.Helpers;
using Services;
using System.Security.Claims;

namespace QuizManagement.Controllers
{
    [Authorize(Policy = "ViewAnalytics")]
    public class StatisticsController : Controller
    {
        private readonly IQuizService _quizService;
        private readonly IDeckService _deckService;

        public StatisticsController(IQuizService quizService, IDeckService deckService)
        {
            _quizService = quizService;
            _deckService = deckService;
        }

        public async Task<IActionResult> Index()
        {
            var statistics = await _quizService.GetUserStatisticsAsync(CurrentUserId());
            var total = statistics.TotalAttempts;
            var model = new StatisticsViewModel
            {
                TotalAttempts = total,
                AveragePercentage = Math.Round(statistics.AveragePercentage, 1),
                BestPercentage = Math.Round(statistics.BestPercentage, 1),
                LowestPercentage = Math.Round(statistics.LowestPercentage, 1),
                PassedAttempts = statistics.PassedAttempts,
                FailedAttempts = total - statistics.PassedAttempts
            };

            model.PassRate = total > 0 ? Math.Round((double)model.PassedAttempts / total * 100, 1) : 0;
            model.RecentScores = statistics.RecentScores
                .Select(score => new ScoreTrendPointViewModel
                {
                    Label = VietnamTime.FromUtc(score.CreatedAt).ToString("dd/MM"),
                    Percentage = score.Percentage
                })
                .ToList();

            model.SubjectStats = statistics.SubjectStats
                .Select(group => BuildGroupPerformance(group.Name, group))
                .ToList();

            model.DeckStats = statistics.DeckStats
                .Select(group => BuildGroupPerformance(
                    $"{group.ParentName} / {group.Name}",
                    group))
                .ToList();

            return View(model);
        }

        public async Task<IActionResult> Leaderboard(int deckId)
        {
            var deck = _deckService.GetDeckForStudy(deckId);
            if (deck is null) return NotFound();

            var leaderboard = await _quizService.GetLeaderboardAsync(deckId, 20);
            var entries = leaderboard
                .Select((entry, index) =>
                {
                    return new LeaderboardEntryViewModel
                    {
                        Rank = index + 1,
                        Username = entry.Username,
                        BestPercentage = Math.Round(entry.BestPercentage, 1),
                        AttemptCount = entry.AttemptCount,
                        LastAttemptAt = entry.LastAttemptAt
                    };
                })
                .ToList();

            ViewBag.SubjectId = deck.SubjectId;

            var model = new LeaderboardViewModel
            {
                DeckId = deckId,
                DeckName = deck.Name,
                SubjectName = deck.Subject.Name,
                Entries = entries
            };

            return View(model);
        }

        [Authorize(Policy = "ManageContent")]
        public async Task<IActionResult> MentorStats()
        {
            var isAdmin = User.IsInRole(AppRoles.Admin);
            var statistics = await _quizService
                .GetMentorStatisticsAsync(CurrentUserId(), isAdmin);
            var subjectStats = statistics.SubjectStats
                .Select(group =>
                {
                    return new MentorSubjectStatViewModel
                    {
                        SubjectName = group.Name,
                        TotalAttempts = group.Attempts,
                        UniqueUsers = group.UniqueUsers,
                        AvgPercentage = Math.Round(group.AveragePercentage, 1),
                        BestPercentage = Math.Round(group.BestPercentage, 1)
                    };
                })
                .ToList();

            var deckStats = statistics.DeckStats
                .Select(group =>
                {
                    return new MentorDeckStatViewModel
                    {
                        DeckId = group.Id,
                        DeckName = group.Name,
                        SubjectName = group.ParentName ?? string.Empty,
                        TotalAttempts = group.Attempts,
                        UniqueUsers = group.UniqueUsers,
                        AvgPercentage = Math.Round(group.AveragePercentage, 1),
                        BestPercentage = Math.Round(group.BestPercentage, 1),
                        LastAttemptAt = group.LastAttemptAt
                    };
                })
                .ToList();

            return View(new MentorStatsViewModel
            {
                TotalAttempts = statistics.TotalAttempts,
                UniqueUsers = statistics.UniqueUsers,
                OverallAvgPercentage = Math.Round(statistics.AveragePercentage, 1),
                OverallBestPercentage = Math.Round(statistics.BestPercentage, 1),
                SubjectStats = subjectStats,
                DeckStats = deckStats
            });
        }

        private static GroupPerformanceViewModel BuildGroupPerformance(
            string name,
            AnalyticsGroupReadModel group)
            => new()
            {
                Name = name,
                Attempts = group.Attempts,
                AveragePercentage = Math.Round(group.AveragePercentage, 1),
                BestPercentage = Math.Round(group.BestPercentage, 1),
                LastAttemptAt = group.LastAttemptAt
            };

        private string CurrentUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new InvalidOperationException("Không tìm thấy UserId trong phiên đăng nhập.");
        }
    }
}
