using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Utils;

namespace SPTarkov.Server.Core.Models.Eft.Ragfair;

public record GetRagfairOfferByIdRequest : IRequestData
{
    [JsonPropertyName("id")]
    public int? Id { get; set; }
}
