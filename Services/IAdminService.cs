using BusinessObjects;

namespace Services
{
    public interface IAdminService
    {
        (int users, int subjects, int decks, int questions, int testHistories) GetSystemStats();
        List<User> GetAllUsers(string? search = null, string? roleFilter = null);
        User? GetUserById(string id);
        (int subjects, int decks, int questions, int testHistories) GetUserStats(string userId);
        AdminMutationResult ChangeRole(string userId, string newRole);
        AdminMutationResult SetDisabled(string userId, bool disabled);
    }
}
