using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace QuizManagement.Infrastructure;

// Marks only [ApiAuthorize] endpoints as needing the bearer token in Swagger UI, so login/register show no padlock.
public sealed class BearerSecurityOperationFilter : IOperationFilter
{
    public const string SchemeId = "Bearer";

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var method = context.MethodInfo;
        var requiresToken =
            (method.GetCustomAttribute<ApiAuthorizeAttribute>() ?? method.DeclaringType?.GetCustomAttribute<ApiAuthorizeAttribute>()) is not null
            && method.GetCustomAttribute<AllowAnonymousAttribute>() is null;
        if (!requiresToken)
            return;

        operation.Security =
        [
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = SchemeId } }] = []
            }
        ];
    }
}
