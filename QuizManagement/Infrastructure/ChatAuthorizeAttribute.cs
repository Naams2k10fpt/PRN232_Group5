using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace QuizManagement.Infrastructure;

// Shared chat endpoints serve web cookies and mobile JWTs. Cookie writes must carry the web CSRF token.
public sealed class ChatAuthorizeAttribute : AuthorizeAttribute, IAsyncActionFilter
{
    public ChatAuthorizeAttribute() => AuthenticationSchemes =
        CookieAuthenticationDefaults.AuthenticationScheme + "," + JwtBearerDefaults.AuthenticationScheme;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;
        if (!HttpMethods.IsGet(http.Request.Method) && !HttpMethods.IsHead(http.Request.Method)
            && !(await http.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme)).Succeeded)
        {
            try { await http.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(http); }
            catch (AntiforgeryValidationException)
            {
                context.Result = new ObjectResult(new ApiError("invalid_csrf", "Phiên làm việc đã thay đổi. Tải lại trang.")) { StatusCode = 403 };
                return;
            }
        }
        await next();
    }
}
