using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AssortGenerator.Common.Helpers;
using QuestValidator.Common;
using QuestValidator.Common.Helpers;
using QuestValidator.Common.Models;
using QuestValidator.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Utils.Json;
using Quest = SPTarkov.Server.Core.Models.Eft.Common.Tables.Quest;
using QuestStatus = QuestValidator.Common.Models.QuestStatus;

namespace GenerateQuestFile
{
    public class Program
    {
        /// <summary>
        /// Generate a quests.json file in /output/
        /// Uses every quest from the live quest dump file
        /// If any quests are missing, it will use the quests.json file to fill in the blanks
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            var inputPath = DiskHelpers.CreateWorkingFolders();
            InputFileHelper.SetInputFiles(inputPath);

            // Read in quest files
            var questBlacklist = QuestHelper.GetQuestBlacklist();
            var existingQuestData = QuestHelper.GetQuestData();
            var mergedLiveData = QuestHelper.LoadMergedLiveQuests(questBlacklist);

            OutputQuestRequirementsToConsole(mergedLiveData);

            JsonWriter.WriteJson(mergedLiveData, "output", Directory.GetCurrentDirectory(), "mergedlivejson");

            // Find the quests that are missing from the live file from existing quests.json
            var missingQuests = GetMissingQuestsNotInLiveFile(existingQuestData, mergedLiveData, questBlacklist);

            // Create a list of quests to output
            // Use all quests in live file
            // Use quests from quests.json to fill in missing quests
            // Add live quests to collection to return later
            var questsToOutputToFile = new Dictionary<MongoId, Quest>();
            foreach (var liveQuest in mergedLiveData)
            {
                questsToOutputToFile.Add(liveQuest.Id, liveQuest);
            }

            // Add missing quests from existing quest data to fill in blanks from live data
            foreach (var missingQuest in missingQuests)
            {
                questsToOutputToFile.Add(missingQuest.Id, missingQuest);
            }

            // Now old + new quests have been merged, check quest list to see if any quests are missing
            foreach (var missingQuest in QuestNames.GetQuests())
            {
                if (!questsToOutputToFile.Any(x => x.Key == missingQuest.Value))
                {
                    LoggingHelpers.LogWarning($" quest not found in new or old data: {missingQuest.Key}");
                }
            }

            if (!questsToOutputToFile.ContainsKey(new MongoId("5e383a6386f77465910ce1f3"))) // TextileP1Bear
            {
                // add textileP1Bear
            }

            if (!questsToOutputToFile.ContainsKey(new MongoId("5e4d515e86f77438b2195244"))) // TextileP2Bear
            {
                // add TextileP2Bear
            }

            foreach (var quest in questsToOutputToFile)
            {
                var originalQuest = existingQuestData.FirstOrDefault(x => x.Key == quest.Key).Value;

                if (originalQuest is null)
                {
                    LoggingHelpers.LogWarning(
                        $"Cant check for original start conditions. Unable to find original quest {quest.Key} {QuestHelper.GetQuestNameById(quest.Key)}, skipping."
                    );
                    continue;
                }

                AddMissingFields(quest);

                var failedRewards = originalQuest.Rewards.Where(x => x.Key == "Fail").ToList();
                if (failedRewards.Count > 0)
                {
                    AddMissingFailRewards(originalQuest, quest);
                }

                // To make diffs more sane, copy the random IDs from the existing quests.json if possible
                //CopyExistingRandomIds(originalQuest, quest.Value);
            }

