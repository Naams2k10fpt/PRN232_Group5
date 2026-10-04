namespace BusinessObjects;

public sealed class StudyGroup
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? SubjectId { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public Subject? Subject { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public ICollection<StudyGroupMember> Members { get; set; } = new List<StudyGroupMember>();
    public ICollection<GroupMessage> Messages { get; set; } = new List<GroupMessage>();
}
