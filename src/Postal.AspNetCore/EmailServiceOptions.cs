
using MailKit.Net.Smtp;
using MailKit.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Postal.AspNetCore
{
    public class EmailServiceOptions
    {
        public EmailServiceOptions()
        {
            PrepareSmtpClientAsync = async () =>
            {
                var client = new SmtpClient();

                await client.ConnectAsync(Host, Port, SecurityOption);

                if (!string.IsNullOrWhiteSpace(UserName))
                    await client.AuthenticateAsync(UserName, Password);

                return client;
            };
        }

        public string Host { get; set; }
        public int Port { get; set; }
        public SecureSocketOptions SecurityOption { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }

        public Func<Task<SmtpClient>> PrepareSmtpClientAsync { get; set; }
    }
}
