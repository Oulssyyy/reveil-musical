using ReveilMusical.Cli;
using ReveilMusical.Domain.Model;

namespace ReveilMusical.UnitTests.Cli;

public class WakeUpArgumentsTests
{
    [Theory]
    [InlineData("lundi", DayOfWeek.Monday)]
    [InlineData("DIMANCHE", DayOfWeek.Sunday)]
    [InlineData("Wednesday", DayOfWeek.Wednesday)]
    public void Parses_french_and_english_day_names(string value, DayOfWeek expected)
    {
        Assert.True(WakeUpArguments.TryParse(["alice", value, "SOLEIL"], out var request, out var error));

        Assert.Null(error);
        Assert.Equal(new WakeUpRequest("alice", expected, WeatherType.Soleil), request);
    }

    [Theory]
    [InlineData("SOLEIL", WeatherType.Soleil)]
    [InlineData("pluie", WeatherType.Pluie)]
    [InlineData("Neige", WeatherType.Neige)]
    [InlineData("NUAGEUX", WeatherType.Nuageux)]
    [InlineData("TORNADE", null)]
    [InlineData("2", null)]
    public void Unknown_weather_is_not_blocking(string value, WeatherType? expected)
    {
        Assert.Equal(expected, WakeUpArguments.ParseWeather(value));
    }

    [Theory]
    [InlineData("funday")]
    [InlineData("3")]
    public void Rejects_unknown_days(string value)
    {
        Assert.False(WakeUpArguments.TryParse(["alice", value, "SOLEIL"], out var request, out var error));

        Assert.Null(request);
        Assert.Contains(value, error);
    }

    [Fact]
    public void Rejects_a_wrong_number_of_arguments()
    {
        Assert.False(WakeUpArguments.TryParse(["alice"], out _, out var error));

        Assert.StartsWith("Usage", error);
    }
}
