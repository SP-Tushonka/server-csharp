using System;
using System.Collections.Generic;
using System.IO;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Utils;

namespace QuestValidator.Common.Helpers
{
    public static class ItemTemplateHelper
    {
        private static Dictionary<MongoId, TemplateItem> _itemCache;

        public static Dictionary<MongoId, TemplateItem> Items
        {
            get
            {
                if (_itemCache is null)
                {
                    var itemsJson = File.ReadAllText(System.IO.Path.Combine(ServerAssets.Database, "templates", "items.json"));
                    _itemCache = DI.GetInstance().GetService<JsonUtil>().Deserialize<Dictionary<MongoId, TemplateItem>>(itemsJson);
                }

                return _itemCache;
            }
        }

        public static TemplateItem GetTemplateById(string templateId)
        {
            if (Items.TryGetValue(new MongoId(templateId), out var item))
            {
                return item;
            }

            LoggingHelpers.LogToConsole($"Could not locate item template with id {templateId}", ConsoleColor.Red);
            return null;
        }
    }
}
