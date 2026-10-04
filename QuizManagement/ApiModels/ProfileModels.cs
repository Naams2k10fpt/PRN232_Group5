using QuizManagement.ViewModels.Profile;

namespace QuizManagement.ApiModels;

// Same fields and validation as the web profile forms.
public sealed class UpdateProfileRequest : EditProfileViewModel;

public sealed class ChangePasswordRequest : ChangePasswordViewModel;

public sealed record AccessTokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
