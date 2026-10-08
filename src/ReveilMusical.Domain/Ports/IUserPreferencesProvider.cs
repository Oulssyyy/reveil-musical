using ReveilMusical.Domain.Model;

namespace ReveilMusical.Domain.Ports;

/// <summary>Service interne retournant les préférences d'un utilisateur.</summary>
public interface IUserPreferencesProvider
{
    /// <returns>Les préférences, ou <c>null</c> si l'utilisateur est inconnu.</returns>
    Task<UserPreferences?> GetAsync(string userId, CancellationToken cancellationToken = default);
}