            // Iterate over quest objects a final time and add hard coded quest requirements if they dont already exist.
            // The table predates the 1.1 rework that replaced quest and level gates with story variables, so it
            // only applies to quests no dump carries.
            var liveQuestIds = mergedLiveData.Select(quest => quest.Id).ToHashSet();
            foreach (var quest in questsToOutputToFile.Where(quest => !liveQuestIds.Contains(quest.Key)))
            {
                var questRequirements = QuestRequirements.GetQuestRequirements(quest.Key);
                if (questRequirements is null || questRequirements.Count == 0)
                {
                    LoggingHelpers.LogWarning($"Quest requirement not found for : {quest.Value.Name}, skipping.");

                    continue;
                }

                foreach (var requirement in questRequirements)
                {
                    if (requirement.PreReqType == PreRequisiteType.Quest)
                    {
                        // Does quest have requirement
                        if (
                            !quest.Value.Conditions.AvailableForStart.Any(x =>
                                x.ConditionType == "Quest" && x.Target.Item == requirement.Quest.Id
                            )
                        )
                        {
                            LoggingHelpers.LogSuccess($"{quest.Value.Name} needs a prereq of quest: {requirement.Quest.Name}, adding.");

                            var hashData = quest.Value.Id.ToString() + requirement.Quest.Id.ToString();
                            var questConditionToAdd = new QuestCondition
                            {
                                ConditionType = "Quest",
                                Id = Sha256(hashData),
                                Index = GetNextIndex(quest.Value.Conditions.AvailableForStart.LastOrDefault()?.Index),
                                ParentId = "",
                                Status = GetQuestStatus(requirement.QuestStatus),
                                Target = new ListOrT<string>(null, requirement.Quest.Id),
                                VisibilityConditions = [],
                                AvailableAfter = 0,
                                DynamicLocale = false,
                            };
                            quest.Value.Conditions.AvailableForStart.Add(questConditionToAdd);
                        }
                        else
                        {
                            if (questRequirements != null)
                            {
                                LoggingHelpers.LogInfo(
                                    $"{quest.Value.Name} already has prereq of quest {requirement.Quest.Name}, skipping."
                                );
                            }
                        }
                    }

                    if (requirement.PreReqType == PreRequisiteType.RemoveQuest)
                    {
                        if (
                            quest.Value.Conditions.AvailableForStart.RemoveAll(x =>
                                x.ConditionType == "Quest" && x.Target.ToString() == requirement.Quest.Id
                            ) > 0
                        )
                        {
                            LoggingHelpers.LogSuccess($"{quest.Value.Name} required {requirement.Quest.Name}, removing.");
                        }
                    }

                    if (requirement.PreReqType == PreRequisiteType.Level)
                    {
                        if (
                            !quest.Value.Conditions.AvailableForStart.Any(x =>
                                x.ConditionType == "Level" && int.Parse(x.Value.ToString()) == requirement.Level
                            )
                        )
                        {
                            LoggingHelpers.LogSuccess($"{quest.Value.Name} needs a prereq of level {requirement.Level}, adding.");

                            string dataToHash = quest.Value.Id.ToString() + "Level";
                            quest.Value.Conditions.AvailableForStart.Add(
                                new QuestCondition
                                {
                                    ConditionType = "Level",
                                    Id = Sha256(dataToHash),
                                    Index = GetNextIndex(quest.Value.Conditions.AvailableForStart.LastOrDefault()?.Index),
                                    ParentId = "",
                                    DynamicLocale = false,
                                    Value = requirement.Level,
                                    CompareMethod = ">=",
                                    VisibilityConditions = [],
                                }
                            );
                        }
                    }

                    if (requirement.PreReqType == PreRequisiteType.RemoveLevel)
                    {
                        if (quest.Value.Conditions.AvailableForStart.RemoveAll(x => x.ConditionType == "Level") > 0)
                        {
                            LoggingHelpers.LogSuccess($"{quest.Value.Name} required level {requirement.Level}, removing.");
                        }
                    }
                }

                // To make diffs more sane, copy the random IDs from the existing quests.json if possible
                var originalQuest = existingQuestData.FirstOrDefault(x => x.Key == quest.Key).Value;
                if (originalQuest != null)
                {
                    CopyExistingRandomIds(originalQuest, quest.Value);
                }
            }
            OutputQuestRequirementsToConsole2(questsToOutputToFile);
            JsonWriter.WriteJson(questsToOutputToFile, "output", Directory.GetCurrentDirectory(), "quests");
        }

        private static void OutputQuestRequirementsToConsole(List<Quest> quests)
        {
            var output = new List<string>();
            foreach (var quest in quests)
            {
                var questConditions = quest.Conditions.AvailableForStart.Where(x => x.ConditionType == "Quest").ToList();
                if (questConditions.Count > 0)
                {
                    foreach (var questCondition in questConditions)
                    {
                        var x = questCondition.Target?.Item ?? string.Join(",", questCondition.Target?.List ?? []);
                        Console.WriteLine($"{QuestHelper.GetQuestNameById(quest.Id)} needs: {QuestHelper.GetQuestNameById(x)}");
                    }
                }
            }
        }

