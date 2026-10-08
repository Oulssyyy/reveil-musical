namespace ReveilMusical.Domain.Model;

/// <summary>
/// Préférences d'un utilisateur, telles que retournées par le service interne de préférences.
/// </summary>
/// <param name="TracksByWeather">Recherche de morceau choisie par l'utilisateur pour chaque type de météo.</param>
/// <param name="FallbackTrack">Morceau de secours pour les cas non couverts.</param>
/// <param name="PreferredChannel">Canal de notification préféré.</param>
/// <param name="Contacts">Adresse de l'utilisateur sur chaque canal (email, numéro, jeton push…).</param>
public sealed record UserPreferences(
    string UserId,
    IReadOnlyDictionary<WeatherType, string> TracksByWeather,
    string FallbackTrack,
    ChannelKey PreferredChannel,
    IReadOnlyDictionary<ChannelKey, string> Contacts)
{
    /// <summary>Choisit la recherche de morceau pour la météo du jour, ou le morceau de secours.</summary>
    public string TrackQueryFor(WeatherType? weather) =>
        weather is { } w && TracksByWeather.TryGetValue(w, out var query) && !string.IsNullOrWhiteSpace(query)
            ? query
            : FallbackTrack;
}
