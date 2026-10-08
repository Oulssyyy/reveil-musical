using ReveilMusical.Domain.Model;

namespace ReveilMusical.Application.WakeUp;

/// <summary>Point d'entrée : l'appel déclenché à l'heure du réveil.</summary>
public interface IWakeUpService
{
    Task<WakeUpReport> WakeUpAsync(WakeUpRequest request, CancellationToken cancellationToken = default);
}
