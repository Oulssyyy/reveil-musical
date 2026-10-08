namespace ReveilMusical.Application.Music;

/// <summary>Ordre de bascule des sources musicales, piloté par configuration.</summary>
public sealed class MusicOptions
{
    public const string SectionName = "Music";

    /// <summary>Noms des sources à interroger, dans l'ordre. Une source absente de la liste n'est pas utilisée.</summary>
    public List<string> ProviderOrder { get; set; } = [];
}
