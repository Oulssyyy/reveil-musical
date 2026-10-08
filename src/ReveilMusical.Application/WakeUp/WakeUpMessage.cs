using System.Globalization;
using ReveilMusical.Domain.Model;

namespace ReveilMusical.Application.WakeUp;

/// <summary>Rédaction du message de réveil, personnalisé selon le jour et la météo.</summary>
internal static class WakeUpMessage
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    public static string Title(DayOfWeek day) => $"Bon {French.DateTimeFormat.GetDayName(day)} !";

    public static string Body(WakeUpRequest request, Track track) =>
        $"{WeatherSentence(request.Weather)} Pour bien commencer la journée : {track}.";

    private static string WeatherSentence(WeatherType? weather) => weather switch
    {
        WeatherType.Soleil => "Le soleil est au rendez-vous.",
        WeatherType.Pluie => "Il pleut aujourd'hui, pensez au parapluie.",
        WeatherType.Neige => "Il neige ce matin, couvrez-vous bien.",
        WeatherType.Nuageux => "Le ciel est nuageux.",
        _ => "Une nouvelle journée commence.",
    };
}
