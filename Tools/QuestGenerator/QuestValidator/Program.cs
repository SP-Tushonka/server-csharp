using System;
using System.Collections.Generic;
using System.Linq;
using AssortGenerator.Common.Helpers;
using QuestValidator.Common.Helpers;
using QuestValidator.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;

namespace QuestValidator
{
    class Program
    {
        static void Main(string[] args)
        {
            var inputPath = DiskHelpers.CreateWorkingFolders();
            InputFileHelper.SetInputFiles(inputPath);

            //read in quest file
            var questData = QuestHelper.GetQuestData();
            var liveQuestData = QuestHelper.GetLiveQuestData().FirstOrDefault();

            if (questData is null || liveQuestData is null)
            {
                LoggingHelpers.LogError(
                    "Unable to read quest data. Are you sure the both quest files are in 'QuestValidator//bin//Debug//netcoreapp3.1//input'"
                );
                return;
            }

            ListMissingQuestsInLive(questData, liveQuestData);

            CheckForMissingQuestsInSptFile(liveQuestData, questData);

            CheckForMissingQuestIcons(questData);

            foreach (var (questId, quest) in questData)
            {
                LogQuestDetails(quest);

                // Get live quest
                var relatedLiveQuest = liveQuestData.FirstOrDefault(x => x.Id == questId);
                if (!ItemExists(relatedLiveQuest, "live quest. Live dump too old ?"))
                {
                    LoggingHelpers.LogInfo("");
                    continue;
                }

                CheckRootItemValues(quest, relatedLiveQuest);

                CheckSuccessRewardItems(quest, relatedLiveQuest);
                CheckStartedRewardItems(quest, relatedLiveQuest);

                CheckAvailableForFinishConditionItems(quest, relatedLiveQuest);
                CheckAvailableForStartConditionItems(quest, relatedLiveQuest);
                CheckFailConditionItems(quest, relatedLiveQuest);

                LoggingHelpers.LogInfo("");
                LoggingHelpers.LogInfo("-----");
                LoggingHelpers.LogInfo("");
            }
        }

        private static void CheckForMissingQuestIcons(Dictionary<MongoId, Quest> questData)
        {
            string iconPath = "TODO add path here Server\\project\\assets\\images\\quests";

            if (iconPath.Contains("TODO add path here"))
            {
                throw new Exception("Add the correct icon path to Program.cs");
            }

            foreach (var quest in questData)
            {
                var imageId = quest.Value.Image.Split("/")[4].Split(".")[0];
                if (!QuestHelper.DoesIconExist(iconPath, imageId))
                {
                    LoggingHelpers.LogError($"ERROR - Quest {quest.Value.Name} is missing an icon file, add {imageId}.png");
                }
            }
        }

        private static void ListMissingQuestsInLive(Dictionary<MongoId, Quest> questData, List<Quest> liveQuestData)
        {
            var missingQuests = new List<Quest>();
            foreach (var quest in questData.Values)
            {
                var liveQuest = liveQuestData.FirstOrDefault(x => x.Id == quest.Id);

                if (liveQuest is null)
                {
                    missingQuests.Add(quest);
                    LoggingHelpers.LogError($"ERROR Quest {quest.Id} {QuestHelper.GetQuestNameById(quest.Id)}  missing in live");
                }
            }
        }

        private static void CheckFailConditionItems(Quest questOld, Quest relatedLiveQuestOld)
        {
            var liveFailConditions = relatedLiveQuestOld.Conditions.Fail;

            // Check count
            CheckValuesMatch(questOld.Conditions.Fail.Count, liveFailConditions.Count, "FailCondition mismatch");

            foreach (var failItem in questOld.Conditions.Fail)
            {
                var liveFailItem = liveFailConditions.FirstOrDefault(x => x.Id == failItem.Id);
                if (!ItemExists(liveFailItem, "condition fail item", failItem.Index.Value))
                {
                    continue;
                }

                // Check parentId
                CheckValuesMatch(failItem.ParentId, liveFailItem.ParentId, "AvailableForFinish parentId mismatch", failItem.Id);
            }
        }

