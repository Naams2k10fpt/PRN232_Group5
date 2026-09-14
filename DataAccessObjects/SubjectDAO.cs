using BusinessObjects;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DataAccessObjects
{
    public class SubjectDAO
    {
        private static SubjectDAO? instance;
        private static readonly object instanceLock = new();

        private SubjectDAO()
        {
        }

        public static SubjectDAO Instance
        {
            get
            {
                lock (instanceLock)
                {
                    instance ??= new SubjectDAO();
                    return instance;
                }
            }
        }

        public IEnumerable<Subject> GetAllSubjects(QuizManagementDbContext context)
        {
            return context.Subjects
                .OrderBy(s => s.Name)
                .ThenBy(s => s.CreatedAt)
                .ToList();
        }

        public Subject? GetSubjectForStudy(QuizManagementDbContext context, int id)
        {
            return context.Subjects.FirstOrDefault(s => s.Id == id);
        }

        public Subject? GetSubjectById(
            QuizManagementDbContext context, int id, string userId, bool allowAll = false)
        {
            return context.Subjects.FirstOrDefault(s =>
                s.Id == id && (allowAll || s.UserId == userId));
        }

        public bool NameExists(QuizManagementDbContext context, string userId, string name, int? excludedId = null)
        {
            return context.Subjects.Any(s =>
                s.UserId == userId &&
                s.Name == name &&
                (!excludedId.HasValue || s.Id != excludedId.Value));
        }

        public void AddSubject(QuizManagementDbContext context, Subject subject)
        {
            subject.CreatedAt = DateTime.UtcNow;
            context.Subjects.Add(subject);
            context.SaveChanges();
        }

        public void UpdateSubject(QuizManagementDbContext context, Subject subject)
        {
            subject.UpdatedAt = DateTime.UtcNow;
            context.Subjects.Update(subject);
            context.SaveChanges();
        }

        public void DeleteSubject(QuizManagementDbContext context, Subject subject)
        {
            var decks = context.Decks
                .IgnoreQueryFilters()
                .Where(deck => deck.SubjectId == subject.Id)
                .ToList();
            var deckIds = decks.Select(deck => deck.Id).ToList();
            var questions = deckIds.Count == 0
                ? new List<Question>()
                : context.Questions
                    .IgnoreQueryFilters()
                    .Where(question => deckIds.Contains(question.DeckId))
                    .ToList();
            var now = DateTime.UtcNow;

            subject.IsDeleted = true;
            subject.UpdatedAt = now;

            foreach (var deck in decks.Where(deck => !deck.IsDeleted))
            {
                deck.IsDeleted = true;
                deck.UpdatedAt = now;
            }

            foreach (var question in questions.Where(question => !question.IsDeleted))
            {
                question.IsDeleted = true;
                question.UpdatedAt = now;
            }

            context.Subjects.Update(subject);
            context.SaveChanges();
        }
    }
}
