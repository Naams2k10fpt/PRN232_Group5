using BusinessObjects;

namespace Repositories
{
    public interface IAdminRepository
    {
        (int users, int subjects, int decks, int questions, int testHistories) GetSystemStats();
        List<User> GetAllUsers(string? search, string? roleFilter);
        User? GetUserById(string id);
        (int subjects, int decks, int questions, int testHistories) GetUserStats(string userId);
        AdminMutationResult UpdateUserAccess(
            string userId,
            string? newRole,
            bool? isDisabled,
            string securityStamp);
    }
}