        private static void OutputQuestRequirementsToConsole2(Dictionary<MongoId, Quest> quests)
        {
            var output = new List<string>();
            foreach (var quest in quests)
            {
                var questConditions = quest.Value.Conditions.AvailableForStart.Where(x => x.ConditionType == "Quest");
                if (questConditions != null)
                {
                    foreach (var questCondition in questConditions)
                    {
                        var x = questCondition.Target?.Item ?? string.Join(",", questCondition.Target?.List ?? []);
                        Console.WriteLine($"{QuestHelper.GetQuestNameById(quest.Value.Id)} needs {QuestHelper.GetQuestNameById(x)}");
                    }
                }
            }
        }

        private static HashSet<QuestStatusEnum> GetQuestStatus(QuestStatus status)
        {
            switch (status)
            {
                case QuestStatus.Started:
                    return [QuestStatusEnum.Started];
                case QuestStatus.Success:
                    return [QuestStatusEnum.Success];
                case QuestStatus.Fail:
                    return [QuestStatusEnum.Fail];
                case QuestStatus.StartedSuccess:
                    return [QuestStatusEnum.Started, QuestStatusEnum.Success];
                case QuestStatus.SuccessFail:
                    return [QuestStatusEnum.Success, QuestStatusEnum.Fail];
            }

            throw new Exception($"Unable to process quest status {status}");
        }

        /// <summary>
        /// Latest version of eft has changed the quest json structure, this method adds missing fields
        /// Mega hack as we dont have a full dump as of 30/06/2022
        /// </summary>
        /// <param name="quest">quest to add missing fields to</param>
        private static void AddMissingFields(KeyValuePair<MongoId, Quest> quest)
        {
            //side
            if (String.IsNullOrEmpty(quest.Value.Side))
            {
                quest.Value.Side = "Pmc";
                LoggingHelpers.LogInfo($"Updated quest {quest.Value.Name} to have a side of 'pmc'");
            }

            //changeQuestMessageText
            if (String.IsNullOrEmpty(quest.Value.ChangeQuestMessageText))
            {
                quest.Value.ChangeQuestMessageText = $"{quest.Value.Id} changeQuestMessageText";
                LoggingHelpers.LogInfo($"Updated quest {quest.Value.Name} to have a changeQuestMessageText value");
            }

            // findInRaid
            quest.Value.Rewards.TryGetValue("Success", out var successRewards);
            foreach (var success in successRewards ?? [])
            {
                if (success.Type == RewardType.Item && success.FindInRaid == null)
                {
                    success.FindInRaid = true;
                    LoggingHelpers.LogInfo($"Updated quest: {quest.Value.Name} to have a success item reward findInRaid value of 'true'");
                }
            }
        }

        private static void AddMissingFailRewards(Quest originalQuest, KeyValuePair<MongoId, Quest> quest)
        {
            originalQuest.Rewards.TryGetValue("Fail", out var failedRewards);
            foreach (var originalFailReward in failedRewards.ToList()) // toList as we're modifying the collection
            {
                // already has a fail reward of same type and target, skip
                if (failedRewards.Any(x => x.Type == originalFailReward.Type && x.Target == originalFailReward.Target))
                {
                    continue;
                }

                failedRewards.Add(originalFailReward);
            }
        }

        private static void CopyExistingRandomIds(Quest originalQuest, Quest quest)
        {
            originalQuest.Rewards.TryGetValue("Started", out var originalStartedRewards);
            quest.Rewards.TryGetValue("Started", out var newStartedRewards);
            CopyRewardRandomIds(originalStartedRewards, newStartedRewards);

            originalQuest.Rewards.TryGetValue("Success", out var originalSuccessRewards);
            quest.Rewards.TryGetValue("Success", out var newSuccessRewards);
            CopyRewardRandomIds(originalSuccessRewards, newSuccessRewards);

            originalQuest.Rewards.TryGetValue("Fail", out var originalFailRewards);
            quest.Rewards.TryGetValue("Fail", out var newFailRewards);
            CopyRewardRandomIds(originalFailRewards, newFailRewards);

            CopyConditionRandomIds(originalQuest.Conditions.AvailableForStart, quest.Conditions.AvailableForStart);
        }

