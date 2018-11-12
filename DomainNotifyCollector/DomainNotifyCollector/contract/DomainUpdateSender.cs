using ContractNotifyCollector;
using ContractNotifyCollector.core;
using ContractNotifyCollector.helper;
using DomainNotifyCollector.contract.mail;
using DomainNotifyCollector.helper;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DomainNotifyCollector.contract
{
    class DomainUpdateSender : ContractTask
    {
        private MongoDBHelper mh = new MongoDBHelper();

        public DomainUpdateSender(string name) : base(name)
        {

        }
        public override void initConfig(JObject config)
        {
            JToken cfg = config["TaskList"].Where(p => p["taskName"].ToString() == name()).ToArray()[0]["taskInfo"];

            auctionStateChangeCol = cfg["auctionStateChangeCol"].ToString();
            notifySubsColl = cfg["notifySubsCol"].ToString();
            batchSize = int.Parse(cfg["batchSize"].ToString());
            batchInterval = int.Parse(cfg["batchInterval"].ToString());

            var mailCfg = cfg["mailInfo"];
            mailConfig = new MailConfig
            {
                mailFrom = mailCfg["mailFrom"].ToString(),
                mailPwd = mailCfg["mailPwd"].ToString(),
                smtpHost = mailCfg["smtpHost"].ToString(),
                smtpPort = int.Parse(mailCfg["smtpPort"].ToString()),
                authCodeSubj = mailCfg["authCodeSubj"].ToString(),
                authCodeBody = mailCfg["authCodeBody"].ToString(),
                domainNotifySubj = mailCfg["domainNotifySubj"].ToString(),
                domainNotifyBody = mailCfg["domainNotifyBody"].ToString(),
            };
            // db info
            remoteDbConnInfo = Config.notifyDbConnInfo;
            
            //
            new Task(() => sendThread()).Start();
            initSuccFlag = true;
        }

        public override void startTask()
        {
            run();
        }

        private string auctionStateChangeCol;
        private string notifySubsColl;
        private int batchSize;
        private int batchInterval;
        private DbConnInfo remoteDbConnInfo;
        private MailConfig mailConfig;
        private bool initSuccFlag = false;

        private void run()
        {
            if (!initSuccFlag) return;
            string dataSortStr = new JObject() { { "lastTime.blockindex", 1 } }.ToString();
            while (true)
            {
                try
                {
                    ping();

                    int sumCount = 0;
                    long cnt = mh.GetDataCount(remoteDbConnInfo.connStr, remoteDbConnInfo.connDB, auctionStateChangeCol, "{}");
                    for (int startIndex = 0; startIndex < cnt; startIndex += batchSize)
                    {

                        // 分页获取变更数据
                        JArray res = mh.GetData(remoteDbConnInfo.connStr, remoteDbConnInfo.connDB, auctionStateChangeCol, "{}", dataSortStr, startIndex, batchSize);
                        if (res == null || res.Count == 0) continue;
                        
                        
                        foreach(var item in res)
                        {
                            string fulldomain = item["fulldomain"].ToString();
                            string maxBuyer = item["maxBuyer"].ToString();
                            string maxPrice = item["maxPrice"].ToString();
                            string findStr = new JObject() { { "domain", fulldomain } }.ToString();
                            long subcnt = mh.GetDataCount(remoteDbConnInfo.connStr, remoteDbConnInfo.connDB, notifySubsColl, findStr);
                            for(int ist=0; ist<subcnt; ist+=batchSize)
                            {
                                // 分页获取订阅邮箱
                                JArray ires = mh.GetData(remoteDbConnInfo.connStr, remoteDbConnInfo.connDB, notifySubsColl, findStr, "{}", ist, batchSize);
                                if (ires == null || ires.Count == 0) continue;

                                var mailArr = ires.Select(p => p["mail"].ToString()).Distinct().ToList();
                                mailArr.ForEach(p =>
                                {

                                    // 加入发送队列
                                    sendQueue.Add(new SendMsg
                                    {
                                        mail = p,
                                        mailData = new List<MailData>{
                                            new MailData
                                            {
                                                fulldomain = fulldomain,
                                                maxBuyer = maxBuyer,
                                                maxPrice = DecimalHelper.formatDecimal(maxPrice)
                                            }
                                        }
                                    });
                                });
                            }
                        }
                        
                        // 删除
                        res.ToList().ForEach(p => {
                            string findStr = new JObject() { { "fulldomain", p["fulldomain"].ToString() } }.ToString();
                            mh.DeleteData(remoteDbConnInfo.connStr, remoteDbConnInfo.connDB, auctionStateChangeCol, findStr);
                        }
                        );
                        sumCount += res.Count;
                    }
                    LogHelper.printLog(name() + " has processed:" + sumCount);
                }
                catch(Exception ex)
                {
                    LogHelper.printEx(ex);
                }
                
            }
        }

        private BlockingCollection<SendMsg> sendQueue = new BlockingCollection<SendMsg>();
        private MailKitClient mc;
        private void sendThread()
        {
            mc = new MailKitClient(mailConfig);
            while(true)
            {
                try
                {
                    SendMsg msg = sendQueue.Take();
                    mc.sendData(msg.mail, msg.mailData);
                }
                catch(Exception ex)
                {
                    LogHelper.printEx(ex);
                }
                
            }
        }
        
        private void ping()
        {
            LogHelper.ping(batchInterval, name());
        }
    }
    class SendMsg
    {
        public string mail { get; set; }
        public List<MailData> mailData { get; set; }
    }
}
