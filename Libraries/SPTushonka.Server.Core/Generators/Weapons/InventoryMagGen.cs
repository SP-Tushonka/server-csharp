using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;

namespace SPTarkov.Server.Core.Generators.Weapons;

[Injectable]
public class InventoryMagGen()
{
    private readonly TemplateItem? _ammoTemplate;
    private readonly TemplateItem? _magazineTemplate;
    private readonly GenerationData? _magCounts;
    private readonly BotBaseInventory? _pmcInventory;
    private readonly MongoId _botId;
    private readonly TemplateItem? _weaponTemplate;
    private readonly HashSet<EquipmentSlots>? _magazineContainers;

    public InventoryMagGen(
        GenerationData magCounts,
        TemplateItem magazineTemplate,
        TemplateItem weaponTemplate,
        TemplateItem ammoTemplate,
        BotBaseInventory pmcInventory,
        MongoId botId,
        HashSet<EquipmentSlots>? magazineContainers = null
    )
        : this()
    {
        _magCounts = magCounts;
        _magazineTemplate = magazineTemplate;
        _weaponTemplate = weaponTemplate;
        _ammoTemplate = ammoTemplate;
        _pmcInventory = pmcInventory;
        _botId = botId;
        _magazineContainers = magazineContainers;
    }

    /// <summary>
    ///     Containers spare magazines go into in order, the vest then pockets when not set
    /// </summary>
    /// <returns>Containers to try in order</returns>
    public HashSet<EquipmentSlots> GetMagazineContainers()
    {
        return _magazineContainers ?? [EquipmentSlots.TacticalVest, EquipmentSlots.Pockets];
    }

    public GenerationData GetMagCount()
    {
        return _magCounts!;
    }

    public TemplateItem GetMagazineTemplate()
    {
        return _magazineTemplate!;
    }

    public TemplateItem GetWeaponTemplate()
    {
        return _weaponTemplate!;
    }

    public TemplateItem GetAmmoTemplate()
    {
        return _ammoTemplate!;
    }

    public BotBaseInventory GetPmcInventory()
    {
        return _pmcInventory!;
    }

    public MongoId GetBotId()
    {
        return _botId;
    }
}
