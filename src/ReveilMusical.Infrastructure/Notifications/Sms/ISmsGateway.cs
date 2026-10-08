namespace ReveilMusical.Infrastructure.Notifications.Sms;

/// <summary>Faux SDK SMS : API asynchrone, paramètres à plat, résultat sous forme de code de statut.</summary>
public interface ISmsGateway
{
    Task<SmsGatewayResponse> TransmitAsync(string phoneNumber, string text, CancellationToken cancellationToken);
}

public sealed record SmsGatewayResponse(int StatusCode, string? Error);
