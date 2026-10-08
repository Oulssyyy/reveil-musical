using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Ports;

namespace ReveilMusical.Infrastructure.Notifications.Push;

/// <summary>Adaptateur : ramène le service push (payload + exceptions propriétaires) à <see cref="INotificationChannel"/>.</summary>
public sealed class PushNotificationChannel(IPushService pushService) : INotificationChannel
{
    public ChannelKey Key { get; } = new("push");

    public async Task SendAsync(WakeUpNotification notification, CancellationToken cancellationToken = default)
    {
        var payload = new PushPayload(
            notification.Recipient,
            notification.Title,
            notification.Message,
            new Dictionary<string, string>
            {
                ["trackTitle"] = notification.Track.Title,
                ["trackArtist"] = notification.Track.Artist,
            });

        try
        {
            await pushService.PushAsync(payload);
        }
        catch (PushServiceException ex)
        {
            throw new NotificationDeliveryException(Key, ex.Message, ex);
        }
    }
}
