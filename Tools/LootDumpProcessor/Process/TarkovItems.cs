using LootDumpProcessor.Logger;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Json;

namespace LootDumpProcessor.Process;

public class TarkovItems(string items)
{
    private readonly Dictionary<MongoId, TemplateItem>? _items = new JsonUtil([new SptJsonConverterRegistrator()]).Deserialize<
        Dictionary<MongoId, TemplateItem>
    >(File.ReadAllText(items));

    public virtual bool IsBaseClass(string tpl, string baseclass_id)
    {
        var template = Find(tpl, nameof(IsBaseClass));
        if (template is null || template.Parent.IsEmpty)
        {
            return false;
        }

        return template.Parent == baseclass_id || IsBaseClass(template.Parent, baseclass_id);
    }

    public virtual bool IsQuestItem(string tpl)
    {
        return Find(tpl, nameof(IsQuestItem))?.Properties?.QuestItem ?? false;
    }

    public virtual string? MaxDurability(string tpl)
    {
        var template = Find(tpl, nameof(MaxDurability));
        return template is null ? null : template.Properties?.MaxDurability?.ToString() ?? "";
    }

    public virtual string? AmmoCaliber(string tpl)
    {
        return Find(tpl, nameof(AmmoCaliber))?.Properties?.Caliber;
    }

    private TemplateItem? Find(string tpl, string caller)
    {
        if (_items == null)
        {
            throw new Exception("The server items couldnt be found or loaded. Check server config is pointing to the correct place");
        }

        if (MongoId.IsValidMongoId(tpl) && _items.TryGetValue(tpl, out var template))
        {
            return template;
        }

        if (LoggerFactory.GetInstance().CanBeLogged(LogLevel.Error))
        {
            LoggerFactory.GetInstance().Log($"[{caller}] Item template '{tpl}' was not found on the server items!", LogLevel.Error);
        }

        return null;
    }
}
