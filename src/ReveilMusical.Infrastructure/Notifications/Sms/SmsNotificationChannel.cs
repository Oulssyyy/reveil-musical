using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Ports;

namespace ReveilMusical.Infrastructure.Notifications.Sms;

/// <summary>Adaptateur : ramène la passerelle SMS (code de statut, 160 caractères) à <see cref="INotificationChannel"/>.</summary>
public sealed class SmsNotificationChannel(ISmsGateway gateway) : INotificationChannel
{
    public ChannelKey Key { get; } = new("sms");

    public async Task SendAsync(WakeUpNotification notification, CancellationToken cancellationToken = default)
    {
        var text = Truncate($"{notification.Title} {notification.Message}", FakeSmsGateway.MaxLength);
        var response = await gateway.TransmitAsync(notification.Recipient, text, cancellationToken);

        if (response.StatusCode is < 200 or >= 300)
            throw new NotificationDeliveryException(Key, $"code {response.StatusCode} ({response.Error})");
    }

    internal static string Truncate(string text, int max) =>
        text.Length <= max ? text : string.Concat(text.AsSpan(0, max - 1), "…");
}