        private static void CheckAvailableForStartConditionItems(Quest questOld, Quest relatedLiveQuestOld)
        {
            var liveStartConditions = relatedLiveQuestOld.Conditions.AvailableForStart;

            // Check count
            CheckValuesMatch(questOld.Conditions.AvailableForStart.Count, liveStartConditions.Count, "AvailableForStartCondition mismtch");

            foreach (var availableForStartItem in questOld.Conditions.AvailableForStart)
            {
                var liveStartItem = liveStartConditions.FirstOrDefault(x => x.Id == availableForStartItem.Id);
                if (!ItemExists(liveStartItem, "AvailableForStart item", availableForStartItem.Index.Value))
                {
                    continue;
                }

                // Check parentId
                CheckValuesMatch(
                    availableForStartItem.ParentId,
                    liveStartItem.ParentId,
                    "AvailableForFinish parentId mismatch",
                    availableForStartItem.Id
                );
            }
        }

        private static void LogQuestDetails(Quest questOld)
        {
            var questName = QuestHelper.GetQuestNameById(questOld.Id);
            var trader = TraderHelper.GetTraderTypeById(questOld.TraderId);
            LoggingHelpers.LogInfo($"### Quest name: {questName} ({questOld.Id}) ({trader})");
            LoggingHelpers.LogInfo($"Wiki: https://escapefromtarkov.fandom.com/wiki/{questName.Replace(' ', '_')}");
        }

        private static void CheckRootItemValues(Quest questOld, Quest relatedLiveQuestOld)
        {
            // Check image id matches
            CheckValuesMatch(questOld.Image[..^4], (relatedLiveQuestOld?.Image)[..^4], "item path mismatch");

            //Check image id contains quest id
            if (!questOld.Image.Contains(questOld.Id))
            {
                LoggingHelpers.LogInfo($"INFO Quest image path does not contain quest id");
            }

            // Check started reward count matches
            questOld.Rewards.TryGetValue("Started", out var questOldStartedRewards);
            relatedLiveQuestOld.Rewards.TryGetValue("Started", out var relatedLiveOldStartedRewards);
            CheckValuesMatch(questOldStartedRewards.Count, relatedLiveOldStartedRewards.Count, "Started item count mismatch");

            // Check success reward count matches
            questOld.Rewards.TryGetValue("Success", out var questOldSuccessRewards);
            relatedLiveQuestOld.Rewards.TryGetValue("Success", out var relatedLiveOldSuccessRewards);
            CheckValuesMatch(questOldSuccessRewards.Count, relatedLiveOldSuccessRewards.Count, "success item count mismatch");

            // Check Fail reward count matches
            questOld.Rewards.TryGetValue("Fail", out var questOldFailRewards);
            relatedLiveQuestOld.Rewards.TryGetValue("Fail", out var relatedLiveOldFailRewards);
            CheckValuesMatch(questOldFailRewards.Count, relatedLiveOldFailRewards.Count, "fail item count mismatch");

            // Check location matches
            CheckValuesMatch(questOld.Location, relatedLiveQuestOld.Location, "location value mismatch");

            // Check traderid matches
            CheckValuesMatch(questOld.TraderId, relatedLiveQuestOld.TraderId, "traderid value mismatch");

            // Check type matches
            CheckValuesMatch(questOld.Type, relatedLiveQuestOld.Type, "quest type value mismatch");
        }

