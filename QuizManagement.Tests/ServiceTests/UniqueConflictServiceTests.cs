using BusinessObjects;
using Microsoft.EntityFrameworkCore;
using Repositories;
using Services;
using Xunit;

namespace QuizManagement.Tests.ServiceTests;

public class UniqueConflictServiceTests
{
    [Fact]
    public void TryCreateUser_PropagatesNonUniqueDatabaseFailure()
    {
        var service = new UserService(new FailingUserRepository());

        Assert.Throws<DbUpdateException>(() => service.TryCreateUser(new User()));
    }

    [Fact]
    public void TryAddDeck_PropagatesNonUniqueDatabaseFailure()
    {
        var service = new DeckService(new FailingDeckRepository());

        Assert.Throws<DbUpdateException>(() => service.TryAddDeck(new Deck()));
    }

    private static DbUpdateException NonUniqueFailure()
        => new("Unrelated database failure", new InvalidOperationException("not a SQL unique error"));

    private sealed class FailingUserRepository : IUserRepository
    {
        public void AddUser(User user) => throw NonUniqueFailure();
        public User? GetByEmail(string email) => throw new NotSupportedException();
        public User? GetByUsername(string username) => throw new NotSupportedException();
        public User? GetById(string id) => throw new NotSupportedException();
        public void UpdateUser(User user) => throw new NotSupportedException();
    }

    private sealed class FailingDeckRepository : IDeckRepository
    {
        public void AddDeck(Deck deck) => throw NonUniqueFailure();
        public IEnumerable<Deck> GetDecksBySubjectForStudy(int subjectId) => throw new NotSupportedException();
        public Deck? GetDeckForStudy(int id) => throw new NotSupportedException();
        public Deck? GetDeckById(int id, string userId, bool allowAll = false) => throw new NotSupportedException();
        public bool NameExists(int subjectId, string name, int? excludedId = null) => throw new NotSupportedException();
        public void UpdateDeck(Deck deck) => throw new NotSupportedException();
        public void DeleteDeck(Deck deck) => throw new NotSupportedException();
    }
}
