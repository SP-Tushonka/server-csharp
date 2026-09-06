# Dump cleaner
### Clean up raw EFT dump JSONs into files usable by SPT
TODO: write details

# Generate Quest File
### Create a quest file for use by SPT by providing a raw quest dump JSON as input
## How to use
1. Build GenerateQuestFile project
2. Copy your aki quests.json into `ChompQuestVerifier\GenerateQuestFile\bin\Debug\net6.0\input` create the input folder if necessary
3. Copy your `resp.client.quest.list.xxx` file into the above folder
4. Run `GenerateQuestFile.exe`
5. Your completed `quests.json` is written to `output` folder


# Quest Validator
### Verifies an SPT quest JSON against a live dump of quest data
## How to use
1. Copy your aki quests.json into QuestValidator\bin\Debug\net6.0\input
2. Copy a live quest file dump (resp.client.quest.list_xxxx) into QuestValidator\bin\Debug\net6.0\input
3. Run QuestValidator project to generate a list of warnings found in aki quests.json, separated by quest.