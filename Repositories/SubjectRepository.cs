using BusinessObjects;
using DataAccessObjects;
using System.Collections.Generic;
using System.Linq;

namespace Repositories
{
    public class SubjectRepository : ISubjectRepository
    {
        private readonly QuizManagementDbContext _context;

        // Tiêm DbContext vào (Khắc phục hoàn toàn lỗi Pseudo-DI)
        public SubjectRepository(QuizManagementDbContext context)
        {
            _context = context;
        }

        public IEnumerable<Subject> GetAllSubjects()
            => SubjectDAO.Instance.GetAllSubjects(_context);

        public Subject? GetSubjectForStudy(int id)
            => SubjectDAO.Instance.GetSubjectForStudy(_context, id);

        public Subject? GetSubjectById(int id, string userId, bool allowAll = false)
            => SubjectDAO.Instance.GetSubjectById(_context, id, userId, allowAll);

        public bool NameExists(string userId, string name, int? excludedId = null)
            => SubjectDAO.Instance.NameExists(_context, userId, name, excludedId);

        public void AddSubject(Subject subject)
            => SubjectDAO.Instance.AddSubject(_context, subject);

        public void UpdateSubject(Subject subject)
            => SubjectDAO.Instance.UpdateSubject(_context, subject);

        public void DeleteSubject(Subject subject)
            => SubjectDAO.Instance.DeleteSubject(_context, subject);
    }
}
