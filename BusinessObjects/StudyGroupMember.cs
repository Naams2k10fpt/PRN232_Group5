namespace BusinessObjects;

public enum StudyGroupMemberRole
{
    Member,
    Moderator,
    Owner
}

public sealed class StudyGroupMember
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public int? LastReadMessageId { get; set; }
    public StudyGroupMemberRole Role { get; set; }
    public bool IsBanned { get; set; }
    public StudyGroup Group { get; set; } = null!;
    public User User { get; set; } = null!;
}
