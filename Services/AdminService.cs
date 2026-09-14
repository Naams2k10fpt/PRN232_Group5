using BusinessObjects;
using Repositories;

namespace Services
{
    public class AdminService : IAdminService
    {
        private readonly IAdminRepository _repository;

        public AdminService(IAdminRepository repository)
        {
            _repository = repository;
        }

        public (int users, int subjects, int decks, int questions, int testHistories) GetSystemStats()
            => _repository.GetSystemStats();

        public List<User> GetAllUsers(string? search = null, string? roleFilter = null)
            => _repository.GetAllUsers(search, roleFilter);

        public User? GetUserById(string id) => _repository.GetUserById(id);

        public (int subjects, int decks, int questions, int testHistories) GetUserStats(string userId)
            => _repository.GetUserStats(userId);

        public AdminMutationResult ChangeRole(string userId, string newRole)
            => _repository.UpdateUserAccess(
                userId,
                newRole,
                isDisabled: null,
                Guid.NewGuid().ToString());

        public AdminMutationResult SetDisabled(string userId, bool disabled)
            => _repository.UpdateUserAccess(
                userId,
                newRole: null,
                disabled,
                Guid.NewGuid().ToString());
    }
}
