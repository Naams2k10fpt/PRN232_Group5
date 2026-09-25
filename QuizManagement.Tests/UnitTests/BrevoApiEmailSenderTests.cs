using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using QuizManagement.Infrastructure;
using Xunit;

namespace QuizManagement.Tests.UnitTests;

public class BrevoApiEmailSenderTests
{
    [Fact]
    public async Task SendAsync_PostsBrevoPayloadWithApiKeyHeader()
    {
        var handler = new RecordingHandler(HttpStatusCode.Created);
        var sender = CreateSender(handler);

        var sent = await sender.SendAsync("user@test.local", "Xác minh email", "<p>link</p>");

        Assert.True(sent);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(BrevoApiEmailSender.Endpoint, request.Uri);
        Assert.Equal("xkeysib-test", request.ApiKey);

        using var json = JsonDocument.Parse(request.Body);
        var root = json.RootElement;
        Assert.Equal("sender@test.local", root.GetProperty("sender").GetProperty("email").GetString());
        Assert.Equal("Quiz Management", root.GetProperty("sender").GetProperty("name").GetString());
        Assert.Equal("user@test.local", root.GetProperty("to")[0].GetProperty("email").GetString());
        Assert.Equal("Xác minh email", root.GetProperty("subject").GetString());
        Assert.Equal("<p>link</p>", root.GetProperty("htmlContent").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task SendAsync_ReturnsFalseOnNonSuccessStatus(HttpStatusCode status)
    {
        var sender = CreateSender(new RecordingHandler(status));

        Assert.False(await sender.SendAsync("user@test.local", "subject", "body"));
    }

    [Fact]
    public async Task SendAsync_ReturnsFalseWhenBrevoIsUnreachable()
    {
        var sender = CreateSender(new ThrowingHandler());

        Assert.False(await sender.SendAsync("user@test.local", "subject", "body"));
    }

    private static BrevoApiEmailSender CreateSender(HttpMessageHandler handler)
        => new(
            new HttpClient(handler),
            Options.Create(new EmailOptions
            {
                Provider = EmailProvider.BrevoApi,
                ApiKey = "xkeysib-test",
                FromAddress = "sender@test.local",
                FromName = "Quiz Management"
            }),
            NullLogger<BrevoApiEmailSender>.Instance);

    private sealed record RecordedRequest(HttpMethod Method, Uri? Uri, string? ApiKey, string Body);

    private sealed class RecordingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(new RecordedRequest(
                request.Method,
                request.RequestUri,
                request.Headers.TryGetValues("api-key", out var values) ? values.Single() : null,
                await request.Content!.ReadAsStringAsync(cancellationToken)));
            return new HttpResponseMessage(status) { Content = new StringContent("{}") };
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => throw new HttpRequestException("network down");
    }
}
