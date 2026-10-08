using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ReveilMusical.Infrastructure.Notifications.Push;

/// <summary>Mock : n'envoie rien, écrit la notification push dans les logs et renvoie un identifiant de message.</summary>
public sealed class FakePushService(IOptions<FakeChannelOptions> options, ILogger<FakePushService> logger) : IPushService
{
    public Task<string> PushAsync(PushPayload payload)
    {
        if (options.Value.PushOutage)
            throw new PushServiceException("service push indisponible");

        var messageId = Guid.NewGuid().ToString("N");
        logger.LogInformation("[PUSH] {MessageId} vers {Token} | {Title} | {Body}",
            messageId, payload.DeviceToken, payload.Title, payload.Body);
        return Task.FromResult(messageId);
    }
}
