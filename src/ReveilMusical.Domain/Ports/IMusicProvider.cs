using ReveilMusical.Domain.Model;

namespace ReveilMusical.Domain.Ports;

/// <summary>Une source de morceaux (iTunes, MusicBrainz, …). Peut échouer : l'appelant gère la bascule.</summary>
public interface IMusicProvider
{
    /// <summary>Nom lisible de la source, utilisé pour l'ordre de bascule et le compte rendu.</summary>
    string Name { get; }

    /// <returns>Le meilleur morceau trouvé, ou <c>null</c> si la recherche ne donne rien.</returns>
    Task<Track?> FindTrackAsync(string query, CancellationToken cancellationToken = default);
}
