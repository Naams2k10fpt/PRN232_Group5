using BusinessObjects;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DataAccessObjects
{
    public class DeckDAO
    {
        private static DeckDAO? instance;
        private static readonly object instanceLock = new();

        private DeckDAO()
        {
        }

        public static DeckDAO Instance
        {
            get
            {
                lock (instanceLock)
                {
                    instance ??= new DeckDAO();
                    return instance;
                }
            }
        }

        public IEnumerable<Deck> GetDecksBySubjectForStudy(
            QuizManagementDbContext context, int subjectId)
        {
            return context.Decks
                .Include(d => d.Subject)
                .Where(d => d.SubjectId == subjectId)
                .OrderBy(d => d.Name)
                .ToList();
        }

        public Deck? GetDeckForStudy(QuizManagementDbContext context, int id)
        {
            return context.Decks
                .Include(d => d.Subject)
                .FirstOrDefault(d => d.Id == id);
        }

        public Deck? GetDeckById(
            QuizManagementDbContext context, int id, string userId, bool allowAll = false)
        {
            return context.Decks
                .Include(d => d.Subject)
                .FirstOrDefault(d => d.Id == id && (allowAll || d.Subject.UserId == userId));
        }

        public bool NameExists(QuizManagementDbContext context, int subjectId, string name, int? excludedId = null)
        {
            return context.Decks.Any(d =>
                d.SubjectId == subjectId &&
                d.Name == name &&
                (!excludedId.HasValue || d.Id != excludedId.Value));
        }

        public void AddDeck(QuizManagementDbContext context, Deck deck)
        {
            deck.CreatedAt = DateTime.UtcNow;
            context.Decks.Add(deck);
            context.SaveChanges();
        }

        public void UpdateDeck(QuizManagementDbContext context, Deck deck)
        {
            deck.UpdatedAt = DateTime.UtcNow;
            context.Decks.Update(deck);
            context.SaveChanges();
        }

        public void DeleteDeck(QuizManagementDbContext context, Deck deck)
        {
            deck.IsDeleted = true;
            deck.UpdatedAt = DateTime.UtcNow;

            foreach (var question in context.Questions.Where(q => q.DeckId == deck.Id))
            {
                question.IsDeleted = true;
                question.UpdatedAt = DateTime.UtcNow;
            }

            context.Decks.Update(deck);
            context.SaveChanges();
        }
    }
}
