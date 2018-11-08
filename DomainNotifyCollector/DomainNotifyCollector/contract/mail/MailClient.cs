using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace DomainNotifyCollector.contract.mail
{
    class MailClient
    {
        private SmtpClient smtpClient;
        private MailConfig config;

        public MailClient(MailConfig config)
        {
            smtpClient = new SmtpClient();
            smtpClient.Credentials = new NetworkCredential(config.mailFrom, config.mailPwd);
            smtpClient.Host = config.smtpHost;
            smtpClient.Port = config.smtpPort;
            smtpClient.EnableSsl = false;
            this.config = config;
        }

        private bool send(MailMessage messge)
        {
            try
            {
                smtpClient.Send(messge);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private MailMessage getMessage(string subject, string body, string to)
        {
            MailMessage msg = new MailMessage();
            msg.From = new MailAddress(config.mailFrom);
            msg.Subject = subject;
            msg.SubjectEncoding = Encoding.UTF8;
            msg.Body = body;
            msg.BodyEncoding = Encoding.UTF8;
            msg.Priority = MailPriority.High;
            msg.IsBodyHtml = false;
            msg.To.Add(to);
            return msg;
        }
        public bool sendCode(string mail, string code)
        {
            string subject = config.authCodeSubj;
            string body = string.Format(config.authCodeBody, code);
            return send(getMessage(subject, body, mail));
        }

        public bool sendData(string mail, List<MailData> data)
        {
            string subject = config.domainNotifySubj;
            StringBuilder sb = new StringBuilder();
            foreach (var it in data)
            {
                string ss = string.Format(config.domainNotifyBody,
                    it.fulldomain,
                    "0x3d64424693c4f084ee5df6163e7c3d412ba3236d8c1853b0b1f73aba5cb8975a",//it.auctionId,
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
            return send(getMessage(subject, body, mail));
        }

        public bool sendBody(string body)
        {
            return sendBody(config.subject, body, config.listener);
        }
        public bool sendBody(string subject, string body, string mail)
        {
            return send(getMessage(subject, body, mail));
        }
    }
}
