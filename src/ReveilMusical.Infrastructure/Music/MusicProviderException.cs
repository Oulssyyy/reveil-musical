namespace ReveilMusical.Infrastructure.Music;

/// <summary>Panne d'une source musicale (HTTP, réponse illisible, quota…), traduite hors du vocabulaire du fournisseur.</summary>
public class MusicProviderException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>La source a atteint son quota de requêtes : on ne l'appelle pas pour ne pas se faire bannir.</summary>
public sealed class MusicProviderRateLimitedException(string provider)
    : MusicProviderException($"quota de requêtes atteint pour {provider}");
