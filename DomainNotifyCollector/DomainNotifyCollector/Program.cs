using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ContractNotifyCollector;
using ContractNotifyCollector.core;
using ContractNotifyCollector.helper;
using DomainNotifyCollector.contract;
using DomainNotifyCollector.contract.monitor;
using log4net;
using log4net.Config;
using log4net.Repository;

/// <summary>
/// 合约汇总进程
/// 
/// </summary>
namespace DomainNotifyCollector
{
    class Program
    {
        /// <summary>
        /// 添加任务列表
        /// 
        /// </summary>
        private static void InitTask()
        {
            AddTask(new DomainUpdateColletor("DomainUpdateCollector"));
            AddTask(new DomainUpdateSender("DomainUpdateSender") );
            AddTask(new DomainAuctionReconciliation("DomainAuctionReconciliation") );
        }

        /// <summary>
        /// 启动任务列表
        /// 
        /// </summary>
        private static void StartTask()
        {
            foreach (var func in list)
            {
                func.Init(Config.getConfig());
            }
            foreach (var func in list)
            {
                new Task(() => {
                    func.Start();
                }).Start();
            }
        }

        private static List<ITask> list = new List<ITask>();
        private static void AddTask(ITask handler)
        {
            list.Add(handler);
        }

        /// <summary>
        /// 程序启动入口
        /// 
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            initConfig();
            ProjectInfo.head();
            InitTask();
            StartTask();
            ProjectInfo.tail();
            while (true)
            {
                System.Threading.Thread.Sleep(1000);
            }
        }

        static void initConfig()
        {
            Config.loadConfig("config.json");
            LogHelper.initLogger("log4net.config");
        }
    }
    
    class ProjectInfo
    {
        private static string appName = "ContactNotifyCollector";

        public static void head()
        {
            string[] info = new string[] {
                "*** Start to run "+appName,
                "*** Auth:tsc",
                "*** Version:0.0.0.1",
                "*** CreateDate:2018-07-25",
                "*** LastModify:2018-08-08"
            };
            foreach (string ss in info)
            {
                log(ss);
            }
        }
        public static void tail()
        {
            log("Program." + appName + " exit");
        }

        static void log(string ss)
        {
            LogHelper.debug(" " + ss);
        }
    }
}
