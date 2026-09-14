using BusinessObjects;
using DataAccessObjects;
using System;
using System.Linq;

namespace Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly QuizManagementDbContext _context;

        public UserRepository(QuizManagementDbContext context)
        {
            _context = context;
        }

        public User? GetByEmail(string email)
        {
            return _context.Users.FirstOrDefault(u => u.Email == email);
        }

        public User? GetByUsername(string username)
        {
            return _context.Users.FirstOrDefault(u => u.Username == username);
        }

        public User? GetById(string id)
        {
            return _context.Users.FirstOrDefault(u => u.Id == id);
        }

        public void AddUser(User user)
        {
            user.CreatedAt = DateTime.UtcNow;
            _context.Users.Add(user);
            _context.SaveChanges();
        }

        public void UpdateUser(User user)
        {
            user.UpdatedAt = DateTime.UtcNow;
            _context.Users.Update(user);
            _context.SaveChanges();
        }
    }
}
