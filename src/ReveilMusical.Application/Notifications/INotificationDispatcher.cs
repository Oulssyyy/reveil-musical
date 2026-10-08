using ReveilMusical.Domain.Model;

namespace ReveilMusical.Application.Notifications;

/// <summary>Distribue une notification sur le canal préféré, puis sur les canaux de secours.</summary>
public interface INotificationDispatcher
{
    Task<DeliveryResult> DispatchAsync(UserPreferences preferences, Func<string, WakeUpNotification> buildFor, CancellationToken cancellationToken = default);
}
