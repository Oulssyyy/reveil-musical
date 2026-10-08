namespace ReveilMusical.Domain.Model;

/// <summary>Entrée de l'appel déclenché à l'heure du réveil.</summary>
/// <param name="Weather">Météo du jour ; <c>null</c> si la valeur reçue n'est pas reconnue (→ morceau de secours).</param>
public sealed record WakeUpRequest(string UserId, DayOfWeek Day, WeatherType? Weather);
