namespace BusinessObjects;

public enum StudyGroupRequestStatus
{
    Pending,
    Approved,
    Rejected,
    Cancelled
}

public sealed class StudyGroupJoinRequest
{
    public int GroupId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public StudyGroupRequestStatus Status { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedByUserId { get; set; }
    public StudyGroup Group { get; set; } = null!;
    public User User { get; set; } = null!;
}
