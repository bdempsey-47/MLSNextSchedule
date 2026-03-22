using System.Text.Json.Serialization;

namespace YSS.Data.Entities;

public class TeamAgeGroupElo
{
    public int Id { get; set; }
    public int TeamId { get; set; }
    public int AgeGroupId { get; set; }
    public int EloRating { get; set; } = 1500;

    [JsonIgnore]
    public Team Team { get; set; } = null!;
    [JsonIgnore]
    public AgeGroup AgeGroup { get; set; } = null!;
}
