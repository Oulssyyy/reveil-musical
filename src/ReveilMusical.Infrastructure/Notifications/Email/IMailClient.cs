namespace ReveilMusical.Infrastructure.Notifications.Email;

/// <summary>Faux SDK email : API synchrone, objet message, succès signalé par un booléen.</summary>
public interface IMailClient
{
    bool SendMail(MailEnvelope envelope);
}

public sealed record MailEnvelope(string To, string Subject, string HtmlBody);
