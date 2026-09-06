using System.Diagnostics;
using Common.Models;
using Common.Models.Input;
using Generator.Helpers.Gear;
using Generator.Weighting;

namespace Generator
{
    public static class BotChancesGenerator
    {
        public static void AddChances(GeneratedBot botToUpdate, Datum rawBot)
        {
            var weightHelper = new WeightingService();

            // TODO: Add check to make sure incoming bot list has gear
            GearChanceHelpers.AddEquipmentChances(botToUpdate, rawBot);
            GearChanceHelpers.AddGenerationChances(botToUpdate, weightHelper);
            GearChanceHelpers.AddModChances(botToUpdate, rawBot);
            GearChanceHelpers.AddEquipmentModChances(botToUpdate, rawBot);
        }
    }
}
