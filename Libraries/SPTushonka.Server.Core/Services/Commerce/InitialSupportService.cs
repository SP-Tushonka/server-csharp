using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Utils;

namespace SPTarkov.Server.Core.Services.Commerce;

/// <summary>
///     Mails a new character a gear crate once a day during its first days
/// </summary>
[Injectable(InjectionType.Singleton)]
public class InitialSupportService(
    ProfileHelper profileHelper,
    MailSendService mailSendService,
    RandomUtil randomUtil,
    TimeUtil timeUtil,
    GiftsConfig giftsConfig
)
{
    /// <summary>
    ///     Id the sent crates are counted under in the profile's received gifts
    /// </summary>
    public const string GiftId = "InitialSupport";

    /// <summary>
    ///     Send the player a crate when one is due
    /// </summary>
    /// <param name="sessionId">Session/Player id</param>
    /// <returns>True when a crate was sent</returns>
    public bool SendDueCrate(MongoId sessionId)
    {
        var settings = giftsConfig.InitialSupport;
        if (settings is null || !settings.Enabled || settings.Containers.Count == 0)
        {
            return false;
        }

        var fullProfile = profileHelper.GetFullProfile(sessionId);
        var createdAt = fullProfile?.CharacterData?.PmcData?.Info?.RegistrationDate;
        if (fullProfile?.SptData is null || createdAt is null || fullProfile.ProfileInfo?.IsWiped == true)
        {
            return false;
        }

        var now = timeUtil.GetTimeStamp();
        if (now > createdAt.Value + settings.Days * 86400L)
        {
            return false;
        }

        fullProfile.SptData.ReceivedGifts ??= [];
        var record = fullProfile.SptData.ReceivedGifts.FirstOrDefault(gift => gift.GiftId == GiftId);

        // A record older than the character belongs to one that was wiped or prestiged since
        if (record is not null && record.TimestampLastAccepted < createdAt.Value)
        {
            fullProfile.SptData.ReceivedGifts.Remove(record);
            record = null;
        }

        if (record is not null && now - record.TimestampLastAccepted < settings.IntervalHours * 3600L)
        {
            return false;
        }

        var crate = new Item
        {
            Id = new MongoId(),
            Template = randomUtil.GetArrayValue(settings.Containers),
            Upd = new Upd { StackObjectsCount = 1 },
        };
        mailSendService.SendLocalisedNpcMessageToPlayer(
            sessionId,
            settings.Trader,
            MessageType.InitialSupport,
            record is null ? settings.FirstMessageId : settings.MessageId,
            [crate],
            timeUtil.GetHoursAsSeconds(settings.CollectionTimeHours)
        );

        if (record is null)
        {
            fullProfile.SptData.ReceivedGifts.Add(
                new ReceivedGift
                {
                    GiftId = GiftId,
                    TimestampLastAccepted = now,
                    Current = 1,
                }
            );
        }
        else
        {
            record.TimestampLastAccepted = now;
            record.Current++;
        }

        return true;
    }
}
