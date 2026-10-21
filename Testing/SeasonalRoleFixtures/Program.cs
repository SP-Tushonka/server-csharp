using System.Reflection;
using Path = System.IO.Path;
using System.Runtime.CompilerServices;
using System.Text.Json;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Json;
using SPTarkov.Server.Core.Services.Server;
using SPTarkov.Server.Core.Services.Locales;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Common.Models.Logging;
var checks=0;void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
var baseline=args.Contains("--baseline");
var bots=Activator.CreateInstance<BotTable>();typeof(BotTable).GetProperty("Types")!.SetValue(bots,new Dictionary<string,BotType?>());
BotType Bot(double chance=22){return new BotType{BotChances=new Chances{EquipmentChances=Enum.GetNames<EquipmentSlots>().ToDictionary(x=>x,_=>chance)},BotInventory=new BotTypeInventory{Equipment=Enum.GetValues<EquipmentSlots>().ToDictionary(x=>x,_=>new Dictionary<MongoId,double>()),Items=new ItemPools{Backpack=[],Pockets=[],SecuredContainer=[],SpecialLoot=[],TacticalVest=[]}}};}
foreach(var role in new[]{"pmcbear","pmcusec","assault","bosstagilla"})bots.Types[role]=Bot(role=="bosstagilla"?90:22);
var config=Activator.CreateInstance<SeasonalEventConfig>();config.EventGear=new();config.EventLoot=new();
var item=new MongoId("5df8a6a186f77412640e2e80");var other=new MongoId("5df8a72c86f77412640e2e83");
Dictionary<string,Dictionary<MongoId,int>> Gear(int weight)=>new(){{"Headwear",new(){{item,weight}}}};
config.EventGear[SeasonalEventType.Halloween]=new(){{"BEAR",Gear(40)},{"uSeC",Gear(50)},{"assault",Gear(60)},{"bossTagilla",Gear(70)}};
var evidence=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"actual-event-config-evidence.json")));
Dictionary<string,Dictionary<MongoId,int>> Loot(JsonElement value)=>value.EnumerateObject().ToDictionary(x=>x.Name,x=>x.Value.EnumerateObject().ToDictionary(y=>new MongoId(y.Name),y=>y.Value.GetInt32()));
config.EventLoot[SeasonalEventType.Christmas]=new(){{"assault",Loot(evidence.RootElement.GetProperty("christmasLoot").GetProperty("assault"))}};
var service=(SeasonalEventService)RuntimeHelpers.GetUninitializedObject(typeof(SeasonalEventService));
void Field(Type type,object value){typeof(SeasonalEventService).GetFields(BindingFlags.Instance|BindingFlags.NonPublic).Single(x=>x.FieldType==type).SetValue(service,value);}
Field(typeof(BotTable),bots);Field(typeof(SeasonalEventConfig),config);Field(typeof(ISptLogger<SeasonalEventService>),DispatchProxy.Create<ISptLogger<SeasonalEventService>,Log>());
var locale=(ServerLocalisationService)RuntimeHelpers.GetUninitializedObject(typeof(ServerLocalisationService));var lt=typeof(ServerLocalisationService).BaseType!;lt.GetProperty("LocalesHydrated")!.SetValue(locale,true);lt.GetProperty("ServerLocale")!.SetValue(locale,"en");var loaded=lt.GetProperty("LoadedLocales",BindingFlags.NonPublic|BindingFlags.Instance)!;loaded.SetValue(locale,Activator.CreateInstance(loaded.PropertyType));Field(typeof(ServerLocalisationService),locale);
void Invoke(string name,SeasonalEventType type){try{typeof(SeasonalEventService).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(service,[type]);}catch(TargetInvocationException e){throw e.InnerException!;}}
if(baseline){var failed=false;try{Invoke("AddEventGearToBots",SeasonalEventType.Halloween);}catch(KeyNotFoundException){failed=true;}Check(failed,"Expected actual missing legacy bear crash");Invoke("AddEventLootToBots",SeasonalEventType.Christmas);Check(bots.Types["assault"]!.BotInventory.Items.Backpack.Count==0,"Expected original discarded loot map bug");Console.WriteLine(JsonSerializer.Serialize(new{checks,baselineReproduced=true,actualCompiledMethods=true,gameOrBackendStarted=false}));return;}
config.EventGear[SeasonalEventType.Halloween]["unknownOptionalRole"]=Gear(80);bots.Types["knownnull"]=null;config.EventGear[SeasonalEventType.Halloween]["knownnull"]=Gear(81);
Invoke("AddEventGearToBots",SeasonalEventType.Halloween);
foreach(var row in new[]{("pmcbear",40),("pmcusec",50),("assault",60),("bosstagilla",70)}){var b=bots.Types[row.Item1]!;Check(b.BotInventory.Equipment[EquipmentSlots.Headwear][item]==row.Item2,"Configured gear weight lost: "+row.Item1);Check(b.BotChances.EquipmentChances["Headwear"]==(row.Item1=="bosstagilla"?90:75),"Chance floor/clamp changed");}
Check(!bots.Types.ContainsKey("bear")&&!bots.Types.ContainsKey("usec"),"Legacy role created");Check(Log.Warnings==2,"Optional/missing roles did not warn");
var literal=Bot();bots.Types["bear"]=literal;config.EventGear[SeasonalEventType.Christmas]=new(){{"BeAr",new(){{"Headwear",new(){{other,91}}}}}};Invoke("AddEventGearToBots",SeasonalEventType.Christmas);Check(literal.BotInventory.Equipment[EquipmentSlots.Headwear][other]==91,"Literal role collision lost priority");Check(!bots.Types["pmcbear"]!.BotInventory.Equipment[EquipmentSlots.Headwear].ContainsKey(other),"Alias overrode explicit installed role");bots.Types.Remove("bear");
Invoke("AddEventLootToBots",SeasonalEventType.Christmas);
var pools=bots.Types["assault"]!.BotInventory.Items;var properties=typeof(ItemPools).GetProperties();foreach(var slot in config.EventLoot[SeasonalEventType.Christmas]["assault"]){var pool=(Dictionary<MongoId,double>)properties.Single(x=>x.Name==slot.Key).GetValue(pools)!;foreach(var row in slot.Value)Check(pool[row.Key]==row.Value,"Real configured Christmas pool/weight absent");}
Check(pools.SecuredContainer.Count==0&&pools.SpecialLoot.Count==0,"Unconfigured pools changed");
config.EventLoot[SeasonalEventType.Halloween]=new(){{"BEAR",new(){{"Pockets",new(){{other,17}}}}},{"uSeC",new(){{"Backpack",new(){{other,18}}}}},{"bossTagilla",new(){{"TacticalVest",new(){{other,19}}}}},{"unknownOptionalRole",new(){{"Pockets",new(){{other,20}}}}}};
Invoke("AddEventLootToBots",SeasonalEventType.Halloween);Check(bots.Types["pmcbear"]!.BotInventory.Items.Pockets[other]==17,"BEAR loot alias failed");Check(bots.Types["pmcusec"]!.BotInventory.Items.Backpack[other]==18,"USEC loot alias failed");Check(bots.Types["bosstagilla"]!.BotInventory.Items.TacticalVest[other]==19,"Normal mixed-case boss loot failed");Check(Log.Warnings==3,"Unknown loot role did not warn");
config.EventLoot[SeasonalEventType.Halloween]=new(){{"assault",new(){{"InventedPool",new(){{other,1}}}}}};var refused=false;try{Invoke("AddEventLootToBots",SeasonalEventType.Halloween);}catch(InvalidOperationException){refused=true;}Check(refused,"Unsupported pool silently accepted");Check(!bots.Types.ContainsKey("bear")&&!bots.Types.ContainsKey("usec"),"Loot created legacy roles");
// Replay every configured role/slot/item using the installed public JSON catalog.
bots.Types.Clear();var botSource=args.SkipWhile(x=>x!="--bot-source").Skip(1).First();var json=new JsonUtil(new IJsonConverterRegistrator[]{new SptJsonConverterRegistrator()});
var requiredRoles=evidence.RootElement.GetProperty("eventGear").EnumerateObject().SelectMany(e=>e.Value.EnumerateObject().Select(x=>x.Name.ToLowerInvariant())).Select(x=>x=="bear"?"pmcbear":x=="usec"?"pmcusec":x).Distinct().ToArray();
var botInputHashes=new Dictionary<string,string>();foreach(var role in requiredRoles){var path=Path.Combine(botSource,role+".json");var raw=File.ReadAllText(path);bots.Types[role]=json.Deserialize<BotType>(raw)!;botInputHashes[role]=Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));}
foreach(var eventRow in evidence.RootElement.GetProperty("eventGear").EnumerateObject()){
 var kind=Enum.Parse<SeasonalEventType>(eventRow.Name,true);config.EventGear[kind]=eventRow.Value.EnumerateObject().ToDictionary(x=>x.Name,x=>Loot(x.Value));Invoke("AddEventGearToBots",kind);
 foreach(var roleRow in config.EventGear[kind]){var name=roleRow.Key.ToLowerInvariant();if(!bots.Types.ContainsKey(name))name=name=="bear"?"pmcbear":name=="usec"?"pmcusec":name;
  Check(bots.Types.TryGetValue(name,out var bot)&&bot is not null,"Installed configured role absent: "+name);
  foreach(var slot in roleRow.Value){Check(bot!.BotChances.EquipmentChances[slot.Key]>=75,"Installed chance floor lost");var pool=bot.BotInventory.Equipment[Enum.Parse<EquipmentSlots>(slot.Key)];foreach(var row in slot.Value)Check(pool[row.Key]==row.Value,"Installed seasonal gear weight lost");}
 }
}
Check(!bots.Types.ContainsKey("bear")&&!bots.Types.ContainsKey("usec"),"Installed catalog created legacy roles");
Console.WriteLine(JsonSerializer.Serialize(new{checks,success=true,actualCompiledMethods=true,actualChristmasPoolValues=true,actualBotJsonCount=botInputHashes.Count,botInputHashes,gameOrBackendStarted=false,profilesUsed=false}));
public class Log:DispatchProxy{public static int Warnings;protected override object? Invoke(MethodInfo? method,object?[]? args){if(method!.Name=="Warning")Warnings++;var t=method.ReturnType;return t==typeof(void)?null:t.IsValueType?Activator.CreateInstance(t):null;}}
