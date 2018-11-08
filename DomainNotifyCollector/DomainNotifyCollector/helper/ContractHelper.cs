using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ThinNeo;
using ThinNeo.Cryptography;
using ThinNeo.Cryptography.Cryptography;

namespace AuctionDomainReconcilia
{
    class ContractHelper
    {
        public static void setApiUrl(string api)
        {
            nns_common.api = api;
        }
        public static void setRegHash(string scripthash)
        {
            regScriptHash = new Hash160(scripthash);
        }
        private static Hash160 regScriptHash;
        public static decimal balanceOf(string addr)
        {
            var result = nns_common.api_InvokeScript(regScriptHash, "balanceOf", "(bytes)" + Helper.Bytes2HexString(Helper.GetPublicKeyHashFromAddress(addr)));
            JObject jo = JObject.Parse(result.Result.textInfo);
            JArray ja = (JArray)jo["result"][0]["stack"];
            if (ja != null && ja.Count > 0)
            {
                string val = jo["result"][0]["stack"][0]["value"].ToString();
                decimal v = decimal.Parse(val.getNumStrFromHexStr(8));
                return v;
            }
            return 0;
        }
        public static decimal balanceOfBid(string addr, string auctionId)
        {
            var result = nns_common.api_InvokeScript(regScriptHash, "balanceOfBid",
                            "(address)" + addr,
                            "(hex256)" + auctionId);
            JArray ja = (JArray)JObject.Parse(result.Result.textInfo)["result"][0]["stack"];
            if (ja != null && ja.Count > 0)
            {
                var val = JObject.Parse(result.Result.textInfo)["result"][0]["stack"][0]["value"].ToString();
                var v = decimal.Parse(val.getNumStrFromHexStr(8));
                return v;
            }
            return 0;
        }

    }

    static class StringHelper
    {
        //十六进制转数值（考虑精度调整）
        public static string getNumStrFromHexStr(this string hexStr, int decimals)
        {
            var rr = changeDecimals(new BigInteger(10001000000), 8);
            BigInteger bi = new BigInteger(ThinNeo.Helper.HexString2Bytes(hexStr));
            //return bi.ToString();
            if (hexStr == "806967ff" || hexStr == "a08601" || hexStr == "a08601")
            {
                Console.WriteLine();
            }
            string biStr = bi.ToString();
            if (biStr.StartsWith("-"))
            {
                var r1 = BigInteger.Divide(bi, BigInteger.Pow(10, decimals));
                var r2 = BigInteger.DivRem(bi, BigInteger.Pow(10, decimals), out BigInteger re);
                return "-" + changeDecimals(BigInteger.Multiply(-1, bi), decimals);
            }

            return changeDecimals(bi, decimals);
            /*
            //小头换大头
            byte[] bytes = ThinNeo.Helper.HexString2Bytes(hexStr).Reverse().ToArray();
            string hex = ThinNeo.Helper.Bytes2HexString(bytes);
            //大整数处理，默认第一位为符号位，0代表正数，需要补位
            hex = "0" + hex;

            BigInteger bi = BigInteger.Parse(hex, System.Globalization.NumberStyles.AllowHexSpecifier);

            return changeDecimals(bi, decimals);
            */
        }
        //根据精度处理小数点（大整数模式处理）
        private static string changeDecimals(BigInteger value, int decimals)
        {
            BigInteger bi = BigInteger.DivRem(value, BigInteger.Pow(10, decimals), out BigInteger remainder);
            string numStr = bi.ToString();
            if (remainder != 0)//如果余数不为零才添加小数点
            {
                //按照精度，处理小数部分左侧补零与右侧去零
                int AddLeftZeoCount = decimals - remainder.ToString().Length;
                string remainderStr = cloneStr("0", AddLeftZeoCount) + removeRightZero(remainder);

                numStr = string.Format("{0}.{1}", bi, remainderStr);
            }

            return numStr;
        }

