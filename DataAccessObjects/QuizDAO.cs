using BusinessObjects;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DataAccessObjects
{
    public class QuizDAO
    {
        private static QuizDAO? instance;
        private static readonly object instanceLock = new();

        private QuizDAO()
        {
        }

        public static QuizDAO Instance
        {
            get
            {
                lock (instanceLock)
                {
                    instance ??= new QuizDAO();
                    return instance;
                }
            }
        }

        public List<Question> GetQuestionsForQuiz(QuizManagementDbContext context, int deckId)
        {
            return context.Questions
                .Include(q => q.Answers)
                .Include(q => q.Deck)
                    .ThenInclude(d => d.Subject)
                .Where(q => q.DeckId == deckId)
                .AsNoTracking()
                .ToList();
        }

        public int GetQuestionCount(QuizManagementDbContext context, int deckId)
        {
            return context.Questions
                .Count(q => q.DeckId == deckId);
        }

        public QuizAttempt AddQuizAttempt(QuizManagementDbContext context, QuizAttempt attempt)
        {
            context.QuizAttempts.Add(attempt);
            context.SaveChanges();
            return attempt;
        }

        public QuizAttempt? GetQuizAttempt(
            QuizManagementDbContext context,
            Guid attemptId,
            int deckId,
            string userId)
        {
            return context.QuizAttempts
                .AsNoTracking()
                .FirstOrDefault(attempt => attempt.Id == attemptId
                    && attempt.DeckId == deckId
                    && attempt.UserId == userId);
        }

        public TestHistory? GetTestHistoryByQuizAttempt(
            QuizManagementDbContext context,
            Guid attemptId,
            int deckId,
            string userId)
        {
            return context.TestHistories
                .AsNoTracking()
                .FirstOrDefault(history => history.QuizAttemptId == attemptId
                    && history.DeckId == deckId
                    && history.UserId == userId);
        }

        public TestHistory? CompleteQuizAttempt(
            QuizManagementDbContext context,
            Guid attemptId,
            int deckId,
            string userId,
            DateTimeOffset completedAtUtc,
            TestHistory history)
        {
            using var transaction = context.Database.BeginTransaction();
            try
            {
                var claimed = context.QuizAttempts
                    .Where(attempt => attempt.Id == attemptId
                        && attempt.DeckId == deckId
                        && attempt.UserId == userId
                        && attempt.CompletedAtUtc == null
                        && (attempt.ExpiresAtUtc == null || completedAtUtc <= attempt.ExpiresAtUtc))
                    .ExecuteUpdate(setters => setters
                        .SetProperty(attempt => attempt.CompletedAtUtc, completedAtUtc));

                if (claimed == 0)
                {
                    var existing = GetTestHistoryByQuizAttempt(
                        context,
                        attemptId,
                        deckId,
                        userId);
                    transaction.Commit();
                    return existing;
                }

                history.QuizAttemptId = attemptId;
                context.TestHistories.Add(history);
                context.SaveChanges();
                transaction.Commit();
                return history;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public TestHistory? GetTestHistoryById(QuizManagementDbContext context, int id, string userId)
        {
            return context.TestHistories
                .IgnoreQueryFilters()
                .Include(h => h.Deck)
                    .ThenInclude(d => d.Subject)
                .Include(h => h.TestResultDetails)
                    .ThenInclude(d => d.Question)
                        .ThenInclude(q => q.Answers)
                .Include(h => h.TestResultDetails)
                    .ThenInclude(d => d.SelectedAnswer)
                .FirstOrDefault(h => h.Id == id && h.UserId == userId);
        }

        public List<TestHistory> GetTestHistoriesByUser(QuizManagementDbContext context, string userId)
        {
            return context.TestHistories
                .IgnoreQueryFilters()
                .Include(h => h.Deck)
                    .ThenInclude(d => d.Subject)
                .Where(h => h.UserId == userId)
                .OrderByDescending(h => h.CreatedAt)
                .ToList();
        }

        public async Task<TestHistoryPage> GetTestHistoryPageAsync(
            QuizManagementDbContext context,
            string userId,
            int page,
            int pageSize)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);
            var query = context.TestHistories
                .IgnoreQueryFilters()
                .Where(history => history.UserId == userId);
            var totalCount = await query.CountAsync();
            page = Math.Min(page, Math.Max(1, (int)Math.Ceiling((double)totalCount / pageSize)));
            var items = await query
                .Include(history => history.Deck)
                    .ThenInclude(deck => deck.Subject)
                .AsNoTracking()
                .OrderByDescending(history => history.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new TestHistoryPage
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<UserStatisticsReadModel> GetUserStatisticsAsync(
            QuizManagementDbContext context,
            string userId)
        {
            var query = context.TestHistories
                .IgnoreQueryFilters()
                .Where(history => history.UserId == userId);
            var overall = await query
                .GroupBy(_ => 1)
                .Select(group => new
                {
                    Total = group.Count(),
                    Average = group.Average(history => history.Percentage),
                    Best = group.Max(history => history.Percentage),
                    Lowest = group.Min(history => history.Percentage),
                    Passed = group.Count(history => history.Percentage >= 50)
                })
                .SingleOrDefaultAsync();
            var recentScores = await query
                .OrderByDescending(history => history.CreatedAt)
                .Take(12)
                .Select(history => new ScorePointReadModel
                {
                    CreatedAt = history.CreatedAt,
                    Percentage = history.Percentage
                })
                .ToListAsync();
            recentScores.Reverse();

            var subjectStats = await query
                .GroupBy(history => new
                {
                    history.Deck.Subject.Id,
                    history.Deck.Subject.Name
                })
                .Select(group => new AnalyticsGroupReadModel
                {
                    Id = group.Key.Id,
                    Name = group.Key.Name,
                    Attempts = group.Count(),
                    AveragePercentage = group.Average(history => history.Percentage),
                    BestPercentage = group.Max(history => history.Percentage),
                    LastAttemptAt = group.Max(history => history.CreatedAt)
                })
                .OrderByDescending(group => group.AveragePercentage)
                .ThenBy(group => group.Name)
                .ToListAsync();

            var deckStats = await query
                .GroupBy(history => new
                {
                    history.DeckId,
                    DeckName = history.Deck.Name,
                    SubjectName = history.Deck.Subject.Name
                })
                .Select(group => new AnalyticsGroupReadModel
                {
                    Id = group.Key.DeckId,
                    Name = group.Key.DeckName,
                    ParentName = group.Key.SubjectName,
                    Attempts = group.Count(),
                    AveragePercentage = group.Average(history => history.Percentage),
                    BestPercentage = group.Max(history => history.Percentage),
                    LastAttemptAt = group.Max(history => history.CreatedAt)
                })
                .OrderByDescending(group => group.AveragePercentage)
                .ThenBy(group => group.ParentName)
                .ThenBy(group => group.Name)
                .ToListAsync();

            return new UserStatisticsReadModel
            {
                TotalAttempts = overall?.Total ?? 0,
                AveragePercentage = overall?.Average ?? 0,
                BestPercentage = overall?.Best ?? 0,
                LowestPercentage = overall?.Lowest ?? 0,
                PassedAttempts = overall?.Passed ?? 0,
                RecentScores = recentScores,
                SubjectStats = subjectStats,
                DeckStats = deckStats
            };
        }

        public Task<List<LeaderboardEntryReadModel>> GetLeaderboardAsync(
            QuizManagementDbContext context,
            int deckId,
            int count)
            => context.TestHistories
                .Where(history => history.DeckId == deckId)
                .GroupBy(history => new { history.UserId, history.User.Username })
                .Select(group => new LeaderboardEntryReadModel
                {
                    Username = group.Key.Username,
                    BestPercentage = group.Max(history => history.Percentage),
                    AttemptCount = group.Count(),
                    LastAttemptAt = group.Max(history => history.CreatedAt)
                })
                .OrderByDescending(entry => entry.BestPercentage)
                .ThenByDescending(entry => entry.LastAttemptAt)
                .Take(Math.Clamp(count, 1, 100))
                .ToListAsync();

        public async Task<MentorStatisticsReadModel> GetMentorStatisticsAsync(
            QuizManagementDbContext context,
            string ownerUserId,
            bool isAdmin)
        {
            var query = context.TestHistories
                .IgnoreQueryFilters()
                .AsQueryable();
            if (!isAdmin)
            {
                query = query.Where(history => history.Deck.Subject.UserId == ownerUserId);
            }

            var overall = await query
                .GroupBy(_ => 1)
                .Select(group => new
                {
                    Total = group.Count(),
                    UniqueUsers = group.Select(history => history.UserId).Distinct().Count(),
                    Average = group.Average(history => history.Percentage),
                    Best = group.Max(history => history.Percentage)
                })
                .SingleOrDefaultAsync();
            var subjectStats = await query
                .GroupBy(history => new
                {
                    history.Deck.Subject.Id,
                    history.Deck.Subject.Name
                })
                .Select(group => new AnalyticsGroupReadModel
                {
                    Id = group.Key.Id,
                    Name = group.Key.Name,
                    Attempts = group.Count(),
                    UniqueUsers = group.Select(history => history.UserId).Distinct().Count(),
                    AveragePercentage = group.Average(history => history.Percentage),
                    BestPercentage = group.Max(history => history.Percentage)
                })
                .OrderByDescending(group => group.Attempts)
                .ThenBy(group => group.Name)
                .ToListAsync();
            var deckStats = await query
                .GroupBy(history => new
                {
                    history.DeckId,
                    DeckName = history.Deck.Name,
                    SubjectName = history.Deck.Subject.Name
                })
                .Select(group => new AnalyticsGroupReadModel
                {
                    Id = group.Key.DeckId,
                    Name = group.Key.DeckName,
                    ParentName = group.Key.SubjectName,
                    Attempts = group.Count(),
                    UniqueUsers = group.Select(history => history.UserId).Distinct().Count(),
                    AveragePercentage = group.Average(history => history.Percentage),
                    BestPercentage = group.Max(history => history.Percentage),
                    LastAttemptAt = group.Max(history => history.CreatedAt)
                })
                .OrderByDescending(group => group.Attempts)
                .ThenBy(group => group.ParentName)
                .ThenBy(group => group.Name)
                .ToListAsync();

            return new MentorStatisticsReadModel
            {
                TotalAttempts = overall?.Total ?? 0,
                UniqueUsers = overall?.UniqueUsers ?? 0,
                AveragePercentage = overall?.Average ?? 0,
                BestPercentage = overall?.Best ?? 0,
                SubjectStats = subjectStats,
                DeckStats = deckStats
            };
        }

        public List<TestHistory> GetRecentTestHistories(QuizManagementDbContext context, string userId, int count)
        {
            return context.TestHistories
                .IgnoreQueryFilters()
                .Include(h => h.Deck)
                    .ThenInclude(d => d.Subject)
                .Where(h => h.UserId == userId)
                .AsNoTracking()
                .OrderByDescending(h => h.CreatedAt)
                .Take(count)
                .ToList();
        }

        public (int totalQuizzes, double averagePercentage, DateTime? lastQuizDate) GetQuizStatistics(
            QuizManagementDbContext context, string userId)
        {
            var histories = context.TestHistories.Where(h => h.UserId == userId);
            var total = histories.Count();
            if (total == 0) return (0, 0, null);
            var avg = histories.Average(h => h.Percentage);
            var last = histories.Max(h => h.CreatedAt);
            return (total, Math.Round(avg, 1), last);
        }

        public List<TestHistory> GetTestHistoriesByDeck(QuizManagementDbContext context, int deckId)
        {
            return context.TestHistories
                .IgnoreQueryFilters()
                .Include(h => h.User)
                .Include(h => h.Deck)
                    .ThenInclude(d => d.Subject)
                .Where(h => h.DeckId == deckId)
                .AsNoTracking()
                .OrderByDescending(h => h.Percentage)
                .ThenByDescending(h => h.CreatedAt)
                .ToList();
        }

        public List<TestHistory> GetTestHistoriesByContentOwner(
            QuizManagementDbContext context, string ownerUserId, bool isAdmin)
        {
            var query = context.TestHistories
                .IgnoreQueryFilters()
                .Include(h => h.User)
                .Include(h => h.Deck)
                    .ThenInclude(d => d.Subject)
                .AsQueryable();

            if (!isAdmin)
                query = query.Where(h => h.Deck.Subject.UserId == ownerUserId);

            return query.AsNoTracking().OrderByDescending(h => h.CreatedAt).ToList();
        }
    }
}
