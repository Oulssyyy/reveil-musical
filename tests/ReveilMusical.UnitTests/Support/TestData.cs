using ReveilMusical.Domain.Model;

namespace ReveilMusical.UnitTests.Support;

internal static class TestData
{
    public static readonly ChannelKey Email = new("email");
    public static readonly ChannelKey Sms = new("sms");
    public static readonly ChannelKey Push = new("push");

    public static UserPreferences Preferences(
        ChannelKey? preferred = null,
        IReadOnlyDictionary<ChannelKey, string>? contacts = null) =>
        new(
            "alice",
            new Dictionary<WeatherType, string> { [WeatherType.Pluie] = "Purple Rain" },
            "Bohemian Rhapsody",
            preferred ?? Email,
            contacts ?? new Dictionary<ChannelKey, string>
            {
                [Email] = "alice@example.com",
                [Sms] = "+33600000001",
                [Push] = "token-alice",
            });

    public static WakeUpRequest Request(WeatherType? weather = WeatherType.Pluie, DayOfWeek day = DayOfWeek.Monday) =>
        new("alice", day, weather);

    public static WakeUpNotification Notification(string recipient = "dest") =>
        new("alice", recipient, "Bon lundi !", "Il pleut.", new Track("Purple Rain", "Prince"));
}
