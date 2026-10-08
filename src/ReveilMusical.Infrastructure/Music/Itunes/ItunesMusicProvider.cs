using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Ports;

namespace ReveilMusical.Infrastructure.Music.Itunes;

/// <summary>Adaptateur iTunes Search API : traduit la réponse iTunes en <see cref="Track"/> métier.</summary>
public sealed class ItunesMusicProvider(IHttpClientFactory httpClientFactory, IOptions<ItunesOptions> options) : IMusicProvider
{
    public const string ProviderName = "itunes";

    public string Name => ProviderName;

    public async Task<Track?> FindTrackAsync(string query, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(ItunesOptions.HttpClientName);
        var uri = $"search?term={Uri.EscapeDataString(query)}&media=music&entity=song&limit={options.Value.Limit}";

        ItunesSearchResponse? response;
        try
        {
            response = await client.GetFromJsonAsync<ItunesSearchResponse>(uri, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException
                                       or TaskCanceledException { InnerException: TimeoutException })
        {
            throw new MusicProviderException($"iTunes indisponible ({ex.Message})", ex);
        }

        // trackViewUrl est volontairement ignoré : il ne doit pas fuiter dans le métier.
        var best = response?.Results?.FirstOrDefault(r =>
            !string.IsNullOrWhiteSpace(r.TrackName) && !string.IsNullOrWhiteSpace(r.ArtistName));

        return best is null ? null : new Track(best.TrackName!, best.ArtistName!);
    }
}
