using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Ports;

namespace ReveilMusical.Infrastructure.Music;

/// <summary>
/// Décorateur qui refuse d'appeler la source au-delà de N requêtes sur une fenêtre glissante d'une minute.
/// Plutôt que d'attendre (le réveil est à heure fixe), il échoue immédiatement : le résolveur bascule
/// alors sur la source suivante ou sur la liste locale.
/// </summary>
public sealed class RateLimitedMusicProvider(IMusicProvider inner, int maxRequestsPerMinute, TimeProvider timeProvider) : IMusicProvider
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
    private readonly Queue<DateTimeOffset> _calls = new();
    private readonly Lock _lock = new();

    public string Name => inner.Name;

    public Task<Track?> FindTrackAsync(string query, CancellationToken cancellationToken = default)
    {
        if (!TryAcquire())
            throw new MusicProviderRateLimitedException(inner.Name);
        return inner.FindTrackAsync(query, cancellationToken);
    }

    private bool TryAcquire()
    {
        var now = timeProvider.GetUtcNow();
        lock (_lock)
        {
            while (_calls.Count > 0 && now - _calls.Peek() >= Window)
                _calls.Dequeue();

            if (_calls.Count >= maxRequestsPerMinute)
                return false;

            _calls.Enqueue(now);
            return true;
        }
    }
}
