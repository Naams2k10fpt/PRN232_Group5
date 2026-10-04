using System;

namespace BusinessObjects;

public partial class StudyGroupMember
{
    public int Id { get; set; }

    public int GroupId { get; set; }

    public string UserId { get; set; } = null!;

    // StudyGroupRoles.Owner or StudyGroupRoles.Member.
    public string Role { get; set; } = StudyGroupRoles.Member;

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public virtual StudyGroup Group { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}

public static class StudyGroupRoles
{
    public const string Owner = "Owner";
    public const string Member = "Member";
}
