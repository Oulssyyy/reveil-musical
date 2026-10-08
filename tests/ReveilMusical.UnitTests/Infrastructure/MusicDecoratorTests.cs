using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NSubstitute;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Ports;
using ReveilMusical.Infrastructure.Music;
using ReveilMusical.UnitTests.Support;

namespace ReveilMusical.UnitTests.Infrastructure;

public class MusicDecoratorTests
{
    private static readonly Track Track = new("Clocks", "Coldplay");
    private readonly IMusicProvider _inner = Substitute.For<IMusicProvider>();

    public MusicDecoratorTests()
    {
        _inner.Name.Returns("itunes");
        _inner.FindTrackAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Track);
    }

    private CachingMusicProvider Cached(IMemoryCache cache) =>
        new(_inner, cache, Options.Create(new MusicCacheOptions { TimeToLive = TimeSpan.FromHours(1) }));

    [Fact]
    public async Task Cache_calls_the_source_only_once_per_query()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = Cached(cache);

        var first = await provider.FindTrackAsync("Clocks");
        var second = await provider.FindTrackAsync("  clocks ");

        Assert.Equal(Track, first);
        Assert.Equal(Track, second);
        await _inner.Received(1).FindTrackAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        Assert.Equal("itunes", provider.Name);
    }

    [Fact]
    public async Task Cache_does_not_remember_empty_results()
    {
        _inner.FindTrackAsync("zzz", Arg.Any<CancellationToken>()).Returns((Track?)null);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = Cached(cache);

        await provider.FindTrackAsync("zzz");
        await provider.FindTrackAsync("zzz");

        await _inner.Received(2).FindTrackAsync("zzz", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rate_limiter_refuses_calls_beyond_the_quota()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var provider = new RateLimitedMusicProvider(_inner, 2, clock);

        await provider.FindTrackAsync("a");
        await provider.FindTrackAsync("b");
        var ex = await Assert.ThrowsAsync<MusicProviderRateLimitedException>(() => provider.FindTrackAsync("c"));

        Assert.Contains("itunes", ex.Message);
        await _inner.Received(2).FindTrackAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        Assert.Equal("itunes", provider.Name);
    }

    [Fact]
    public async Task Rate_limiter_frees_slots_once_the_window_has_slid()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var provider = new RateLimitedMusicProvider(_inner, 1, clock);

        await provider.FindTrackAsync("a");
        clock.Advance(TimeSpan.FromSeconds(59));
        await Assert.ThrowsAsync<MusicProviderRateLimitedException>(() => provider.FindTrackAsync("b"));
        clock.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(Track, await provider.FindTrackAsync("c"));
    }

    [Theory]
    [InlineData(WeatherType.Soleil, "Here Comes the Sun")]
    [InlineData(WeatherType.Pluie, "Singin' in the Rain")]
    [InlineData(WeatherType.Neige, "Let It Snow!")]
    [InlineData(WeatherType.Nuageux, "Both Sides Now")]
    [InlineData(null, "Good Morning")]
    public void Local_fallback_always_has_a_track_for_the_weather(WeatherType? weather, string expected)
    {
        var track = new HardcodedTrackFallback().Pick(weather, DayOfWeek.Sunday);

        Assert.Equal(expected, track.Title);
    }

    [Fact]
    public void Local_fallback_varies_with_the_day()
    {
        var fallback = new HardcodedTrackFallback();

        Assert.NotEqual(fallback.Pick(WeatherType.Pluie, DayOfWeek.Monday), fallback.Pick(WeatherType.Pluie, DayOfWeek.Tuesday));
    }
}
