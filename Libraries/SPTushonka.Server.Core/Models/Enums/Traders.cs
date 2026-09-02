using SPTarkov.Server.Core.Models.Common;

namespace SPTarkov.Server.Core.Models.Enums;

public static class Traders
{
    public static MongoId PRAPOR = new("54cb50c76803fa8b248b4571");
    public static MongoId THERAPIST = new("54cb57776803fa99248b456e");
    public static MongoId FENCE = new("579dc571d53a0658a154fbec");
    public static MongoId SKIER = new("58330581ace78e27b8b10cee");
    public static MongoId PEACEKEEPER = new("5935c25fb3acc3127c3d8cd9");
    public static MongoId MECHANIC = new("5a7c2eca46aef81a7ca2145d");
    public static MongoId RAGMAN = new("5ac3b934156ae10c4430e83c");
    public static MongoId JAEGER = new("5c0647fdd443bc2504c2d371");
    public static MongoId LIGHTHOUSEKEEPER = new("638f541a29ffd1183d187f57");
    public static MongoId BTR = new("656f0f98d80a697f855d34b1");
    public static MongoId REF = new("6617beeaa9cfa777ca915b7c");
    public static MongoId STORYLINE = new("67f7af56c117b6140af2a607");
    public static MongoId KERMAN = new("688246518448b05efd61d461");
    public static MongoId VOEVODA = new("688246958448b05efd61d462");
    public static MongoId TARAN = new("68fe15910f29ba3fdbba9d54");
    public static MongoId RADIO_STATION = new("68fe15990f29ba3fdbba9d55");
    public static MongoId SCIENTIST = new("69e0d6cc77b63940375b9173");

    /// <summary>
    /// These traders should only be added after a pre-1.0 profile wipes
    /// </summary>
    public static readonly HashSet<MongoId> WipeOnly = [STORYLINE, KERMAN, VOEVODA, TARAN, RADIO_STATION, SCIENTIST];

    /// <summary>
    /// Traders with no assort to restock
    /// </summary>
    public static readonly HashSet<MongoId> NoAssortRefresh =
    [
        LIGHTHOUSEKEEPER,
        STORYLINE,
        KERMAN,
        VOEVODA,
        TARAN,
        RADIO_STATION,
        SCIENTIST,
    ];
}
