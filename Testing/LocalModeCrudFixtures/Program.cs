using System.Reflection;
using System.Text.Json;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Json;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Services.Profile;
using SPTarkov.Server.Core.Services.Commerce;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Eft.Launcher;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Common.Models.Logging;
var checks=0;var observations=new Dictionary<string,object?>();
void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
void Refuse(Action action,string message){var refused=false;try{action();}catch(InvalidOperationException){refused=true;}Check(refused,message);}
async Task RefuseAsync(Func<Task> action,string message){var refused=false;try{await action();}catch(IOException){refused=true;}Check(refused,message);}
object SetNew(object obj,string name){var p=obj.GetType().GetProperty(name)!;var value=Activator.CreateInstance(p.PropertyType)!;p.SetValue(obj,value);return value;}
var previous=Directory.GetCurrentDirectory();var scratch=Directory.CreateTempSubdirectory("runtime-crud-fixture-").FullName;
Directory.SetCurrentDirectory(scratch);
try {
 var json=new JsonUtil(new IJsonConverterRegistrator[]{new SptJsonConverterRegistrator()});
 var config=Activator.CreateInstance<CoreConfig>();config.Features=Activator.CreateInstance<ServerFeatures>();config.Features.CompressProfile=true;config.Features.CreateNewProfileTypesBlacklist=[];config.AllowProfileWipe=true;
 var hash=new HashUtil(new RandomUtil(null!,null!));
 var saves=new SaveServer(new FileUtil(),[],json,hash,null!,null!,DispatchProxy.Create<ISptLogger<SaveServer>,NullLog>(),config);
 Directory.CreateDirectory("user/profiles");Directory.CreateDirectory("user/local-modes");File.WriteAllText("user/local-modes/enabled.json","{\"enabled\":true}");
 var modes=new LocalGameModeService(saves);
 var global=Activator.CreateInstance<GlobalTable>();var cfg=SetNew(global,"Configuration");var exp=SetNew(cfg,"Exp");var level=SetNew(exp,"Level");var ep=level.GetType().GetProperty("ExperienceTable")!;
 var arr=Array.CreateInstance(ep.PropertyType.GetElementType()!,4);for(var i=0;i<4;i++){var e=Activator.CreateInstance(ep.PropertyType.GetElementType()!)!;e.GetType().GetProperty("Experience")!.SetValue(e,100);arr.SetValue(e,i);}ep.SetValue(level,arr);
 var helper=new ProfileHelper(null!,null!,global,null!,saves,null!,null!,null!,null!,modes);
 var profiles=new ProfileController(null!,saves,null!,null!,helper,null!,null!,null!,null!,modes);
 var launcher=new LauncherV2Controller([],[],null!,hash,saves,null!,config,null!,profiles);
 var regA=new RegisterData{Username="SyntheticAccountA",Edition="standard"};var regB=new RegisterData{Username="SyntheticAccountB",Edition="standard"};
 Check(await launcher.Register(regA),"Canonical register A failed");Check(await launcher.Register(regB),"Canonical register B failed");Check(!await launcher.Register(regA),"Duplicate account registered");
 Check(launcher.Login(regA)&&launcher.Login(regB),"Canonical login failed");
 var a=saves.GetProfiles().Single(x=>x.Value.ProfileInfo!.Username==regA.Username).Key;var b=saves.GetProfiles().Single(x=>x.Value.ProfileInfo!.Username==regB.Username).Key;
 foreach(var profile in saves.GetProfiles().Values){var pmc=profile.CharacterData!.PmcData!;SetNew(pmc,"Info").GetType().GetProperty("Level")!.SetValue(pmc.Info,1);profile.SptData=new();profile.ProfileInfo!.IsWiped=false;}
 var root=saves.GetProfile(a);root.CharacterData!.PmcData!.TarCoinBalance=1234;root.CharacterData.PmcData.BattlePassUniversalDocumentBalance=77;root.PurchasedShopOffers=[];root.PurchasedShopStashRows=4;await saves.SaveProfileAsync(a);
 var regular=await modes.SelectAsync(a,"regular");var season=await modes.SelectAsync(a,"pvp-season");var child=new MongoId(regular.ProfileId);var seasonId=new MongoId(season.ProfileId);
 Check(profiles.GetMiniProfiles().Count==2,"Canonical account list exposed children");
 Check(saves.GetProfile(child).ProfileInfo!.LocalModeOwnerId==a&&saves.GetProfile(seasonId).ProfileInfo!.LocalMode=="pvp-season","Child ownership metadata missing");
 observations["internalChildCanLogin"]=launcher.Login(new LoginRequestData{Username=saves.GetProfile(child).ProfileInfo!.Username});
 Check(!(bool)observations["internalChildCanLogin"]!,"Internal child login accepted");
 Check(!launcher.Wipe(new RegisterData{Username=saves.GetProfile(child).ProfileInfo!.Username,Edition="standard"}),"Internal child wipe accepted");
 Check(!saves.RemoveProfile(child),"Direct internal child deletion accepted");
 var beforeChild=File.ReadAllBytes($"user/profiles/{seasonId}.json");
 Check(launcher.Wipe(regA),"Canonical root wipe marking failed");Check(root.ProfileInfo!.IsWiped==true,"Root wipe not marked");Check(root.CharacterData.PmcData.TarCoinBalance==1234,"Wipe marker changed wallet");Check(File.ReadAllBytes($"user/profiles/{seasonId}.json").SequenceEqual(beforeChild),"Root wipe touched child");
 var replacement=new SPTarkov.Server.Core.Models.Eft.Profile.SptProfile{ProfileInfo=root.ProfileInfo,CharacterData=new Characters{PmcData=new PmcData{TarCoinBalance=0},ScavData=new PmcData()}};
 var store=(TarcoinStoreService)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(TarcoinStoreService));store.CarryOverPurchases(root,replacement);
 Check(replacement.CharacterData.PmcData.TarCoinBalance==1234&&replacement.CharacterData.PmcData.BattlePassUniversalDocumentBalance==77&&replacement.PurchasedShopStashRows==4,"Canonical wipe commerce carry lost account data");
 var internalName=saves.GetProfile(child).ProfileInfo!.Username;
 observations["internalChildCanDeleteViaLauncher"]=launcher.Remove(new LoginRequestData{Username=internalName});
 Check(!(bool)observations["internalChildCanDeleteViaLauncher"]!,"Internal child removal accepted");
 observations["deletedChildIndexStillReferenced"]=modes.ProfileForMode(a,"regular")==child&&!saves.ProfileExists(child);
 Check(launcher.Remove(regA),"Canonical account deletion failed");Check(!saves.ProfileExists(a)&&!File.Exists($"user/profiles/{a}.json"),"Root not deleted");
 observations["orphanChildRetained"]=saves.ProfileExists(seasonId)&&File.Exists($"user/profiles/{seasonId}.json");
 var index=File.ReadAllText("user/local-modes/index.json");observations["deletedAccountStillInIndex"]=index.Contains(a.ToString());
 var fresh=new LocalGameModeService(saves);string? failure=null;try{_ =fresh.ProfileForMode(b,"regular");}catch(InvalidOperationException e){failure=e.Message;}
 observations["remainingAccountFreshModeError"]=failure;Check(launcher.Login(regB),"Deletion broke canonical other account login");
 Check(failure is null,"Fresh remaining account mode lookup failed after deletion");
 Check(!(bool)observations["orphanChildRetained"]!&&!(bool)observations["deletedAccountStillInIndex"]!,"Account deletion left child/index orphan");
 Check(!saves.ProfileExists(child)&&!saves.ProfileExists(seasonId)&&!File.Exists($"user/profiles/{child}.json")&&!File.Exists($"user/profiles/{seasonId}.json"),"Owned account children not removed");
 var bRegular=new MongoId((await modes.SelectAsync(b,"regular")).ProfileId);
 Check(!File.ReadAllText("user/local-modes/index.json").Contains(a.ToString()),"Cached service reintroduced deleted account index");
 var rootB=saves.GetProfile(b);var bRaw=File.ReadAllBytes($"user/profiles/{b}.json");var validIndex=File.ReadAllText("user/local-modes/index.json");
 Check(saves.DeleteProfileById(b),"Memory-only wipe removal failed");saves.AddProfile(rootB);
 Check(File.ReadAllBytes($"user/profiles/{b}.json").SequenceEqual(bRaw)&&File.ReadAllText("user/local-modes/index.json")==validIndex,"Memory-only wipe removal changed disk/index");
 foreach(var bad in new[]{"{\"../escape\":{}}","{\""+b+"\":{\"regular\":\""+a+"\"}}","{\""+b+"\":{\"regular\":\""+bRegular+"\",\"pvp-season\":\""+bRegular+"\"}}","{\""+b+"\":{},\""+b+"\":{}}"}) {
  File.WriteAllText("user/local-modes/index.json",bad);Refuse(()=>launcher.Remove(regB),"Malformed/unowned index did not refuse");
  Check(File.ReadAllBytes($"user/profiles/{b}.json").SequenceEqual(bRaw)&&File.ReadAllText("user/local-modes/index.json")==bad,"Refused deletion mutated profile/index");
 }
 File.WriteAllText("user/local-modes/index.json",validIndex);
 // Reject an index pointing to an existing but unowned root, not just a missing ID.
 var ownerBefore=saves.GetProfile(bRegular).ProfileInfo!.LocalModeOwnerId;
 saves.GetProfile(bRegular).ProfileInfo!.LocalModeOwnerId=a;
 Refuse(()=>launcher.Remove(regB),"Mismatched loaded ownership accepted");saves.GetProfile(bRegular).ProfileInfo!.LocalModeOwnerId=ownerBefore;
 var childBRaw=File.ReadAllBytes($"user/profiles/{bRegular}.json");
 var childBNode=System.Text.Json.Nodes.JsonNode.Parse(childBRaw)!;childBNode["info"]!["localModeOwnerId"]=a.ToString();File.WriteAllText($"user/profiles/{bRegular}.json",childBNode.ToJsonString());
 Refuse(()=>launcher.Remove(regB),"Mismatched disk ownership accepted");Check(File.ReadAllBytes($"user/profiles/{b}.json").SequenceEqual(bRaw),"Disk ownership refusal changed root");File.WriteAllBytes($"user/profiles/{bRegular}.json",childBRaw);
 // Force the old unfiltered username lookup to encounter the child first.
 var collisionChild=saves.GetProfile(bRegular);collisionChild.ProfileInfo!.IsWiped=false;
 MongoId? collisionRoot=null;
 for(var attempt=0;attempt<128&&collisionRoot is null;attempt++) {
  var id=new MongoId();saves.CreateProfile(new SPTarkov.Server.Core.Models.Eft.Profile.Info{ProfileId=id,ScavengerId=new MongoId(),Aid=777,Username=collisionChild.ProfileInfo.Username,Edition="standard",IsWiped=false});
  // Reinsert the fixture-owned child so same-bucket ordering also exercises the collision.
  saves.DeleteProfileById(bRegular);saves.AddProfile(collisionChild);
  if(saves.GetProfiles().First(x=>x.Value.ProfileInfo!.Username==collisionChild.ProfileInfo.Username).Key==bRegular)collisionRoot=id;
  else saves.DeleteProfileById(id);
 }
 Check(collisionRoot is not null,"Could not arrange explicit child-first username collision");
 var childEdition=collisionChild.ProfileInfo.Edition;
 Check(launcher.Wipe(new RegisterData{Username=collisionChild.ProfileInfo.Username,Edition="unheard_edition"}),"Collision root wipe refused");
 Check(saves.GetProfile(collisionRoot!.Value).ProfileInfo!.IsWiped==true&&saves.GetProfile(collisionRoot.Value).ProfileInfo!.Edition=="unheard_edition","Wipe missed authenticated collision root");
 Check(collisionChild.ProfileInfo.IsWiped==false&&collisionChild.ProfileInfo.Edition==childEdition,"Wipe mutated child with same username");saves.DeleteProfileById(collisionRoot.Value);
 // Interrupted first provisioning may leave an owned child before index publication.
 var orphan=new MongoId();saves.CreateProfile(new SPTarkov.Server.Core.Models.Eft.Profile.Info{ProfileId=orphan,ScavengerId=new MongoId(),Aid=rootB.ProfileInfo!.Aid,Username="SyntheticUnindexedChild",Edition="standard",LocalModeOwnerId=b,LocalMode="pvp-season"});await saves.SaveProfileAsync(orphan);
 foreach(var orphanIndex in new string?[]{null,"{}",validIndex}) {
  if(orphanIndex is null)File.Delete("user/local-modes/index.json");else File.WriteAllText("user/local-modes/index.json",orphanIndex);
  Refuse(()=>launcher.Remove(regB),"Unindexed owned child did not protect root");
  Check(saves.ProfileExists(b)&&saves.ProfileExists(orphan)&&File.ReadAllBytes($"user/profiles/{b}.json").SequenceEqual(bRaw),"Orphan refusal deleted data");
 }
 // Fixture-only removal of its deliberately injected orphan, never a public server action.
 saves.DeleteProfileById(orphan);File.Delete($"user/profiles/{orphan}.json");File.WriteAllText("user/local-modes/index.json",validIndex);
 var regC=new RegisterData{Username="SyntheticAccountC",Edition="standard"};Check(await launcher.Register(regC),"Register C failed");var c=saves.GetProfiles().Single(x=>x.Value.ProfileInfo!.Username==regC.Username).Key;
 var cChild=new MongoId((await modes.SelectAsync(c,"regular")).ProfileId);var childPath=$"user/profiles/{cChild}.json";var childRaw=File.ReadAllBytes(childPath);
 await using(var pendingSave=await saves.BeginLocalProfileTransactionAsync(new[]{c}))
 using(var locked=new FileStream(childPath,FileMode.Open,FileAccess.Read,FileShare.Read)) {
  using var started=new ManualResetEventSlim();
  var removal=Task.Run(()=>{started.Set();try{launcher.Remove(regC);return false;}catch(IOException){return true;}});
  Check(started.Wait(TimeSpan.FromSeconds(2)),"Deletion worker did not start");await Task.Delay(50);
  Check(!removal.IsCompleted&&File.Exists($"user/profiles/{c}.json"),"Deletion bypassed pending profile-save locks");
  await pendingSave.CommitAsync();
  Check(await removal,"Real filesystem delete denial did not surface");
  Check(!File.Exists($"user/profiles/{c}.json")&&File.Exists(childPath),"Expected partial root deletion not reached");
  Check(!saves.ProfileExists(c)&&!saves.ProfileExists(cChild),"Committed failed delete can be rewritten from memory");
  var staleSaveRefused=false;try{await saves.SaveProfileAsync(c);}catch(Exception){staleSaveRefused=true;}
  Check(staleSaveRefused&&!File.Exists($"user/profiles/{c}.json"),"Stale background save recreated deleted root");
  Check(Directory.GetFiles("user/local-modes/account-deletions","*.json").Length==1,"Recovery intent missing");
  await RefuseAsync(()=>modes.SelectAsync(b,"pvp-season"),"Index writer bypassed unfinished deletion");
 }
 var pendingIndex=File.ReadAllText("user/local-modes/index.json");File.WriteAllText("user/local-modes/index.json",pendingIndex+" ");
 Refuse(()=>new LocalModeAccountDeletion().Recover(),"Changed index overwritten during recovery");Check(File.ReadAllBytes($"user/profiles/{b}.json").SequenceEqual(bRaw),"Index conflict recovery touched unrelated root");File.WriteAllText("user/local-modes/index.json",pendingIndex);
 File.AppendAllText(childPath," ");Refuse(()=>new LocalModeAccountDeletion().Recover(),"Changed child overwritten during recovery");Check(File.Exists(childPath),"Changed child removed");File.WriteAllBytes(childPath,childRaw);
 Check(new LocalModeAccountDeletion().Recover()==1,"Recovery did not finish partial delete");Check(!File.Exists(childPath),"Recovery left owned child");
 Check(new LocalModeAccountDeletion().Recover()==0,"Recovery was not idempotent");
 Check(new LocalGameModeService(saves).ProfileForMode(b,"regular")==bRegular,"Remaining account failed after recovery");
 Check(File.ReadAllBytes($"user/profiles/{b}.json").SequenceEqual(bRaw),"Recovery changed unrelated root");
 var badJournal="user/local-modes/account-deletions/invalid.json";File.WriteAllText(badJournal,"{\"Schema\":9}");Refuse(()=>new LocalModeAccountDeletion().Recover(),"Invalid journal accepted");Check(File.ReadAllBytes($"user/profiles/{b}.json").SequenceEqual(bRaw),"Invalid journal touched profile");File.Delete(badJournal);
 // Actual mode publication and explicit deletion share one index mutation gate.
 var regD=new RegisterData{Username="SyntheticAccountD",Edition="standard"};Check(await launcher.Register(regD),"Register D failed");
 var d=saves.GetProfiles().Single(x=>x.Value.ProfileInfo!.Username==regD.Username).Key;
 using var createStarted=new ManualResetEventSlim();using var deleteStarted=new ManualResetEventSlim();
 LocalModeAccountDeletion.MutationGate.Wait();
 Task<Runtime.LocalModes.LocalModeBinding>? publishing=null;Task<bool>? deleting=null;
 try {
  publishing=Task.Run(()=>{createStarted.Set();return modes.SelectAsync(b,"pvp-season");});
  deleting=Task.Run(()=>{deleteStarted.Set();return launcher.Remove(regD);});
  Check(createStarted.Wait(TimeSpan.FromSeconds(2))&&deleteStarted.Wait(TimeSpan.FromSeconds(2)),"Index workers did not start");await Task.Delay(50);
  Check(!publishing.IsCompleted&&!deleting.IsCompleted,"Index mutation gate was bypassed");
 } finally {LocalModeAccountDeletion.MutationGate.Release();}
 var published=await publishing!;Check(await deleting!,"Serialized ordinary root deletion failed");
 Check(!saves.ProfileExists(d)&&new LocalGameModeService(saves).ProfileForMode(b,"pvp-season")==new MongoId(published.ProfileId),"Concurrent mode publication lost account/index state");
 // Ordinary missing-file deletion never removes a root with indexed/claimed children.
 var compatIndex=File.ReadAllText("user/local-modes/index.json");File.Delete($"user/profiles/{b}.json");
 var missingRootRefused=false;try{launcher.Remove(regB);}catch(FileNotFoundException){missingRootRefused=true;}
 Check(missingRootRefused&&saves.ProfileExists(b)&&File.Exists($"user/profiles/{bRegular}.json"),"Missing root file bypassed indexed child validation");
 Check(File.ReadAllText("user/local-modes/index.json")==compatIndex,"Missing root file changed index");
 File.Delete("user/local-modes/index.json");Refuse(()=>launcher.Remove(regB),"Missing root and unindexed child used legacy fast path");
 var regAbsent=new RegisterData{Username="SyntheticAbsentFile",Edition="standard"};Check(await launcher.Register(regAbsent),"Register absent-file fixture failed");
 var absent=saves.GetProfiles().Single(x=>x.Value.ProfileInfo!.Username==regAbsent.Username).Key;File.Delete($"user/profiles/{absent}.json");
 Check(launcher.Remove(regAbsent)&&!saves.ProfileExists(absent),"Ordinary absent-file deletion semantics changed");
 File.WriteAllBytes($"user/profiles/{b}.json",bRaw);File.WriteAllText("user/local-modes/index.json",compatIndex);
 var regBom=new RegisterData{Username="SyntheticBOM",Edition="standard"};Check(await launcher.Register(regBom),"Register BOM fixture failed");
 var bomId=saves.GetProfiles().Single(x=>x.Value.ProfileInfo!.Username==regBom.Username).Key;var bomPath=$"user/profiles/{bomId}.json";
 File.WriteAllBytes(bomPath,new byte[]{0xEF,0xBB,0xBF}.Concat(File.ReadAllBytes(bomPath)).ToArray());
 Check((await json.DeserializeFromFileAsync<SptProfile>(bomPath))!.ProfileInfo!.ProfileId==bomId,"Canonical JSON loader rejected BOM fixture");
 Console.WriteLine("CANONICAL_BOM_LOAD_ACCEPTED");
 Check(launcher.Remove(regBom)&&!File.Exists(bomPath),"BOM account deletion failed");
 // Missing-index first provisioning cannot race through legacy root deletion.
 var savedIndex=File.ReadAllText("user/local-modes/index.json");File.Delete("user/local-modes/index.json");
 var regE=new RegisterData{Username="SyntheticAccountE",Edition="standard"};Check(await launcher.Register(regE),"Register E failed");
 var eRoot=saves.GetProfiles().Single(x=>x.Value.ProfileInfo!.Username==regE.Username).Key;
 using var firstCreateStarted=new ManualResetEventSlim();using var firstDeleteStarted=new ManualResetEventSlim();
 LocalModeAccountDeletion.MutationGate.Wait();Task<Runtime.LocalModes.LocalModeBinding>? firstPublish=null;Task<bool>? firstDelete=null;
 try {
  firstPublish=Task.Run(()=>{firstCreateStarted.Set();return modes.SelectAsync(eRoot,"regular");});
  firstDelete=Task.Run(()=>{firstDeleteStarted.Set();return launcher.Remove(regE);});
  Check(firstCreateStarted.Wait(TimeSpan.FromSeconds(2))&&firstDeleteStarted.Wait(TimeSpan.FromSeconds(2)),"First-provision workers did not start");await Task.Delay(50);
  Check(!firstPublish.IsCompleted&&!firstDelete.IsCompleted,"Missing-index root delete bypassed mode mutation gate");
 } finally {LocalModeAccountDeletion.MutationGate.Release();}
 var createdBeforeDelete=false;try{await firstPublish!;createdBeforeDelete=true;}catch(Exception){}
 Check(await firstDelete!,"First-provision concurrent root deletion failed");
 Check(!saves.ProfileExists(eRoot)&&!File.Exists($"user/profiles/{eRoot}.json"),"First-provision root deletion incomplete");
 Check(!saves.GetProfiles().Values.Any(x=>x.ProfileInfo?.LocalModeOwnerId==eRoot),"First-provision race orphaned a child");
 Check(!File.Exists("user/local-modes/index.json")||!File.ReadAllText("user/local-modes/index.json").Contains(eRoot.ToString()),"First-provision race orphaned the index");
 observations["firstProvisionCreateWon"]=createdBeforeDelete;
 File.WriteAllText("user/local-modes/index.json",savedIndex);
 // Normal character recreation temporarily removes a child from memory.
 // Its indexed identity must still be locked before deletion snapshots.
 var recreatedChild=saves.GetProfile(bRegular);
 await using(var recreation=await saves.BeginLocalProfileTransactionAsync(new[]{bRegular})) {
  Check(saves.DeleteProfileById(bRegular),"Fixture could not enter normal recreation interval");
  using var recreatingDeleteStarted=new ManualResetEventSlim();
  var deleteDuringRecreation=Task.Run(()=>{recreatingDeleteStarted.Set();return launcher.Remove(regB);});
  Check(recreatingDeleteStarted.Wait(TimeSpan.FromSeconds(2)),"Recreation deletion worker did not start");await Task.Delay(50);
  Check(!deleteDuringRecreation.IsCompleted,"Root deletion omitted temporarily unloaded indexed child lock");
  saves.AddProfile(recreatedChild);await recreation.CommitAsync();
  Check(await deleteDuringRecreation,"Root delete failed after child recreation completed");
  Check(!saves.ProfileExists(b)&&!File.Exists($"user/profiles/{bRegular}.json"),"Recreated child survived explicit root deletion");
 }
 // Exercise the actual SaveServer.LoadAsync startup recovery hook with no
 // migration stubs: the committed deletion removes its last profile first.
 Directory.CreateDirectory("startup-proof/user/profiles");Directory.CreateDirectory("startup-proof/user/local-modes");
 var parentDirectory=Directory.GetCurrentDirectory();Directory.SetCurrentDirectory("startup-proof");
 try {
  File.WriteAllBytes($"user/profiles/{b}.json",bRaw);File.WriteAllBytes($"user/profiles/{bRegular}.json",childBRaw);
  File.WriteAllText("user/local-modes/enabled.json","{\"enabled\":true}");
  File.WriteAllText("user/local-modes/index.json",JsonSerializer.Serialize(new Dictionary<string,Dictionary<string,string>>{[b.ToString()]=new(){["regular"]=bRegular.ToString()}}));
  var startupDeletion=new LocalModeAccountDeletion();var startupPlan=startupDeletion.Prepare(b.ToString(),new Dictionary<string,SptProfile>{[b.ToString()]=rootB,[bRegular.ToString()]=recreatedChild});
  using(var lockedStartupChild=new FileStream($"user/profiles/{bRegular}.json",FileMode.Open,FileAccess.Read,FileShare.Read)) {
   try{startupDeletion.Commit(startupPlan);}catch(IOException){}
   Check(startupPlan.Committed&&!File.Exists($"user/profiles/{b}.json"),"Startup recovery fixture was not committed/partial");
  }
  var startupSaves=new SaveServer(new FileUtil(),[],json,hash,null!,null!,DispatchProxy.Create<ISptLogger<SaveServer>,NullLog>(),config);
  await startupSaves.LoadAsync();
  Check(Directory.GetFiles("user/profiles","*.json").Length==0&&Directory.GetFiles("user/local-modes/account-deletions","*.json").Length==0,"SaveServer startup did not recover committed deletion");
  var startupLauncher=new LauncherV2Controller([],[],null!,hash,startupSaves,null!,config,null!,null!);
  Check(await startupLauncher.Register(new RegisterData{Username="SyntheticAfterRecovery",Edition="standard"}),"Canonical account registration failed after startup recovery");
  var survivor=startupSaves.GetProfiles().Single().Key;
  Check(new LocalGameModeService(startupSaves).ProfileForMode(survivor,"regular") is null,"Fresh mode service remained invalid after startup recovery");
 } finally{Directory.SetCurrentDirectory(parentDirectory);}
 observations["fixedRecoveryAndOwnershipCasesPassed"]=true;
 Console.WriteLine(JsonSerializer.Serialize(new{checks,observations,success=true,actualCompiledControllers=true,actualProfilesUsed=false,serverOrGameStarted=false,scratchDeleted=true}));
}finally{Directory.SetCurrentDirectory(previous);if(!Path.GetFullPath(scratch).StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(scratch).StartsWith("runtime-crud-fixture-"))throw new Exception("Unsafe cleanup");Directory.Delete(scratch,true);}
public class NullLog:DispatchProxy{protected override object? Invoke(MethodInfo? method,object?[]? args){var t=method!.ReturnType;return t==typeof(void)?null:t.IsValueType?Activator.CreateInstance(t):null;}}
