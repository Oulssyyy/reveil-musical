using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Ports;

namespace ReveilMusical.Infrastructure.Music;

public sealed class MusicCacheOptions
{
    public const string SectionName = "Music:Cache";

    public TimeSpan TimeToLive { get; set; } = TimeSpan.FromHours(12);
}

/// <summary>
/// Décorateur de cache : une même recherche n'interroge la source qu'une fois par période,
/// ce qui épargne le quota (~20 req/min pour iTunes). Seuls les succès sont mis en cache.
/// </summary>
public sealed class CachingMusicProvider(IMusicProvider inner, IMemoryCache cache, IOptions<MusicCacheOptions> options) : IMusicProvider
{
    public string Name => inner.Name;

    public async Task<Track?> FindTrackAsync(string query, CancellationToken cancellationToken = default)
    {
        var key = $"music:{inner.Name}:{query.Trim().ToLowerInvariant()}";
        if (cache.TryGetValue(key, out Track? cached))
            return cached;

        var track = await inner.FindTrackAsync(query, cancellationToken);
        if (track is not null)
            cache.Set(key, track, options.Value.TimeToLive);
        return track;
    }
}
