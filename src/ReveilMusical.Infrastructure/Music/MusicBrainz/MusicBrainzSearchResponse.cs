using System.Text.Json.Serialization;

namespace ReveilMusical.Infrastructure.Music.MusicBrainz;

/// <summary>DTO du format MusicBrainz : interne à l'adaptateur.</summary>
internal sealed record MusicBrainzSearchResponse(
    [property: JsonPropertyName("recordings")] List<MusicBrainzRecordingDto>? Recordings);

internal sealed record MusicBrainzRecordingDto(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("artist-credit")] List<MusicBrainzArtistCreditDto>? ArtistCredit);

internal sealed record MusicBrainzArtistCreditDto(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("joinphrase")] string? JoinPhrase);
