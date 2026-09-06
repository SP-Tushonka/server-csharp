using System.Collections.Generic;
using QuestValidator.Common.Models;
using SPTarkov.Server.Core.Models.Common;

namespace QuestValidator.Common.Helpers
{
    public static class QuestRequirements
    {
        public class QuestData
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public List<PreRequisite> PreRequisites { get; set; }

            public QuestData AddPrerequisiteQuest(Quest quest, QuestStatus status)
            {
                // TODO: add check if desired prereq is same id as quest, prevent it

                (PreRequisites ??= new List<PreRequisite>()).Add(
                    new PreRequisite
                    {
                        PreReqType = PreRequisiteType.Quest,
                        Quest = GetQuestData(quest),
                        QuestStatus = status,
                    }
                );

                return this;
            }

            public QuestData AddPrerequisiteLevel(int level)
            {
                // TODO: add check if level prereq exits already, dont add

                (PreRequisites ??= new List<PreRequisite>()).Add(new PreRequisite { PreReqType = PreRequisiteType.Level, Level = level });

                return this;
            }

            public QuestData RemovePrerequisiteQuest(Quest quest)
            {
                (PreRequisites ??= new List<PreRequisite>()).Add(
                    new PreRequisite { PreReqType = PreRequisiteType.RemoveQuest, Quest = GetQuestData(quest) }
                );

                return this;
            }

            public QuestData RemovePrerequisiteLevel()
            {
                (PreRequisites ??= new List<PreRequisite>()).Add(new PreRequisite { PreReqType = PreRequisiteType.RemoveLevel });

                return this;
            }
        }

        public class PreRequisite
        {
            public PreRequisiteType PreReqType { get; set; }
            public QuestStatus QuestStatus { get; set; }
            public QuestData Quest { get; set; }
            public int Level { get; set; }
        }

        private static readonly Dictionary<Quest, QuestData> QuestWithPrecedingQuestDict = new()
        {
            {
                Quest.BreathingRoom,
                GetQuestData(Quest.BreathingRoom).AddPrerequisiteQuest(Quest.FriendFromNorvinskPart5, QuestStatus.Success)
            },
            {
                Quest.Collector,
                GetQuestData(Quest.Collector)
                    .AddPrerequisiteQuest(Quest.Athlete, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.DecontaminationService, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.HealthCarePrivacyP5, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.CarRepair, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.TheTarkovShooterP8, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.GunsmithP22, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.GeneralWares, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.Setup, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.Flint, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.LendLeaseP1, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.Chumming, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.OnlyBusiness, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.MakeUltraGreatAgain, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.BigSale, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.TheBloodOfWarP2, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.DressedToKill, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.DatabaseP2, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.FarmingP4, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.SewItGoodP4, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.SignalP4, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.TheKeyToSuccess, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.NoFussNeeded, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.Gratitude, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.BadHabit, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.SalesNight, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.Scout, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.Insider, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.Supervisor, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.HotDelivery, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.Minibus, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.TheBloodOfWarP3, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.LivingHighIsNotACrimeP2, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.Scavenger, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.GoldenSwag, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.ChemicalP3, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.FriendFromTheWestP2, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.VitaminsP2, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.InformedMeansArmed, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.Fertilizers, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.AShooterBornInHeaven, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.PsychoSniper, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.ScrapMetal, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.EagleEye, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.HumanitarianSupplies, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.TheCultP2, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.SpaTourP7, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.CargoXP3, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.WetJobP6, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.LendLeaseP2, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.TheGuide, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.BPDepot, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.IceCreamCones, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.ThePunisherP6, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.ShakingUpTeller, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.TestDriveP1, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.PerfectMediator, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.TheSurvivalistPathWoundedBeast, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.HuntsmanPathEraserP2, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.Ambulance, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.CourtesyVisit, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.ShadyBusiness, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.Nostalgia, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.FishingPlace, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.TheSurvivalistPathJunkie, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.TheSurvivalistPathCombatMedic, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.Insomnia, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.RevisionReserve, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.ClassifiedTechnologies, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.Documents, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.NoPlaceForRenegades, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.DiseaseHistory, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.SurplusGoods, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.BackDoor, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.InventoryCheck, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.FuelMatter, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.PestControl, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.HuntsmanPathFactoryChief, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.CargoXP4, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.LongRoad, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.TheHermit, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.MissingCargo, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.HuntsmanPathOutcasts, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.EasyJobPart2, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.LostContact, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.Overpopulation, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.CorporateSecrets, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.RevisionLighthouse, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.SeasideVacation, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.EnergyCrisis, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.HuntsmanPathJustice, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.HuntsmanPathSellOut, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.HuntsmanPathWoodsKeeper, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.BunkerP2, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.ColleaguesP2, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.PeacekeepingMission, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.Import, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.TheChemistryCloset, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.HuntsmanPathController, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.HuntingTrip, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.StrayDogs, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.BroadcastPart1, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.Reconnaissance, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.DrugTrafficking, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.SafeCorridor, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.TerraGroupEmployee, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.Stirrup, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.CharismaBringsSuccess, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.HuntsmanPathEvilWatchman, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.RiggedGame, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.SearchMission, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.Crisis, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.Intimidator, QuestStatus.Success)
                    .AddPrerequisiteQuest(Quest.RegulatedMaterials, QuestStatus.Success)
            },
        };

        private static QuestData GetQuestData(Quest quest)
        {
            return new QuestData { Id = QuestNames.GetIdByEnum(quest), Name = QuestNames.GetNameByEnum(quest) };
        }

        public static List<PreRequisite> GetQuestRequirements(MongoId questId)
        {
            var questEnum = QuestNames.GetEnumById(questId);
            if (QuestWithPrecedingQuestDict.TryGetValue(questEnum, out var quest))
            {
                return quest.PreRequisites;
            }

            return null;
        }
    }
}
