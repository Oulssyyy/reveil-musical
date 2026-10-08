using ReveilMusical.Domain.Model;

namespace ReveilMusical.Cli;

/// <summary>Traduction des arguments bruts (ID, jour, météo) en requête métier.</summary>
internal static class WakeUpArguments
{
    private static readonly IReadOnlyDictionary<string, DayOfWeek> FrenchDays =
        new Dictionary<string, DayOfWeek>(StringComparer.OrdinalIgnoreCase)
        {
            ["LUNDI"] = DayOfWeek.Monday,
            ["MARDI"] = DayOfWeek.Tuesday,
            ["MERCREDI"] = DayOfWeek.Wednesday,
            ["JEUDI"] = DayOfWeek.Thursday,
            ["VENDREDI"] = DayOfWeek.Friday,
            ["SAMEDI"] = DayOfWeek.Saturday,
            ["DIMANCHE"] = DayOfWeek.Sunday,
        };

    public static bool TryParse(string[] args, out WakeUpRequest? request, out string? error)
    {
        request = null;
        error = null;
        if (args.Length != 3)
        {
            error = "Usage : ReveilMusical.Cli <userId> <jour> <SOLEIL|PLUIE|NEIGE|NUAGEUX>";
            return false;
        }

        if (!TryParseDay(args[1], out var day))
        {
            error = $"Jour inconnu : {args[1]}";
            return false;
        }

        // Une météo inconnue n'est pas bloquante : on tombera sur le morceau de secours.
        request = new WakeUpRequest(args[0], day, ParseWeather(args[2]));
        return true;
    }

    internal static bool TryParseDay(string value, out DayOfWeek day) =>
        FrenchDays.TryGetValue(value, out day)
        || (Enum.TryParse(value, ignoreCase: true, out day) && Enum.IsDefined(day) && !int.TryParse(value, out _));

    internal static WeatherType? ParseWeather(string value) =>
        Enum.TryParse<WeatherType>(value, ignoreCase: true, out var weather) && Enum.IsDefined(weather) && !int.TryParse(value, out _)
            ? weather
            : null;
}
