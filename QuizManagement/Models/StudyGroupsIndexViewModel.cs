using BusinessObjects;
using QuizManagement.Services;

namespace QuizManagement.Models;

public sealed class StudyGroupsIndexViewModel
{
    public List<GroupSummary> MyGroups { get; init; } = [];
    public GroupDiscoverPage Discovery { get; init; } = new([], 0, 1, 12);
    public List<GroupInvitationSummary> Invitations { get; init; } = [];
    public List<GroupSubjectOption> PublicSubjects { get; init; } = [];
    public List<GroupSubjectOption> MySubjects { get; init; } = [];
    public string Search { get; init; } = string.Empty;
    public StudyGroupAccessMode? AccessMode { get; init; }
    public int? SubjectId { get; init; }
    public string Sort { get; init; } = "newest";
}
