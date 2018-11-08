using AuctionDomainReconcilia;
using AuctionDomainReconcilia.lib;
using ContractNotifyCollector.core;
using ContractNotifyCollector.helper;
using DomainNotifyCollector.contract.mail;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace DomainNotifyCollector.contract.monitor
{
    class DomainAuctionReconciliation : ContractTask
    {
        private static MongoDBHelper mh = new MongoDBHelper();
        private JObject config;
        public DomainAuctionReconciliation(string name): base(name)
        {

        }
        public override void initConfig(JObject config)
        {
            this.config = config;
            initConfig();
        }

        public override void startTask()
        {
            if (!initSuccFlag) return;
            while(true)
            {
                try
                {
                    ping();
                    process();
                } catch (Exception ex)
                {
                    LogHelper.printEx(ex);
                }
            }
        }

        private void process()
        {
            // 已对账高度
            int lmax = getLmax();

            // 最新高度
            int rmax = getRmax();

            
            //if (lmax >= rmax) return;
            
            // 获取参与竞拍地址列表
            List<string> addrs = MongoDBHelperExtra.getAuctionAddrList(mongodbConnStr, mongodbDatabase, auctionStateCol, lmax, rmax, bonusAddr);
            if (addrs == null || addrs.Count == 0)
            {
                updateRecord(rmax);
                return;
            }
            
            // 获取地址余额(cc vs db)
            addrs.ForEach(p =>
            {
                string addr = p;
                //
                decimal d1 = balanceOf(addr);

                decimal d2 = MongoDBHelperExtra.queryAddrBalance(mongodbConnStr, mongodbDatabase, cgasBalanceCol, addr, regscripthash);

                if(d1 != d2)
                {
                    //Console.WriteLine("{0}-->{1} -> {2}", addr, d1, d2);
                    string msg = String.Format("addr={0},smBalance={1},dbBalance={2}", addr, d1, d2);
                    addrBalanceQueue.Add(msg);
                }

            });

            // 获取地址竞拍余额(cc vs db)
            addrs.ForEach(p => {
                string addr = p;
                // 
                List<string> list = MongoDBHelperExtra.getAuctionAddrIdList(mongodbConnStr, mongodbDatabase, auctionStateCol, lmax, rmax, addr);
                if (list == null || list.Count == 0) return;

                list.ForEach(pk => {
                    string id = pk;

                    decimal d1 = balanceOfBid(addr, id);

                    decimal d2 = MongoDBHelperExtra.queryAddrIdBalance(mongodbConnStr, mongodbDatabase, auctionStateCol, addr, id);
                    if (d1 != d2)
                    {
                        //Console.WriteLine("{0}-->{1} -> {2}-> {3}", addr, id, d1, d2);
                        string msg = String.Format("addr={0},auctionId={1},smBalance={2},dbBalance={3}", addr, id, d1, d2);
                        addrIdBalanceQueue.Add(msg);
                    }
                });
            });
            
            addrBalanceQueue.Add(string.Format("addr={0},auctionId={1},smBalance={2},dbBalance={3}", 1,1,1,1));
            addrIdBalanceQueue.Add(string.Format("addr={0},auctionId={1},smBalance={2},dbBalance={3}", 1,1,1,1));
            // 发送
            StringBuilder sb = new StringBuilder();
            sb.Append("注册器下账户地址余额:");
            foreach( var it in addrBalanceQueue.ToArray())
            {
                sb.Append("\n\t").Append(it);
            }
            sb.Append("\n注册器下账户地址竞拍余额:");
            foreach (var it in addrIdBalanceQueue)
            {
                sb.Append("\n\t").Append(it);
            }
            Console.WriteLine(sb.ToString());
            mc.sendBody(sb.ToString());
            while(addrBalanceQueue.Count > 0) addrBalanceQueue.Take();
            while(addrIdBalanceQueue.Count > 0) addrIdBalanceQueue.Take();


            // 更新高度
            updateRecord(rmax);
            LogHelper.debug(string.Format("{0} has processed at {1}", name(), rmax));
        }
        private BlockingCollection<string> addrBalanceQueue = new BlockingCollection<string>();
        private BlockingCollection<string> addrIdBalanceQueue = new BlockingCollection<string>();
        static decimal balanceOf(string addr)
        {
            return ContractHelper.balanceOf(addr);
        }
        static decimal balanceOfBid(string addr, string auctionId)
        {
            return ContractHelper.balanceOfBid(addr, auctionId);
        }
        private static int getLmax()
        {
            string findStr = "{key:'auctionState'}";
            var res = mh.GetData(mongodbConnStr, mongodbDatabase, reconciliaRecord, findStr);
            if (res != null && res.Count > 0)
            {
                return int.Parse(res[0]["lastBlockindex"].ToString());
            }
            return -1;
        }
        private static int getRmax()
        {
            return MongoDBHelperExtra.getRmax(mongodbConnStr, mongodbDatabase, auctionStateCol);
        }
        private static void updateRecord(int blockindex)
        {
            string findStr = "{key:'auctionState'}";
            string dataStr = new JObject() { { "$set", new JObject() { { "lastBlockindex", blockindex } } } }.ToString();
            mh.UpdateData(mongodbConnStr, mongodbDatabase, reconciliaRecord, dataStr, findStr);
        }
        private void ping()
        {
            LogHelper.ping(3000, name());
        }

        private static string mongodbConnStr;
        private static string mongodbDatabase;
        private static string auctionStateCol;
        private static string cgasBalanceCol;
        private static string RegAddr;
        private static string bonusAddr;
        private static string apiUrl;
        private static string regscripthash;
        private static string reconciliaRecord;

        private MailClient mc;
        private bool initSuccFlag = false;
        private void initConfig()
        {
            JToken cfg = config["TaskList"].Where(p => p["taskName"].ToString() == name() && p["taskNet"].ToString() == networkType()).ToArray()[0]["taskInfo"];

            mongodbConnStr = cfg["mongodbConnStr"].ToString();
            mongodbDatabase = cfg["mongodbDatabase"].ToString();
            auctionStateCol = cfg["auctionStateCol"].ToString();
            cgasBalanceCol = cfg["cgasBalanceCol"].ToString();
            RegAddr = cfg["registerAddress"].ToString();
            bonusAddr = cfg["bonusAddress"].ToString();
            apiUrl = cfg["apiUrl"].ToString();
            regscripthash = cfg["regscripthash"].ToString();
            reconciliaRecord = cfg["reconciliaRecord"].ToString();

            // 合约调用
            ContractHelper.setApiUrl(apiUrl);
            ContractHelper.setRegHash(regscripthash);

            cfg = cfg["mailInfo"];
            mc = new MailClient(new MailConfig {
                mailFrom = cfg["mailFrom"].ToString(),
                mailPwd = cfg["mailPwd"].ToString(),
                smtpHost = cfg["smtpHost"].ToString(),
                smtpPort = int.Parse(cfg["smtpPort"].ToString()),
                subject = cfg["subject"].ToString(),
                listener = cfg["listener"].ToString(),
            });
            

            initSuccFlag = true;
        }
    }
}
