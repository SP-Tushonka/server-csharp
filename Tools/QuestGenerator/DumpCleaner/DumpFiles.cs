namespace DumpCleaner
{
    public static class DumpFiles
    {
        public static List<DumpData> filenames = new List<DumpData>()
        {
            new DumpData
            {
                InputName = "resp.client.items",
                OutputName = "items",
                OutputFolder = "templates",
            },
            new DumpData
            {
                InputName = "resp.client.handbook.templates",
                OutputName = "handbook",
                OutputFolder = "templates",
            },
            new DumpData
            {
                InputName = "resp.client.customization",
                OutputName = "customization",
                OutputFolder = "templates",
            },
            new DumpData
            {
                InputName = "resp.client.account.customization",
                OutputName = "character",
                OutputFolder = "templates",
            },
            new DumpData
            {
                InputName = "resp.client.achievement.list",
                OutputName = "achievements",
                OutputFolder = "templates",
                SpecialCase = true,
            },
            new DumpData
            {
                InputName = "resp.client.locale.ru",
                OutputName = "ru",
                OutputFolder = "locales\\global",
            },
            new DumpData
            {
                InputName = "resp.client.locale.en",
                OutputName = "en",
                OutputFolder = "locales\\global",
            },
            new DumpData
            {
                InputName = "resp.client.locale.jp",
                OutputName = "jp",
                OutputFolder = "locales\\global",
            },
            new DumpData
            {
                InputName = "resp.client.locale.es-mx",
                OutputName = "es-mx",
                OutputFolder = "locales\\global",
            },
            new DumpData
            {
                InputName = "resp.client.locale.kr",
                OutputName = "kr",
                OutputFolder = "locales\\global",
            },
            new DumpData
            {
                InputName = "resp.client.locale.es",
                OutputName = "es",
                OutputFolder = "locales\\global",
            },
            new DumpData
            {
                InputName = "resp.client.locale.fr",
                OutputName = "fr",
                OutputFolder = "locales\\global",
            },
            new DumpData
            {
                InputName = "resp.client.locale.ge",
                OutputName = "ge",
                OutputFolder = "locales\\global",
            },
            new DumpData
            {
                InputName = "resp.client.locale.it",
                OutputName = "it",
                OutputFolder = "locales\\global",
            },
            new DumpData
            {
                InputName = "resp.client.locale.hu",
                OutputName = "hu",
                OutputFolder = "locales\\global",
            },
            new DumpData
            {
                InputName = "resp.client.locale.tu",
                OutputName = "tu",
                OutputFolder = "locales\\global",
            },
            new DumpData
            {
                InputName = "resp.client.locale.pl",
                OutputName = "pl",
                OutputFolder = "locales\\global",
            },
            new DumpData
            {
                InputName = "resp.client.locale.po",
                OutputName = "po",
                OutputFolder = "locales\\global",
            },
            new DumpData
            {
                InputName = "resp.client.locale.sk",
                OutputName = "sk",
                OutputFolder = "locales\\global",
            },
            new DumpData
            {
                InputName = "resp.client.locale.cz",
                OutputName = "cz",
                OutputFolder = "locales\\global",
            },
            new DumpData
            {
                InputName = "resp.client.locale.ch",
                OutputName = "ch",
                OutputFolder = "locales\\global",
            },
            new DumpData
            {
                InputName = "resp.client.locale.ro",
                OutputName = "ro",
                OutputFolder = "locales\\global",
            },
            new DumpData
            {
                InputName = "resp.client.match.local.start",
                OutputName = "locations",
                OutputFolder = "locations",
                SpecialCase = true,
            },
            new DumpData
            {
                InputName = "resp.client.menu.locale.en",
                OutputName = "en",
                OutputFolder = "locales\\menu",
            },
            new DumpData
            {
                InputName = "resp.client.languages",
                OutputName = "languages",
                OutputFolder = "locales",
            },
            new DumpData
            {
                InputName = "resp.client.globals",
                OutputName = "globals",
                OutputFolder = "",
            },
            new DumpData
            {
                InputName = "resp.client.settings",
                OutputName = "settings",
                OutputFolder = "",
            },
            new DumpData
            {
                InputName = "resp.client.hideout.production.recipes",
                OutputName = "production",
                OutputFolder = "hideout",
            },
            new DumpData
            {
                InputName = "resp.client.hideout.areas",
                OutputName = "areas",
                OutputFolder = "hideout",
            },
            new DumpData
            {
                InputName = "resp.client.hideout.production.scavcase.recipes",
                OutputName = "scavcase",
                OutputFolder = "hideout",
            },
            new DumpData
            {
                InputName = "resp.client.hideout.settings",
                OutputName = "settings",
                OutputFolder = "hideout",
            },
            new DumpData
            {
                InputName = "resp.client.hideout.qte.list",
                OutputName = "qte",
                OutputFolder = "hideout",
            },
            new DumpData
            {
                InputName = "resp.client.hideout.customization.offer.list",
                OutputName = "customisation",
                OutputFolder = "hideout",
            },
            new DumpData
            {
                InputName = "resp.client.locations",
                OutputName = "locations",
                OutputFolder = "locations",
                SpecialCase = true,
            },
            new DumpData
            {
                InputName = "resp.client.location.getLocalloot",
                OutputName = "locations",
                OutputFolder = "locations",
                SpecialCase = true,
            },
            new DumpData
            {
                InputName = "resp.client.trading.api.traderSettings",
                OutputName = "traders",
                OutputFolder = "traders",
                SpecialCase = true,
            },
            new DumpData
            {
                InputName = "resp.client.prestige.list",
                OutputName = "prestige",
                OutputFolder = "templates",
                SpecialCase = false,
            },
            new DumpData
            {
                InputName = "resp.client.customization.storage",
                OutputName = "customisationStorage",
                OutputFolder = "templates",
                SpecialCase = false,
            },
            new DumpData
            {
                InputName = "resp.client.trading.customization.5ac3b934156ae10c4430e83c.usec",
                OutputName = "usecsuits",
                OutputFolder = "traders/5ac3b934156ae10c4430e83c",
            },
            new DumpData
            {
                InputName = "resp.client.trading.customization.5ac3b934156ae10c4430e83c.bear",
                OutputName = "bearsuits",
                OutputFolder = "traders/5ac3b934156ae10c4430e83c",
            },
        };
    }

    public class DumpData
    {
        public string InputName { get; set; }
        public string OutputName { get; set; }
        public string OutputFolder { get; set; }
        public bool SpecialCase { get; internal set; }
    }

    public class Dump
    {
        public int err { get; set; }
        public string errMsg { get; set; }
        public object data { get; set; }
    }
}
