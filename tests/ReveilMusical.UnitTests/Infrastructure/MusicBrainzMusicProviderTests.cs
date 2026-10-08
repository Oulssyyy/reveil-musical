using System.Net;
using NSubstitute;
using ReveilMusical.Infrastructure.Music;
using ReveilMusical.Infrastructure.Music.MusicBrainz;
using ReveilMusical.UnitTests.Support;

namespace ReveilMusical.UnitTests.Infrastructure;

public class MusicBrainzMusicProviderTests
{
    private static MusicBrainzMusicProvider Provider(StubHttpMessageHandler handler)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(MusicBrainzOptions.HttpClientName)
            .Returns(_ => new HttpClient(handler) { BaseAddress = new Uri("https://mb.test/ws/2/") });
        return new MusicBrainzMusicProvider(factory);
    }

    [Fact]
    public async Task Maps_title_and_joined_artist_credits()
    {
        var handler = StubHttpMessageHandler.Json("""
            { "recordings": [
              { "id": "1", "title": "Under Pressure", "artist-credit": [
                { "name": "Queen", "joinphrase": " & " }, { "name": "David Bowie" } ] }
            ] }
            """);

        var track = await Provider(handler).FindTrackAsync("Under Pressure");

        Assert.Equal("Under Pressure", track!.Title);
        Assert.Equal("Queen & David Bowie", track.Artist);
    }

    [Fact]
    public async Task Skips_recordings_without_title_or_artist()
    {
        var handler = StubHttpMessageHandler.Json("""
            { "recordings": [
              { "title": "Sans artiste", "artist-credit": [] },
              { "title": null, "artist-credit": [ { "name": "X" } ] },
              { "title": "Clocks", "artist-credit": [ { "name": "Coldplay" } ] }
            ] }
            """);

        var track = await Provider(handler).FindTrackAsync("Clocks");

        Assert.Equal("Clocks", track!.Title);
    }

    [Theory]
    [InlineData("""{ "recordings": [] }""")]
    [InlineData("""{ }""")]
    public async Task Returns_null_when_nothing_is_found(string json)
    {
        Assert.Null(await Provider(StubHttpMessageHandler.Json(json)).FindTrackAsync("zzz"));
    }

    [Fact]
    public async Task Queries_the_recording_endpoint_in_json()
    {
        var handler = StubHttpMessageHandler.Json("""{ "recordings": [] }""");

        await Provider(handler).FindTrackAsync("Purple Rain");

        var uri = handler.Requests.Single().RequestUri!.AbsoluteUri;
        Assert.StartsWith("https://mb.test/ws/2/recording?query=Purple%20Rain", uri);
        Assert.Contains("fmt=json", uri);
    }

    [Fact]
    public async Task Wraps_http_errors_in_a_provider_exception()
    {
        var handler = StubHttpMessageHandler.Json("{}", HttpStatusCode.Forbidden);

        var ex = await Assert.ThrowsAsync<MusicProviderException>(() => Provider(handler).FindTrackAsync("q"));
        Assert.Contains("MusicBrainz", ex.Message);
    }

    [Fact]
    public void Builds_an_identifiable_user_agent()
    {
        var options = new MusicBrainzOptions { ApplicationName = "App", ApplicationVersion = "2.0", Contact = "me@x.org" };

        Assert.Equal("App/2.0 ( me@x.org )", options.UserAgent);
    }

    [Fact]
    public void Exposes_its_name()
    {
        Assert.Equal("musicbrainz", Provider(StubHttpMessageHandler.Json("{}")).Name);
    }
}
