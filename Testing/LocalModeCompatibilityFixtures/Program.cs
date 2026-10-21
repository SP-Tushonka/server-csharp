using System.Text.Json;
using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services.Profile;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Json;
using SPTarkov.Server.Core.Services.Commerce;
using SPTarkov.Server.Core.Services.Server;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Eft.Game;
using SPTarkov.Server.Core.Models.Eft.Ws;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Servers.Http;
using SPTarkov.Server.Core.Servers.Ws;
using SPTarkov.Common.Models.Logging;
using Microsoft.AspNetCore.Http;
using System.Net.WebSockets;
using System.Reflection;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Helpers.Commerce;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Models.Eft.BattlePass;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Routers;

var checks = 0;
void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
async Task Reject(Func<Task> action) { var failed = false; try { await action(); } catch (InvalidOperationException) { failed = true; } catch (ArgumentException) { failed = true; } Check(failed, "Expected rejection"); }
var previous = Directory.GetCurrentDirectory();
var scratch = Directory.CreateTempSubdirectory("runtime-mode-fixture-").FullName;
Directory.SetCurrentDirectory(scratch);
try
{
    var json = new JsonUtil(Array.Empty<IJsonConverterRegistrator>());
    JsonUtil.JsonSerializerOptionsNoIndent!.Converters.Add(new IdConverter());
    JsonUtil.JsonSerializerOptionsIndented!.Converters.Add(new IdConverter());
    var config = Activator.CreateInstance<CoreConfig>();
    config.Features = Activator.CreateInstance<ServerFeatures>();
    config.Features.CompressProfile = true;
    SaveServer NewSaves() => new(new FileUtil(), [], json, new HashUtil(null!), null!, null!, null!, config);
    var saves = NewSaves();
    var owner = new MongoId("111111111111111111111111");
    var root = new SptProfile
    {
        ProfileInfo = new Info { ProfileId = owner, ScavengerId = new MongoId("222222222222222222222222"),
            Aid = 999999, Username = "DisposableModeFixture", Edition = "unheard_edition", IsWiped = false },
        CharacterData = new Characters { PmcData = new PmcData { TarCoinBalance = 486250, BattlePassUniversalDocumentBalance = 749 }, ScavData = new PmcData() },
        PurchasedShopOffers = ["synthetic-offer-already-bought"],
    };
    saves.AddProfile(root);
    Directory.CreateDirectory("user/profiles");
    await saves.SaveProfileAsync(owner);
    var rootPath = $"user/profiles/{owner}.json";
    var original = File.ReadAllBytes(rootPath);
    var disabled = new LocalGameModeService(saves);
    Check(!disabled.Enabled, "Feature must require explicit opt-in");
    Check(disabled.ResolveRequest(owner, "/client/game/profile/list") == owner, "Disabled routing changed");
    Check(!Directory.Exists("user/local-modes"), "Reading disabled modes wrote files");
    Directory.CreateDirectory("user/local-modes");
    File.WriteAllText("user/local-modes/enabled.json", "{\"enabled\":true}");
    var modes = new LocalGameModeService(saves);
    Check(modes.Enabled, "Opt-in missing");
    Check(modes.ProfileForMode(owner, "regular") is null, "Read created a character");
    Check(!File.Exists("user/local-modes/index.json"), "Read created index");
    var before = modes.Capture(owner);
    var regular = await modes.SelectAsync(owner, "regular");
    var regularId = new MongoId(regular.ProfileId);
    Check(regularId != owner && regular.CookieId == owner.ToString(), "Mode aliases PvE/cookie");
    Check(File.ReadAllBytes(rootPath).SequenceEqual(original), "Selecting a new mode changed PvE bytes");
    Check(saves.GetProfile(regularId).ProfileInfo!.LocalModeOwnerId == owner, "Child lost owner");
    Check(saves.GetProfile(regularId).ProfileInfo!.IsWiped == true, "New character bypassed normal creation");
    Check(saves.GetProfile(regularId).CharacterData!.PmcData!.BattlePassUniversalDocumentBalance is null, "Copied PvE documents");
    Check(modes.AccountBalance(regularId) == 486250, "Account wallet duplicated/lost");
    Check(modes.ResolveRequest(owner, "/client/game/profile/list") == regularId, "Gameplay route wrong");
    Check(modes.ResolveRequest(regularId, "/client/game/mode") == owner, "Control route lost account");
    Check(modes.ResolveRequest(owner, "/launcher/profiles") == owner, "Launcher identity changed");
    Check(!modes.IsCurrent(before), "Old channel remains admitted");
    Check(modes.BeginRaid(regularId), "First raid did not acquire");
    modes.RaidStarted(regularId, "synthetic.local.raid");
    await Reject(() => modes.SelectAsync(owner, "pvp-season"));
    Check(modes.ProfileForMode(owner, "pvp-season") is null, "Blocked switch created a character");
    await Reject(() => Task.Run(() => modes.VerifyRaidEnd(regularId, "other.raid")));
    modes.VerifyRaidEnd(regularId, "synthetic.local.raid");
    modes.RaidSaved(regularId, true);
    await Reject(() => modes.SelectAsync(owner, "pve"));
    Check(!modes.BeginRaid(regularId), "Transfer acquired a second raid lease");
    modes.RaidStarted(regularId, "synthetic.destination.raid");
    modes.VerifyRaidEnd(regularId, "synthetic.destination.raid");
    modes.RaidSaved(regularId, false);
    var season = await modes.SelectAsync(owner, "pvp-season");
    Check(season.ProfileId != regular.ProfileId && season.ProfileId != owner.ToString(), "Season aliases another mode");
    Check(File.ReadAllBytes(rootPath).SequenceEqual(original), "Season selection changed PvE bytes");
    var fresh = NewSaves();
    foreach (var file in Directory.EnumerateFiles("user/profiles", "*.json")) fresh.AddProfile(json.Deserialize<SptProfile>(File.ReadAllText(file))!);
    var reopened = new LocalGameModeService(fresh);
    Check(reopened.ProfileForMode(owner, "regular") == regularId, "Persisted mode index missing after reload");
    Check(reopened.Capture(owner).ProfileId == owner.ToString(), "New process did not default to PvE");

    // Exercise actual SaveServer multi-profile transaction and before-image rollback.
    var child = saves.GetProfile(regularId);
    child.CharacterData!.PmcData!.BattlePassUniversalDocumentBalance = 0;
    await saves.SaveProfileAsync(regularId);
    await using (var transaction = await saves.BeginLocalProfileTransactionAsync(new[] { owner, regularId }))
    {
        root.CharacterData!.PmcData!.TarCoinBalance = 481250;
        root.PurchasedShopOffers!.Add("synthetic-new-offer");
        child.CharacterData.PmcData.BattlePassUniversalDocumentBalance = 250;
        await transaction.CommitAsync();
    }
    Check(json.Deserialize<SptProfile>(File.ReadAllText(rootPath))!.CharacterData!.PmcData!.TarCoinBalance == 481250, "Account debit not saved");
    Check(json.Deserialize<SptProfile>(File.ReadAllText($"user/profiles/{regularId}.json"))!.CharacterData!.PmcData!.BattlePassUniversalDocumentBalance == 250, "Delivery not saved");
    await using (var transaction = await saves.BeginLocalProfileTransactionAsync(new[] { owner, regularId }))
    {
        saves.GetProfile(owner).CharacterData!.PmcData!.TarCoinBalance = 1;
        saves.GetProfile(regularId).CharacterData!.PmcData!.BattlePassUniversalDocumentBalance = 999;
    }
    Check(saves.GetProfile(owner).CharacterData!.PmcData!.TarCoinBalance == 481250, "Rollback lost wallet");
    Check(saves.GetProfile(regularId).CharacterData!.PmcData!.BattlePassUniversalDocumentBalance == 250, "Rollback retained phantom grant");
    Check(!Directory.EnumerateFiles("user/local-modes/transactions", "*.json").Any(), "Completed journals leaked");

    var journal = new LocalModeProfileJournal();
    var snapshots = new Dictionary<string, string>
    {
        [owner.ToString()] = File.ReadAllText(rootPath),
        [regularId.ToString()] = File.ReadAllText($"user/profiles/{regularId}.json"),
    };
    var unfinished = journal.Begin(snapshots);
    File.WriteAllText(rootPath, "interrupted partial save");
    Check(journal.Recover() == 1, "Started transaction not recovered");
    Check(File.ReadAllText(rootPath) == snapshots[owner.ToString()], "Recovery did not restore exact before-image");
    // Simulate a crash after committed intent and only one profile replacement.
    var committed = journal.Begin(snapshots);
    var after = snapshots.ToDictionary(x => x.Key, x => x.Value);
    after[owner.ToString()] = snapshots[owner.ToString()].Replace("481250", "480000");
    var entries = after.OrderBy(x => x.Key).Select(x => new LocalModeProfileJournal.Entry(x.Key, x.Value,
        Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(x.Value))))).ToList();
    File.WriteAllText(committed.Path, JsonSerializer.Serialize(committed.Intent with { State = "committed", After = entries }));
    Check(journal.Recover() == 1, "Committed transaction not recovered");
    Check(File.ReadAllText(rootPath) == after[owner.ToString()], "Committed recovery refunded debit");
    Check(File.ReadAllText($"user/profiles/{regularId}.json") == after[regularId.ToString()], "Committed recovery lost delivery");
    await Reject(() => Task.Run(() => journal.Begin(new Dictionary<string, string> { ["../bad"] = "{}" })));

    // Actual HTTP response writers must keep the original launcher account cookie.
    var listener = new SptHttpListener(null!, [], null!, null!, json, null!,
        new RequestEncryptionUtil(DispatchProxy.Create<ISptLogger<RequestEncryptionUtil>, NullLog>()));
    var http = new DefaultHttpContext();
    http.Response.Body = new MemoryStream();
    http.Items[LocalGameModeService.AccountContextKey] = owner.ToString();
    await listener.SendJsonAsync(http.Response, "{}", regularId);
    Check(http.Response.Headers.SetCookie.ToString() == $"PHPSESSID={owner}", "Raw JSON leaked child cookie");
    var compressed = new DefaultHttpContext();
    compressed.Response.Body = new MemoryStream();
    compressed.Items[LocalGameModeService.AccountContextKey] = owner.ToString();
    await listener.SendZlibJsonAsync(compressed.Request, compressed.Response, "{}", regularId);
    Check(compressed.Response.Headers.SetCookie.ToString() == $"PHPSESSID={owner}", "Compressed JSON leaked child cookie");

    // Actual lobby registration accepts a child URL but stores the account channel.
    await modes.SelectAsync(owner, "regular");
    var lobby = new LobbySocketChannel(json, modes);
    var log = DispatchProxy.Create<ISptLogger<SessionRequestWebSocketHandler>, NullLog>();
    var handler = new SessionRequestWebSocketHandler(log, null!, lobby, json, modes);
    var socket = new RecordingSocket();
    var socketContext = new DefaultHttpContext();
    socketContext.Request.Path = $"/ws/session/{regularId}";
    await handler.OnConnectionAsync(socket, socketContext, "disposable");
    Check(await lobby.SendNotificationAsync(owner, new WsExpansionsBalance
        { EventType = NotificationEventType.UpdateAccountTarcoinBalance, EventIdentifier = new MongoId(), Balance = 480000 }), "Child URL did not register account push channel");
    Check(socket.Messages > 0, "Actual channel did not send");
    var queue = new NotificationService();
    var legacy = new SptWebSocketConnectionHandler(DispatchProxy.Create<ISptLogger<SptWebSocketConnectionHandler>, NullLog>(), null!, json, null!, [], modes);
    var notifications = new NotificationSendHelper(DispatchProxy.Create<ISptLogger<NotificationSendHelper>, NullLog>(), legacy, lobby, saves, queue, null!, json, modes);

    // Use the actual purchase service, document DTO and account journal. One
    // account offer cannot be purchased again through another character slot.
    saves.GetProfile(owner).CharacterData!.PmcData!.TarCoinBalance = 480000;
    await saves.SaveProfileAsync(owner);
    var offer = new ShopOffer { Id = new MongoId("333333333333333333333333"), Countable = false,
        Items = [new ShopOfferItem { Type = ShopOfferItemType.BattlePassUniversalDocument, Quantity = 250 }] };
    var repeatable = new ShopOffer { Id = new MongoId("444444444444444444444444"), Countable = true,
        Items = [new ShopOfferItem { Type = ShopOfferItemType.BattlePassUniversalDocument, Quantity = 1 }] };
    var shop = new ShopTable { Content = new ShopContent { Menu = [], Pages = [], Offers = [offer, repeatable],
        Prices = [new ShopPrice { Id = offer.Id, AmountWithDiscount = 1000 }, new ShopPrice { Id = repeatable.Id, AmountWithDiscount = 10 }], Locale = [] } };
    var store = new TarcoinStoreService(DispatchProxy.Create<ISptLogger<TarcoinStoreService>, NullLog>(), saves, shop, null!, null!, notifications, null!, config, modes);
    var bought = await store.TryPurchaseAsync(regularId, offer.Id.ToString(), 1);
    Check(bought.Outcome == ShopPurchaseOutcome.Success, "Actual purchase failed");
    Check(store.GetBalance(owner) == 479000 && store.GetBalance(regularId) == 479000, "Account balance not shared");
    Check(saves.GetProfile(owner).CharacterData!.PmcData!.BattlePassUniversalDocumentBalance == 749, "Purchase altered PvE documents");
    Check(saves.GetProfile(regularId).CharacterData!.PmcData!.BattlePassUniversalDocumentBalance == 500, "Purchase did not deliver once to selected character");
    Check(saves.GetProfile(owner).PurchasedShopOffers!.Contains(offer.Id.ToString()), "Offer ownership not recorded on account");
    Check(await store.SignPurchaseAsync(regularId, bought.TransactionId), "Actual purchase notifications failed");
    await modes.SelectAsync(owner, "pvp-season");
    var seasonId = new MongoId(season.ProfileId);
    Check(!await lobby.SendNotificationAsync(owner, new WsExpansionsBalance
        { EventType = NotificationEventType.UpdateAccountTarcoinBalance, EventIdentifier = new MongoId(), Balance = 479000 }),
        "Old character socket received new-mode notifications");
    var reconnected = new DefaultHttpContext();
    reconnected.Request.Path = $"/ws/session/{seasonId}";
    await handler.OnConnectionAsync(new RecordingSocket(), reconnected, "season-reconnect");
    Check(await lobby.SendNotificationAsync(owner, new WsExpansionsBalance
        { EventType = NotificationEventType.UpdateAccountTarcoinBalance, EventIdentifier = new MongoId(), Balance = 479000 }),
        "New character socket did not receive account notifications");
    Check((await store.TryPurchaseAsync(seasonId, offer.Id.ToString(), 1)).Outcome == ShopPurchaseOutcome.PurchaseLimitExceeded,
        "Switching mode allowed duplicate one-time purchase");
    Check(store.GetBalance(seasonId) == 479000, "Rejected duplicate changed wallet");
    var purchases = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => store.TryPurchaseAsync(seasonId, repeatable.Id.ToString(), 1)));
    Check(purchases.All(x => x.Outcome == ShopPurchaseOutcome.Success), "Concurrent valid purchases failed");
    Check(store.GetBalance(owner) == 478900, "Concurrent purchases lost account debits");
    Check(saves.GetProfile(seasonId).CharacterData!.PmcData!.BattlePassUniversalDocumentBalance == 10, "Concurrent purchases lost/duplicated delivery");
    await store.CreditAsync(seasonId, 100);
    Check(store.GetBalance(owner) == 479000, "Child credit did not reach account wallet");
    Check(json.Deserialize<SptProfile>(File.ReadAllText(rootPath))!.CharacterData!.PmcData!.TarCoinBalance == 479000, "Child credit not persisted to account");
    Check(store.TrySpend(seasonId, 100), "Public account spend failed");
    Check(json.Deserialize<SptProfile>(File.ReadAllText(rootPath))!.CharacterData!.PmcData!.TarCoinBalance == 478900, "Public child spend not persisted to account");
    Check(!store.TrySpend(seasonId, int.MaxValue), "Overspend accepted");
    Check(modes.BeginRaid(seasonId), "Fresh season raid failed");
    modes.RaidStarted(seasonId, "abnormal-end-test");
    modes.GameSessionBoundary(owner);
    Check((await modes.SelectAsync(owner, "pve")).ProfileId == owner.ToString(), "Next guarded game remained locked after session ended");

    // Reuse the unchanged ProfileHelper/Ragfair binding and BattlePass controller.
    await modes.SelectAsync(owner, "regular");
    var profileHelper = new ProfileHelper(null!, null!, null!, null!, saves, null!, null!, null!, null!, modes);
    var selected = saves.GetProfile(regularId).CharacterData!.PmcData!;
    var selectedInfo = selected.GetType().GetProperty("Info")!;
    selectedInfo.SetValue(selected, Activator.CreateInstance(selectedInfo.PropertyType));
    selectedInfo.PropertyType.GetProperty("Level")!.SetValue(selectedInfo.GetValue(selected), 3);
    selectedInfo.PropertyType.GetProperty("GameVersion")!.SetValue(selectedInfo.GetValue(selected), "unheard_edition");
    var fleaProfile = modes.ResolveRequest(owner, "/client/ragfair/find");
    Check(fleaProfile == regularId && ReferenceEquals(profileHelper.GetPmcProfile(fleaProfile), selected),
        "Existing flea profile lookup did not receive selected character");
    Check(profileHelper.GetPmcProfile(fleaProfile)!.Info!.Level == 3, "Flea access would inherit another character's level");
    Check(modes.ResolveRequest(owner, "/client/ragfair/offer/findbyid") == regularId, "Flea offer route bypassed selected character");
    Check(modes.ResolveRequest(owner, "/client/game/profile/items/moving") == regularId, "Existing item-event routes bypassed selected character");
    var passId = new MongoId("555555555555555555555555");
    var rewardId = new MongoId("666666666666666666666666");
    var scheme = new MongoId("777777777777777777777777");
    var reward = new BattlePassPageReward { Id = rewardId, Location = null!, Rewards = [], ImageUrl = "", BigImageUrl = "", Cost = new() { [scheme] = 1 } };
    var pass = new BattlePass { Id = passId, ExchangeRate = 1, ItemExchangeSettings = null!, DocumentLimits = null!,
        Pages = [new BattlePassPage { PrevPageItemsRequirement = 0, Rewards = [reward] }],
        Documents = [new BattlePassDocument { Id = scheme, ItemId = new MongoId("888888888888888888888888"), ImageUrl = "", UnavailableImageUrl = "" }] };
    var seasons = new SeasonTable { Active = null!, Perks = null!, BattlePass = new BattlePassActiveResponse { BattlePasses = [pass] } };
    var outputs = new EventOutputHolder(profileHelper, null!, null!);
    var outputMap = (Dictionary<MongoId, ItemEventRouterResponse>)typeof(EventOutputHolder).GetField("_outputStore", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(outputs)!;
    outputMap[regularId] = new ItemEventRouterResponse { ProfileChanges = [], Warnings = [] };
    var rewards = new RewardHelper(DispatchProxy.Create<ISptLogger<RewardHelper>, NullLog>(), null!, null!, null!, null!, null!, profileHelper, null!, null!, null!, null!, null!, notifications, store, null!);
    var battlePass = new BattlePassController(DispatchProxy.Create<ISptLogger<BattlePassController>, NullLog>(), seasons, profileHelper,
        null!, null!, rewards, null!, null!, null!, outputs);
    var documentsBefore = selected.BattlePassUniversalDocumentBalance;
    var claim = new BattlePassUnlockRewardRequest { BattlePassId = passId, RewardId = rewardId, UniversalDocuments = 1, Items = [] };
    battlePass.UnlockReward(selected, claim, regularId);
    Check(selected.BattlePassUniversalDocumentBalance == documentsBefore - 1, "Canonical BattlePass did not debit selected character");
    Check(selected.BattlePassProgress!.Single().ObtainedRewardIds!.Contains(rewardId), "Canonical BattlePass did not track selected character reward");
    Check(saves.GetProfile(owner).CharacterData!.PmcData!.BattlePassUniversalDocumentBalance == 749, "Child BattlePass claim touched PvE documents");
    battlePass.UnlockReward(selected, claim, regularId);
    Check(selected.BattlePassUniversalDocumentBalance == documentsBefore - 1 && selected.BattlePassProgress.Single().ObtainedRewardIds!.Count == 1,
        "Canonical duplicate BattlePass claim charged or granted twice");
    // Canonical online availability must stay disabled: local raid routing is a client decision.
    var matchConfig = new MatchConfig { Enabled = false };
    var matchController = new MatchController(null!, matchConfig, null!, null!, null!, null!);
    Check(!matchController.GetEnabled(), "Disabled online matchmaking remains unavailable");
    var availability = new HttpResponseUtil(json, null!).GetBody(matchController.GetEnabled());
    using (var availabilityJson = JsonDocument.Parse(availability))
    {
        Check(availabilityJson.RootElement.GetProperty("err").GetInt32() == 0, "Availability response succeeds");
        Check(!availabilityJson.RootElement.GetProperty("data").GetBoolean(), "Canonical availability data is false");
    }
    var availabilityHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(availability))).ToLowerInvariant();
    Console.WriteLine(JsonSerializer.Serialize(new { availability, availabilityHash }));
    Console.WriteLine(JsonSerializer.Serialize(new { checks, success = true, actualServerServices = true,
        gameOrBackendStarted = false, originalProfileUsed = false, scratchDeletedAfterward = true }));
}
finally
{
    Directory.SetCurrentDirectory(previous);
    var resolved = Path.GetFullPath(scratch);
    var temp = Path.GetFullPath(Path.GetTempPath());
    if (!resolved.StartsWith(temp, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(resolved).StartsWith("runtime-mode-fixture-"))
        throw new InvalidOperationException("Refusing fixture cleanup outside its temporary directory");
    Directory.Delete(resolved, true);
}

public class NullLog : DispatchProxy
{
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        var type = method!.ReturnType;
        if (type == typeof(void)) return null;
        if (type == typeof(Task)) return Task.CompletedTask;
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }
}

sealed class RecordingSocket : WebSocket
{
    public int Messages { get; private set; }
    public override WebSocketCloseStatus? CloseStatus => null;
    public override string? CloseStatusDescription => null;
    public override WebSocketState State => WebSocketState.Open;
    public override string? SubProtocol => null;
    public override void Abort() { }
    public override void Dispose() { }
    public override Task CloseAsync(WebSocketCloseStatus status, string? description, CancellationToken cancellationToken) => Task.CompletedTask;
    public override Task CloseOutputAsync(WebSocketCloseStatus status, string? description, CancellationToken cancellationToken) => Task.CompletedTask;
    public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken) => throw new NotSupportedException();
    public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType type, bool endOfMessage, CancellationToken cancellationToken)
    { Messages++; return Task.CompletedTask; }
}

sealed class IdConverter : JsonConverter<MongoId>
{
    public override MongoId Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => new(reader.GetString()!);
    public override void Write(Utf8JsonWriter writer, MongoId value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());
    public override MongoId ReadAsPropertyName(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => new(reader.GetString()!);
    public override void WriteAsPropertyName(Utf8JsonWriter writer, MongoId value, JsonSerializerOptions options) => writer.WritePropertyName(value.ToString());
}
