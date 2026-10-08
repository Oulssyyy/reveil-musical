using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ReveilMusical.Infrastructure.Notifications.Sms;

/// <summary>Mock : n'envoie rien, écrit le SMS dans les logs.</summary>
public sealed class FakeSmsGateway(IOptions<FakeChannelOptions> options, ILogger<FakeSmsGateway> logger) : ISmsGateway
{
    public const int MaxLength = 160;

    public Task<SmsGatewayResponse> TransmitAsync(string phoneNumber, string text, CancellationToken cancellationToken)
    {
        if (options.Value.SmsOutage)
            return Task.FromResult(new SmsGatewayResponse(503, "passerelle SMS indisponible"));
        if (text.Length > MaxLength)
            return Task.FromResult(new SmsGatewayResponse(413, "message trop long"));

        logger.LogInformation("[SMS] à {Phone} | {Text}", phoneNumber, text);
        return Task.FromResult(new SmsGatewayResponse(200, null));
    }
}
