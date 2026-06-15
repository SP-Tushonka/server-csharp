using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Dialog;
using SPTarkov.Server.Core.Models.Eft.Quests;
using SPTarkov.Server.Core.Models.Spt.Servers;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Services.Locales;
using SPTarkov.Server.Core.Utils;

namespace SPTarkov.Server.Core.Callbacks;

[Injectable]
public class DataCallbacks(
    HttpResponseUtil httpResponseUtil,
    LocaleTable localeTable,
    GlobalTable globalTable,
    TemplateTable templateTable,
    SettingsTable settingsTable,
    HideoutTable hideoutTable,
    TraderController traderController,
    HideoutController hideoutController,
    LocaleService localeService
)
{
    /// <summary>
    ///     Handle client/settings
    /// </summary>
    /// <returns></returns>
    public ValueTask<StreamedJsonBody> GetSettings(string url, EmptyRequestData _, MongoId sessionID)
    {
        var returns = httpResponseUtil.GetStreamedBody(settingsTable);
        return new ValueTask<StreamedJsonBody>(returns);
    }

    /// <summary>
    ///     Handle client/globals
    /// </summary>
    /// <returns></returns>
    public ValueTask<StreamedJsonBody> GetGlobals(string url, EmptyRequestData _, MongoId sessionID)
    {
        var returns = httpResponseUtil.GetStreamedBody(globalTable);

        return new ValueTask<StreamedJsonBody>(returns);
    }

    /// <summary>
    ///     Handle client/items
    /// </summary>
    /// <returns></returns>
    public ValueTask<StreamedJsonBody> GetTemplateItems(string url, EmptyRequestData _, MongoId sessionID)
    {
        return new ValueTask<StreamedJsonBody>(httpResponseUtil.GetStreamedBody(templateTable.Items));
    }

    /// <summary>
    ///     Handle client/handbook/templates
    /// </summary>
    /// <returns></returns>
    public ValueTask<StreamedJsonBody> GetTemplateHandbook(string url, EmptyRequestData _, MongoId sessionID)
    {
        return new ValueTask<StreamedJsonBody>(httpResponseUtil.GetStreamedBody(templateTable.Handbook));
    }

    /// <summary>
    ///     Handle client/customization
    /// </summary>
    /// <returns></returns>
    public ValueTask<StreamedJsonBody> GetTemplateSuits(string url, EmptyRequestData _, MongoId sessionID)
    {
        return new ValueTask<StreamedJsonBody>(httpResponseUtil.GetStreamedBody(templateTable.Customization));
    }

    /// <summary>
    ///     Handle client/account/customization
    /// </summary>
    /// <returns></returns>
    public ValueTask<string> GetTemplateCharacter(string url, EmptyRequestData _, MongoId sessionID)
    {
        return new ValueTask<string>(httpResponseUtil.GetBody(templateTable.Character));
    }

    /// <summary>
    ///     Handle client/hideout/settings
    /// </summary>
    /// <returns></returns>
    public ValueTask<string> GetHideoutSettings(string url, EmptyRequestData _, MongoId sessionID)
    {
        return new ValueTask<string>(httpResponseUtil.GetBody(hideoutTable.Settings));
    }

    /// <summary>
    ///     Handle client/hideout/areas
    /// </summary>
    /// <returns></returns>
    public ValueTask<StreamedJsonBody> GetHideoutAreas(string url, EmptyRequestData _, MongoId sessionID)
    {
        return new ValueTask<StreamedJsonBody>(httpResponseUtil.GetStreamedBody(hideoutTable.Areas));
    }

    /// <summary>
    ///     Handle client/hideout/production/recipes
    /// </summary>
    /// <returns></returns>
    public ValueTask<StreamedJsonBody> GetHideoutProduction(string url, EmptyRequestData _, MongoId sessionID)
    {
        return new ValueTask<StreamedJsonBody>(httpResponseUtil.GetStreamedBody(hideoutTable.Production));
    }

    /// <summary>
    ///     Handle client/languages
    /// </summary>
    /// <returns></returns>
    public ValueTask<string> GetLocalesLanguages(string url, EmptyRequestData _, MongoId sessionID)
    {
        return new ValueTask<string>(httpResponseUtil.GetBody(localeTable.Languages));
    }

    /// <summary>
    ///     Handle client/menu/locale
    /// </summary>
    /// <returns></returns>
    public ValueTask<string> GetLocalesMenu(string url, EmptyRequestData _, MongoId sessionID)
    {
        var localeId = url.Replace("/client/menu/locale/", "");
        var result = localeTable.Menu?[localeId] ?? localeTable.Menu?.FirstOrDefault(m => m.Key == "en").Value;

        if (result == null)
        {
            throw new Exception($"Unable to determine locale for request with {localeId}");
        }

        return new ValueTask<string>(httpResponseUtil.GetBody(result));
    }

    /// <summary>
    ///     Handle client/locale
    /// </summary>
    /// <returns></returns>
    public ValueTask<StreamedJsonBody> GetLocalesGlobal(string url, EmptyRequestData _, MongoId sessionID)
    {
        var localeId = url.Replace("/client/locale/", "");
        var locales = localeService.GetLocaleDb(localeId);

        return new ValueTask<StreamedJsonBody>(httpResponseUtil.GetStreamedBody(locales));
    }

    /// <summary>
    ///     Handle client/hideout/qte/list
    /// </summary>
    /// <returns></returns>
    public ValueTask<string> GetQteList(string url, EmptyRequestData _, MongoId sessionID)
    {
        return new ValueTask<string>(httpResponseUtil.GetUnclearedBody(hideoutController.GetQteList(sessionID)));
    }

    /// <summary>
    ///     Handle client/items/prices/
    /// </summary>
    /// <returns></returns>
    public ValueTask<string> GetItemPrices(string url, EmptyRequestData _, MongoId sessionID)
    {
        var traderId = url.Replace("/client/items/prices/", "");

        return new ValueTask<string>(httpResponseUtil.GetBody(traderController.GetItemPrices(sessionID, traderId)));
    }

    /// <summary>
    /// Handle /client/dialogue
    /// </summary>
    public ValueTask<StreamedJsonBody> GetDialogue(string url, EmptyRequestData _, MongoId sessionID)
    {
        return new ValueTask<StreamedJsonBody>(httpResponseUtil.GetStreamedBody(templateTable.Dialogue));
    }

    // TODO: These are base implementations to get the game loading.

    /// <summary>
    /// Handle /client/quest/getMainQuestNotesList
    /// </summary>
    public ValueTask<string> GetMainQuestNoteList(string url, EmptyRequestData _, MongoId sessionID)
    {
        return new ValueTask<string>(httpResponseUtil.GetUnclearedBody(templateTable.MainQuestNotes));
    }

    /// <summary>
    /// Handle /client/quest/getMainQuestsList
    /// </summary>
    public ValueTask<string> GetMainQuestsList(string url, EmptyRequestData _, MongoId sessionID)
    {
        // TODO: Implement me! Seems to only send chapters you have unlocked
        return new ValueTask<string>(httpResponseUtil.GetUnclearedBody(new MainQuestsList { Chapters = [] }));
    }

    /// <summary>
    /// Handle /client/variable/group
    /// </summary>
    public ValueTask<string> GetVariableGroup(string url, EmptyRequestData _, MongoId sessionID)
    {
        // TODO: Implement me! No idea what this does, seems related to the story
        return new ValueTask<string>(httpResponseUtil.GetUnclearedBody(new List<VariableGroupData>()));
    }

    /// <summary>
    /// Handle /client/subtitle-track/list
    /// </summary>
    public ValueTask<string> GetSubtitleTrackList(string url, EmptyRequestData _, MongoId sessionID)
    {
        // TODO: Implement me! No Idea
        return new ValueTask<string>(httpResponseUtil.GetUnclearedBody(new List<SubtitleGroupData>()));
    }

    /// <summary>
    /// Handle /client/tape/list
    /// </summary>
    public ValueTask<string> GetTapeList(string url, EmptyRequestData _, MongoId sessionID)
    {
        // TODO: Implement me! No idea, but same model as /client/subtitle-track/list
        return new ValueTask<string>(httpResponseUtil.GetUnclearedBody(new List<SubtitleGroupData>()));
    }

    /// <summary>
    /// Handle /client/ending/list
    /// </summary>
    public ValueTask<string> GetEndingList(string url, EmptyRequestData _, MongoId sessionID)
    {
        // TODO: Implement me! Needs model, looks achievement/quest like, but doesn't fit any current model
        return new ValueTask<string>(httpResponseUtil.GetUnclearedBody(new { elements = new List<object>() }));
    }
}
