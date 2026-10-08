namespace ReveilMusical.Domain.Model;

/// <summary>
/// Morceau tel que le métier le connaît : un titre et un artiste.
/// Aucun champ propre à un fournisseur (trackViewUrl, MBID…) n'a sa place ici.
/// </summary>
public sealed record Track(string Title, string Artist)
{
    public override string ToString() => $"{Title} — {Artist}";
}
