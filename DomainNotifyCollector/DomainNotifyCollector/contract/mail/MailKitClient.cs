using MailKit.Net.Smtp;
using MimeKit;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DomainNotifyCollector.contract.mail
{
    class MailKitClient
    {
        private SmtpClient client;
        private MailConfig config;

        public MailKitClient(MailConfig config)
        {
            this.config = config;
            init();
        }

        private void init()
        {
            client = new SmtpClient();
            client.ServerCertificateValidationCallback = (s, c, h, e) => true;
            client.Connect(config.smtpHost, config.smtpPort, config.smtpEnableSsl);
            client.AuthenticationMechanisms.Remove("XOAUTH2");
            client.Authenticate(config.mailFrom, config.mailPwd);
        }

        public void sendMsg(string message, string to)
        {
            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress("sccot", config.mailFrom));
            msg.To.Add(new MailboxAddress("", to));
            msg.Subject = config.subject;
            msg.Body = new TextPart("plain") { Text = string.Format(config.body, message) };
        }
        public void sendAuthCode(string message, string to)
        {
            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress("sccot", config.mailFrom));
            msg.To.Add(new MailboxAddress("applyer", to));
            msg.Subject = config.authCodeSubj;
            msg.Body = new TextPart("plain") { Text = string.Format(config.authCodeBody, message) };
        }
        public bool sendDomainNotify(string message, string to)
        {
            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress("sccot", config.mailFrom));
            msg.To.Add(new MailboxAddress("subscriber", to));
            msg.Subject = config.domainNotifySubj;
            msg.Body = new TextPart("plain") { Text = string.Format(config.domainNotifyBody, message) };

            return send(msg);
        }
        public bool sendData(string mail, List<MailData> data)
        {
            string subject = config.domainNotifySubj;
            StringBuilder sb = new StringBuilder();
            foreach (var it in data)
            {
                string ss = string.Format(config.domainNotifyBody,
                    it.fulldomain,
                    it.maxPrice,
                    it.maxBuyer
                    );
                string[] fmt = ss.Split(",");
                foreach (var s in ss.Split(","))
                {
                    sb.Append(s).Append("\n");
                }
            };
            string body = sb.ToString();

            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress("sccot", config.mailFrom));
            msg.To.Add(new MailboxAddress("ss", mail));
            msg.Subject = config.domainNotifySubj;
            msg.Body = new TextPart("plain") { Text = body };

            return send(msg);
        }
        public bool sendToListener(string message)
        {
            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress("sccot", config.mailFrom));

            string toEmail = config.listener;
            if (toEmail.IndexOf(",") >= 0)
            {
                var toArr = toEmail.Split(",");
                toEmail = toArr[0];
                if (toArr.Length > 1)
                {
                    foreach (var addr in toArr.Skip(1).ToArray())
                    {
                        msg.Cc.Add(new MailboxAddress("", addr));
                    }
                }
            }
            msg.To.Add(new MailboxAddress("listener", toEmail));

            msg.Subject = config.subject;
            msg.Body = new TextPart("plain") { Text = string.Format(config.body, message) };

            return send(msg);
        }
        private bool send(MimeMessage msg)
        {
            try
            {
                client.SendAsync(msg);
                return true;
            }
            catch
            {
                init();
                return false;
            }
        }
        
    }
}
