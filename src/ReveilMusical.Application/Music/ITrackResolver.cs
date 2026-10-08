using ReveilMusical.Domain.Model;

namespace ReveilMusical.Application.Music;

/// <summary>Résout une recherche en morceau, sans jamais renvoyer de silence.</summary>
public interface ITrackResolver
{
    Task<TrackResult> ResolveAsync(string query, WakeUpRequest request, CancellationToken cancellationToken = default);
}
