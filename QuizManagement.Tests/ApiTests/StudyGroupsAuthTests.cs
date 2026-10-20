using System.Net;
using Xunit;
using static QuizManagement.Tests.ApiTests.AuthApiTests;

namespace QuizManagement.Tests.ApiTests;

public class StudyGroupsAuthTests
{
    [Theory]
    [InlineData("/api/groups/discovery")]
    [InlineData("/api/groups/options")]
    [InlineData("/api/groups/invitations")]
    [InlineData("/api/groups/1/management")]
    [InlineData("/api/groups/invite/invalid")]
    public async Task ReadEndpoints_RequireBearerAuthentication(string url)
    {
        using var host = new ApiTestHost();
        using var client = host.CreateClient(new() { AllowAutoRedirect = false });
        await AssertError(await client.GetAsync(url), HttpStatusCode.Unauthorized, "unauthorized");
    }
}
