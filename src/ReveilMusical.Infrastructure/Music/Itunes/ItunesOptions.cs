namespace ReveilMusical.Infrastructure.Music.Itunes;

public sealed class ItunesOptions
{
    public const string SectionName = "Music:Itunes";
    public const string HttpClientName = "itunes";

    public string BaseUrl { get; set; } = "https://itunes.apple.com/";
    public int Limit { get; set; } = 5;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>iTunes tolère ~20 requêtes/minute.</summary>
    public int MaxRequestsPerMinute { get; set; } = 20;
}
