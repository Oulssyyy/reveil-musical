using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Ports;

namespace ReveilMusical.Application.Music;

/// <summary>
/// Interroge les sources musicales dans l'ordre configuré et bascule sur la suivante en cas de panne
/// ou de résultat vide. Si toutes échouent, la liste locale prend le relais : le silence n'est jamais une option.
/// </summary>
public sealed class FailoverTrackResolver(
    IEnumerable<IMusicProvider> providers,
    ILocalTrackFallback localFallback,
    IOptions<MusicOptions> options,
    ILogger<FailoverTrackResolver> logger) : ITrackResolver
{
    public const string LocalSourceName = "local-fallback";

    private readonly IReadOnlyList<IMusicProvider> _orderedProviders = Order(providers, options.Value.ProviderOrder);

    public async Task<TrackResult> ResolveAsync(string query, WakeUpRequest request, CancellationToken cancellationToken = default)
    {
        var incidents = new List<string>();
        var providersToTry = string.IsNullOrWhiteSpace(query) ? [] : _orderedProviders;

        foreach (var provider in providersToTry)
        {
            try
            {
                var track = await provider.FindTrackAsync(query, cancellationToken);
                if (track is not null)
                    return new TrackResult(track, provider.Name, Degraded: incidents.Count > 0, incidents);

                incidents.Add($"{provider.Name} : aucun résultat pour « {query} »");
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Source musicale {Provider} indisponible, bascule sur la suivante", provider.Name);
                incidents.Add($"{provider.Name} : {ex.Message}");
            }
        }

        var fallback = localFallback.Pick(request.Weather, request.Day);
        logger.LogWarning("Aucune source musicale disponible, morceau local utilisé : {Track}", fallback);
        incidents.Add("Liste locale utilisée");
        return new TrackResult(fallback, LocalSourceName, Degraded: true, incidents);
    }

    private static IReadOnlyList<IMusicProvider> Order(IEnumerable<IMusicProvider> providers, IReadOnlyList<string> order)
    {
        var byName = providers.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
        return order
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(byName.ContainsKey)
            .Select(name => byName[name])
            .ToList();
    }
}