        //生成左侧补零字符串
        private static string cloneStr(string str, int cloneCount)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 1; i <= cloneCount; i++)
            {
                sb.Append(str);
            }
            return sb.ToString();
        }

        //去除大整数小数（余数）部分的右侧0
        private static BigInteger removeRightZero(BigInteger bi)
        {
            string strReverse0 = strReverse(bi.ToString());
            BigInteger bi0 = BigInteger.Parse(strReverse0);
            string strReverse1 = strReverse(bi0.ToString());
            var r = BigInteger.Parse(strReverse1);
            return BigInteger.Parse(strReverse1);
        }

        //反转字符串
        private static string strReverse(string str)
        {
            char[] arr = str.ToCharArray();
            Array.Reverse(arr);

            return new string(arr);
        }
    }
    class nns_common
    {
        public static string api = "http://127.0.0.1:20332";

        public static Hash256 nameHash(string domain)
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes(domain);
            SHA256 sha256 = SHA256.Create();
            return new Hash256(sha256.ComputeHash(data));
        }
        public static Hash256 nameHashSub(byte[] roothash, string subdomain)
        {
            var bs = System.Text.Encoding.UTF8.GetBytes(subdomain);
            if (bs.Length == 0)
                return roothash;
            SHA256 sha256 = SHA256.Create();
            var domain = sha256.ComputeHash(bs).Concat(roothash).ToArray();
            return new Hash256(sha256.ComputeHash(domain));
        }
        public static string nameHashFull(string domain, string root)
        {
            root = root.StartsWith(".") ? root.Substring(1) : root;
            var roothash = nameHash(root);
            var fullhash = nameHashSub(roothash, domain);
            return fullhash.ToString();
        }

        #region apitool
        public class ResultItem
        {
            public byte[] data;
            public ResultItem[] subItem;
            public static ResultItem FromJson(string type, MyJson.IJsonNode value)
            {
                ResultItem item = new ResultItem();
                if (type == "Array")
                {
                    item.subItem = new ResultItem[value.AsList().Count];
                    for (var i = 0; i < item.subItem.Length; i++)
                    {
                        var subjson = value.AsList()[i].AsDict();
                        var subtype = subjson["type"].AsString();
                        item.subItem[i] = FromJson(subtype, subjson["value"]);
                    }
                }
                else if (type == "ByteArray")
                {
                    item.data = ThinNeo.Helper.HexString2Bytes(value.AsString());
                }
                else if (type == "Integer")
                {
                    item.data = System.Numerics.BigInteger.Parse(value.AsString()).ToByteArray();
                }
                else if (type == "Boolean")
                {
                    if (value.AsBool())
                        item.data = new byte[1] { 0x01 };
                    else
                        item.data = new byte[0];
                }
                else if (type == "String")
                {
                    item.data = System.Text.Encoding.UTF8.GetBytes(value.AsString());
                }
                else
                {
                    throw new Exception("not support type:" + type);
                }
                return item;
            }
            public string AsHexString()
            {
                return ThinNeo.Helper.Bytes2HexString(data);
            }
            public string AsHashString()
            {
                return "0x" + ThinNeo.Helper.Bytes2HexString(data.Reverse().ToArray());
            }
            public string AsString()
            {
                return System.Text.Encoding.UTF8.GetString(data);
            }
            public Hash160 AsHash160()
            {
                if (data.Length == 0)
                    return null;
                return new Hash160(data);
            }
            public Hash256 AsHash256()
            {
                if (data.Length == 0)
                    return null;
                return new Hash256(data);
            }
            public bool AsBoolean()
            {
                if (data.Length == 0 || data[0] == 0)
                    return false;
                return true;
            }
            public System.Numerics.BigInteger AsInteger()
            {
                return new System.Numerics.BigInteger(data);
            }
        }

        public class Result
        {
            public string textInfo;
            public ResultItem value; //不管什么类型统一转byte[]
        }

        public static async Task<Result> api_InvokeScript(Hash160 scripthash, string methodname, params string[] subparam)
        {
            byte[] data = null;
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                MyJson.JsonNode_Array array = new MyJson.JsonNode_Array();
                for (var i = 0; i < subparam.Length; i++)
                {
                    array.AddArrayValue(subparam[i]);
                }
                sb.EmitParamJson(array);
                sb.EmitPushString(methodname);
                sb.EmitAppCall(scripthash);
                data = sb.ToArray();
            }
            string script = ThinNeo.Helper.Bytes2HexString(data);

            byte[] postdata;
            var url = Helper.MakeRpcUrlPost(api, "invokescript", out postdata, new MyJson.JsonNode_ValueString(script));
            var text = await Helper.HttpPost(url, postdata);
            MyJson.JsonNode_Object json = MyJson.Parse(text) as MyJson.JsonNode_Object;

            Result rest = new Result();
            rest.textInfo = text;
            if (json.ContainsKey("result"))
            {
                var result = json["result"].AsList()[0].AsDict()["stack"].AsList();
                rest.value = ResultItem.FromJson("Array", result);
            }
            return rest;// subPrintLine("得到的结果是：" + result);
        }

        public static async Task<string> api_SendTransaction(byte[] prikey, Hash160 schash, string methodname, params string[] subparam)
        {
            byte[] data = null;
            //MakeTran
            ThinNeo.Transaction tran = null;
            {

                using (ScriptBuilder sb = new ScriptBuilder())
                {
                    MyJson.JsonNode_Array array = new MyJson.JsonNode_Array();
                    for (var i = 0; i < subparam.Length; i++)
                    {
                        array.AddArrayValue(subparam[i]);
                    }
                    sb.EmitParamJson(array);
                    sb.EmitPushString(methodname);
                    sb.EmitAppCall(schash);
                    data = sb.ToArray();
                    Console.WriteLine(ThinNeo.Helper.Bytes2HexString(data));
                }
            }

            return await nns_common.api_SendTransaction(prikey, data);
        }


        /// <summary>
        /// 重载交易构造方法，对于复杂交易传入脚本
        /// </summary>
        /// <param name="prikey">私钥</param>
        /// <param name="script">交易脚本</param>
        /// <returns></returns>
        public static async Task<string> api_SendTransaction(byte[] prikey, byte[] script)
        {
            byte[] pubkey = ThinNeo.Helper.GetPublicKeyFromPrivateKey(prikey);
            string address = ThinNeo.Helper.GetAddressFromPublicKey(pubkey);

            //获取地址的资产列表
            Dictionary<string, List<Utxo>> dir = await Helper.GetBalanceByAddress("", address);
            //Dictionary<string, List<Utxo>> dir = await Helper.GetBalanceByAddress(nnc_1.api, address);
            if (dir.ContainsKey("") == false)
            //if (dir.ContainsKey(Nep55_1.id_GAS) == false)
            {
                Console.WriteLine("no gas");
                return null;
            }
            //MakeTran
            ThinNeo.Transaction tran = null;
            {

                byte[] data = script;
                tran = Helper.makeTran(dir[""], null, new ThinNeo.Hash256(""), 0);
                //tran = Helper.makeTran(dir[Nep55_1.id_GAS], null, new ThinNeo.Hash256(Nep55_1.id_GAS), 0);
                tran.type = ThinNeo.TransactionType.InvocationTransaction;
                var idata = new ThinNeo.InvokeTransData();
                tran.extdata = idata;
                idata.script = data;
                idata.gas = 0;
            }

            //sign and broadcast
            var signdata = ThinNeo.Helper.Sign(tran.GetMessage(), prikey);
            tran.AddWitness(signdata, pubkey, address);
            var trandata = tran.GetRawData();
            var strtrandata = ThinNeo.Helper.Bytes2HexString(trandata);
            byte[] postdata;
            var url = Helper.MakeRpcUrlPost(nns_common.api, "sendrawtransaction", out postdata, new MyJson.JsonNode_ValueString(strtrandata));
            var result = await Helper.HttpPost(url, postdata);
            return result;
        }


        #endregion

    }
    public static class Helper
    {
        public static int BitLen(int w)
        {
            return (w < 1 << 15 ? (w < 1 << 7
                ? (w < 1 << 3 ? (w < 1 << 1
                ? (w < 1 << 0 ? (w < 0 ? 32 : 0) : 1)
                : (w < 1 << 2 ? 2 : 3)) : (w < 1 << 5
                ? (w < 1 << 4 ? 4 : 5)
                : (w < 1 << 6 ? 6 : 7)))
                : (w < 1 << 11
                ? (w < 1 << 9 ? (w < 1 << 8 ? 8 : 9) : (w < 1 << 10 ? 10 : 11))
                : (w < 1 << 13 ? (w < 1 << 12 ? 12 : 13) : (w < 1 << 14 ? 14 : 15)))) : (w < 1 << 23 ? (w < 1 << 19
                ? (w < 1 << 17 ? (w < 1 << 16 ? 16 : 17) : (w < 1 << 18 ? 18 : 19))
                : (w < 1 << 21 ? (w < 1 << 20 ? 20 : 21) : (w < 1 << 22 ? 22 : 23))) : (w < 1 << 27
                ? (w < 1 << 25 ? (w < 1 << 24 ? 24 : 25) : (w < 1 << 26 ? 26 : 27))
                : (w < 1 << 29 ? (w < 1 << 28 ? 28 : 29) : (w < 1 << 30 ? 30 : 31)))));
        }
        public static int GetBitLength(this BigInteger i)
        {
            byte[] b = i.ToByteArray();
            return (b.Length - 1) * 8 + BitLen(i.Sign > 0 ? b[b.Length - 1] : 255 - b[b.Length - 1]);
        }

        public static int GetLowestSetBit(this BigInteger i)
        {
            if (i.Sign == 0)
                return -1;
            byte[] b = i.ToByteArray();
            int w = 0;
            while (b[w] == 0)
                w++;
            for (int x = 0; x < 8; x++)
                if ((b[w] & 1 << x) > 0)
                    return x + w * 8;
            throw new Exception();
        }

        public static BigInteger Mod(this BigInteger x, BigInteger y)
        {
            x %= y;
            if (x.Sign < 0)
                x += y;
            return x;
        }

        public static bool TestBit(this BigInteger i, int index)
        {
            return (i & (BigInteger.One << index)) > BigInteger.Zero;
        }
        internal static BigInteger ModInverse(this BigInteger a, BigInteger n)
        {
            BigInteger i = n, v = 0, d = 1;
            while (a > 0)
            {
                BigInteger t = i / a, x = a;
                a = i % x;
                i = x;
                x = d;
                d = v - t * x;
                v = x;
            }
            v %= n;
            if (v < 0) v = (v + n) % n;
            return v;
        }

        internal static BigInteger NextBigInteger(this Random rand, int sizeInBits)
        {
            if (sizeInBits < 0)
                throw new ArgumentException("sizeInBits must be non-negative");
            if (sizeInBits == 0)
                return 0;
            byte[] b = new byte[sizeInBits / 8 + 1];
            rand.NextBytes(b);
            if (sizeInBits % 8 == 0)
                b[b.Length - 1] = 0;
            else
                b[b.Length - 1] &= (byte)((1 << sizeInBits % 8) - 1);
            return new BigInteger(b);
        }

        internal static BigInteger NextBigInteger(this System.Security.Cryptography.RandomNumberGenerator rng, int sizeInBits)
        {
            if (sizeInBits < 0)
                throw new ArgumentException("sizeInBits must be non-negative");
            if (sizeInBits == 0)
                return 0;
            byte[] b = new byte[sizeInBits / 8 + 1];
            rng.GetBytes(b);
            if (sizeInBits % 8 == 0)
                b[b.Length - 1] = 0;
            else
                b[b.Length - 1] &= (byte)((1 << sizeInBits % 8) - 1);
            return new BigInteger(b);
        }

        public static string Bytes2HexString(byte[] data)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var d in data)
            {
                sb.Append(d.ToString("x02"));
            }
            return sb.ToString();
        }
        public static byte[] HexString2Bytes(string str)
        {
            if (str.IndexOf("0x") == 0)
                str = str.Substring(2);
            byte[] outd = new byte[str.Length / 2];
            for (var i = 0; i < str.Length / 2; i++)
            {
                outd[i] = byte.Parse(str.Substring(i * 2, 2), System.Globalization.NumberStyles.HexNumber);
            }
            return outd;
        }
        //static SHA256 sha256 = SHA256.Create();
        //static RIPEMD160Managed ripemd160 = new RIPEMD160Managed();
        //static System.Security.Cryptography.RIPEMD160 ripemd160 = System.Security.Cryptography.RIPEMD160.Create();

        public static string GetWifFromPrivateKey(byte[] prikey)
        {
            if (prikey.Length != 32)
                throw new Exception("error prikey.");
            byte[] data = new byte[34];
            data[0] = 0x80;
            data[33] = 0x01;
            for (var i = 0; i < 32; i++)
            {
                data[i + 1] = prikey[i];
            }
            SHA256 sha256 = SHA256.Create();
            byte[] checksum = sha256.ComputeHash(data);
            checksum = sha256.ComputeHash(checksum);
            checksum = checksum.Take(4).ToArray();
            byte[] alldata = data.Concat(checksum).ToArray();
            string wif = Base58.Encode(alldata);
            return wif;
        }
        public static byte[] GetPrivateKeyFromWIF(string wif)
        {
            if (wif == null) throw new ArgumentNullException();
            byte[] data = Base58.Decode(wif);
            //检查标志位
            if (data.Length != 38 || data[0] != 0x80 || data[33] != 0x01)
                throw new Exception("wif length or tag is error");
            //取出检验字节
            var sum = data.Skip(data.Length - 4);
            byte[] realdata = data.Take(data.Length - 4).ToArray();

            //验证,对前34字节进行进行两次hash取前4个字节
            SHA256 sha256 = SHA256.Create();
            byte[] checksum = sha256.ComputeHash(realdata);
            checksum = sha256.ComputeHash(checksum);
            var sumcalc = checksum.Take(4);
            if (sum.SequenceEqual(sumcalc) == false)
                throw new Exception("the sum is not match.");

            byte[] privateKey = new byte[32];
            Buffer.BlockCopy(data, 1, privateKey, 0, privateKey.Length);
            Array.Clear(data, 0, data.Length);
            return privateKey;
        }

        public static byte[] GetPublicKeyFromPrivateKey(byte[] privateKey)
        {
            var PublicKey = ThinNeo.Cryptography.ECC.ECCurve.Secp256r1.G * privateKey;
            return PublicKey.EncodePoint(true);
        }
        public static byte[] GetPublicKeyFromPrivateKey_NoComp(byte[] privateKey)
        {
            var PublicKey = ThinNeo.Cryptography.ECC.ECCurve.Secp256r1.G * privateKey;
            return PublicKey.EncodePoint(false);//.Skip(1).ToArray();
        }
        public static byte[] GetScriptFromPublicKey(byte[] publicKey)
        {
            byte[] script = new byte[publicKey.Length + 2];
            script[0] = (byte)publicKey.Length;
            Array.Copy(publicKey, 0, script, 1, publicKey.Length);
            script[script.Length - 1] = 172;//CHECKSIG
            return script;
        }
        public static Hash160 GetScriptHashFromScript(byte[] script)
        {
            SHA256 sha256 = SHA256.Create();
            var scripthash = sha256.ComputeHash(script);
            RIPEMD160Managed ripemd160 = new RIPEMD160Managed();
            scripthash = ripemd160.ComputeHash(scripthash);
            return scripthash;
        }
        public static Hash160 GetScriptHashFromPublicKey(byte[] publicKey)
        {
            SHA256 sha256 = SHA256.Create();
            byte[] script = GetScriptFromPublicKey(publicKey);
            var scripthash = sha256.ComputeHash(script);
            RIPEMD160Managed ripemd160 = new RIPEMD160Managed();
            scripthash = ripemd160.ComputeHash(scripthash);
            return scripthash;
        }
        public static string GetAddressFromScriptHash(Hash160 scripthash)
        {
            byte[] data = new byte[20 + 1];
            data[0] = 0x17;
            Array.Copy(scripthash, 0, data, 1, 20);
            SHA256 sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(data);
            hash = sha256.ComputeHash(hash);

            var alldata = data.Concat(hash.Take(4)).ToArray();

            return Base58.Encode(alldata);
        }
        public static string GetAddressFromPublicKey(byte[] publickey)
        {
            byte[] scriptHash = GetScriptHashFromPublicKey(publickey);
            return GetAddressFromScriptHash(scriptHash);
        }
        //public static byte[] GetPublicKeyHash(byte[] publickey)
        //{
        //    var hash1 = sha256.ComputeHash(publickey);
        //    var hash2 = ripemd160.ComputeHash(hash1);
        //    return hash2;
        //}
        public static Hash160 GetPublicKeyHashFromAddress(string address)
        {
            var alldata = Base58.Decode(address);
            if (alldata.Length != 25)
                throw new Exception("error length.");
            var data = alldata.Take(alldata.Length - 4).ToArray();
            if (data[0] != 0x17)
                throw new Exception("not a address");
            SHA256 sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(data);
            hash = sha256.ComputeHash(hash);
            var hashbts = hash.Take(4).ToArray();
            var datahashbts = alldata.Skip(alldata.Length - 4).ToArray();
            if (hashbts.SequenceEqual(datahashbts) == false)
                throw new Exception("not match hash");
            var pkhash = data.Skip(1).ToArray();
            return new Hash160(pkhash);
        }
        public static Hash160 GetPublicKeyHashFromAddress_WithoutCheck(string address)
        {
            var alldata = Base58.Decode(address);
            if (alldata.Length != 25)
                throw new Exception("error length.");
            if (alldata[0] != 0x17)
                throw new Exception("not a address");
            var data = alldata.Take(alldata.Length - 4).ToArray();
            var pkhash = data.Skip(1).ToArray();
            return new Hash160(pkhash);
        }




        public static byte[] Sign(byte[] message, byte[] prikey)
        {
            var Secp256r1_G = Helper.HexString2Bytes("04" + "6B17D1F2E12C4247F8BCE6E563A440F277037D812DEB33A0F4A13945D898C296" + "4FE342E2FE1A7F9B8EE7EB4A7C0F9E162BCE33576B315ECECBB6406837BF51F5");

            var PublicKey = ThinNeo.Cryptography.ECC.ECCurve.Secp256r1.G * prikey;
            var pubkey = PublicKey.EncodePoint(false).Skip(1).ToArray();

            var ecdsa = new ThinNeo.Cryptography.ECC.ECDsa(prikey, ThinNeo.Cryptography.ECC.ECCurve.Secp256r1);
            SHA256 sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(message);
            var result = ecdsa.GenerateSignature(hash);
            var data1 = result[0].ToByteArray();
            if (data1.Length > 32)
                data1 = data1.Take(32).ToArray();
            var data2 = result[1].ToByteArray();
            if (data2.Length > 32)
                data2 = data2.Take(32).ToArray();

            data1 = data1.Reverse().ToArray();
            data2 = data2.Reverse().ToArray();

            byte[] newdata = new byte[64];
            Array.Copy(data1, 0, newdata, 32 - data1.Length, data1.Length);
            Array.Copy(data2, 0, newdata, 64 - data2.Length, data2.Length);

            return newdata;// data1.Concat(data2).ToArray();
            //#if NET461
            //const int ECDSA_PRIVATE_P256_MAGIC = 0x32534345;
            //byte[] first = { 0x45, 0x43, 0x53, 0x32, 0x20, 0x00, 0x00, 0x00 };
            //prikey = first.Concat(pubkey).Concat(prikey).ToArray();
            //using (System.Security.Cryptography.CngKey key = System.Security.Cryptography.CngKey.Import(prikey, System.Security.Cryptography.CngKeyBlobFormat.EccPrivateBlob))
            //using (System.Security.Cryptography.ECDsaCng ecdsa = new System.Security.Cryptography.ECDsaCng(key))

            //using (var ecdsa = System.Security.Cryptography.ECDsa.Create(new System.Security.Cryptography.ECParameters
            //{
            //    Curve = System.Security.Cryptography.ECCurve.NamedCurves.nistP256,
            //    D = prikey,
            //    Q = new System.Security.Cryptography.ECPoint
            //    {
            //        X = pubkey.Take(32).ToArray(),
            //        Y = pubkey.Skip(32).ToArray()
            //    }
            //}))
            //{
            //    var hash = sha256.ComputeHash(message);
            //    return ecdsa.SignHash(hash);
            //}
        }

        public static bool VerifySignature(byte[] message, byte[] signature, byte[] pubkey)
        {
            //unity dotnet不完整，不能用dotnet自带的ecdsa
            var PublicKey = ThinNeo.Cryptography.ECC.ECPoint.DecodePoint(pubkey, ThinNeo.Cryptography.ECC.ECCurve.Secp256r1);
            var ecdsa = new ThinNeo.Cryptography.ECC.ECDsa(PublicKey);
            var b1 = signature.Take(32).Reverse().Concat(new byte[] { 0x00 }).ToArray();
            var b2 = signature.Skip(32).Reverse().Concat(new byte[] { 0x00 }).ToArray();
            var num1 = new BigInteger(b1);
            var num2 = new BigInteger(b2);
            SHA256 sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(message);
            return ecdsa.VerifySignature(hash, num1, num2);
            //var PublicKey = ThinNeo.Cryptography.ECC.ECPoint.DecodePoint(pubkey, ThinNeo.Cryptography.ECC.ECCurve.Secp256r1);
            //var usepk = PublicKey.EncodePoint(false).Skip(1).ToArray();

            ////byte[] first = { 0x45, 0x43, 0x53, 0x31, 0x20, 0x00, 0x00, 0x00 };
            ////usepk = first.Concat(usepk).ToArray();

            ////using (System.Security.Cryptography.CngKey key = System.Security.Cryptography.CngKey.Import(usepk, System.Security.Cryptography.CngKeyBlobFormat.EccPublicBlob))
            ////using (System.Security.Cryptography.ECDsaCng ecdsa = new System.Security.Cryptography.ECDsaCng(key))

            //using (var ecdsa = System.Security.Cryptography.ECDsa.Create(new System.Security.Cryptography.ECParameters
            //{
            //    Curve = System.Security.Cryptography.ECCurve.NamedCurves.nistP256,
            //    Q = new System.Security.Cryptography.ECPoint
            //    {
            //        X = usepk.Take(32).ToArray(),
            //        Y = usepk.Skip(32).ToArray()
            //    }
            //}))
            //{
            //    var hash = sha256.ComputeHash(message);
            //    return ecdsa.VerifyHash(hash, signature);
            //}
        }


        public static string GetNep2FromPrivateKey(byte[] prikey, string passphrase)
        {
            var pubkey = Helper.GetPublicKeyFromPrivateKey(prikey);
            var script_hash = Helper.GetScriptHashFromPublicKey(pubkey);

            string address = Helper.GetAddressFromScriptHash(script_hash);

            var b1 = Sha256(Encoding.ASCII.GetBytes(address));
            var b2 = Sha256(b1);
            byte[] addresshash = b2.Take(4).ToArray();
            byte[] derivedkey = SCrypt.DeriveKey(Encoding.UTF8.GetBytes(passphrase), addresshash, 16384, 8, 8, 64);
            byte[] derivedhalf1 = derivedkey.Take(32).ToArray();
            byte[] derivedhalf2 = derivedkey.Skip(32).ToArray();
            var xorinfo = XOR(prikey, derivedhalf1);
            byte[] encryptedkey = AES256Encrypt(xorinfo, derivedhalf2);
            byte[] buffer = new byte[39];
            buffer[0] = 0x01;
            buffer[1] = 0x42;
            buffer[2] = 0xe0;
            Buffer.BlockCopy(addresshash, 0, buffer, 3, addresshash.Length);
            Buffer.BlockCopy(encryptedkey, 0, buffer, 7, encryptedkey.Length);
            return Base58CheckEncode(buffer);
        }
        public static byte[] GetPrivateKeyFromNEP2(string nep2, string passphrase, int N = 16384, int r = 8, int p = 8)
        {
            if (nep2 == null) throw new ArgumentNullException(nameof(nep2));
            if (passphrase == null) throw new ArgumentNullException(nameof(passphrase));
            byte[] data = Base58CheckDecode(nep2);
            if (data.Length != 39 || data[0] != 0x01 || data[1] != 0x42 || data[2] != 0xe0)
                throw new FormatException();
            byte[] addresshash = new byte[4];
            Buffer.BlockCopy(data, 3, addresshash, 0, 4);
            byte[] derivedkey = SCrypt.DeriveKey(Encoding.UTF8.GetBytes(passphrase), addresshash, N, r, p, 64);
            byte[] derivedhalf1 = derivedkey.Take(32).ToArray();
            byte[] derivedhalf2 = derivedkey.Skip(32).ToArray();
            byte[] encryptedkey = new byte[32];
            Buffer.BlockCopy(data, 7, encryptedkey, 0, 32);
            byte[] prikey = XOR(AES256Decrypt(encryptedkey, derivedhalf2), derivedhalf1);
            var pubkey = GetPublicKeyFromPrivateKey(prikey);
            var address = GetAddressFromPublicKey(pubkey);
            var hash = Sha256(Encoding.ASCII.GetBytes(address));
            hash = Sha256(hash);
            for (var i = 0; i < 4; i++)
            {
                if (hash[i] != addresshash[i])
                    throw new Exception("check error.");
            }
            //Cryptography.ECC.ECPoint pubkey = Cryptography.ECC.ECCurve.Secp256r1.G * prikey;
            //UInt160 script_hash = Contract.CreateSignatureRedeemScript(pubkey).ToScriptHash();
            //string address = ToAddress(script_hash);
            //if (!Encoding.ASCII.GetBytes(address).Sha256().Sha256().Take(4).SequenceEqual(addresshash))
            //    throw new FormatException();
            return prikey;


        }
        public static byte[] Sha256(byte[] data, int start = 0, int length = -1)
        {
            byte[] tdata = null;

            if (start == 0 && length == -1)
            {
                tdata = data;
            }
            else
            {
                tdata = new byte[length];
                Array.Copy(data, 0, tdata, 0, length);
            }
            SHA256 sha256 = SHA256.Create();
            return sha256.ComputeHash(tdata);

        }
        public static byte[] Base58CheckDecode(string input)
        {
            byte[] buffer = ThinNeo.Cryptography.Cryptography.Base58.Decode(input);
            if (buffer.Length < 4) throw new FormatException();

            var b1 = Sha256(buffer, 0, buffer.Length - 4);

            byte[] checksum = Sha256(b1);

            if (!buffer.Skip(buffer.Length - 4).SequenceEqual(checksum.Take(4)))
                throw new FormatException();
            return buffer.Take(buffer.Length - 4).ToArray();
        }
        public static string Base58CheckEncode(byte[] data)
        {
            var b1 = Sha256(data);
            byte[] checksum = Sha256(b1);
            byte[] buffer = new byte[data.Length + 4];
            Buffer.BlockCopy(data, 0, buffer, 0, data.Length);
            Buffer.BlockCopy(checksum, 0, buffer, data.Length, 4);
            return ThinNeo.Cryptography.Cryptography.Base58.Encode(buffer);
        }
        static byte[] XOR(byte[] x, byte[] y)
        {
            if (x.Length != y.Length) throw new ArgumentException();
            return x.Zip(y, (a, b) => (byte)(a ^ b)).ToArray();
        }
        internal static byte[] AES256Encrypt(byte[] block, byte[] key)
        {
            using (System.Security.Cryptography.Aes aes = System.Security.Cryptography.Aes.Create())
            {
                aes.Key = key;
                aes.Mode = System.Security.Cryptography.CipherMode.ECB;
                aes.Padding = System.Security.Cryptography.PaddingMode.None;
                using (System.Security.Cryptography.ICryptoTransform encryptor = aes.CreateEncryptor())
                {
                    return encryptor.TransformFinalBlock(block, 0, block.Length);
                }
            }
        }
        internal static byte[] AES256Decrypt(byte[] block, byte[] key)
        {
            using (System.Security.Cryptography.Aes aes = System.Security.Cryptography.Aes.Create())
            {
                aes.Key = key;
                aes.Mode = System.Security.Cryptography.CipherMode.ECB;
                aes.Padding = System.Security.Cryptography.PaddingMode.None;
                using (System.Security.Cryptography.ICryptoTransform decryptor = aes.CreateDecryptor())
                {
                    return decryptor.TransformFinalBlock(block, 0, block.Length);
                }
            }
        }

        public static byte[] nameHash(string domain)
        {
            return Sha256(System.Text.Encoding.UTF8.GetBytes(domain));
        }

        public static byte[] nameHashSub(byte[] roothash, string subdomain)
        {
            var bs = System.Text.Encoding.UTF8.GetBytes(subdomain);
            if (bs.Length == 0)
                return roothash;

            byte[] domain = Sha256(bs).Concat(roothash).ToArray();
            return Sha256(domain);
        }

        // ********************************************************************************************************
        // ********************************************************************************************************
        // ********************************************************************************************************
        public static string MakeRpcUrlPost(string url, string method, out byte[] data, params MyJson.IJsonNode[] _params)
        {
            //if (url.Last() != '/')
            //    url = url + "/";
            var json = new MyJson.JsonNode_Object();
            json["id"] = new MyJson.JsonNode_ValueNumber(1);
            json["jsonrpc"] = new MyJson.JsonNode_ValueString("2.0");
            json["method"] = new MyJson.JsonNode_ValueString(method);
            StringBuilder sb = new StringBuilder();
            var array = new MyJson.JsonNode_Array();
            for (var i = 0; i < _params.Length; i++)
            {

                array.Add(_params[i]);
            }
            json["params"] = array;
            data = System.Text.Encoding.UTF8.GetBytes(json.ToString());
            return url;
        }
        public static string MakeRpcUrl(string url, string method, params MyJson.IJsonNode[] _params)
        {
            StringBuilder sb = new StringBuilder();
            if (url.Last() != '/')
                url = url + "/";

            sb.Append(url + "?jsonrpc=2.0&id=1&method=" + method + "&params=[");
            for (var i = 0; i < _params.Length; i++)
            {
                _params[i].ConvertToString(sb);
                if (i != _params.Length - 1)
                    sb.Append(",");
            }
            sb.Append("]");
            return sb.ToString();
        }

        //获取地址的utxo来得出地址的资产  
        public static async Task<Dictionary<string, List<Utxo>>> GetBalanceByAddress(string api, string _addr)
        {
            MyJson.JsonNode_Object response = (MyJson.JsonNode_Object)MyJson.Parse(await Helper.HttpGet(api + "?method=getutxo&id=1&params=['" + _addr + "']"));
            MyJson.JsonNode_Array resJA = (MyJson.JsonNode_Array)response["result"];
            Dictionary<string, List<Utxo>> _dir = new Dictionary<string, List<Utxo>>();
            foreach (MyJson.JsonNode_Object j in resJA)
            {
                Utxo utxo = new Utxo(j["addr"].ToString(), new ThinNeo.Hash256(j["txid"].ToString()), j["asset"].ToString(), decimal.Parse(j["value"].ToString()), int.Parse(j["n"].ToString()));
                if (_dir.ContainsKey(j["asset"].ToString()))
                {
                    _dir[j["asset"].ToString()].Add(utxo);
                }
                else
                {
                    List<Utxo> l = new List<Utxo>();
                    l.Add(utxo);
                    _dir[j["asset"].ToString()] = l;
                }

            }
            return _dir;
        }
        public static ThinNeo.Transaction makeTran(List<Utxo> utxos, string targetaddr, ThinNeo.Hash256 assetid, decimal sendcount)
        {
            var tran = new ThinNeo.Transaction();
            tran.type = ThinNeo.TransactionType.ContractTransaction;
            tran.version = 0;//0 or 1
            tran.extdata = null;

            tran.attributes = new ThinNeo.Attribute[0];
            var scraddr = "";
            utxos.Sort((a, b) =>
            {
                if (a.value > b.value)
                    return 1;
                else if (a.value < b.value)
                    return -1;
                else
                    return 0;
            });
            decimal count = decimal.Zero;
            List<ThinNeo.TransactionInput> list_inputs = new List<ThinNeo.TransactionInput>();
            for (var i = 0; i < utxos.Count; i++)
            {
                ThinNeo.TransactionInput input = new ThinNeo.TransactionInput();
                input.hash = utxos[i].txid;
                input.index = (ushort)utxos[i].n;
                list_inputs.Add(input);
                count += utxos[i].value;
                scraddr = utxos[i].addr;
                if (count >= sendcount)
                {
                    break;
                }
            }
            tran.inputs = list_inputs.ToArray();
            if (count >= sendcount)//输入大于等于输出
            {
                List<ThinNeo.TransactionOutput> list_outputs = new List<ThinNeo.TransactionOutput>();
                //输出
                if (sendcount > decimal.Zero && targetaddr != null)
                {
                    ThinNeo.TransactionOutput output = new ThinNeo.TransactionOutput();
                    output.assetId = assetid;
                    output.value = sendcount;
                    output.toAddress = ThinNeo.Helper.GetPublicKeyHashFromAddress(targetaddr);
                    list_outputs.Add(output);
                }

                //找零
                var change = count - sendcount;
                if (change > decimal.Zero)
                {
                    ThinNeo.TransactionOutput outputchange = new ThinNeo.TransactionOutput();
                    outputchange.toAddress = ThinNeo.Helper.GetPublicKeyHashFromAddress(scraddr);
                    outputchange.value = change;
                    outputchange.assetId = assetid;
                    list_outputs.Add(outputchange);

                }
                tran.outputs = list_outputs.ToArray();
            }
            else
            {
                throw new Exception("no enough money.");
            }
            return tran;
        }

        /// <summary>
        /// 同步get请求
        /// </summary>
        /// <param name="url">链接地址</param>    
        /// <param name="formData">写在header中的键值对</param>
        /// <returns></returns>

        public static async Task<string> HttpGet(string url)
        {
            WebClient wc = new WebClient();
            return await wc.DownloadStringTaskAsync(url);
        }
        public static async Task<string> HttpPost(string url, byte[] data)
        {
            WebClient wc = new WebClient();
            wc.Headers["content-type"] = "text/plain;charset=UTF-8";
            byte[] retdata = await wc.UploadDataTaskAsync(url, "POST", data);
            return System.Text.Encoding.UTF8.GetString(retdata);
        }

        //流模式post
        public static string Post(string url, string data, Encoding encoding, int type = 3)
        {
            try
            {
                HttpWebRequest req = WebRequest.CreateHttp(new Uri(url));
                if (type == 1)
                {
                    req.ContentType = "application/json;charset=utf-8";
                }
                else if (type == 2)
                {
                    req.ContentType = "application/xml;charset=utf-8";
                }
                else
                {
                    req.ContentType = "application/x-www-form-urlencoded;charset=utf-8";
                }

                req.Method = "POST";
                //req.Accept = "text/xml,text/javascript";
                req.ContinueTimeout = 60000;

                byte[] postData = encoding.GetBytes(data);
                Stream reqStream = req.GetRequestStreamAsync().Result;
                reqStream.Write(postData, 0, postData.Length);
                reqStream.Dispose();

                var rsp = (HttpWebResponse)req.GetResponseAsync().Result;
                var result = GetResponseAsString(rsp, encoding);

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return string.Empty;

            }
        }
        private static string GetResponseAsString(HttpWebResponse rsp, Encoding encoding)
        {
            Stream stream = null;
            StreamReader reader = null;

            try
            {
                // 以字符流的方式读取HTTP响应
                stream = rsp.GetResponseStream();
                reader = new StreamReader(stream, encoding);
                return reader.ReadToEnd();
            }
            finally
            {
                // 释放资源
                if (reader != null) reader.Dispose();
                if (stream != null) stream.Dispose();
                if (rsp != null) rsp.Dispose();
            }
        }



    }
    public class Utxo
    {
        //txid[n] 是utxo的属性
        public ThinNeo.Hash256 txid;
        public int n;

        //asset资产、addr 属于谁，value数额，这都是查出来的
        public string addr;
        public string asset;
        public decimal value;
        public Utxo(string _addr, ThinNeo.Hash256 _txid, string _asset, decimal _value, int _n)
        {
            this.addr = _addr;
            this.txid = _txid;
            this.asset = _asset;
            this.value = _value;
            this.n = _n;
        }
    }
}
