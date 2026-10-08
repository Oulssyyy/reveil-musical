using ReveilMusical.Domain.Model;

namespace ReveilMusical.Domain.Ports;

/// <summary>Dernier recours musical, sans dépendance externe : ne doit jamais échouer.</summary>
public interface ILocalTrackFallback
{
    Track Pick(WeatherType? weather, DayOfWeek day);
}
