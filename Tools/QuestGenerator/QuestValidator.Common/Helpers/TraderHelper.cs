using System.Collections.Generic;
using QuestValidator.Models.Other;

namespace AssortGenerator.Common.Helpers
{
    public static class TraderHelper
    {
        private static Dictionary<Trader, string> _traders;

        public static Dictionary<Trader, string> GetTraders()
        {
            if (_traders is null)
            {
                _traders = new Dictionary<Trader, string>
                {
                    { Trader.Prapor, "54cb50c76803fa8b248b4571" },
                    { Trader.Therapist, "54cb57776803fa99248b456e" },
                    { Trader.Skier, "58330581ace78e27b8b10cee" },
                    { Trader.Peacekeeper, "5935c25fb3acc3127c3d8cd9" },
                    { Trader.Mechanic, "5a7c2eca46aef81a7ca2145d" },
                    { Trader.Ragman, "5ac3b934156ae10c4430e83c" },
                    { Trader.Jaeger, "5c0647fdd443bc2504c2d371" },
                };
            }

            return _traders;
        }

        public static Trader GetTraderTypeById(string traderId)
        {
            Trader returnType = 0;
            foreach (var item in GetTraders())
            {
                if (item.Value == traderId)
                {
                    returnType = item.Key;
                    break;
                }
            }

            return returnType;
        }
    }
}
