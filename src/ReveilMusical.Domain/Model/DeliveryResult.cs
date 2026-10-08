namespace ReveilMusical.Domain.Model;

/// <summary>Résultat de la distribution d'une notification sur les canaux disponibles.</summary>
public sealed record DeliveryResult(ChannelKey? Channel, bool Delivered, bool Degraded, IReadOnlyList<string> Incidents);
