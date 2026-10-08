namespace ReveilMusical.Infrastructure.Notifications;

/// <summary>Réglages communs aux faux SDK : permet de simuler une panne d'un canal par configuration.</summary>
public sealed class FakeChannelOptions
{
    public const string SectionName = "Notifications:Fakes";

    public bool EmailOutage { get; set; }
    public bool SmsOutage { get; set; }
    public bool PushOutage { get; set; }
}
