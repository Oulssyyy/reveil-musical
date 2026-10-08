using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReveilMusical.Application.Music;
using ReveilMusical.Application.Notifications;
using ReveilMusical.Application.WakeUp;
using ReveilMusical.Domain.Ports;
using ReveilMusical.Infrastructure.Music;
using ReveilMusical.Infrastructure.Music.Itunes;
using ReveilMusical.Infrastructure.Music.MusicBrainz;
using ReveilMusical.Infrastructure.Notifications;
using ReveilMusical.Infrastructure.Notifications.Email;
using ReveilMusical.Infrastructure.Notifications.Push;
using ReveilMusical.Infrastructure.Notifications.Sms;
using ReveilMusical.Infrastructure.Preferences;

namespace ReveilMusical.Infrastructure;

/// <summary>
/// Racine de composition : seul endroit qui connaît les implémentations concrètes.
/// Aucune n'est instanciée à la main (<c>new</c>) : le conteneur s'en charge.
/// Changer de fournisseur ou ajouter un canal = modifier ce fichier et/ou la configuration, rien d'autre.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddReveilMusical(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddMemoryCache();

        // Métier
        services.Configure<MusicOptions>(configuration.GetSection(MusicOptions.SectionName));
        services.Configure<NotificationOptions>(configuration.GetSection(NotificationOptions.SectionName));
        services.AddSingleton<IWakeUpService, WakeUpService>();
        services.AddSingleton<ITrackResolver, FailoverTrackResolver>();
        services.AddSingleton<INotificationDispatcher, FailoverNotificationDispatcher>();

        // Préférences (mock du service interne), protégées par un cache « dernière valeur connue »
        services.AddKeyedSingleton<IUserPreferencesProvider, InMemoryUserPreferencesProvider>(Inner);
        services.AddSingleton<IUserPreferencesProvider>(sp =>
            ActivatorUtilities.CreateInstance<LastKnownPreferencesProvider>(sp,
                sp.GetRequiredKeyedService<IUserPreferencesProvider>(Inner)));

        AddMusic(services, configuration);
        AddNotifications(services, configuration);
        return services;
    }

    private const string Inner = "inner";

    private static void AddMusic(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MusicCacheOptions>(configuration.GetSection(MusicCacheOptions.SectionName));
        services.Configure<ItunesOptions>(configuration.GetSection(ItunesOptions.SectionName));
        services.Configure<MusicBrainzOptions>(configuration.GetSection(MusicBrainzOptions.SectionName));

        services.AddHttpClient(ItunesOptions.HttpClientName, (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<ItunesOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = options.Timeout;
        });
        services.AddHttpClient(MusicBrainzOptions.HttpClientName, (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<MusicBrainzOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = options.Timeout;
            client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
        });

        services.AddKeyedSingleton<IMusicProvider, ItunesMusicProvider>(ItunesMusicProvider.ProviderName);
        services.AddKeyedSingleton<IMusicProvider, MusicBrainzMusicProvider>(MusicBrainzMusicProvider.ProviderName);

        // Chaque source exposée au métier = Cache( Quota( Source ) )
        AddDecoratedMusicProvider(services, ItunesMusicProvider.ProviderName,
            sp => sp.GetRequiredService<IOptions<ItunesOptions>>().Value.MaxRequestsPerMinute);
        AddDecoratedMusicProvider(services, MusicBrainzMusicProvider.ProviderName,
            sp => sp.GetRequiredService<IOptions<MusicBrainzOptions>>().Value.MaxRequestsPerMinute);

        services.AddSingleton<ILocalTrackFallback, HardcodedTrackFallback>();
    }

    private static void AddDecoratedMusicProvider(IServiceCollection services, string name, Func<IServiceProvider, int> maxRequestsPerMinute)
    {
        services.AddSingleton<IMusicProvider>(sp =>
        {
            var source = sp.GetRequiredKeyedService<IMusicProvider>(name);
            var limited = ActivatorUtilities.CreateInstance<RateLimitedMusicProvider>(sp, source, maxRequestsPerMinute(sp));
            return ActivatorUtilities.CreateInstance<CachingMusicProvider>(sp, limited);
        });
    }

    private static void AddNotifications(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FakeChannelOptions>(configuration.GetSection(FakeChannelOptions.SectionName));

        // Faux SDK, chacun avec son interface propre…
        services.AddSingleton<IMailClient, FakeMailClient>();
        services.AddSingleton<ISmsGateway, FakeSmsGateway>();
        services.AddSingleton<IPushService, FakePushService>();

        // …ramenés à l'interface commune par leurs adaptateurs.
        services.AddSingleton<INotificationChannel, EmailNotificationChannel>();
        services.AddSingleton<INotificationChannel, SmsNotificationChannel>();
        services.AddSingleton<INotificationChannel, PushNotificationChannel>();
    }
}
