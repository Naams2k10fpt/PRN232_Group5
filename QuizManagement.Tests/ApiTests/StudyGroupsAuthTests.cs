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
    [InlineData("/api/groups/1/channels")]
    [InlineData("/api/chat/attachments/11111111-1111-1111-1111-111111111111")]
    public async Task ReadEndpoints_RequireAuthentication(string url)
    {
        using var host = new ApiTestHost();
        using var client = host.CreateClient(new() { AllowAutoRedirect = false });
        await AssertError(await client.GetAsync(url), HttpStatusCode.Unauthorized, "unauthorized");
    }
}
