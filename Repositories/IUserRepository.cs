using BusinessObjects;

namespace Repositories
{
    public interface IUserRepository
    {
        User? GetByEmail(string email);
        User? GetByUsername(string username);
        User? GetById(string id);
        void AddUser(User user);
        void UpdateUser(User user);
    }
}
