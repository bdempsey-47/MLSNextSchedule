namespace YSS.Functions.Models;

public record TournamentSeason(
    int TournamentId,
    string Label,
    string LeagueName,
    DateTime SeasonStart,
    DateTime SeasonEnd
)
{
    public bool IsActive(DateTime now) => now >= SeasonStart && now <= SeasonEnd;
}
