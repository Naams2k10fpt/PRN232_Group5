using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

namespace QuizManagement.Infrastructure;

// Use on /api/* controllers instead of [Authorize]: authenticates with the bearer token only, so an API call
// never gets a cookie-login redirect and answers 401/403 JSON instead.
public sealed class ApiAuthorizeAttribute : AuthorizeAttribute
{
    public ApiAuthorizeAttribute()
    {
        AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme;
    }
}
