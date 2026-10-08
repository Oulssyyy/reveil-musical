namespace ReveilMusical.Domain.Model;

/// <summary>Compte rendu d'un réveil : ce qui a été joué, par qui, sur quel canal, et si on a dû se dégrader.</summary>
/// <param name="TrackSource">Nom de la source qui a fourni le morceau.</param>
/// <param name="Channel">Canal effectivement utilisé, <c>null</c> si aucun n'a pu délivrer.</param>
/// <param name="Degraded">Vrai si un mode dégradé a été utilisé (fournisseur, canal ou préférences de secours).</param>
public sealed record WakeUpReport(
    string UserId,
    Track Track,
    string TrackSource,
    ChannelKey? Channel,
    bool Delivered,
    bool Degraded,
    IReadOnlyList<string> Incidents);
