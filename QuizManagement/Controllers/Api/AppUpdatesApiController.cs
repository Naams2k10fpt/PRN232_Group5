using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace QuizManagement.Controllers.Api;

[ApiController]
public sealed class AppUpdatesApiController(IConfiguration configuration) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("api/app-updates/android")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Android()
    {
        var release = configuration.GetSection("AppUpdates:Android");
        var versionName = release["VersionName"]?.Trim();
        var abi = release["Abi"]?.Trim();
        if (string.IsNullOrWhiteSpace(versionName)
            || abi is not ("arm64-v8a" or "armeabi-v7a" or "x86_64" or "universal")
            || !int.TryParse(release["VersionCode"], out var versionCode)
            || versionCode is <= 0 or > 2_100_000_000
            || !Uri.TryCreate(release["UpdateUrl"], UriKind.Absolute, out var updateUrl)
            || updateUrl.Scheme != Uri.UriSchemeHttps
            || string.IsNullOrEmpty(updateUrl.Host)
            || updateUrl.UserInfo.Length != 0)
        {
            return NoContent();
        }

        return Ok(new { versionName, versionCode, abi, updateUrl = updateUrl.AbsoluteUri });
    }
}
