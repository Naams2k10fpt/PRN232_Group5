using BusinessObjects;
using DataAccessObjects;

namespace Repositories
{
    public class AdminRepository : IAdminRepository
    {
        private readonly QuizManagementDbContext _context;

        public AdminRepository(QuizManagementDbContext context)
        {
            _context = context;
        }

        public (int users, int subjects, int decks, int questions, int testHistories) GetSystemStats()
            => AdminDAO.Instance.GetSystemStats(_context);

        public List<User> GetAllUsers(string? search, string? roleFilter)
            => AdminDAO.Instance.GetAllUsers(_context, search, roleFilter);

        public User? GetUserById(string id)
            => AdminDAO.Instance.GetUserById(_context, id);

        public (int subjects, int decks, int questions, int testHistories) GetUserStats(string userId)
            => AdminDAO.Instance.GetUserStats(_context, userId);

        public AdminMutationResult UpdateUserAccess(
            string userId,
            string? newRole,
            bool? isDisabled,
            string securityStamp)
            => AdminDAO.Instance.UpdateUserAccess(
                _context,
                userId,
                newRole,
                isDisabled,
                securityStamp);
    }
}
