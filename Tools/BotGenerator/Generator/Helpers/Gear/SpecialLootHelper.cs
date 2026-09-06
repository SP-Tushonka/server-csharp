using System.Collections.Generic;
using System.Linq;
using Common.Models;
using SPTarkov.Server.Core.Models.Common;

namespace Generator.Helpers.Gear
{
    public static class SpecialLootHelper
    {
        private static readonly List<string> _genericBossKeys = new List<string>()
        {
            "5c1d0d6d86f7744bb2683e1f", // "TerraGroup Labs keycard (Yellow)",
            "5c1d0c5f86f7744bb2683cf0", // "TerraGroup Labs keycard (Blue)",
            "5c1e495a86f7743109743dfb", // "TerraGroup Labs keycard (Violet)",
            "5c1d0dc586f7744baf2e7b79", // "TerraGroup Labs keycard (Green)",
            "5c1d0efb86f7744baf2e7b7b", // "TerraGroup Labs keycard (Red)",
            "5c1d0f4986f7744bb01837fa", // "TerraGroup Labs keycard (Black)",
            "5c94bbff86f7747ee735c08f", // "TerraGroup Labs access keycard",
            "5e42c83786f7742a021fdf3c", // "Object #21WS keycard",
            "5e42c81886f7742a01529f57", // "Object #11SR keycard",
            "59136a4486f774447a1ed172", // "Dorm guard desk key",
            "5780cf7f2459777de4559322", // "Dorm room 314 marked key",
            "5d80c60f86f77440373c4ece", // "RB-BK marked key",
            "5d80c62a86f7744036212b3f", // "RB-VO marked key",
            "5ede7a8229445733cb4c18e2", // "RB-PKPM marked key",
            "5da743f586f7744014504f72", // "USEC Customs stash key",
            "5d8e15b686f774445103b190", // "HEP station storage room key",
            "5a13f24186f77410e57c5626", // "Health Resort east wing room 222 key",
            "5448ba0b4bdc2d02308b456c", // "Factory emergency exit key",
            "5a1452ee86f7746f33111763", // "Health Resort west wing room 222 key",
            "5a13f35286f77413ef1436b0", // "Health Resort east wing room 226 key",
            "5a0eec9686f77402ac5c39f2", // "Health Resort east wing room 310 key",
            "5a13ef7e86f7741290491063", // "Health Resort west wing room 301 key",
            "5a0ee30786f774023b6ee08f", // "Health Resort west wing room 216 key",
            "5a0ee76686f7743698200d5c", // "Health Resort east wing room 216 key",
            "5913877a86f774432f15d444", // "Gas station storage room key",
            "5780d0652459777df90dcb74", // "Gas station office key",
            "5d80c88d86f77440556dbf07", // "RB-AM key",
            "5ede7b0c6d23e5473e6e8c66", // "RB-RLSA key",
            "5d8e0e0e86f774321140eb56", // "RB-KPRL key",
            "5d80cb3886f77440556dbf09", // "RB-PSP1 key",
            "5d95d6fa86f77424484aa5e9", // "RB-PSP2 key",
            "5d80cb5686f77440545d1286", // "RB-PSV1 key",
            "5d80c6fc86f774403a401e3c", // "RB-TB key",
            "5d9f1fa686f774726974a992", // "RB-ST key",
            "5d947d3886f774447b415893", // "RB-SMP key",
            "5e42c71586f7747f245e1343", // "ULTRA medical storage key",
            "5ad5d7d286f77450166e0a89", // "KIBA Arms International outlet outer door key",
            "5addaffe86f77470b455f900", // "KIBA Arms International outlet inner grate door key",
            "5ad5d64486f774079b080af8", // "NecrusPharm pharmacy key",
            "591afe0186f77431bd616a11", // "ZB-014 key",
            "5c1e2d1f86f77431e9280bee", // "TerraGroup Labs Weapons testing area key",
            "5c1f79a086f7746ed066fb8f", // "TerraGroup Labs Arsenal storage room key",
            "5c1e2a1e86f77431ea0ea84c", // "TerraGroup Labs Manager office room key",
            "5a144bdb86f7741d374bbde0", // "Health Resort east wing room 205 key",
            "5a0ee4b586f7743698200d22", // "Health Resort east wing room 206 key",
            "5a145d4786f7744cbb6f4a12", // "Health Resort east wing room 306 key",
            "5a145d7b86f7744cbb6f4a13", // "Health Resort east wing room 308 key",
            "5a0eecf686f7740350630097", // "Health Resort east wing room 313 key",
            "5a0eee1486f77402aa773226", // "Health Resort east wing room 328 key"
        };

        public static IEnumerable<string> GetGenericBossKeys()
        {
            return _genericBossKeys;
        }

        public static Dictionary<MongoId, double> GetGenericBossKeysDictionary()
        {
            return _genericBossKeys.ToDictionary(x => (MongoId)x, y => 1d);
        }

        public static Dictionary<MongoId, double> GetSpecialLootForBotType(BotType botType)
        {
            var results = new Dictionary<MongoId, double>();
            switch (botType)
            {
                case BotType.assault:
                    break;
                case BotType.pmcbot:
                    break;
                case BotType.marksman:
                    break;
                case BotType.bossbully:
                    break;
                case BotType.bossgluhar:
                    break;
                case BotType.bosskilla:
                    break;
                case BotType.bosskojaniy:
                    results.Add("5d08d21286f774736e7c94c3", 1); // Shturman's stash key
                    results.Add("5c94bbff86f7747ee735c08f", 1); // labs keycard
                    break;
                case BotType.bosssanitar:
                    results.Add("5efde6b4f5448336730dbd61", 1); // Keycard with a blue marking
                    break;
                case BotType.bossstormtrooper:
                    break;
                case BotType.followerbully:
                    break;
                case BotType.followergluharassault:
                    break;
                case BotType.followergluharscout:
                    break;
                case BotType.followergluharsecurity:
                    break;
                case BotType.followergluharsnipe:
                    break;
                case BotType.followerkojaniy:
                    break;
                case BotType.followersanitar:
                    break;
                case BotType.followerstormtrooper:
                    break;
                case BotType.cursedassault:
                    break;
                case BotType.sectantpriest:
                    break;
                case BotType.sectantwarrior:
                    break;
                case BotType.usec:
                    break;
                case BotType.bossboar:
                    break;
                case BotType.bosstagilla:
                    break;
                default:
                    break;
            }

            return results;
        }
    }
}
