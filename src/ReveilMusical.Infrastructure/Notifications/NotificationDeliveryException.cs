using ReveilMusical.Domain.Model;

namespace ReveilMusical.Infrastructure.Notifications;

/// <summary>Échec d'envoi sur un canal, quelle que soit la façon dont le SDK sous-jacent le signale.</summary>
public sealed class NotificationDeliveryException(ChannelKey channel, string reason, Exception? inner = null)
    : Exception($"échec d'envoi {channel} : {reason}", inner)
{
    public ChannelKey Channel { get; } = channel;
}
