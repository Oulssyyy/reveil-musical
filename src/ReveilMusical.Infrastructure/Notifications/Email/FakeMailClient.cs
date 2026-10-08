using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ReveilMusical.Infrastructure.Notifications.Email;

/// <summary>Mock : n'envoie rien, écrit l'email dans les logs.</summary>
public sealed class FakeMailClient(IOptions<FakeChannelOptions> options, ILogger<FakeMailClient> logger) : IMailClient
{
    public bool SendMail(MailEnvelope envelope)
    {
        if (options.Value.EmailOutage)
            return false;

        logger.LogInformation("[EMAIL] à {To} | {Subject} | {Body}", envelope.To, envelope.Subject, envelope.HtmlBody);
        return true;
    }
}
