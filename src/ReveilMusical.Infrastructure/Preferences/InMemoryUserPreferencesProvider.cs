using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Ports;

namespace ReveilMusical.Infrastructure.Preferences;

/// <summary>Mock du service interne de préférences, avec quelques utilisateurs de démonstration.</summary>
public sealed class InMemoryUserPreferencesProvider : IUserPreferencesProvider
{
    private static readonly ChannelKey Email = new("email");
    private static readonly ChannelKey Sms = new("sms");
    private static readonly ChannelKey Push = new("push");

    private static readonly IReadOnlyDictionary<string, UserPreferences> Users =
        new[]
        {
            new UserPreferences(
                "alice",
                new Dictionary<WeatherType, string>
                {
                    [WeatherType.Soleil] = "Here Comes the Sun",
                    [WeatherType.Pluie] = "Purple Rain",
                    [WeatherType.Neige] = "Let It Snow",
                },
                "Bohemian Rhapsody",
                Email,
                new Dictionary<ChannelKey, string> { [Email] = "alice@example.com", [Sms] = "+33600000001" }),
            new UserPreferences(
                "bob",
                new Dictionary<WeatherType, string>
                {
                    [WeatherType.Pluie] = "Riders on the Storm",
                    [WeatherType.Nuageux] = "Clocks",
                },
                "Wake Me Up",
                Sms,
                new Dictionary<ChannelKey, string> { [Sms] = "+33600000002", [Push] = "device-token-bob" }),
            new UserPreferences(
                "chloe",
                new Dictionary<WeatherType, string> { [WeatherType.Soleil] = "Walking on Sunshine" },
                "Good Morning",
                Push,
                new Dictionary<ChannelKey, string> { [Push] = "device-token-chloe", [Email] = "chloe@example.com" }),
        }.ToDictionary(u => u.UserId, StringComparer.OrdinalIgnoreCase);

    public Task<UserPreferences?> GetAsync(string userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.GetValueOrDefault(userId));
}
