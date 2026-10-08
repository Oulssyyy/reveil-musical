namespace ReveilMusical.Domain.Model;

/// <summary>
/// Identifiant opaque d'un canal de notification ("email", "sms", "push", demain "whatsapp"…).
/// Volontairement une chaîne et non un enum : ajouter un canal ne doit pas modifier le domaine.
/// </summary>
public readonly record struct ChannelKey
{
    public string Value { get; }

    public ChannelKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Un canal doit avoir un identifiant.", nameof(value));
        Value = value.Trim().ToLowerInvariant();
    }

    public override string ToString() => Value;
}
