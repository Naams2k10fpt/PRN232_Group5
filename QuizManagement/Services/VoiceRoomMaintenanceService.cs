namespace QuizManagement.Services;

/// <summary>Also removes callers whose accounts are disabled while a call is running.</summary>
public sealed class VoiceRoomMaintenanceService(VoiceRoomState state, IServiceScopeFactory scopes,
    ILogger<VoiceRoomMaintenanceService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                foreach (var groupId in state.GroupIds())
                {
                    try
                    {
                        using var scope = scopes.CreateScope();
                        await scope.ServiceProvider.GetRequiredService<VoiceRoomService>()
                            .SynchronizeAsync(groupId, stoppingToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogError(ex, "Could not synchronize voice room {GroupId}", groupId);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
