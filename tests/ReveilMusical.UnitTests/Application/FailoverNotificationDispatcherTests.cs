using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ReveilMusical.Application.Notifications;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Ports;
using ReveilMusical.UnitTests.Support;

namespace ReveilMusical.UnitTests.Application;

public class FailoverNotificationDispatcherTests
{
    private static INotificationChannel Channel(ChannelKey key, Exception? error = null)
    {
        var channel = Substitute.For<INotificationChannel>();
        channel.Key.Returns(key);
        if (error is not null)
            channel.SendAsync(Arg.Any<WakeUpNotification>(), Arg.Any<CancellationToken>()).ThrowsAsync(error);
        return channel;
    }

    private static FailoverNotificationDispatcher Dispatcher(IEnumerable<INotificationChannel> channels, params string[] fallbackOrder) =>
        new(channels, Options.Create(new NotificationOptions { FallbackOrder = [.. fallbackOrder] }),
            NullLogger<FailoverNotificationDispatcher>.Instance);

    private static WakeUpNotification Build(string recipient) => TestData.Notification(recipient);

    [Fact]
    public async Task Sends_on_the_preferred_channel_with_the_matching_contact()
    {
        var email = Channel(TestData.Email);
        var sms = Channel(TestData.Sms);

        var result = await Dispatcher([email, sms], "sms").DispatchAsync(TestData.Preferences(TestData.Email), Build);

        Assert.True(result.Delivered);
        Assert.False(result.Degraded);
        Assert.Equal(TestData.Email, result.Channel);
        await email.Received(1).SendAsync(Arg.Is<WakeUpNotification>(n => n.Recipient == "alice@example.com"), Arg.Any<CancellationToken>());
        await sms.DidNotReceiveWithAnyArgs().SendAsync(default!, default);
    }

    [Fact]
    public async Task Falls_back_in_the_configured_order_when_the_preferred_channel_fails()
    {
        var email = Channel(TestData.Email, new InvalidOperationException("smtp down"));
        var sms = Channel(TestData.Sms);
        var push = Channel(TestData.Push);

        var result = await Dispatcher([email, sms, push], "push", "sms").DispatchAsync(TestData.Preferences(TestData.Email), Build);

        Assert.True(result.Delivered);
        Assert.True(result.Degraded);
        Assert.Equal(TestData.Push, result.Channel);
        Assert.Contains(result.Incidents, i => i.Contains("smtp down"));
        await sms.DidNotReceiveWithAnyArgs().SendAsync(default!, default);
    }

    [Fact]
    public async Task Ignores_blank_entries_in_the_fallback_order()
    {
        var email = Channel(TestData.Email, new InvalidOperationException());
        var sms = Channel(TestData.Sms);

        var result = await Dispatcher([email, sms], "", " ", "sms").DispatchAsync(TestData.Preferences(TestData.Email), Build);

        Assert.Equal(TestData.Sms, result.Channel);
    }

    [Fact]
    public async Task Tries_any_other_known_contact_even_if_not_in_the_fallback_order()
    {
        var email = Channel(TestData.Email, new InvalidOperationException());
        var sms = Channel(TestData.Sms);

        var result = await Dispatcher([email, sms]).DispatchAsync(TestData.Preferences(TestData.Email), Build);

        Assert.Equal(TestData.Sms, result.Channel);
    }

    [Fact]
    public async Task Skips_channels_without_contact_for_the_user()
    {
        var contacts = new Dictionary<ChannelKey, string> { [TestData.Email] = "a@b.c", [TestData.Sms] = " " };
        var sms = Channel(TestData.Sms);
        var push = Channel(TestData.Push);
        var email = Channel(TestData.Email);

        var result = await Dispatcher([sms, push, email], "sms", "push", "email")
            .DispatchAsync(TestData.Preferences(TestData.Sms, contacts), Build);

        Assert.Equal(TestData.Email, result.Channel);
        Assert.Contains(result.Incidents, i => i.Contains("sms") && i.Contains("aucune adresse"));
        Assert.Contains(result.Incidents, i => i.Contains("push") && i.Contains("aucune adresse"));
    }

    [Fact]
    public async Task Skips_a_preferred_channel_that_is_not_installed()
    {
        var email = Channel(TestData.Email);
        var whatsapp = new ChannelKey("whatsapp");
        var contacts = new Dictionary<ChannelKey, string> { [whatsapp] = "+336", [TestData.Email] = "a@b.c" };

        var result = await Dispatcher([email]).DispatchAsync(TestData.Preferences(whatsapp, contacts), Build);

        Assert.Equal(TestData.Email, result.Channel);
        Assert.Contains(result.Incidents, i => i.Contains("whatsapp : canal non disponible"));
    }

    [Fact]
    public async Task Reports_a_failure_when_no_channel_could_deliver()
    {
        var email = Channel(TestData.Email, new InvalidOperationException("a"));
        var sms = Channel(TestData.Sms, new InvalidOperationException("b"));
        var push = Channel(TestData.Push, new InvalidOperationException("c"));

        var result = await Dispatcher([email, sms, push]).DispatchAsync(TestData.Preferences(), Build);

        Assert.False(result.Delivered);
        Assert.True(result.Degraded);
        Assert.Null(result.Channel);
        Assert.Equal(3, result.Incidents.Count);
    }

    [Fact]
    public async Task Propagates_cancellation_requested_by_the_caller()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var email = Channel(TestData.Email, new OperationCanceledException(cts.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Dispatcher([email]).DispatchAsync(TestData.Preferences(), Build, cts.Token));
    }
}
