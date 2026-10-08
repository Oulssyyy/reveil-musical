using ReveilMusical.Domain.Model;

namespace ReveilMusical.Domain.Ports;

/// <summary>Interface commune à laquelle chaque canal concret est ramené par un adaptateur.</summary>
public interface INotificationChannel
{
    ChannelKey Key { get; }

    /// <summary>Envoie la notification ; lève une exception en cas d'échec.</summary>
    Task SendAsync(WakeUpNotification notification, CancellationToken cancellationToken = default);
}
