namespace ReveilMusical.Infrastructure.Notifications.Push;

/// <summary>Faux SDK push : payload structuré avec données libres, échec signalé par exception.</summary>
public interface IPushService
{
    Task<string> PushAsync(PushPayload payload);
}

public sealed record PushPayload(string DeviceToken, string Title, string Body, IReadOnlyDictionary<string, string> Data);

public sealed class PushServiceException(string message) : Exception(message);
