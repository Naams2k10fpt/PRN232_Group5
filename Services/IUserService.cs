using BusinessObjects;

namespace Services
{
    public interface IUserService
    {
        User? GetByEmail(string email);
        User? GetByUsername(string username);
        User? GetById(string id);
        bool TryCreateUser(User user);
        void UpdateProfile(User user);
        void ChangePassword(User user, string newPasswordHash);
    }
}
