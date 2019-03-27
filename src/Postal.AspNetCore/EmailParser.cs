using MimeKit;
using MimeKit.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Postal
{
    /// <summary>
    /// Converts the raw string output of a view into a <see cref="MailMessage"/>.
    /// </summary>
    public class EmailParser : IEmailParser
    {
        /// <summary>
        /// Creates a new <see cref="EmailParser"/>.
        /// </summary>
        /// 
        public EmailParser(IEmailViewRender alternativeViewRenderer)
        {
            this.alternativeViewRenderer = alternativeViewRenderer;
        }

        readonly IEmailViewRender alternativeViewRenderer;

        /// <summary>
        /// Parses the email view output into a <see cref="MailMessage"/>.
        /// </summary>
        /// <param name="emailViewOutput">The email view output.</param>
        /// <param name="email">The <see cref="Email"/> used to generate the output.</param>
        /// <returns>A <see cref="MailMessage"/> containing the email headers and content.</returns>
        public async Task<MimeMessage> ParseAsync(string emailViewOutput, Email email)
        {
            var message = new MimeMessage();
            var builder = email.BodyBuilder;

            if (string.IsNullOrWhiteSpace(emailViewOutput))
            {
                throw new ArgumentNullException(nameof(emailViewOutput));
            }
            using (var reader = new StringReader(emailViewOutput))
            {
                await ParserUtils.ParseHeadersAsync(reader, (key, value) => ProcessHeaderAsync(key, value, message, email));
                AssignCommonHeaders(message, email);
                if (builder.TextBody is null && builder.HtmlBody is null)
                {
                    var messageBody = reader.ReadToEnd().Trim();
                    if (email.ImageEmbedder.HasImages)
                    {
                        builder.TextBody = "Plain text not available.";
                        foreach (var r in email.ImageEmbedder.Builder.LinkedResources)
                            builder.LinkedResources.Add(r);
                    }
                    else
                    {
                        if (messageBody.StartsWith("<"))
                            builder.HtmlBody = messageBody;
                        else
                            builder.TextBody = messageBody;
                    }
                }
            }

            message.Body = builder.ToMessageBody();

            return message;
        }


        private void AssignCommonHeaders(MimeMessage message, Email email)
        {
            if (message.To.Count == 0)
            {
                AssignCommonHeader<string>(email, "to", to => message.To.Add(new MailboxAddress(to)));
                AssignCommonHeader<MailboxAddress>(email, "to", to => message.To.Add(to));
            }
            if (message.From == null)
            {
                AssignCommonHeader<string>(email, "from", from => message.From.Add(new MailboxAddress(from)));
                AssignCommonHeader<MailboxAddress>(email, "from", from => message.From.Add(from));
            }
            if (message.Cc.Count == 0)
            {
                AssignCommonHeader<string>(email, "cc", cc => message.Cc.Add(new MailboxAddress(cc)));
                AssignCommonHeader<MailboxAddress>(email, "cc", cc => message.Cc.Add(cc));
            }
            if (message.Bcc.Count == 0)
            {
                AssignCommonHeader<string>(email, "bcc", bcc => message.Bcc.Add(new MailboxAddress(bcc)));
                AssignCommonHeader<MailboxAddress>(email, "bcc", bcc => message.Bcc.Add(bcc));
            }
            if (message.ResentReplyTo.Count == 0)
            {
                AssignCommonHeader<string>(email, "replyto", replyTo => message.ResentReplyTo.Add(new MailboxAddress(replyTo)));
                AssignCommonHeader<MailboxAddress>(email, "replyto", replyTo => message.ResentReplyTo.Add(replyTo));
            }
            if (message.Sender == null)
            {
                AssignCommonHeader<string>(email, "sender", sender => message.Sender = new MailboxAddress(sender));
                AssignCommonHeader<MailboxAddress>(email, "sender", sender => message.Sender = sender);
            }
            if (string.IsNullOrEmpty(message.Subject))
            {
                AssignCommonHeader<string>(email, "subject", subject => message.Subject = subject);
            }
        }

        private void AssignCommonHeader<T>(Email email, string header, Action<T> assign)
            where T : class
        {
            if (email.ViewData.TryGetValue(header, out object value))
            {
                if (value is T typedValue)
                    assign(typedValue);
            }
        }

        private async Task ProcessHeaderAsync(string key, string value, MimeMessage message, Email email)
        {
            if (IsAlternativeViewsHeader(key))
                await CreateAlternativeViews(value, email);
            else
                AssignEmailHeaderToMailMessage(key, value, message);
        }

        private async Task CreateAlternativeViews(string deliminatedViewNames, Email email)
        {
            var viewNames = deliminatedViewNames.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var alternativeViewName in viewNames)
            {
                var fullViewName = GetAlternativeViewName(email, alternativeViewName);
                var output = await alternativeViewRenderer.RenderAsync(email, fullViewName);

                string contentType;
                string body;
                using (var reader = new StringReader(output))
                {
                    contentType = ParseHeadersForContentType(reader);
                    body = reader.ReadToEnd();
                }

                if (string.IsNullOrWhiteSpace(contentType))
                {
                    if (alternativeViewName.Equals("text", StringComparison.OrdinalIgnoreCase))
                        contentType = "text/plain";
                    else if (alternativeViewName.Equals("html", StringComparison.OrdinalIgnoreCase))
                        contentType = "text/html";
                }
                var ct = ContentType.Parse(contentType);

                switch (ct.MimeType)
                {
                    case "text/plain":
                        email.BodyBuilder.TextBody = body;
                        break;
                    case "text/html":
                        email.BodyBuilder.HtmlBody = body;
                        break;
                    default:
                        throw new Exception("The 'Content-Type' header is unknown or missing from the alternative view '" + fullViewName + "'.");
                }

                foreach (var r in email.ImageEmbedder.Builder.LinkedResources)
                    email.BodyBuilder.LinkedResources.Add(r);
            }
        }

        private static string GetAlternativeViewName(Email email, string alternativeViewName)
        {
            if (email.ViewName.StartsWith("~"))
            {
                var index = email.ViewName.LastIndexOf('.');
                return email.ViewName.Insert(index + 1, alternativeViewName + ".");
            }
            else
            {
                return email.ViewName + "." + alternativeViewName;
            }
        }

        private string ParseHeadersForContentType(StringReader reader)
        {
            string contentType = null;
            ParserUtils.ParseHeaders(reader, (key, value) =>
            {
                if (key.Equals("content-type", StringComparison.OrdinalIgnoreCase))
                {
                    contentType = value;
                }
            });
            return contentType;
        }

        private bool IsAlternativeViewsHeader(string headerName)
        {
            return headerName.Equals("views", StringComparison.OrdinalIgnoreCase);
        }

        private void AssignEmailHeaderToMailMessage(string key, string value, MimeMessage message)
        {
            switch (key)
            {
                case "to":
                    message.To.Add(new MailboxAddress(value));
                    break;
                case "from":
                    message.From.Add(new MailboxAddress(value));
                    break;
                case "subject":
                    message.Subject = value;
                    break;
                case "cc":
                    message.Cc.Add(new MailboxAddress(value));
                    break;
                case "bcc":
                    message.Bcc.Add(new MailboxAddress(value));
                    break;
                case "reply-to":
                    message.ReplyTo.Add(new MailboxAddress(value));
                    break;
                case "sender":
                    message.Sender = new MailboxAddress(value);
                    break;
                case "priority":
                    MessagePriority priority;
                    if (Enum.TryParse(value, true, out priority))
                    {
                        message.Priority = priority;
                    }
                    else
                    {
                        throw new ArgumentException(string.Format("Invalid email priority: {0}. It must be High, Medium or Low.", value));
                    }
                    break;
                case "content-type":
                    var charsetMatch = Regex.Match(value, @"\bcharset\s*=\s*(.*)$");
                    if (charsetMatch.Success)
                    {
                        message.Body.ContentType.Charset = charsetMatch.Groups[1].Value;
                    }
                    break;
                default:
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        message.Headers[key] = "   (empty)";
                    }
                    else
                    {
                        message.Headers[key] = value;
                    }
                    break;
            }
        }
    }
}