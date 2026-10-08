namespace ReveilMusical.UnitTests.Support;

/// <summary>Horloge pilotable pour tester les fenêtres de quota sans attendre.</summary>
internal sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan delta) => _now += delta;
}
