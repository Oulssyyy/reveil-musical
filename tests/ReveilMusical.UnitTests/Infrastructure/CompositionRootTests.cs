using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReveilMusical.Application.WakeUp;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Ports;
using ReveilMusical.Infrastructure;
using ReveilMusical.Infrastructure.Music;
using ReveilMusical.Infrastructure.Music.MusicBrainz;
using ReveilMusical.Infrastructure.Preferences;

namespace ReveilMusical.UnitTests.Infrastructure;

/// <summary>Vérifie le câblage IoC, sans réseau : les sources pointent vers un port fermé.</summary>
public class CompositionRootTests
{
    private static ServiceProvider Build(params (string Key, string? Value)[] overrides)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Music:ProviderOrder:0"] = "itunes",
            ["Music:ProviderOrder:1"] = "musicbrainz",
            ["Music:Itunes:BaseUrl"] = "http://127.0.0.1:9/",
            ["Music:MusicBrainz:BaseUrl"] = "http://127.0.0.1:9/",
            ["Music:MusicBrainz:Contact"] = "tests@example.com",
            ["Notifications:FallbackOrder:0"] = "push",
            ["Notifications:FallbackOrder:1"] = "sms",
            ["Notifications:FallbackOrder:2"] = "email",
        };
        foreach (var (key, value) in overrides)
            settings[key] = value;

        var services = new ServiceCollection().AddLogging();
        services.AddReveilMusical(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    [Fact]
    public void Registers_every_source_and_channel_behind_their_abstractions()
    {
        using var provider = Build();

        Assert.NotNull(provider.GetRequiredService<IWakeUpService>());
        Assert.Equal(["itunes", "musicbrainz"], provider.GetServices<IMusicProvider>().Select(p => p.Name));
        Assert.All(provider.GetServices<IMusicProvider>(), p => Assert.IsType<CachingMusicProvider>(p));
        Assert.Equal(["email", "sms", "push"], provider.GetServices<INotificationChannel>().Select(c => c.Key.Value));
        Assert.IsType<LastKnownPreferencesProvider>(provider.GetRequiredService<IUserPreferencesProvider>());
    }

    [Fact]
    public void MusicBrainz_client_sends_an_identifiable_user_agent()
    {
        using var provider = Build();

        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(MusicBrainzOptions.HttpClientName);

        Assert.Equal("ReveilMusical/1.0 ( tests@example.com )", client.DefaultRequestHeaders.UserAgent.ToString());
    }

    [Fact]
    public async Task Wakes_the_user_up_from_the_local_list_when_every_source_is_down()
    {
        using var provider = Build();

        var report = await provider.GetRequiredService<IWakeUpService>()
            .WakeUpAsync(new WakeUpRequest("alice", DayOfWeek.Monday, WeatherType.Pluie));

        Assert.True(report.Delivered);
        Assert.True(report.Degraded);
        Assert.Equal("local-fallback", report.TrackSource);
        Assert.Equal(new ChannelKey("email"), report.Channel);
    }

    [Fact]
    public async Task Switches_channel_when_the_preferred_one_is_down()
    {
        using var provider = Build(("Notifications:Fakes:SmsOutage", "true"), ("Music:ProviderOrder:0", null), ("Music:ProviderOrder:1", null));

        var report = await provider.GetRequiredService<IWakeUpService>()
            .WakeUpAsync(new WakeUpRequest("bob", DayOfWeek.Monday, WeatherType.Pluie));

        Assert.True(report.Delivered);
        Assert.Equal(new ChannelKey("push"), report.Channel);
        Assert.Contains(report.Incidents, i => i.Contains("503"));
    }
}
