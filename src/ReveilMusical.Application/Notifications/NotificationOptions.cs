namespace ReveilMusical.Application.Notifications;

/// <summary>Ordre de bascule des canaux quand le canal préféré est indisponible.</summary>
public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    public List<string> FallbackOrder { get; set; } = [];
}
