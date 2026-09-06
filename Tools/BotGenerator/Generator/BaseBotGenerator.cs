using Common.Extensions;
using Common.Models;
using Common.Models.Input;
using Generator.Helpers;
using Generator.Helpers.Gear;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using BotType = Common.Models.BotType;

namespace Generator
{
    public static class BaseBotGenerator
    {
        public static void UpdateBaseDetails(GeneratedBot botData, Datum rawBotData)
        {
            UpdateBodyPartHealth(botData, rawBotData);
            AddExperience(botData, rawBotData);
            AddStandingForKill(botData, rawBotData);
            AddAggressorBonus(botData, rawBotData);
            AddSkills(botData, rawBotData);
            botData.Data.BotExperience.UseSimpleAnimator = rawBotData.Info.Settings.UseSimpleAnimator;

            AddVisualAppearanceItems(botData, rawBotData);
            AddName(botData, rawBotData);
            AddVoice(botData, rawBotData);
        }

        private static void AddSkills(GeneratedBot botToUpdate, Datum rawBotData)
        {
            var skills = botToUpdate.Data.BotSkills.Common;

            // Find the smallest and biggest value for each skill
            foreach (var skill in rawBotData.Skills.Common)
            {
                if (skills.TryGetValue(skill.Id, out var existingSkill))
                {
                    existingSkill.Min = Math.Min(existingSkill.Min, skill.Progress);
                    existingSkill.Max = Math.Max(existingSkill.Max, skill.Progress);
                }
                else
                {
                    skills.Add(skill.Id, new MinMax<double>(skill.Progress, skill.Progress));
                }
            }
        }

        private static void AddStandingForKill(GeneratedBot botToUpdate, Datum rawBotData)
        {
            var standingForKill = botToUpdate.Data.BotExperience.StandingForKill;

            if (!standingForKill.ContainsKey(rawBotData.Info.Settings.BotDifficulty))
            {
                standingForKill.Add(rawBotData.Info.Settings.BotDifficulty, rawBotData.Info.Settings.StandingForKill);
            }
        }

        private static void AddAggressorBonus(GeneratedBot botToUpdate, Datum rawBotData)
        {
            var aggressorBonus = botToUpdate.Data.BotExperience.AggressorBonus;

            if (!aggressorBonus.ContainsKey(rawBotData.Info.Settings.BotDifficulty))
            {
                aggressorBonus.Add(rawBotData.Info.Settings.BotDifficulty, rawBotData.Info.Settings.AggressorBonus);
            }
        }

        private static void AddExperience(GeneratedBot botToUpdate, Datum rawBotData)
        {
            var reward = botToUpdate.Data.BotExperience.Reward;

            reward.TryGetValue(rawBotData.Info.Settings.BotDifficulty, out var minMaxValues);
            if (minMaxValues is null)
            {
                reward.Add(
                    rawBotData.Info.Settings.BotDifficulty,
                    new MinMax<int>(rawBotData.Info.Settings.Experience, rawBotData.Info.Settings.Experience)
                );

                return;
            }

            minMaxValues.Min = Math.Min(minMaxValues.Min, rawBotData.Info.Settings.Experience);
            minMaxValues.Max = Math.Max(minMaxValues.Max, rawBotData.Info.Settings.Experience);
        }

        private static void AddVoice(GeneratedBot bot, Datum rawBot)
        {
            GearHelpers.IncrementDictionaryValue(bot.Data.BotAppearance.Voice, rawBot.Customization.Voice);
        }

        public static async Task AddDifficulties(GeneratedBot bot)
        {
            string workingPath = Directory.GetCurrentDirectory();
            string botType = bot.Role.ToString();
            var botDifficultyFiles = Directory
                .GetFiles($"{workingPath}//Assets", "*.txt", SearchOption.TopDirectoryOnly)
                .Where(x => x.Contains(botType, StringComparison.InvariantCultureIgnoreCase))
                .ToList();

            await DifficultyHelper.AddDifficultySettings(bot, botDifficultyFiles);
        }

        private static void UpdateBodyPartHealth(GeneratedBot botToUpdate, Datum rawBot)
        {
            var bodyPartHpToAdd = new BodyPart
            {
                Head = new MinMax<double>(rawBot.Health.BodyParts.Head.Health.Current, rawBot.Health.BodyParts.Head.Health.Maximum),
                Chest = new MinMax<double>(rawBot.Health.BodyParts.Chest.Health.Current, rawBot.Health.BodyParts.Chest.Health.Maximum),
                Stomach = new MinMax<double>(
                    rawBot.Health.BodyParts.Stomach.Health.Current,
                    rawBot.Health.BodyParts.Stomach.Health.Maximum
                ),
                LeftArm = new MinMax<double>(
                    rawBot.Health.BodyParts.LeftArm.Health.Current,
                    rawBot.Health.BodyParts.LeftArm.Health.Maximum
                ),
                RightArm = new MinMax<double>(
                    rawBot.Health.BodyParts.RightArm.Health.Current,
                    rawBot.Health.BodyParts.RightArm.Health.Maximum
                ),
                LeftLeg = new MinMax<double>(
                    rawBot.Health.BodyParts.LeftLeg.Health.Current,
                    rawBot.Health.BodyParts.LeftLeg.Health.Maximum
                ),
                RightLeg = new MinMax<double>(
                    rawBot.Health.BodyParts.RightLeg.Health.Current,
                    rawBot.Health.BodyParts.RightLeg.Health.Maximum
                ),
            };

            // Record equality compares the MinMax values
            if (!botToUpdate.BodyParts.Contains(bodyPartHpToAdd))
            {
                botToUpdate.BodyParts.Add(bodyPartHpToAdd);
            }
        }

        private static void AddVisualAppearanceItems(GeneratedBot botToUpdate, Datum rawBot)
        {
            var appearance = botToUpdate.Data.BotAppearance;
            GearHelpers.IncrementDictionaryValue(appearance.Feet, rawBot.Customization.Feet);

            GearHelpers.IncrementDictionaryValue(appearance.Body, rawBot.Customization.Body);

            GearHelpers.IncrementDictionaryValue(appearance.Head, rawBot.Customization.Head);

            GearHelpers.IncrementDictionaryValue(appearance.Hands, rawBot.Customization.Hands);
        }

        private static void AddName(GeneratedBot botToUpdate, Datum rawBot)
        {
            var name = rawBot.Info.Nickname.Split();
            botToUpdate.Data.FirstNames.AddUnique(name[0]);
            if (name.Length > 1)
            {
                // Add lastnames to all bots except raiders
                if (botToUpdate.Role != BotType.pmcbot)
                {
                    botToUpdate.LastNames.AddUnique(name[1]);
                }
            }
        }
    }
}
