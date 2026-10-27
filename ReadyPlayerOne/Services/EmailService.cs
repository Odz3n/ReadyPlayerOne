using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using ReadyPlayerOne.Configurations;
using ReadyPlayerOne.Interfaces;
using System.Net.Mail;

namespace ReadyPlayerOne.Services
{
    public class EmailService: IEmailService
    {
        private readonly SmtpSettings _settings;
        public EmailService(IOptions<SmtpSettings> options)
        {
            _settings = options.Value;
        }
        public async Task SendEmailAsync(
            string to,
            string subject,
            string htmlBody,
            CancellationToken token)
        {
            var message = new MimeMessage();

            message.From.Add(MailboxAddress.Parse(_settings.Email));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;
            message.Body = new BodyBuilder
            {
                HtmlBody = htmlBody
            }.ToMessageBody();

            using var client = new MailKit.Net.Smtp.SmtpClient();
            await client.ConnectAsync(_settings.Host, _settings.Port, SecureSocketOptions.StartTls, token);
            await client.AuthenticateAsync(_settings.Email, _settings.Password, token);
            await client.SendAsync(message, token);
            await client.DisconnectAsync(true, token);
        }
    }
}
