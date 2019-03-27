using MimeKit;
using System;
using System.Net.Mail;
using System.Threading.Tasks;

namespace Postal
{
    /// <summary>
    /// Parses raw string output of email views into <see cref="MimeMessage"/>.
    /// </summary>
    public interface IEmailParser
    {
        /// <summary>
        /// Creates a <see cref="MimeMessage"/> from the string output of an email view.
        /// </summary>
        /// <param name="emailViewOutput">The string output of the email view.</param>
        /// <param name="email">The email data used to render the view.</param>
        /// <returns>The created <see cref="MailMessage"/></returns>
        Task<MimeMessage> ParseAsync(string emailViewOutput, Email email);
    }
}
