using System.Net;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Ports;

namespace ReveilMusical.Infrastructure.Notifications.Email;

/// <summary>Adaptateur : ramène le SDK email (synchrone, booléen, HTML) à <see cref="INotificationChannel"/>.</summary>
public sealed class EmailNotificationChannel(IMailClient mailClient) : INotificationChannel
{
    public ChannelKey Key { get; } = new("email");

    public Task SendAsync(WakeUpNotification notification, CancellationToken cancellationToken = default)
    {
        var html = $"<p>{WebUtility.HtmlEncode(notification.Message)}</p>";
        var envelope = new MailEnvelope(notification.Recipient, notification.Title, html);

        if (!mailClient.SendMail(envelope))
            throw new NotificationDeliveryException(Key, "le serveur mail a refusé l'envoi");
        return Task.CompletedTask;
    }
}
