using System;
using System.Collections.Generic;
using System.Text;

namespace DomainNotifyCollector.contract.mail
{
    class MailConfig
    {
        public string mailFrom { get; set; }
        public string mailPwd { get; set; }
        public string smtpHost { get; set; }
        public int smtpPort { get; set; }
        public bool smtpEnableSsl { get; set; } = false;
        public string authCodeSubj { get; set; }
        public string authCodeBody { get; set; }
        public string domainNotifySubj { get; set; }
        public string domainNotifyBody { get; set; }
        public string subject { get; set; }
        public string body { get; set; }
        public string listener { get; set; }
    }
}