        private static void CopyRewardRandomIds(List<Reward> originalRewards, List<Reward> rewards)
        {
            foreach (var reward in rewards)
            {
                var originalReward = originalRewards.FirstOrDefault(x => x.Id == reward.Id);
                if (originalReward == null)
                {
                    LoggingHelpers.LogWarning($"Unable to find matching original reward for: {reward.Id}. Skipping.");
                    continue;
                }

                reward.Target = originalReward.Target;

                if (reward.Items != null)
                {
                    foreach (var item in reward.Items)
                    {
                        var originalItem = originalReward.Items.FirstOrDefault(x => x.Template == item.Template && x.SlotId == item.SlotId);
                        if (originalItem == null)
                        {
                            LoggingHelpers.LogWarning(
                                $"Unable to find matching original reward item for {reward.Id}-{item.Template}. Skipping"
                            );
                            continue;
                        }

                        item.Id = originalItem.Id;
                        item.ParentId = originalItem.ParentId;
                    }

                    // Above changes can cause the target and first items id to become mismatched
                    if (reward.Items.FirstOrDefault().Id != reward.Target)
                    {
                        reward.Target = reward.Items.FirstOrDefault().Id;
                    }
                }
            }
        }

        // Allow stripping all whitespace in a string, used for comparing _props.target, which may have differing whitespace but still match
        private static readonly Regex whitespace = new(@"\s+");

        private static string StripAllWhitespace(string input)
        {
            if (input == null)
            {
                return "";
            }

            return whitespace.Replace(input, "");
        }

        private static void CopyConditionRandomIds(List<QuestCondition> originalConditions, List<QuestCondition> conditions)
        {
            foreach (var condition in conditions)
            {
                var originalCondition = originalConditions.FirstOrDefault(x =>
                    x.ConditionType == condition.ConditionType
                    && x.Index == condition.Index
                    && StripAllWhitespace(x.Target?.ToString()) == StripAllWhitespace(condition.Target?.ToString())
                    && x.Counter?.Id == condition.Counter?.Id
                );

                if (originalCondition == null)
                {
                    LoggingHelpers.LogWarning(
                        $"Unable to find matching original condition for {condition.ConditionType}-{StripAllWhitespace(condition.Target?.ToString())}. Skipping."
                    );
                    continue;
                }

                condition.Id = originalCondition.Id;
            }
        }

        /// <summary>
        /// Get a bsg happy guid, must be 24 chars long
        /// </summary>
        /// <param name="randomSalt"></param>
        /// <returns></returns>
        static string Sha256(string randomSalt)
        {
            var crypt = new System.Security.Cryptography.SHA256Managed();
            var hash = new System.Text.StringBuilder();
            byte[] crypto = crypt.ComputeHash(Encoding.UTF8.GetBytes(randomSalt));
            foreach (byte theByte in crypto)
            {
                hash.Append(theByte.ToString("x2"));
            }
            return hash.ToString().Substring(0, 24);
        }

        /// <summary>
        /// Loop over live quests and use if it exists, otherwise use existing data
        /// </summary>
        private static List<Quest> GetMissingQuestsNotInLiveFile(
            Dictionary<MongoId, Quest> existingQuests,
            List<Quest> liveQuestData,
            HashSet<string> blacklistedQuests
        )
        {
            var missingQuestsToReturn = new List<Quest>();
            foreach (var quest in existingQuests.Values)
            {
                var liveQuest = liveQuestData.Find(x => x.Id == quest.Id);
                if (liveQuest is null)
                {
                    if (blacklistedQuests?.Contains(quest.Id) ?? false)
                    {
                        LoggingHelpers.LogInfo($"Skipping quest: {quest.Name}");
                        continue;
                    }

                    missingQuestsToReturn.Add(quest);
                    LoggingHelpers.LogError(
                        $"ERROR Quest {quest.Id} {QuestHelper.GetQuestNameById(quest.Id)} missing in live file. Will use fallback quests.json"
                    );
                }
                else
                {
                    LoggingHelpers.LogSuccess($"SUCCESS Quest {quest.Id} {QuestHelper.GetQuestNameById(quest.Id)} found in live file.");
                }
            }

            return missingQuestsToReturn;
        }

        private static int GetNextIndex(int? previousIndex)
        {
            if (previousIndex == null)
            {
                return 0;
            }

            return previousIndex.Value + 1;
        }
    }
}
