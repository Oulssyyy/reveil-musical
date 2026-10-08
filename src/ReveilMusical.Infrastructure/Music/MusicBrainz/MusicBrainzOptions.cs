namespace ReveilMusical.Infrastructure.Music.MusicBrainz;

public sealed class MusicBrainzOptions
{
    public const string SectionName = "Music:MusicBrainz";
    public const string HttpClientName = "musicbrainz";

    public string BaseUrl { get; set; } = "https://musicbrainz.org/ws/2/";
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>MusicBrainz rejette les requêtes sans User-Agent identifiable (application + contact).</summary>
    public string ApplicationName { get; set; } = "ReveilMusical";
    public string ApplicationVersion { get; set; } = "1.0";
    public string Contact { get; set; } = "contact@reveil-musical.example";

    /// <summary>MusicBrainz limite à ~1 requête/seconde par client.</summary>
    public int MaxRequestsPerMinute { get; set; } = 50;

    public string UserAgent => $"{ApplicationName}/{ApplicationVersion} ( {Contact} )";
}
