namespace YSS.Functions.Models;

public record TournamentSeason(
    string TournamentId,
    string Label,
    string LeagueName,
    DateTime SeasonStart,
    DateTime SeasonEnd
)
{
    public bool IsActive(DateTime now) => now >= SeasonStart && now <= SeasonEnd;
}
