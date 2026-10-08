using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ReveilMusical.Application.Music;
using ReveilMusical.Application.Notifications;
using ReveilMusical.Application.WakeUp;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Ports;
using ReveilMusical.UnitTests.Support;

namespace ReveilMusical.UnitTests.Application;

public class WakeUpServiceTests
{
    private static readonly Track Track = new("Purple Rain", "Prince");

    private readonly IUserPreferencesProvider _preferences = Substitute.For<IUserPreferencesProvider>();
    private readonly ITrackResolver _resolver = Substitute.For<ITrackResolver>();
    private readonly INotificationDispatcher _dispatcher = Substitute.For<INotificationDispatcher>();
    private readonly WakeUpService _service;

    public WakeUpServiceTests()
    {
        _service = new WakeUpService(_preferences, _resolver, _dispatcher, NullLogger<WakeUpService>.Instance);
        _preferences.GetAsync("alice", Arg.Any<CancellationToken>()).Returns(TestData.Preferences());
        _resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<WakeUpRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TrackResult(Track, "itunes", false, []));
        _dispatcher.DispatchAsync(Arg.Any<UserPreferences>(), Arg.Any<Func<string, WakeUpNotification>>(), Arg.Any<CancellationToken>())
            .Returns(new DeliveryResult(TestData.Email, true, false, []));
    }

    [Fact]
    public async Task Wakes_the_user_up_with_the_track_chosen_for_the_weather()
    {
        var report = await _service.WakeUpAsync(TestData.Request(WeatherType.Pluie));

        Assert.True(report.Delivered);
        Assert.False(report.Degraded);
        Assert.Equal(Track, report.Track);
        Assert.Equal("itunes", report.TrackSource);
        Assert.Equal(TestData.Email, report.Channel);
        await _resolver.Received(1).ResolveAsync("Purple Rain", Arg.Any<WakeUpRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Uses_the_fallback_track_when_the_weather_is_not_covered()
    {
        await _service.WakeUpAsync(TestData.Request(WeatherType.Soleil));

        await _resolver.Received(1).ResolveAsync("Bohemian Rhapsody", Arg.Any<WakeUpRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Builds_a_notification_mentioning_the_day_the_weather_and_the_track()
    {
        Func<string, WakeUpNotification>? build = null;
        _dispatcher.DispatchAsync(Arg.Any<UserPreferences>(), Arg.Do<Func<string, WakeUpNotification>>(f => build = f), Arg.Any<CancellationToken>())
            .Returns(new DeliveryResult(TestData.Email, true, false, []));

        await _service.WakeUpAsync(TestData.Request(WeatherType.Pluie, DayOfWeek.Wednesday));
        var notification = build!("alice@example.com");

        Assert.Equal("alice@example.com", notification.Recipient);
        Assert.Equal("Bon mercredi !", notification.Title);
        Assert.Contains("pleut", notification.Message);
        Assert.Contains("Purple Rain — Prince", notification.Message);
        Assert.Equal(Track, notification.Track);
    }

    [Fact]
    public async Task Is_degraded_when_the_music_or_the_channel_was_degraded()
    {
        _resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<WakeUpRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TrackResult(Track, "local-fallback", true, ["itunes : down"]));
        _dispatcher.DispatchAsync(Arg.Any<UserPreferences>(), Arg.Any<Func<string, WakeUpNotification>>(), Arg.Any<CancellationToken>())
            .Returns(new DeliveryResult(TestData.Sms, true, true, ["email : down"]));

        var report = await _service.WakeUpAsync(TestData.Request());

        Assert.True(report.Degraded);
        Assert.True(report.Delivered);
        Assert.Equal(["itunes : down", "email : down"], report.Incidents);
    }

    [Fact]
    public async Task Reports_undelivered_when_the_user_is_unknown()
    {
        var report = await _service.WakeUpAsync(TestData.Request() with { UserId = "nobody" });

        Assert.False(report.Delivered);
        Assert.True(report.Degraded);
        Assert.Equal(Track, report.Track);
        Assert.Contains("Préférences utilisateur introuvables", report.Incidents);
        await _resolver.Received(1).ResolveAsync(string.Empty, Arg.Any<WakeUpRequest>(), Arg.Any<CancellationToken>());
        await _dispatcher.DidNotReceiveWithAnyArgs().DispatchAsync(default!, default!, default);
    }

    [Fact]
    public async Task Never_throws_when_the_preferences_service_is_down()
    {
        _preferences.GetAsync("alice", Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException());

        var report = await _service.WakeUpAsync(TestData.Request());

        Assert.False(report.Delivered);
        Assert.Null(report.Channel);
    }

    [Fact]
    public async Task Rejects_a_null_request()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.WakeUpAsync(null!));
    }

    [Theory]
    [InlineData(WeatherType.Soleil, "soleil")]
    [InlineData(WeatherType.Pluie, "pleut")]
    [InlineData(WeatherType.Neige, "neige")]
    [InlineData(WeatherType.Nuageux, "nuageux")]
    [InlineData(null, "nouvelle journée")]
    public void Message_describes_the_weather(WeatherType? weather, string expected)
    {
        var body = WakeUpMessage.Body(TestData.Request(weather), Track);

        Assert.Contains(expected, body);
    }

    [Theory]
    [InlineData(DayOfWeek.Monday, "Bon lundi !")]
    [InlineData(DayOfWeek.Sunday, "Bon dimanche !")]
    public void Title_uses_the_french_day_name(DayOfWeek day, string expected)
    {
        Assert.Equal(expected, WakeUpMessage.Title(day));
    }
}
