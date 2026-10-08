using System.Net;
using Microsoft.Extensions.Options;
using NSubstitute;
using ReveilMusical.Infrastructure.Music;
using ReveilMusical.Infrastructure.Music.Itunes;
using ReveilMusical.UnitTests.Support;

namespace ReveilMusical.UnitTests.Infrastructure;

public class ItunesMusicProviderTests
{
    private static ItunesMusicProvider Provider(StubHttpMessageHandler handler)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(ItunesOptions.HttpClientName)
            .Returns(_ => new HttpClient(handler) { BaseAddress = new Uri("https://itunes.test/") });
        return new ItunesMusicProvider(factory, Options.Create(new ItunesOptions { Limit = 3 }));
    }

    [Fact]
    public async Task Maps_the_first_complete_result_to_a_business_track()
    {
        var handler = StubHttpMessageHandler.Json("""
            { "resultCount": 2, "results": [
              { "trackName": null, "artistName": "X" },
              { "trackName": "Purple Rain", "artistName": "Prince", "trackViewUrl": "https://music.apple.com/x" }
            ] }
            """);

        var track = await Provider(handler).FindTrackAsync("Purple Rain");

        Assert.NotNull(track);
        Assert.Equal("Purple Rain", track.Title);
        Assert.Equal("Prince", track.Artist);
    }

    [Fact]
    public async Task Queries_the_search_endpoint_with_an_encoded_term()
    {
        var handler = StubHttpMessageHandler.Json("""{ "resultCount": 0, "results": [] }""");

        await Provider(handler).FindTrackAsync("Singin' & Rain");

        var uri = handler.Requests.Single().RequestUri!.AbsoluteUri;
        Assert.StartsWith("https://itunes.test/search?term=Singin%27%20%26%20Rain", uri);
        Assert.Contains("media=music", uri);
        Assert.Contains("limit=3", uri);
    }

    [Theory]
    [InlineData("""{ "resultCount": 0, "results": [] }""")]
    [InlineData("""{ "resultCount": 0 }""")]
    public async Task Returns_null_when_nothing_is_found(string json)
    {
        Assert.Null(await Provider(StubHttpMessageHandler.Json(json)).FindTrackAsync("zzz"));
    }

    [Fact]
    public async Task Wraps_http_errors_in_a_provider_exception()
    {
        var handler = StubHttpMessageHandler.Json("{}", HttpStatusCode.ServiceUnavailable);

        var ex = await Assert.ThrowsAsync<MusicProviderException>(() => Provider(handler).FindTrackAsync("q"));
        Assert.Contains("iTunes", ex.Message);
    }

    [Fact]
    public async Task Wraps_unreadable_responses_in_a_provider_exception()
    {
        await Assert.ThrowsAsync<MusicProviderException>(
            () => Provider(StubHttpMessageHandler.Json("<html>")).FindTrackAsync("q"));
    }

    [Fact]
    public void Exposes_its_name()
    {
        Assert.Equal("itunes", Provider(StubHttpMessageHandler.Json("{}")).Name);
    }
}