        private static void CheckSuccessRewardItems(Quest questOld, Quest relatedLiveQuestOld)
        {
            relatedLiveQuestOld.Rewards.TryGetValue("Success", out var liveQuestSuccessRewardItems);

            questOld.Rewards.TryGetValue("Success", out var questOldSuccessRewards);
            foreach (var questSuccessRewardItem in questOldSuccessRewards.Where(x => x.Type == RewardType.Item))
            {
                // Get live reward item by index and type
                var relatedLiveRewardItem = GetLiveRewardItem(questSuccessRewardItem, liveQuestSuccessRewardItems);

                if (relatedLiveRewardItem is null)
                {
                    LogUnableToFindSuccessItemInLiveData(questSuccessRewardItem, relatedLiveRewardItem);
                    continue;
                }

                // Ensure target matches the objects items[0].id value
                if (questSuccessRewardItem.Items[0]?.Id != questSuccessRewardItem.Target)
                {
                    LoggingHelpers.LogWarning(
                        $"WARNING target does not match first item: {questSuccessRewardItem.Target}, expected {questSuccessRewardItem.Items[0]?.Id}"
                    );
                }

                // Check template ids match
                CheckValuesMatch(
                    questSuccessRewardItem.Items[0].Template,
                    relatedLiveRewardItem.Items[0].Template,
                    "mismatch for template id",
                    questSuccessRewardItem.Items[0].Id,
                    true
                );

                // Check value values match
                CheckValuesMatch(
                    questSuccessRewardItem.Value.ToString(),
                    relatedLiveRewardItem.Value.ToString(),
                    "mismatch for success item reward value",
                    questSuccessRewardItem.Id
                );

                // Check item stack count
                if (questSuccessRewardItem.Items[0]?.Upd != null && relatedLiveRewardItem.Items[0]?.Upd != null)
                {
                    CheckValuesMatch(
                        questSuccessRewardItem.Items[0].Upd.StackObjectsCount.Value,
                        relatedLiveRewardItem.Items[0].Upd.StackObjectsCount.Value,
                        "mismatch for success item StackObjectsCount",
                        questSuccessRewardItem.Items[0].Id
                    );
                }

                // check sub items match
                CheckSubItemsMatch(questSuccessRewardItem, relatedLiveRewardItem);
            }

            foreach (var questSuccessRewardItem in questOldSuccessRewards.Where(x => x.Type == RewardType.Experience))
            {
                var relatedLiveRewardItem = liveQuestSuccessRewardItems.FirstOrDefault(x => x.Type == RewardType.Experience);
                if (!ItemExists(relatedLiveRewardItem, "experience success reward item", questSuccessRewardItem.Index.Value))
                {
                    continue;
                }

                // check experience value matches
                CheckValuesMatch(questSuccessRewardItem.Value.Value, relatedLiveRewardItem.Value.Value, "experience value mismatch");
            }

            foreach (var questSuccessRewardItem in questOldSuccessRewards.Where(x => x.Type == RewardType.TraderStanding))
            {
                var relatedLiveRewardItem = liveQuestSuccessRewardItems.FirstOrDefault(x =>
                    x.Target == questSuccessRewardItem.Target && x.Type == RewardType.TraderStanding
                );
                if (!ItemExists(relatedLiveRewardItem, "TraderStanding success reward item", questSuccessRewardItem.Index.Value))
                {
                    continue;
                }

                // check standing value matches
                CheckValuesMatch(questSuccessRewardItem.Value.Value, relatedLiveRewardItem.Value.Value, "trader standing value mismatch");

                // check target value matches
                CheckValuesMatch(questSuccessRewardItem.Target, relatedLiveRewardItem.Target, "trader target value mismatch");
            }

            foreach (var questSuccessRewardItem in questOldSuccessRewards.Where(x => x.Type == RewardType.AssortmentUnlock))
            {
                // Find the assort unlock item in the list of success rewards
                var possibleLiveRewardItems = liveQuestSuccessRewardItems.Where(x =>
                    x.Id == questSuccessRewardItem.Id && x.Type == RewardType.AssortmentUnlock
                );
                Reward relatedLiveRewardItem = null;

                // we found the one we want
                if (possibleLiveRewardItems?.Count() == 1)
                {
                    relatedLiveRewardItem = possibleLiveRewardItems.First();
                }

                // multiple found
                if (possibleLiveRewardItems?.Count() > 1)
                {
                    // be more specific, get my index
                    relatedLiveRewardItem = possibleLiveRewardItems.FirstOrDefault(x => x.Index == questSuccessRewardItem.Index);

                    // nothing found by index, try by
                    if (relatedLiveRewardItem is null)
                    {
                        relatedLiveRewardItem = possibleLiveRewardItems.FirstOrDefault(x => x.TraderId == questSuccessRewardItem.TraderId);
                    }
                }

                if (relatedLiveRewardItem is null)
                {
                    relatedLiveRewardItem = liveQuestSuccessRewardItems.Find(x =>
                        x.TraderId == questSuccessRewardItem.TraderId
                        && x.Index == questSuccessRewardItem.Index
                        && x.Type == RewardType.AssortmentUnlock
                        && x.Items[0].Template == questSuccessRewardItem.Items[0].Template
                    );
                }

                if (!ItemExists(relatedLiveRewardItem, "AssortmentUnlock success reward item", questSuccessRewardItem.Index.Value))
                {
                    continue;
                }

                // Check loyalty level
                CheckValuesMatch(
                    questSuccessRewardItem.LoyaltyLevel.Value,
                    relatedLiveRewardItem.LoyaltyLevel.Value,
                    "loyalty level value mismatch",
                    questSuccessRewardItem.Id
                );

                // Check traderId
                CheckValuesMatch(
                    questSuccessRewardItem.TraderId.ToString(),
                    relatedLiveRewardItem.TraderId.ToString(),
                    "traderId value mismatch",
                    questSuccessRewardItem.Id
                );

                // check target equals items[0].id
                CheckValuesMatch(
                    questSuccessRewardItem.Target,
                    questSuccessRewardItem.Items[0].Id,
                    "target value does not match items[0].id mismatch",
                    questSuccessRewardItem.Id
                );
            }
        }

