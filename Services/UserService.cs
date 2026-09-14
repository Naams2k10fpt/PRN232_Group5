using BusinessObjects;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Repositories;

namespace Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _repository;

        public UserService(IUserRepository repository)
        {
            _repository = repository;
        }

        public User? GetByEmail(string email) => _repository.GetByEmail(email);

        public User? GetByUsername(string username) => _repository.GetByUsername(username);

        public User? GetById(string id) => _repository.GetById(id);

        public bool TryCreateUser(User user)
        {
            try
            {
                _repository.AddUser(user);
                return true;
            }
            catch (DbUpdateException exception)
                when (exception.InnerException is SqlException { Number: 2601 or 2627 })
            {
                return false;
            }
        }

        public void UpdateProfile(User user) => _repository.UpdateUser(user);

        public void ChangePassword(User user, string newPasswordHash)
        {
            user.PasswordHash = newPasswordHash;
            user.SecurityStamp = Guid.NewGuid().ToString();
            _repository.UpdateUser(user);
        }
    }
}
