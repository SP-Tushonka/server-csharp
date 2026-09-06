# BotGenerator

Generates bot templates based on user-provided game dumps.

# Setup

 - Set 'Generator' as start-up project
 - Build solution to create required folders in bin folder.
 - in Generator\bin\Debug\netcoreapp3.1
   - Place live dumps in subfolder: dumps. - These are the files called 'resp.client.game.bot.generate.xxxxxx.json'
 - Run code, bot jsons will be created in the 'output' folder e.g. assault.json


# Maintenance

 - Ensure the asset files are kept up to date (Generator\bin\Debug\netcoreapp3.1\Assets\) 
   - They can be found in Eft/EscapeFromTarkov_Data/resources.assets  as a text asset

# Common problems

 - Don't place the request jsons (files starting with req.*) in the dump folder, these don't contain any bot data.