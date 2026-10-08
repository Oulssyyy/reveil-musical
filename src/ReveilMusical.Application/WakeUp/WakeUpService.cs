using Microsoft.Extensions.Logging;
using ReveilMusical.Application.Music;
using ReveilMusical.Application.Notifications;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Ports;

namespace ReveilMusical.Application.WakeUp;

/// <summary>
/// Orchestration métier du réveil : préférences → choix du morceau → notification.
/// Ne dépend que d'abstractions ; ne lève jamais d'exception vers l'ordonnanceur, elle rend un compte rendu.
/// </summary>
public sealed class WakeUpService(
    IUserPreferencesProvider preferencesProvider,
    ITrackResolver trackResolver,
    INotificationDispatcher dispatcher,
    ILogger<WakeUpService> logger) : IWakeUpService
{
    public async Task<WakeUpReport> WakeUpAsync(WakeUpRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        UserPreferences? preferences;
        try
        {
            preferences = await preferencesProvider.GetAsync(request.UserId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Préférences indisponibles pour {UserId}", request.UserId);
            preferences = null;
        }

        var query = preferences?.TrackQueryFor(request.Weather);
        var trackResult = await trackResolver.ResolveAsync(query ?? string.Empty, request, cancellationToken);

        if (preferences is null)
        {
            logger.LogCritical("Utilisateur {UserId} sans préférences : impossible de le contacter", request.UserId);
            return new WakeUpReport(request.UserId, trackResult.Track, trackResult.Source, null,
                Delivered: false, Degraded: true,
                [.. trackResult.Incidents, "Préférences utilisateur introuvables"]);
        }

        var title = WakeUpMessage.Title(request.Day);
        var body = WakeUpMessage.Body(request, trackResult.Track);
        var delivery = await dispatcher.DispatchAsync(
            preferences,
            recipient => new WakeUpNotification(request.UserId, recipient, title, body, trackResult.Track),
            cancellationToken);

        logger.LogInformation("Réveil {UserId} : {Track} ({Source}) via {Channel}",
            request.UserId, trackResult.Track, trackResult.Source, delivery.Channel?.ToString() ?? "aucun canal");

        return new WakeUpReport(
            request.UserId,
            trackResult.Track,
            trackResult.Source,
            delivery.Channel,
            delivery.Delivered,
            Degraded: trackResult.Degraded || delivery.Degraded,
            [.. trackResult.Incidents, .. delivery.Incidents]);
    }
}