        private static void CheckSubItemsMatch(Reward questSuccessRewardItem, Reward relatedLiveRewardItem)
        {
            foreach (var subItem in questSuccessRewardItem.Items.Where(x => !string.IsNullOrEmpty(x.SlotId)))
            {
                // find live item by slotid
                var liveCounterpart = relatedLiveRewardItem.Items.Where(x => x.SlotId == subItem.SlotId);
                if (liveCounterpart is null || liveCounterpart.Count() == 0)
                {
                    // Look for live item by template id
                    liveCounterpart = relatedLiveRewardItem.Items.Where(x => x.Template == subItem.Template);
                    if (liveCounterpart is null || liveCounterpart.Count() == 0)
                    {
                        LoggingHelpers.LogWarning(
                            $"a live counterpart for the subItem: {subItem.SlotId} could not be found by slotId or tpId, skipping subItem check"
                        );
                        continue;
                    }
                }
                if (liveCounterpart.Count() > 1)
                {
                    LoggingHelpers.LogWarning($"Multiple live counterparts for the subItem {subItem.SlotId} found, skipping subItem check");
                    continue;
                }

                var firstLiveItem = liveCounterpart.FirstOrDefault();
                CheckValuesMatch(
                    subItem.Template,
                    firstLiveItem.Template,
                    $"mismatch for success subItem({subItem.SlotId}) reward templateId",
                    subItem.Id
                );
            }
        }

        private static void LogUnableToFindSuccessItemInLiveData(Reward questSuccessRewardItem, Reward relatedLiveRewardItem)
        {
            if (relatedLiveRewardItem is null)
            {
                LoggingHelpers.LogError(
                    $"ERROR unable to find success reward item in live quest data by index: ({questSuccessRewardItem.Index}) OR template id: {questSuccessRewardItem.Items[0].Template} ({ItemTemplateHelper.GetTemplateById(questSuccessRewardItem.Items[0].Template).Name})"
                );

                LoggingHelpers.LogError("Existing items:");
                LogSuccessItems(questSuccessRewardItem);

                LoggingHelpers.LogError($"ERROR Skipping quest success item. id: {questSuccessRewardItem.Id}");
            }
        }

        private static void LogSuccessItems(Reward rewardItem)
        {
            foreach (var item in rewardItem.Items)
            {
                LoggingHelpers.LogInfo($"{item.Template} ({ItemTemplateHelper.GetTemplateById(item.Template).Name})");
            }
        }

