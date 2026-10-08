using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Utils;

namespace SPTarkov.Server.Core.Models.Eft.Profile;

public record GetProfileSettingsRequest : IRequestData
{
    /// <summary>
    ///     Chosen value for profile.Info.SelectedMemberCategory
    /// </summary>
    [JsonPropertyName("memberCategory")]
    public int? MemberCategory { get; set; }

    [JsonPropertyName("squadInviteRestriction")]
    public bool? SquadInviteRestriction { get; set; }

    /// <summary>
    ///     Chosen value for profile.Info.SelectedPrestigeGameMode, the mode whose prestige is shown
    /// </summary>
    [JsonPropertyName("prestigeGameMode")]
    public string? PrestigeGameMode { get; set; }
}
