namespace BusinessObjects;

public enum StudyGroupInvitationStatus
{
    Pending,
    Accepted,
    Declined,
    Revoked
}

public sealed class StudyGroupInvitation
{
    public int GroupId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string InvitedByUserId { get; set; } = string.Empty;
    public StudyGroupInvitationStatus Status { get; set; }
    public DateTime InvitedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public StudyGroup Group { get; set; } = null!;
    public User User { get; set; } = null!;
}
