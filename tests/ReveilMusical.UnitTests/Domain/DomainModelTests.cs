using ReveilMusical.Domain.Model;
using ReveilMusical.UnitTests.Support;

namespace ReveilMusical.UnitTests.Domain;

public class DomainModelTests
{
    [Fact]
    public void TrackQueryFor_returns_the_track_chosen_for_the_weather()
    {
        Assert.Equal("Purple Rain", TestData.Preferences().TrackQueryFor(WeatherType.Pluie));
    }

    [Theory]
    [InlineData(WeatherType.Soleil)]
    [InlineData(null)]
    public void TrackQueryFor_returns_the_fallback_track_when_the_weather_is_not_covered(WeatherType? weather)
    {
        Assert.Equal("Bohemian Rhapsody", TestData.Preferences().TrackQueryFor(weather));
    }

    [Fact]
    public void TrackQueryFor_ignores_a_blank_choice()
    {
        var preferences = TestData.Preferences() with
        {
            TracksByWeather = new Dictionary<WeatherType, string> { [WeatherType.Neige] = "  " },
        };

        Assert.Equal("Bohemian Rhapsody", preferences.TrackQueryFor(WeatherType.Neige));
    }

    [Fact]
    public void ChannelKey_is_normalized()
    {
        Assert.Equal(new ChannelKey("email"), new ChannelKey(" EMail "));
        Assert.Equal("email", new ChannelKey("Email").ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ChannelKey_rejects_blank_values(string value)
    {
        Assert.Throws<ArgumentException>(() => new ChannelKey(value));
    }

    [Fact]
    public void Track_is_displayed_as_title_and_artist()
    {
        Assert.Equal("Purple Rain — Prince", new Track("Purple Rain", "Prince").ToString());
    }
}
