using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Ports;

namespace ReveilMusical.Infrastructure.Music;

/// <summary>Petite liste codée en dur : utilisable sans réseau, elle garantit qu'il y a toujours un morceau.</summary>
public sealed class HardcodedTrackFallback : ILocalTrackFallback
{
    private static readonly IReadOnlyDictionary<WeatherType, Track[]> ByWeather = new Dictionary<WeatherType, Track[]>
    {
        [WeatherType.Soleil] = [new("Here Comes the Sun", "The Beatles"), new("Walking on Sunshine", "Katrina and the Waves")],
        [WeatherType.Pluie] = [new("Singin' in the Rain", "Gene Kelly"), new("Riders on the Storm", "The Doors")],
        [WeatherType.Neige] = [new("Let It Snow!", "Dean Martin"), new("Snow (Hey Oh)", "Red Hot Chili Peppers")],
        [WeatherType.Nuageux] = [new("Both Sides Now", "Joni Mitchell"), new("Get Off of My Cloud", "The Rolling Stones")],
    };

    private static readonly Track[] Default = [new("Good Morning", "Gene Kelly"), new("Wake Me Up", "Avicii")];

    public Track Pick(WeatherType? weather, DayOfWeek day)
    {
        var candidates = weather is { } w && ByWeather.TryGetValue(w, out var list) ? list : Default;
        return candidates[(int)day % candidates.Length];
    }
}
