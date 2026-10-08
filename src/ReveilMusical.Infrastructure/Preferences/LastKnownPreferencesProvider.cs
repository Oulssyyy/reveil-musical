using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Ports;

namespace ReveilMusical.Infrastructure.Preferences;

/// <summary>
/// Décorateur de résilience : mémorise les dernières préférences connues de chaque utilisateur et
/// les ressert si le service de préférences tombe. Des préférences d'hier valent mieux qu'aucun réveil.
/// </summary>
public sealed class LastKnownPreferencesProvider(IUserPreferencesProvider inner, ILogger<LastKnownPreferencesProvider> logger)
    : IUserPreferencesProvider
{
    private readonly ConcurrentDictionary<string, UserPreferences> _lastKnown = new(StringComparer.OrdinalIgnoreCase);

    public async Task<UserPreferences?> GetAsync(string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var preferences = await inner.GetAsync(userId, cancellationToken);
            if (preferences is not null)
                _lastKnown[userId] = preferences;
            return preferences;
        }
        catch (Exception ex) when (ex is not OperationCanceledException && _lastKnown.TryGetValue(userId, out _))
        {
            logger.LogWarning(ex, "Service de préférences en panne, dernières préférences connues utilisées pour {UserId}", userId);
            return _lastKnown[userId];
        }
    }
}
