using BusinessObjects;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DataAccessObjects
{
    public class QuestionDAO
    {
        private static QuestionDAO? instance;
        private static readonly object instanceLock = new();

        private QuestionDAO()
        {
        }

        public static QuestionDAO Instance
        {
            get
            {
                lock (instanceLock)
                {
                    instance ??= new QuestionDAO();
                    return instance;
                }
            }
        }

        public IEnumerable<Question> GetQuestionsByDeckForStudy(
            QuizManagementDbContext context, int deckId)
        {
            return context.Questions
                .Include(q => q.Answers)
                .Include(q => q.Deck)
                .ThenInclude(d => d.Subject)
                .Where(q => q.DeckId == deckId)
                .OrderByDescending(q => q.CreatedAt)
                .ToList();
        }

        public IEnumerable<FlashcardProgress> GetFlashcardProgresses(
            QuizManagementDbContext context,
            string userId,
            int deckId)
        {
            return context.FlashcardProgresses
                .AsNoTracking()
                .Where(progress => progress.UserId == userId && progress.Question.DeckId == deckId)
                .ToList();
        }

        public FlashcardProgress ReviewFlashcard(
            QuizManagementDbContext context,
            string userId,
            int questionId,
            bool remembered,
            DateTime reviewedAtUtc)
        {
            var progress = context.FlashcardProgresses
                .SingleOrDefault(item => item.UserId == userId && item.QuestionId == questionId);
            if (progress is null)
            {
                progress = new FlashcardProgress { UserId = userId, QuestionId = questionId };
                context.FlashcardProgresses.Add(progress);
            }

            progress.Review(remembered, reviewedAtUtc);
            context.SaveChanges();
            return progress;
        }

        public Question? GetQuestionById(
            QuizManagementDbContext context, int id, string userId, bool allowAll = false)
        {
            return context.Questions
                .Include(q => q.Answers)
                .Include(q => q.Deck)
                .ThenInclude(d => d.Subject)
                .FirstOrDefault(q => q.Id == id && (allowAll || q.Deck.Subject.UserId == userId));
        }

        public void AddQuestion(QuizManagementDbContext context, Question question)
            => AddQuestions(context, [question]);

        public void AddQuestions(QuizManagementDbContext context, IEnumerable<Question> questions)
        {
            var batch = questions.ToList();
            var createdAt = DateTime.UtcNow;
            foreach (var question in batch)
                question.CreatedAt = createdAt;

            context.Questions.AddRange(batch);
            context.SaveChanges();
        }

        public QuestionUpdateResult TryUpdateQuestion(QuizManagementDbContext context, Question question)
        {
            var existingQuestion = context.Questions
                .Include(q => q.Answers)
                .First(q => q.Id == question.Id);

            var incomingAnswers = question.Answers.ToList();
            var incomingIds = incomingAnswers.Where(a => a.Id > 0).Select(a => a.Id).ToHashSet();
            var removedAnswers = existingQuestion.Answers.Where(a => !incomingIds.Contains(a.Id)).ToList();
            var removedIds = removedAnswers.Select(answer => answer.Id).ToList();
            if (removedIds.Count > 0 && context.TestResultDetails
                    .Any(detail => detail.SelectedAnswerId.HasValue
                        && removedIds.Contains(detail.SelectedAnswerId.Value)))
            {
                return QuestionUpdateResult.ReferencedAnswer;
            }

            if (question.RowVersion.Length != 8)
            {
                context.ChangeTracker.Clear();
                return QuestionUpdateResult.ConcurrencyConflict;
            }

            context.Entry(existingQuestion)
                .Property(existing => existing.RowVersion)
                .OriginalValue = question.RowVersion;

            existingQuestion.Content = question.Content;
            existingQuestion.Explanation = question.Explanation;
            existingQuestion.QuestionType = question.QuestionType;
            existingQuestion.UpdatedAt = DateTime.UtcNow;
            existingQuestion.UpdatedBy = question.UpdatedBy;

            context.Answers.RemoveRange(removedAnswers);

            foreach (var incomingAnswer in incomingAnswers)
            {
                if (incomingAnswer.Id > 0)
                {
                    var existingAnswer = existingQuestion.Answers.First(a => a.Id == incomingAnswer.Id);
                    existingAnswer.Content = incomingAnswer.Content;
                    existingAnswer.IsCorrect = incomingAnswer.IsCorrect;
                }
                else
                {
                    existingQuestion.Answers.Add(new Answer
                    {
                        Content = incomingAnswer.Content,
                        IsCorrect = incomingAnswer.IsCorrect
                    });
                }
            }

            try
            {
                context.SaveChanges();
                return QuestionUpdateResult.Updated;
            }
            catch (DbUpdateConcurrencyException)
            {
                context.ChangeTracker.Clear();
                return QuestionUpdateResult.ConcurrencyConflict;
            }
        }

        public void DeleteQuestion(QuizManagementDbContext context, Question question)
        {
            question.IsDeleted = true;
            question.UpdatedAt = DateTime.UtcNow;
            context.Questions.Update(question);
            context.SaveChanges();
        }
    }
}
