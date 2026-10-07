namespace BusinessObjects;

public sealed class StudyGroupInviteLink
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public string ProtectedToken { get; set; } = string.Empty;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public StudyGroup Group { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
}
