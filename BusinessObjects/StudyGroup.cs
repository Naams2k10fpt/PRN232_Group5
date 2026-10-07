namespace BusinessObjects;

public enum StudyGroupAccessMode
{
    PublicOpen,
    PublicApproval,
    PrivateInviteOnly
}

public sealed class StudyGroup
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? SubjectId { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public StudyGroupAccessMode AccessMode { get; set; }
    public Subject? Subject { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public ICollection<StudyGroupMember> Members { get; set; } = new List<StudyGroupMember>();
    public ICollection<GroupMessage> Messages { get; set; } = new List<GroupMessage>();
    public ICollection<StudyGroupJoinRequest> JoinRequests { get; set; } = new List<StudyGroupJoinRequest>();
    public ICollection<StudyGroupInvitation> Invitations { get; set; } = new List<StudyGroupInvitation>();
    public ICollection<StudyGroupInviteLink> InviteLinks { get; set; } = new List<StudyGroupInviteLink>();
}
