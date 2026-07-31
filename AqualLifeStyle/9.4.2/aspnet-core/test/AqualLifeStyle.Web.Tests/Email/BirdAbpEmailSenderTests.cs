using System;
using System.Net.Mail;
using System.Threading.Tasks;
using Abp.Net.Mail;
using AqualLifeStyle.Email;
using AqualLifeStyle.Web.Host.Email;
using NSubstitute;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Web.Tests.Email
{
    public class BirdAbpEmailSenderTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task SendAsync_RejectsCcAndBccRecipients(bool useBlindCopy)
        {
            var gateway = Substitute.For<ITransactionalEmailDeliveryGateway>();
            var sender = new BirdAbpEmailSender(
                Substitute.For<IEmailSenderConfiguration>(),
                gateway);
            using var message = new MailMessage("sender@example.test", "recipient@example.test");
            if (useBlindCopy)
                message.Bcc.Add("copy@example.test");
            else
                message.CC.Add("copy@example.test");

            await Should.ThrowAsync<InvalidOperationException>(() => sender.SendAsync(message));

            await gateway.DidNotReceive().SendAsync(Arg.Any<TransactionalEmail>());
        }
    }
}
