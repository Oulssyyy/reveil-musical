using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Ports;
using ReveilMusical.Infrastructure.Preferences;
using ReveilMusical.UnitTests.Support;

namespace ReveilMusical.UnitTests.Infrastructure;

public class PreferencesTests
{
    [Theory]
    [InlineData("alice", "email")]
    [InlineData("BOB", "sms")]
    [InlineData("chloe", "push")]
    public async Task In_memory_mock_knows_the_demo_users(string userId, string channel)
    {
        var preferences = await new InMemoryUserPreferencesProvider().GetAsync(userId);

        Assert.NotNull(preferences);
        Assert.Equal(new ChannelKey(channel), preferences.PreferredChannel);
        Assert.True(preferences.Contacts.ContainsKey(preferences.PreferredChannel));
    }

    [Fact]
    public async Task In_memory_mock_returns_null_for_unknown_users()
    {
        Assert.Null(await new InMemoryUserPreferencesProvider().GetAsync("nobody"));
    }

    [Fact]
    public async Task Last_known_preferences_are_served_when_the_service_goes_down()
    {
        var inner = Substitute.For<IUserPreferencesProvider>();
        var expected = TestData.Preferences();
        inner.GetAsync("alice", Arg.Any<CancellationToken>()).Returns(expected);
        var provider = new LastKnownPreferencesProvider(inner, NullLogger<LastKnownPreferencesProvider>.Instance);

        Assert.Same(expected, await provider.GetAsync("alice"));
        inner.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException("down"));

        Assert.Same(expected, await provider.GetAsync("ALICE"));
    }

    [Fact]
    public async Task Failure_is_propagated_when_nothing_was_ever_known()
    {
        var inner = Substitute.For<IUserPreferencesProvider>();
        inner.GetAsync("alice", Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException("down"));
        var provider = new LastKnownPreferencesProvider(inner, NullLogger<LastKnownPreferencesProvider>.Instance);

        await Assert.ThrowsAsync<HttpRequestException>(() => provider.GetAsync("alice"));
    }
}
