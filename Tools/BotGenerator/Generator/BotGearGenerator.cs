using System.Diagnostics;
using Common.Models;
using Common.Models.Input;
using Generator.Helpers.Gear;

namespace Generator
{
    public static class BotGearGenerator
    {
        public static void AddGear(GeneratedBot botToUpdate, Datum rawBotData)
        {
            GearHelpers.AddEquippedGear(botToUpdate, rawBotData);
            GearHelpers.AddAmmo(botToUpdate, rawBotData);
            GearHelpers.AddEquippedMods(botToUpdate, rawBotData);
            //GearHelpers.AddCartridges(botToUpdate, rawBotData);
        }
    }
}
