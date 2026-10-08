using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ReveilMusical.Infrastructure.Notifications;
using ReveilMusical.Infrastructure.Notifications.Email;
using ReveilMusical.Infrastructure.Notifications.Push;
using ReveilMusical.Infrastructure.Notifications.Sms;
using ReveilMusical.UnitTests.Support;

namespace ReveilMusical.UnitTests.Infrastructure;

public class NotificationChannelTests
{
    private static IOptions<FakeChannelOptions> Fakes(bool email = false, bool sms = false, bool push = false) =>
        Options.Create(new FakeChannelOptions { EmailOutage = email, SmsOutage = sms, PushOutage = push });

    // ---- Email

    [Fact]
    public async Task Email_adapter_sends_an_html_encoded_mail()
    {
        var client = Substitute.For<IMailClient>();
        client.SendMail(Arg.Any<MailEnvelope>()).Returns(true);
        var channel = new EmailNotificationChannel(client);

        await channel.SendAsync(TestData.Notification("a@b.c") with { Message = "Rock & roll" });

        Assert.Equal(TestData.Email, channel.Key);
        client.Received(1).SendMail(new MailEnvelope("a@b.c", "Bon lundi !", "<p>Rock &amp; roll</p>"));
    }

    [Fact]
    public async Task Email_adapter_turns_a_refusal_into_an_exception()
    {
        var client = Substitute.For<IMailClient>();
        client.SendMail(Arg.Any<MailEnvelope>()).Returns(false);

        var ex = await Assert.ThrowsAsync<NotificationDeliveryException>(
            () => new EmailNotificationChannel(client).SendAsync(TestData.Notification()));
        Assert.Equal(TestData.Email, ex.Channel);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Fake_mail_client_reports_outages(bool outage, bool expected)
    {
        var client = new FakeMailClient(Fakes(email: outage), NullLogger<FakeMailClient>.Instance);

        Assert.Equal(expected, client.SendMail(new MailEnvelope("a", "b", "c")));
    }

    // ---- SMS

    [Fact]
    public async Task Sms_adapter_sends_title_and_message_as_flat_text()
    {
        var gateway = Substitute.For<ISmsGateway>();
        gateway.TransmitAsync(default!, default!, default).ReturnsForAnyArgs(new SmsGatewayResponse(200, null));
        var channel = new SmsNotificationChannel(gateway);

        await channel.SendAsync(TestData.Notification("+336"));

        Assert.Equal(TestData.Sms, channel.Key);
        await gateway.Received(1).TransmitAsync("+336", "Bon lundi ! Il pleut.", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sms_adapter_truncates_long_messages_to_fit_one_sms()
    {
        var gateway = new FakeSmsGateway(Fakes(), NullLogger<FakeSmsGateway>.Instance);
        var channel = new SmsNotificationChannel(gateway);

        await channel.SendAsync(TestData.Notification() with { Message = new string('x', 500) });

        Assert.Equal(FakeSmsGateway.MaxLength, SmsNotificationChannel.Truncate(new string('x', 500), FakeSmsGateway.MaxLength).Length);
        Assert.Equal("abc", SmsNotificationChannel.Truncate("abc", 10));
        Assert.EndsWith("…", SmsNotificationChannel.Truncate("abcdef", 4));
    }

    [Fact]
    public async Task Sms_adapter_turns_an_error_status_into_an_exception()
    {
        var gateway = new FakeSmsGateway(Fakes(sms: true), NullLogger<FakeSmsGateway>.Instance);

        var ex = await Assert.ThrowsAsync<NotificationDeliveryException>(
            () => new SmsNotificationChannel(gateway).SendAsync(TestData.Notification()));
        Assert.Contains("503", ex.Message);
    }

    [Fact]
    public async Task Fake_sms_gateway_rejects_messages_that_are_too_long()
    {
        var gateway = new FakeSmsGateway(Fakes(), NullLogger<FakeSmsGateway>.Instance);

        var response = await gateway.TransmitAsync("+336", new string('x', 161), CancellationToken.None);

        Assert.Equal(413, response.StatusCode);
    }

    // ---- Push

    [Fact]
    public async Task Push_adapter_sends_a_payload_with_the_track_as_data()
    {
        var service = Substitute.For<IPushService>();
        var channel = new PushNotificationChannel(service);

        await channel.SendAsync(TestData.Notification("token"));

        Assert.Equal(TestData.Push, channel.Key);
        await service.Received(1).PushAsync(Arg.Is<PushPayload>(p =>
            p.DeviceToken == "token" && p.Title == "Bon lundi !" && p.Body == "Il pleut."
            && p.Data["trackTitle"] == "Purple Rain" && p.Data["trackArtist"] == "Prince"));
    }

    [Fact]
    public async Task Push_adapter_translates_the_sdk_exception()
    {
        var service = Substitute.For<IPushService>();
        service.PushAsync(Arg.Any<PushPayload>()).ThrowsAsync(new PushServiceException("quota"));

        var ex = await Assert.ThrowsAsync<NotificationDeliveryException>(
            () => new PushNotificationChannel(service).SendAsync(TestData.Notification()));
        Assert.IsType<PushServiceException>(ex.InnerException);
    }

    [Fact]
    public async Task Fake_push_service_returns_a_message_id_or_fails_on_outage()
    {
        var payload = new PushPayload("t", "a", "b", new Dictionary<string, string>());

        var id = await new FakePushService(Fakes(), NullLogger<FakePushService>.Instance).PushAsync(payload);
        Assert.Equal(32, id.Length);

        await Assert.ThrowsAsync<PushServiceException>(
            () => new FakePushService(Fakes(push: true), NullLogger<FakePushService>.Instance).PushAsync(payload));
    }
}