        /// <summary>
        /// Find live success item reward by index
        /// If item at index does not match templateId to desired item
        /// get live success item reward by template id
        /// </summary>
        /// <param name="questSuccessRewardItem"></param>
        /// <param name="liveQuestSuccessRewardItems"></param>
        /// <returns></returns>
        private static Reward GetLiveRewardItem(Reward questSuccessRewardItem, List<Reward> liveQuestSuccessRewardItems)
        {
            var LiveItemRewards = liveQuestSuccessRewardItems.Where(x => x.Type == RewardType.Item);
            var liveRewardItemByIndex = LiveItemRewards.FirstOrDefault(x => x.Index == questSuccessRewardItem.Index);

            // no item found by index, find by template id
            if (liveRewardItemByIndex is null)
            {
                foreach (
                    var liveItem in LiveItemRewards.SelectMany(liveItem =>
                        liveItem
                            .Items.Where(subItem => subItem.Template == questSuccessRewardItem.Items[0].Template)
                            .Select(subItem => liveItem)
                    )
                )
                {
                    return liveItem;
                }
            }

            // item found by index but template id didnt match
            if (liveRewardItemByIndex != null && liveRewardItemByIndex.Items[0].Template != questSuccessRewardItem.Items[0].Template)
            {
                return LiveItemRewards.FirstOrDefault(x => x.Items[0].Template == questSuccessRewardItem.Items[0].Template);
            }

            return liveRewardItemByIndex;
        }

        private static void CheckStartedRewardItems(Quest questOld, Quest relatedLiveQuestOld)
        {
            relatedLiveQuestOld.Rewards.TryGetValue("Started", out var liveQuestStartedRewardItems);

            questOld.Rewards.TryGetValue("Started", out var questOldStartedRewardItems);
            foreach (var questStartedRewardItem in questOldStartedRewardItems.Where(x => x.Type == RewardType.Item))
            {
                var errorMessage = string.Empty;
                // Get live reward item by index and type
                var relatedLiveRewardItem = liveQuestStartedRewardItems.Find(x =>
                    x.Index == questStartedRewardItem.Index && x.Type == RewardType.Item
                );
                if (relatedLiveRewardItem is null)
                {
                    // Get live reward item by templateId and type as we cant find it by index
                    relatedLiveRewardItem = liveQuestStartedRewardItems.Find(x =>
                        x.Items != null && x.Items[0]?.Template == questStartedRewardItem.Items[0]?.Template && x.Type == RewardType.Item
                    );
                    if (relatedLiveRewardItem is null)
                    {
                        LoggingHelpers.LogError(
                            $"ERROR unable to find started reward item in live quest data by index: ({questStartedRewardItem.Index}) OR template id: {questStartedRewardItem.Items[0].Template}"
                        );
                        LoggingHelpers.LogError($"ERROR Skipping quest started item: {questStartedRewardItem.Id}");
                        continue;
                    }
                }

                // Ensure target matches the objects items[0].id value
                if (questStartedRewardItem.Items[0]?.Id != questStartedRewardItem.Target)
                {
                    LoggingHelpers.LogWarning(
                        $"WARNING target does not match first item: {questStartedRewardItem.Target}, expected {questStartedRewardItem.Items[0]?.Id}"
                    );
                }

                // Check template ids match
                CheckValuesMatch(
                    questStartedRewardItem.Items[0].Template,
                    relatedLiveRewardItem.Items[0].Template,
                    "mismatch for template id",
                    questStartedRewardItem.Items[0].Id,
                    true
                );

                // Check 'value' values match
                CheckValuesMatch(
                    questStartedRewardItem.Value.Value,
                    relatedLiveRewardItem.Value.Value,
                    "mismatch for success item reward value",
                    questStartedRewardItem.Id
                );

                // Check item stack count
                if (questStartedRewardItem.Items[0] != null && questStartedRewardItem.Items[0].Upd != null)
                {
                    CheckValuesMatch(
                        questStartedRewardItem.Items[0].Upd.StackObjectsCount.Value,
                        relatedLiveRewardItem.Items[0].Upd.StackObjectsCount.Value,
                        "mismatch for started item StackObjectsCount",
                        questStartedRewardItem.Items[0].Id
                    );
                }
            }

            foreach (var questStartedRewardItem in questOldStartedRewardItems.Where(x => x.Type == RewardType.AssortmentUnlock))
            {
                // Get live reward item by id
                var relatedLiveRewardItem = liveQuestStartedRewardItems.FirstOrDefault(x =>
                    x.Id == questStartedRewardItem.Id && x.Type == RewardType.AssortmentUnlock
                );
                if (relatedLiveRewardItem is null)
                {
                    // Cant find live reward item by id, get my template id inside items[0]
                    relatedLiveRewardItem = liveQuestStartedRewardItems.Find(x =>
                        x.TraderId == questStartedRewardItem.TraderId
                        && x.Index == questStartedRewardItem.Index
                        && x.Type == RewardType.AssortmentUnlock
                        && x.Items[0].Template == questStartedRewardItem.Items[0].Template
                    );
                }

                if (
                    !ItemExists(
                        relatedLiveRewardItem,
                        "AssortmentUnlock started reward item",
                        questStartedRewardItem.Index.GetValueOrDefault(-1)
                    )
                )
                {
                    continue;
                }

                // Check loyalty level
                CheckValuesMatch(
                    questStartedRewardItem.LoyaltyLevel.Value,
                    relatedLiveRewardItem.LoyaltyLevel.Value,
                    "loyalty level value mismatch",
                    questStartedRewardItem.Id
                );

                // Check traderId
                CheckValuesMatch(
                    questStartedRewardItem.TraderId.ToString(),
                    relatedLiveRewardItem.TraderId.ToString(),
                    "traderId value mismatch",
                    questStartedRewardItem.Id
                );

                // check target equals items[0].id
                CheckValuesMatch(
                    questStartedRewardItem.Target,
                    questStartedRewardItem.Items[0].Id,
                    "target value does not match items[0].id mismatch",
                    questStartedRewardItem.Id
                );
            }
        }

