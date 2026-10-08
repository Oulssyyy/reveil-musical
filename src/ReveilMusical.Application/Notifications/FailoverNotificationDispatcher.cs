using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Ports;

namespace ReveilMusical.Application.Notifications;

/// <summary>
/// Essaie le canal préféré de l'utilisateur, puis ses autres canaux connus dans l'ordre configuré.
/// Le dispatcher ne connaît que <see cref="INotificationChannel"/> : aucun détail d'email, SMS ou push.
/// </summary>
public sealed class FailoverNotificationDispatcher(
    IEnumerable<INotificationChannel> channels,
    IOptions<NotificationOptions> options,
    ILogger<FailoverNotificationDispatcher> logger) : INotificationDispatcher
{
    private readonly IReadOnlyDictionary<ChannelKey, INotificationChannel> _channels =
        channels.ToDictionary(c => c.Key);

    private readonly IReadOnlyList<ChannelKey> _fallbackOrder =
        options.Value.FallbackOrder.Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => new ChannelKey(k)).ToList();

    public async Task<DeliveryResult> DispatchAsync(
        UserPreferences preferences,
        Func<string, WakeUpNotification> buildFor,
        CancellationToken cancellationToken = default)
    {
        var incidents = new List<string>();

        foreach (var key in CandidateChannels(preferences))
        {
            if (!_channels.TryGetValue(key, out var channel))
            {
                incidents.Add($"{key} : canal non disponible");
                continue;
            }
            if (!preferences.Contacts.TryGetValue(key, out var recipient) || string.IsNullOrWhiteSpace(recipient))
            {
                incidents.Add($"{key} : aucune adresse connue pour l'utilisateur");
                continue;
            }

            try
            {
                await channel.SendAsync(buildFor(recipient), cancellationToken);
                return new DeliveryResult(key, Delivered: true, Degraded: key != preferences.PreferredChannel, incidents);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Canal {Channel} en échec pour {UserId}, bascule", key, preferences.UserId);
                incidents.Add($"{key} : {ex.Message}");
            }
        }

        logger.LogCritical("Aucun canal n'a pu réveiller l'utilisateur {UserId}", preferences.UserId);
        return new DeliveryResult(null, Delivered: false, Degraded: true, incidents);
    }

    private IEnumerable<ChannelKey> CandidateChannels(UserPreferences preferences)
    {
        var tried = new HashSet<ChannelKey>();
        var candidates = new[] { preferences.PreferredChannel }
            .Concat(_fallbackOrder)
            .Concat(preferences.Contacts.Keys);

        foreach (var key in candidates)
            if (tried.Add(key))
                yield return key;
    }
}
