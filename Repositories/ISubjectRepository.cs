using System.Collections.Generic;
using BusinessObjects;

namespace Repositories
{
    public interface ISubjectRepository
    {
        IEnumerable<Subject> GetAllSubjects();
        Subject? GetSubjectForStudy(int id);
        Subject? GetSubjectById(int id, string userId, bool allowAll = false);
        bool NameExists(string userId, string name, int? excludedId = null);
        void AddSubject(Subject subject);
        void UpdateSubject(Subject subject);
        void DeleteSubject(Subject subject);
    }
}