        private static void CheckAvailableForFinishConditionItems(Quest questOld, Quest relatedLiveQuestOld)
        {
            // Check count
            CheckValuesMatch(
                questOld.Conditions.AvailableForFinish.Count,
                relatedLiveQuestOld.Conditions.AvailableForFinish.Count,
                "AvailableForFinish mismatch"
            );

            foreach (var availableForFinishCondition in questOld.Conditions.AvailableForFinish)
            {
                var liveFinishItem = relatedLiveQuestOld.Conditions.AvailableForFinish.Find(x => x.Id == availableForFinishCondition.Id);
                if (!ItemExists(liveFinishItem, "AvailableForFinish item", availableForFinishCondition.Index.Value))
                {
                    continue;
                }

                // Check parentId
                CheckValuesMatch(
                    availableForFinishCondition.ParentId,
                    liveFinishItem.ParentId,
                    "AvailableForFinish parentId mismatch",
                    availableForFinishCondition.Id
                );

                // check AvailableForFinish resetOnSessionEnd
                if (
                    (availableForFinishCondition.Counter.Conditions.FirstOrDefault()?.ResetOnSessionEnd.HasValue ?? false)
                    && (liveFinishItem.Counter.Conditions.FirstOrDefault()?.ResetOnSessionEnd.HasValue ?? false)
                )
                {
                    CheckValuesMatch(
                        availableForFinishCondition.Counter.Conditions.FirstOrDefault().ResetOnSessionEnd.GetValueOrDefault(false),
                        liveFinishItem.Counter.Conditions.FirstOrDefault().ResetOnSessionEnd.GetValueOrDefault(false),
                        "AvailableForFinish resetOnSessionEnd value mismatch",
                        availableForFinishCondition.Id
                    );
                }

                // check AvailableForFinish target
                CheckValuesMatch(
                    Convert.ToString(availableForFinishCondition.Target),
                    Convert.ToString(liveFinishItem.Target),
                    "AvailableForFinish target value mismatch",
                    availableForFinishCondition.Id
                );

                // check weapons allowed match
                //CheckValuesMatch(availableForFinishItem.weapon.Count, liveFinishItem.counter.conditions), "AvailableForFinish target value mismatch", availableForFinishItem.id);
            }
        }

