using BusinessObjects;
using Repositories;
using System.Collections.Generic;

namespace Services
{
    public class SubjectService : ISubjectService
    {
        private readonly ISubjectRepository _repository;

        // Tiêm Repository vào Service
        public SubjectService(ISubjectRepository repository)
        {
            _repository = repository;
        }

        public IEnumerable<Subject> GetAllSubjects() => _repository.GetAllSubjects();
        public Subject? GetSubjectForStudy(int id) => _repository.GetSubjectForStudy(id);
        public Subject? GetSubjectById(int id, string userId, bool allowAll = false)
            => _repository.GetSubjectById(id, userId, allowAll);
        public bool NameExists(string userId, string name, int? excludedId = null) => _repository.NameExists(userId, name, excludedId);
        public void AddSubject(Subject subject) => _repository.AddSubject(subject);

        // Sau này bạn có thể thêm các logic kiểm tra (validation) ở đây trước khi gọi Update/Delete
        public void UpdateSubject(Subject subject) => _repository.UpdateSubject(subject);
        public void DeleteSubject(Subject subject) => _repository.DeleteSubject(subject);
    }
}
