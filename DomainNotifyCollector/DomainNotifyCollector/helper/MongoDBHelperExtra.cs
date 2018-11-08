using DomainNotifyCollector.helper;
using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AuctionDomainReconcilia.lib
{
    class MongoDBHelperExtra
    {

        public static decimal queryAddrBalance(string mongodbConnStr, string mongodbDatabase, string coll, string address, string reghash)
        {
            var client = new MongoClient(mongodbConnStr);
            var database = client.GetDatabase(mongodbDatabase);
            var collection = database.GetCollection<BsonDocument>(coll);

            var query = collection.Find(BsonDocument.Parse(new JObject() { { "address", address }, { "register", reghash } }.ToString())).ToList();
            if(query != null && query.Count > 0)
            {
                return decimal.Parse(DecimalHelper.formatDecimal(query[0]["balance"].ToString()), System.Globalization.NumberStyles.Float);
            }
            return 0;
        }
        public static decimal queryAddrIdBalance(string mongodbConnStr, string mongodbDatabase, string coll, string address, string auctionId)
        {
            IList<IPipelineStageDefinition> stages = null;
            PipelineDefinition<BsonDocument, BsonDocument> pipeline = null;
            List<BsonDocument> query = null;
            //
            string unwind = new JObject() { { "$unwind", "$addwholist" } }.ToString();
            string match = new JObject() { { "$match", new JObject() {
                { "auctionState", "0401" },
                { "addwholist.accountTime", null}, // ***************未结算
                { "addwholist.getdomainTime", null}, /// ************未结算
                { "addwholist.address", address },
                { "auctionId", auctionId}
            } } }.ToString();
            string project = new JObject() { { "$project", new JObject() { { "_id", 0 }, { "addwholist", 1 } } } }.ToString();
            
            //
            stages = new List<IPipelineStageDefinition>();
            stages.Add(new JsonPipelineStageDefinition<BsonDocument, BsonDocument>(unwind));
            stages.Add(new JsonPipelineStageDefinition<BsonDocument, BsonDocument>(match));
            stages.Add(new JsonPipelineStageDefinition<BsonDocument, BsonDocument>(project));
            pipeline = new PipelineStagePipelineDefinition<BsonDocument, BsonDocument>(stages);

            query = Aggregate(mongodbConnStr, mongodbDatabase, coll, pipeline);
            if(query != null && query.Count > 0)
            {
                return decimal.Parse(query[0]["addwholist"]["totalValue"].ToString(), System.Globalization.NumberStyles.Float);
            }
            return 0;
        }
        public static List<JObject> getAuctionAddrAndId(string mongodbConnStr, string mongodbDatabase, string coll, int start, int end, string bonusAddr)
        {
            IList<IPipelineStageDefinition> stages = null;
            PipelineDefinition<BsonDocument, BsonDocument> pipeline = null;
            List<BsonDocument> query = null;
            //
            string project = new JObject() { { "$project", new JObject() {
                { "auctionState", 1 },
                { "lastTime.blockindex", 1 },
                { "addwholist", 1 },
                { "auctionId", 1 }
            } } }.ToString();
            string unwind = new JObject() { { "$unwind", "$addwholist" } }.ToString();
            string match = new JObject() { { "$match", new JObject() {
                { "auctionState", "0401" },
                { "addwholist.address", new JObject(){ {"$ne", bonusAddr } } },
                { "lastTime.blockindex", new JObject(){ {"$gt", start }, { "$lte", end } } }
            } } }.ToString();
            string projectSecond = new JObject() { { "$project", new JObject() { { "_id", 0 }, { "auctionId",1 } } } }.ToString();

            //
            stages = new List<IPipelineStageDefinition>();
            stages.Add(new JsonPipelineStageDefinition<BsonDocument, BsonDocument>(project));
            stages.Add(new JsonPipelineStageDefinition<BsonDocument, BsonDocument>(unwind));
            stages.Add(new JsonPipelineStageDefinition<BsonDocument, BsonDocument>(match));
            stages.Add(new JsonPipelineStageDefinition<BsonDocument, BsonDocument>(projectSecond));
            pipeline = new PipelineStagePipelineDefinition<BsonDocument, BsonDocument>(stages);

            query = Aggregate(mongodbConnStr, mongodbDatabase, coll, pipeline);
            if (query != null && query.Count > 0)
            {
                return query.Select(p => new JObject() { {"address", p["addwholist"]["address"].ToString() }, { "auctionId", p["auctionId"].ToString() } }).ToList();
            }
            return null;
        }

        public static List<string> getAuctionAddrIdList(string mongodbConnStr, string mongodbDatabase, string coll, int start, int end, string addess)
        {
            IList<IPipelineStageDefinition> stages = null;
            PipelineDefinition<BsonDocument, BsonDocument> pipeline = null;
            List<BsonDocument> query = null;
            //
            string project = new JObject() { { "$project", new JObject() { { "auctionState", 1 }, { "lastTime.blockindex", 1 }, { "addwholist", 1 }, { "auctionId", 1 } } } }.ToString();
            string unwind = new JObject() { { "$unwind", "$addwholist" } }.ToString();
            string match = new JObject() { { "$match", new JObject() {
                { "auctionState", "0401" },
                { "addwholist.address", addess },
                { "lastTime.blockindex", new JObject(){ {"$gt", start }, { "$lte", end } } }
            } } }.ToString();
            string projectSecond = new JObject() { { "$project", new JObject() { { "_id", 0 }, { "auctionId",1 } } } }.ToString();

            //
            stages = new List<IPipelineStageDefinition>();
            stages.Add(new JsonPipelineStageDefinition<BsonDocument, BsonDocument>(project));
            stages.Add(new JsonPipelineStageDefinition<BsonDocument, BsonDocument>(unwind));
            stages.Add(new JsonPipelineStageDefinition<BsonDocument, BsonDocument>(match));
            stages.Add(new JsonPipelineStageDefinition<BsonDocument, BsonDocument>(projectSecond));
            pipeline = new PipelineStagePipelineDefinition<BsonDocument, BsonDocument>(stages);

            query = Aggregate(mongodbConnStr, mongodbDatabase, coll, pipeline);
            if (query != null && query.Count > 0)
            {
                return query.Select(p => p["auctionId"].ToString()).ToList();
            }
            return null;
        }
        public static List<string> getAuctionAddrList(string mongodbConnStr, string mongodbDatabase, string coll, int start, int end, string bonusAddr)
        {

            IList<IPipelineStageDefinition> stages = null;
            PipelineDefinition<BsonDocument, BsonDocument> pipeline = null;
            List<BsonDocument> query = null;
            //
            string project = new JObject(){ { "$project", new JObject() { { "auctionState", 1 }, { "lastTime.blockindex", 1 }, { "addwholist", 1 } } } }.ToString();
            string unwind = new JObject() { { "$unwind", "$addwholist" } }.ToString();
            string match = new JObject() { { "$match", new JObject() {
                { "auctionState", "0401" },
                { "addwholist.address", new JObject(){ {"$ne", bonusAddr } } },
                { "lastTime.blockindex", new JObject(){ {"$gt", start }, { "$lte", end } } }
            } } }.ToString();
            string projectSecond = new JObject() { { "$project", new JObject() { { "_id", 0 }, { "addwholist.address", 1 } } } }.ToString();
            //
            stages = new List<IPipelineStageDefinition>();
            stages.Add(new JsonPipelineStageDefinition<BsonDocument, BsonDocument>(project));
            stages.Add(new JsonPipelineStageDefinition<BsonDocument, BsonDocument>(unwind));
            stages.Add(new JsonPipelineStageDefinition<BsonDocument, BsonDocument>(match));
            stages.Add(new JsonPipelineStageDefinition<BsonDocument, BsonDocument>(projectSecond));
            pipeline = new PipelineStagePipelineDefinition<BsonDocument, BsonDocument>(stages);

            query = Aggregate(mongodbConnStr, mongodbDatabase, coll, pipeline);
            if(query != null && query.Count > 0)
            {
                return query.Select(p => p["addwholist"]["address"].ToString()).Distinct().ToList();
            }
            return null;
        }
        public static int getRmax(string mongodbConnStr, string mongodbDatabase, string coll)
        {
            IList<IPipelineStageDefinition> stages = null;
            PipelineDefinition<BsonDocument, BsonDocument> pipeline = null;
            List<BsonDocument> query = null;
            //
            string project = new JObject() { { "$project", new JObject() { { "_id", 0 }, { "auctionState", 1 }, { "lastTime.blockindex", 1 } } } }.ToString();
            string match = new JObject() { { "$match", new JObject() {
                { "auctionState", "0401" }
            } } }.ToString();
            string sort = new JObject() { {"$sort", new JObject() { { "lastTime.blockindex", -1} } } }.ToString();
            string limit = new JObject() { { "$limit", 1 } }.ToString();
            //
            stages = new List<IPipelineStageDefinition>();
            stages.Add(new JsonPipelineStageDefinition<BsonDocument, BsonDocument>(project));
            stages.Add(new JsonPipelineStageDefinition<BsonDocument, BsonDocument>(match));
            stages.Add(new JsonPipelineStageDefinition<BsonDocument, BsonDocument>(sort));
            stages.Add(new JsonPipelineStageDefinition<BsonDocument, BsonDocument>(limit));
            pipeline = new PipelineStagePipelineDefinition<BsonDocument, BsonDocument>(stages);
            //
            query = Aggregate(mongodbConnStr, mongodbDatabase, coll, pipeline);
            
            if(query != null && query.Count > 0)
            {
                return int.Parse(query[0]["lastTime"]["blockindex"].ToString());
            }
            return -1;
        }

        private static List<BsonDocument> Aggregate(string mongodbConnStr, string mongodbDatabase, string coll, PipelineDefinition<BsonDocument, BsonDocument> pipeline)
        {
            var client = new MongoClient(mongodbConnStr);
            var database = client.GetDatabase(mongodbDatabase);
            var collection = database.GetCollection<BsonDocument>(coll);
            var query = collection.Aggregate(pipeline).ToList();

            client = null;
            return query;
        }
    }
}