        private static bool ItemExists(object itemToCheck, string message, int index = -1)
        {
            if (itemToCheck is null)
            {
                if (index == -1)
                {
                    LoggingHelpers.LogError($"ERROR no match found for {message}");
                }
                else
                {
                    LoggingHelpers.LogError($"ERROR no match found for {message} at index: {index}");
                }

                return false;
            }

            return true;
        }

        private static void CheckValuesMatch(int firstValue, int secondValue, string message, string associatedId = "")
        {
            if (firstValue != secondValue)
            {
                if (associatedId == string.Empty)
                {
                    LoggingHelpers.LogWarning($"WARNING {message}: '{firstValue}', expected '{secondValue}'");
                }
                else
                {
                    LoggingHelpers.LogWarning($"WARNING {associatedId} {message}: '{firstValue}', expected '{secondValue}'");
                }
            }
        }

        private static void CheckValuesMatch<T>(T firstValue, T secondValue, string message, string associatedId = "")
            where T : struct
        {
            if (firstValue.Equals(secondValue))
            {
                if (associatedId == string.Empty)
                {
                    LoggingHelpers.LogWarning($"WARNING {message}: '{firstValue}', expected '{secondValue}'");
                }
                else
                {
                    LoggingHelpers.LogWarning($"WARNING {associatedId} {message}: '{firstValue}', expected '{secondValue}'");
                }
            }
        }

        private static void CheckValuesMatch(
            string firstValue,
            string secondValue,
            string message,
            string associatedId = "",
            bool performTemplateIdLookup = false
        )
        {
            if (firstValue != secondValue)
            {
                if (performTemplateIdLookup)
                {
                    firstValue = $"{firstValue} ({ItemTemplateHelper.GetTemplateById(firstValue).Name})";
                    secondValue = $"{secondValue} ({ItemTemplateHelper.GetTemplateById(secondValue).Name})";
                }
                if (associatedId == string.Empty)
                {
                    LoggingHelpers.LogWarning($"WARNING {message}: '{firstValue}', expected '{secondValue}'");
                }
                else
                {
                    LoggingHelpers.LogWarning($"WARNING {associatedId} {message}: '{firstValue}', expected '{secondValue}'");
                }
            }
        }

        private static void CheckValuesMatch(bool firstValue, bool secondValue, string message, string associatedId = "")
        {
            if (firstValue != secondValue)
            {
                if (associatedId == string.Empty)
                {
                    LoggingHelpers.LogWarning($"WARNING {message}: '{firstValue}', expected '{secondValue}'");
                }
                else
                {
                    LoggingHelpers.LogWarning($"WARNING {associatedId} {message}: '{firstValue}', expected '{secondValue}'");
                }
            }
        }

        private static void CheckForMissingQuestsInSptFile(List<Quest> liveQuestData, Dictionary<MongoId, Quest> sptQuestData)
        {
            // iterate over live quests and look for quests that exist in live but not in aki
            var missingQuests = new List<string>();
            foreach (var liveQuest in liveQuestData)
            {
                if (!sptQuestData.ContainsKey(liveQuest.Id))
                {
                    missingQuests.Add($"{liveQuest.Id} {QuestHelper.GetQuestNameById(liveQuest.Id)}");
                }
            }
            // Quests in live but not in aki were found, log it
            if (missingQuests.Count > 0)
            {
                LoggingHelpers.LogWarning($"WARNING aki quest list is missing quests:");
                foreach (var item in missingQuests)
                {
                    LoggingHelpers.LogWarning(item);
                }
            }
        }
    }
}
