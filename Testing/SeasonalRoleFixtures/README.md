# Seasonal role compatibility

Base server source: `305449df5cdeb5dfa45d641ab8866b8295d1cb68`.
Only production change: `SeasonalEventService.cs`.

Legacy event labels `bear` and `usec` resolve to the exact Roles constants after checking an installed literal role. Unknown optional roles retain the warning and continue. Equipment weights and chance floors remain unchanged. Loot writes now target the existing typed ItemPools dictionary for its configured slot; unsupported pool names fail explicitly.

The fixture invokes actual compiled protected methods. Its baseline mode against frozen Core7c7 reproduces both the missing-role exception and discarded loot mutation. The corrected build checks synthetic collision/refusal cases and all26 installed seasonal gear role entries using24 actual BotType JSON files via the canonical JsonUtil converters, plus all13 actual Christmas loot weights across three pools.

Build Core using .NET10, the repository's offline NuGet config, cached packages and `GIT_LFS_SKIP_SMUDGE=1`. Build the fixture with `-p:CoreDirectory=.../Libraries/SPTushonka.Server.Core/bin/Debug/net10.0`; run its DLL with `--bot-source .../SPT_Data/database/bots/types`. No server or game runs, and no profiles are read or written. The fixture's baseline mode must instead reference the frozen predecessor Core.

A cold backend integration test remains necessary. These fixtures prove the two amended methods and their actual configuration/model inputs, not every seasonal startup service.
