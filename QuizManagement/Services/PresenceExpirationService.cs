namespace QuizManagement.Services;

public sealed class PresenceExpirationService : BackgroundService
{
    private readonly RoomPresence _presence;
    private readonly PresenceBroadcaster _broadcaster;
    private readonly ILogger<PresenceExpirationService> _logger;

    public PresenceExpirationService(RoomPresence presence, PresenceBroadcaster broadcaster,
        ILogger<PresenceExpirationService> logger)
    {
        _presence = presence;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                foreach (var userId in _presence.ExpireWebDisconnects())
                {
                    try { await _broadcaster.BroadcastWebsiteChangeAsync(userId, stoppingToken); }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogError(ex, "Could not broadcast expired website presence for {UserId}", userId);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
