using System.Collections.Generic;
using System.IO;
using Common;
using Common.Json;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace Generator.Helpers
{
    public static class ItemTemplateHelper
    {
        private static Dictionary<MongoId, TemplateItem> _itemCache;

        public static Dictionary<MongoId, TemplateItem> Items
        {
            get
            {
                if (_itemCache == null)
                {
                    _itemCache = ToolJson.Util.DeserializeFromFile<Dictionary<MongoId, TemplateItem>>(
                        System.IO.Path.Combine(ServerAssets.Database, "templates", "items.json")
                    );
                }

                return _itemCache;
            }
        }

        public static TemplateItem GetTemplateById(MongoId templateId)
        {
            if (Items.TryGetValue(templateId, out var template))
            {
                return template;
            }

            LoggingHelpers.LogToConsole($"Could not locate item template with id {templateId}", ConsoleColor.Red);
            return null;
        }
    }
}
