using ContractNotifyCollector;
using ContractNotifyCollector.core;
using ContractNotifyCollector.helper;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;

namespace DomainNotifyCollector.contract
{
    class DomainUpdateColletor : ContractTask
    {
        private MongoDBHelper mh = new MongoDBHelper();
        public DomainUpdateColletor(string name) : base(name)
        {

        }
        public override void initConfig(JObject config)
        {
            JToken cfg = config["TaskList"].Where(p => p["taskName"].ToString() == name()).ToArray()[0]["taskInfo"];

            auctionKey = cfg["auctionKey"].ToString();
            auctionRecordCol = cfg["auctionRecordCol"].ToString();
            auctionStateCol = cfg["auctionStateCol"].ToString();
            auctionStateChangeCol = cfg["auctionStateChangeCol"].ToString();
            notifySubsColl = cfg["notifySubsCol"].ToString();
            batchSize = int.Parse(cfg["batchSize"].ToString());
            batchInterval = int.Parse(cfg["batchInterval"].ToString());
            // db info
            localDbConnInfo = Config.localDbConnInfo;
            remoteDbConnInfo = Config.notifyDbConnInfo;
            
            //
            initSuccFlag = true;
        }

        public override void startTask()
        {
            run();
        }

        private DbConnInfo localDbConnInfo;
        private DbConnInfo remoteDbConnInfo;
        private string auctionKey;
        private string auctionRecordCol;
        private string auctionStateCol;
        private string auctionStateChangeCol;
        private string notifySubsColl;
        private int batchSize;
        private int batchInterval;
        private bool initSuccFlag = false;

        private void run()
        {
            if (!initSuccFlag) return;
            long lastBlockindex = getLastBlockindex();
            string dataFieldStr = new JObject() { { "auctionId", 1 }, { "fulldomain", 1 }, { "maxBuyer", 1 }, { "maxPrice", 1 }}.ToString();
            string dataSortStr = new JObject() { { "lastTime.blockindex", 1 } }.ToString();
            lastBlockindex = 2891910;
            while (true)
            {
                try
                {
                    ping();

                    // 
                    long newlastBlockindex = getLastBlockindex();
                    if (newlastBlockindex <= lastBlockindex) continue;
                    

                    JObject timeFilter = new JObject() {
                        {
                            "lastTime.blockindex",
                            new JObject() { { "$gt", lastBlockindex}, { "$lte", newlastBlockindex } }
                        }
                    };
                    JObject stateFilter = new JObject()
                    {
                        {
                            "$or",
                            new JArray()
                            {
                                new JObject(){{"auctionState", AuctionState.STATE_CONFIRM} },
                                new JObject(){{"auctionState", AuctionState.STATE_RANDOM} }
                            }
                        }
                    };
                    string findStr = new JObject()
                    {
                        {
                            "$and",
                            new JArray()
                            {
                                timeFilter,
                                stateFilter
                            }
                        }
                    }.ToString() ;


                    int sumCount = 0;
                    long cnt = mh.GetDataCount(remoteDbConnInfo.connStr, remoteDbConnInfo.connDB, auctionStateCol, findStr);
                    for (int startIndex=0; startIndex < cnt; startIndex+=batchSize)
                    {
                        // 分页获取变更数据
                        JArray res = mh.GetDataWithField(remoteDbConnInfo.connStr, remoteDbConnInfo.connDB, auctionStateCol, dataFieldStr, findStr, dataSortStr, startIndex, batchSize);
                        if (res == null || res.Count == 0) continue;

                        /*
                        // 筛选变更数据
                        // ...    
                        string[] fulldomainArr = getDistinctField();
                        if (fulldomainArr == null || fulldomainArr.Length == 0) continue;
                        */

                        // 入库变更数据
                        mh.PutData(localDbConnInfo.connStr, localDbConnInfo.connDB, auctionStateChangeCol, res);
                        sumCount += res.Count;
                    }
                    lastBlockindex = newlastBlockindex;
                    LogHelper.printLog(name() + " has processed:" + sumCount);

                }
                catch (Exception ex)
                {
                    LogHelper.printEx(ex);
                }
            }
        }

        private void ping()
        {
            LogHelper.ping(batchInterval, name());
        }

        private long getLastBlockindex()
        {
            string findStr = new JObject() { { "contractColl", auctionKey } }.ToString();
            JArray res = mh.GetData(remoteDbConnInfo.connStr, remoteDbConnInfo.connDB, auctionRecordCol, findStr);
            long lastBlockindex = long.Parse(res[0]["lastBlockindex"].ToString());
            return lastBlockindex;
        }

        private string[] getDistinctField()
        {
            return mh.Distinct(localDbConnInfo.connStr, localDbConnInfo.connDB, notifySubsColl, "domain");
        }

        
    }
}
