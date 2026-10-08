using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ReveilMusical.Application.Music;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Ports;
using ReveilMusical.UnitTests.Support;

namespace ReveilMusical.UnitTests.Application;

public class FailoverTrackResolverTests
{
    private static readonly Track LocalTrack = new("Good Morning", "Gene Kelly");
    private readonly ILocalTrackFallback _local = Substitute.For<ILocalTrackFallback>();

    public FailoverTrackResolverTests()
    {
        _local.Pick(Arg.Any<WeatherType?>(), Arg.Any<DayOfWeek>()).Returns(LocalTrack);
    }

    private static IMusicProvider Provider(string name, Track? track = null, Exception? error = null)
    {
        var provider = Substitute.For<IMusicProvider>();
        provider.Name.Returns(name);
        if (error is not null)
            provider.FindTrackAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).ThrowsAsync(error);
        else
            provider.FindTrackAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(track);
        return provider;
    }

    private FailoverTrackResolver Resolver(IEnumerable<IMusicProvider> providers, params string[] order) =>
        new(providers, _local, Options.Create(new MusicOptions { ProviderOrder = [.. order] }),
            NullLogger<FailoverTrackResolver>.Instance);

    [Fact]
    public async Task Uses_the_first_provider_when_it_answers()
    {
        var first = Provider("a", new Track("T", "A"));
        var second = Provider("b", new Track("U", "B"));

        var result = await Resolver([first, second], "a", "b").ResolveAsync("q", TestData.Request());

        Assert.Equal(new Track("T", "A"), result.Track);
        Assert.Equal("a", result.Source);
        Assert.False(result.Degraded);
        Assert.Empty(result.Incidents);
        await second.DidNotReceiveWithAnyArgs().FindTrackAsync(default!, default);
    }

    [Fact]
    public async Task Fails_over_to_the_next_provider_when_one_is_down()
    {
        var down = Provider("a", error: new HttpRequestException("boom"));
        var up = Provider("b", new Track("U", "B"));

        var result = await Resolver([down, up], "a", "b").ResolveAsync("q", TestData.Request());

        Assert.Equal("b", result.Source);
        Assert.True(result.Degraded);
        Assert.Contains(result.Incidents, i => i.Contains("boom"));
    }

    [Fact]
    public async Task Fails_over_when_a_provider_finds_nothing()
    {
        var empty = Provider("a");
        var up = Provider("b", new Track("U", "B"));

        var result = await Resolver([empty, up], "a", "b").ResolveAsync("q", TestData.Request());

        Assert.Equal("b", result.Source);
        Assert.Contains(result.Incidents, i => i.Contains("aucun résultat"));
    }

    [Fact]
    public async Task Falls_back_to_the_local_list_when_every_provider_fails()
    {
        var request = TestData.Request(WeatherType.Neige, DayOfWeek.Friday);
        var resolver = Resolver([Provider("a", error: new TimeoutException()), Provider("b")], "a", "b");

        var result = await resolver.ResolveAsync("q", request);

        Assert.Equal(LocalTrack, result.Track);
        Assert.Equal(FailoverTrackResolver.LocalSourceName, result.Source);
        Assert.True(result.Degraded);
        _local.Received(1).Pick(WeatherType.Neige, DayOfWeek.Friday);
    }

    [Fact]
    public async Task Follows_the_configured_order_and_ignores_unlisted_providers()
    {
        var a = Provider("a", new Track("A", "A"));
        var b = Provider("b", new Track("B", "B"));
        var c = Provider("c", new Track("C", "C"));

        var result = await Resolver([a, b, c], "", "B", "b", "a", "unknown").ResolveAsync("q", TestData.Request());

        Assert.Equal("b", result.Source);
        await c.DidNotReceiveWithAnyArgs().FindTrackAsync(default!, default);
    }

    [Fact]
    public async Task Uses_the_local_list_when_no_provider_is_configured()
    {
        var a = Provider("a", new Track("A", "A"));

        var result = await Resolver([a]).ResolveAsync("q", TestData.Request());

        Assert.Equal(FailoverTrackResolver.LocalSourceName, result.Source);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Does_not_query_providers_with_a_blank_query(string query)
    {
        var a = Provider("a", new Track("A", "A"));

        var result = await Resolver([a], "a").ResolveAsync(query, TestData.Request());

        Assert.Equal(LocalTrack, result.Track);
        await a.DidNotReceiveWithAnyArgs().FindTrackAsync(default!, default);
    }

    [Fact]
    public async Task Propagates_cancellation_requested_by_the_caller()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var a = Provider("a", error: new OperationCanceledException(cts.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Resolver([a], "a").ResolveAsync("q", TestData.Request(), cts.Token));
    }
}
