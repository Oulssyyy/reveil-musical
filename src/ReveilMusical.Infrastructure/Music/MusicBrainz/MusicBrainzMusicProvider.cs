using System.Net.Http.Json;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Ports;

namespace ReveilMusical.Infrastructure.Music.MusicBrainz;

/// <summary>
/// Adaptateur MusicBrainz. Le User-Agent obligatoire est posé sur le client HTTP nommé
/// (voir <c>ServiceCollectionExtensions</c>) : le détail reste dans l'infrastructure.
/// </summary>
public sealed class MusicBrainzMusicProvider(IHttpClientFactory httpClientFactory) : IMusicProvider
{
    public const string ProviderName = "musicbrainz";

    public string Name => ProviderName;

    public async Task<Track?> FindTrackAsync(string query, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(MusicBrainzOptions.HttpClientName);
        var uri = $"recording?query={Uri.EscapeDataString(query)}&fmt=json&limit=5";

        MusicBrainzSearchResponse? response;
        try
        {
            response = await client.GetFromJsonAsync<MusicBrainzSearchResponse>(uri, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException
                                       or TaskCanceledException { InnerException: TimeoutException })
        {
            throw new MusicProviderException($"MusicBrainz indisponible ({ex.Message})", ex);
        }

        foreach (var recording in response?.Recordings ?? [])
        {
            var artist = ArtistOf(recording);
            if (!string.IsNullOrWhiteSpace(recording.Title) && !string.IsNullOrWhiteSpace(artist))
                return new Track(recording.Title, artist);
        }
        return null;
    }

    private static string ArtistOf(MusicBrainzRecordingDto recording) =>
        string.Concat((recording.ArtistCredit ?? []).Select(c => c.Name + c.JoinPhrase)).Trim();
}
