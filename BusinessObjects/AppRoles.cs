namespace BusinessObjects;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Mentor = "Mentor";
    public const string User = "User";

    public static readonly string[] All = [Admin, Mentor, User];
}

public enum AdminMutationResult
{
    Updated,
    NotFound,
    LastActiveAdmin
}
