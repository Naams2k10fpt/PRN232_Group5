using System.Security.Cryptography;
using System.Text;

namespace QuizManagement.Services;

public sealed class VoiceRoomOptions
{
    public const string SectionName = "VoiceRooms";
    public int MaxParticipants { get; set; } = 6;
    public List<VoiceIceServer> IceServers { get; set; } =
        [new(["stun:stun.l.google.com:19302"])];
    public string[] TurnUrls { get; set; } = [];
    public string? TurnSharedSecret { get; set; }
    public bool RelayOnly { get; set; }

    public bool IsValid() => MaxParticipants is >= 2 and <= 8
        && IceServers.Count <= 8
        && IceServers.All(server => server.Urls.Length is > 0 and <= 8
            && server.Urls.All(IsIceUrl))
        && TurnUrls.Length <= 8 && TurnUrls.All(IsTurnUrl)
        && (TurnUrls.Length == 0 || !string.IsNullOrWhiteSpace(TurnSharedSecret))
        && (!RelayOnly || TurnUrls.Length > 0
            || IceServers.Any(server => server.Urls.Any(IsTurnUrl)));

    public List<VoiceIceServer> ForUser(string userId, DateTimeOffset now)
    {
        var servers = IceServers.ToList();
        if (TurnUrls.Length > 0 && !string.IsNullOrWhiteSpace(TurnSharedSecret))
        {
            // coturn's REST API accepts short-lived HMAC credentials, never the shared secret.
            var username = $"{now.AddHours(1).ToUnixTimeSeconds()}:{userId}";
            var credential = Convert.ToBase64String(HMACSHA1.HashData(
                Encoding.UTF8.GetBytes(TurnSharedSecret), Encoding.UTF8.GetBytes(username)));
            servers.Add(new(TurnUrls, username, credential));
        }
        return servers;
    }

    private static bool IsIceUrl(string url) => !string.IsNullOrWhiteSpace(url)
        && url.Length <= 2048 && (IsTurnUrl(url)
            || url.StartsWith("stun:", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("stuns:", StringComparison.OrdinalIgnoreCase));

    private static bool IsTurnUrl(string url) => !string.IsNullOrWhiteSpace(url)
        && url.Length <= 2048 && (url.StartsWith("turn:", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("turns:", StringComparison.OrdinalIgnoreCase));
}

public sealed record VoiceIceServer(string[] Urls, string? Username = null, string? Credential = null);
