using BusinessObjects;
using Microsoft.EntityFrameworkCore;

namespace DataAccessObjects
{
    // Read-only queries behind the study catalog (subjects, decks, flashcards) used by the mobile API.
    // Everything is AsNoTracking and counts are computed in SQL, so a list costs one or two statements.
    public class CatalogDAO
    {
        private static CatalogDAO? instance;
        private static readonly object instanceLock = new();

        private CatalogDAO()
        {
        }

        public static CatalogDAO Instance
        {
            get
            {
                lock (instanceLock)
                {
                    instance ??= new CatalogDAO();
                    return instance;
                }
            }
        }

        public PagedResult<SubjectSummaryReadModel> GetSubjectSummaries(
            QuizManagementDbContext context, string? search, int page, int pageSize)
        {
            var query = context.Subjects.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var normalizedSearch = TextNormalization.Normalize(search);
                query = query.Where(subject =>
                    EF.Property<string>(subject, "NormalizedName").Contains(normalizedSearch));
            }

            var totalCount = query.Count();
            var items = ProjectSubjects(query
                    .OrderBy(subject => subject.Name)
                    .ThenBy(subject => subject.Id)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize))
                .ToList();

            return new PagedResult<SubjectSummaryReadModel>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public SubjectSummaryReadModel? GetSubjectSummary(QuizManagementDbContext context, int id)
        {
            return ProjectSubjects(context.Subjects.AsNoTracking().Where(subject => subject.Id == id))
                .FirstOrDefault();
        }

        public bool SubjectExists(QuizManagementDbContext context, int id)
        {
            return context.Subjects.Any(subject => subject.Id == id);
        }

        public List<DeckSummaryReadModel> GetDeckSummaries(
            QuizManagementDbContext context, int subjectId, string userId, DateTime nowUtc)
        {
            return ProjectDecks(
                    context.Decks.AsNoTracking()
                        .Where(deck => deck.SubjectId == subjectId)
                        .OrderBy(deck => deck.Name)
                        .ThenBy(deck => deck.Id),
                    userId,
                    nowUtc)
                .ToList();
        }

        public DeckSummaryReadModel? GetDeckSummary(
            QuizManagementDbContext context, int deckId, string userId, DateTime nowUtc)
        {
            return ProjectDecks(
                    context.Decks.AsNoTracking().Where(deck => deck.Id == deckId),
                    userId,
                    nowUtc)
                .FirstOrDefault();
        }

        // Same order as the web flashcard page: newest question first.
        public List<Question> GetFlashcardsByDeck(QuizManagementDbContext context, int deckId)
        {
            return context.Questions
                .AsNoTracking()
                .Include(question => question.Answers)
                .Where(question => question.DeckId == deckId)
                .OrderByDescending(question => question.CreatedAt)
                .ThenBy(question => question.Id)
                .ToList();
        }

        public bool QuestionBelongsToDeck(QuizManagementDbContext context, int deckId, int questionId)
        {
            return context.Questions.Any(question => question.Id == questionId && question.DeckId == deckId);
        }

        // The soft-delete query filters also apply to the navigations below, so deleted decks and
        // questions are not counted.
        private static IQueryable<SubjectSummaryReadModel> ProjectSubjects(IQueryable<Subject> subjects)
        {
            return subjects.Select(subject => new SubjectSummaryReadModel
            {
                Id = subject.Id,
                Name = subject.Name,
                OwnerName = subject.User.Username,
                DeckCount = subject.Decks.Count(),
                QuestionCount = subject.Decks.SelectMany(deck => deck.Questions).Count(),
                CreatedAt = subject.CreatedAt
            });
        }

        // A user has at most one progress row per question (unique index), so "due" is simply
        // "no progress row that is still waiting for its next review".
        private static IQueryable<DeckSummaryReadModel> ProjectDecks(
            IQueryable<Deck> decks, string userId, DateTime nowUtc)
        {
            return decks.Select(deck => new DeckSummaryReadModel
            {
                Id = deck.Id,
                SubjectId = deck.SubjectId,
                SubjectName = deck.Subject.Name,
                Name = deck.Name,
                TimeLimitMinutes = deck.TimeLimitMinutes,
                QuestionCount = deck.Questions.Count(),
                DueCount = deck.Questions.Count(question =>
                    !question.FlashcardProgresses.Any(progress =>
                        progress.UserId == userId && progress.NextReviewAtUtc > nowUtc)),
                LearnedCount = deck.Questions.Count(question =>
                    question.FlashcardProgresses.Any(progress =>
                        progress.UserId == userId && progress.Repetition > 0)),
                NextReviewAtUtc = deck.Questions
                    .SelectMany(question => question.FlashcardProgresses)
                    .Where(progress => progress.UserId == userId && progress.NextReviewAtUtc > nowUtc)
                    .Min(progress => (DateTime?)progress.NextReviewAtUtc)
            });
        }
    }
}
