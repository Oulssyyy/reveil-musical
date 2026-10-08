namespace ReveilMusical.Domain.Model;

/// <summary>Morceau résolu et nom de la source qui l'a fourni.</summary>
public sealed record TrackResult(Track Track, string Source, bool Degraded, IReadOnlyList<string> Incidents);
