using System.Text.Json.Serialization;

namespace ReveilMusical.Infrastructure.Music.Itunes;

/// <summary>DTO du format iTunes : interne à l'adaptateur, ne sort jamais de l'infrastructure.</summary>
internal sealed record ItunesSearchResponse(
    [property: JsonPropertyName("resultCount")] int ResultCount,
    [property: JsonPropertyName("results")] List<ItunesTrackDto>? Results);

internal sealed record ItunesTrackDto(
    [property: JsonPropertyName("trackName")] string? TrackName,
    [property: JsonPropertyName("artistName")] string? ArtistName,
    [property: JsonPropertyName("trackViewUrl")] string? TrackViewUrl);
