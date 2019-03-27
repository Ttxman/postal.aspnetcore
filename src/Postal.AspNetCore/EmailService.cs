using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Postal.AspNetCore;
using Microsoft.Extensions.Logging;
using MailKit.Net.Smtp;
using MimeKit;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Postal.Tests")]
namespace Postal
{
    /// <summary>
    /// Sends email using the default <see cref="SmtpClient"/>.
    /// </summary>
    public class EmailService : IEmailService
    {
        /// <summary>
        /// Creates a new <see cref="EmailService"/>.
        /// </summary>
        public EmailService(IEmailViewRender emailViewRenderer, IEmailParser emailParser, IOptions<EmailServiceOptions> options, ILogger<EmailService> logger)
        {
            this.emailViewRenderer = emailViewRenderer;
            this.emailParser = emailParser;
            this.options = options.Value;
            this.logger = logger;
        }

        protected readonly IEmailViewRender emailViewRenderer;
        protected IEmailParser emailParser;
        protected EmailServiceOptions options;
        protected ILogger<EmailService> logger;

        //for unit testing
        internal Func<Task<SmtpClient>> PrepareSmtpClientAsync => options.PrepareSmtpClientAsync;

        /// <summary>
        /// Send an email asynchronously, using an <see cref="SmtpClient"/>.
        /// </summary>
        /// <param name="email">The email to send.</param>
        /// <returns>A <see cref="Task"/> that completes once the email has been sent.</returns>
        public async Task SendAsync(Email email)
        {
            var mailMessage = await CreateMailMessageAsync(email);
            await SendAsync(mailMessage);
        }

        /// <summary>
        /// Send an email asynchronously, using an <see cref="SmtpClient"/>.
        /// </summary>
        /// <param name="email">The email to send.</param>
        /// <returns>A <see cref="Task"/> that completes once the email has been sent.</returns>
        public async Task SendAsync(MimeMessage mailMessage)
        {
            using (var smtp = await options.PrepareSmtpClientAsync())
            {
                this.logger.LogDebug($"Smtp created: host: {options.Host}, port: {options.Port}, securiry: {options.SecurityOption}");
                this.logger.LogInformation($"Smtp send email from {mailMessage.From} to {mailMessage.To}");
                await smtp.SendAsync(mailMessage);
                await smtp.DisconnectAsync(true);
            }
        }

        /// <summary>
        /// Renders the email view and builds a <see cref="MailMessage"/>. Does not send the email.
        /// </summary>
        /// <param name="email">The email to render.</param>
        /// <returns>A <see cref="MailMessage"/> containing the rendered email.</returns>
        public async Task<MimeMessage> CreateMailMessageAsync(Email email)
        {
            var rawEmailString = await emailViewRenderer.RenderAsync(email);
            emailParser = new EmailParser(emailViewRenderer);
            var mailMessage = await emailParser.ParseAsync(rawEmailString, email);
            return mailMessage;
        }
    }
}