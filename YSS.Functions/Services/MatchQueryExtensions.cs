using YSS.Data.Entities;
using Microsoft.EntityFrameworkCore;
using YSS.Constants;

namespace YSS.Functions.Services;

public static class MatchQueryExtensions
{
    public static IQueryable<Match> FilterByProgram(
        this IQueryable<Match> query, bool isAcademy, bool isHomegrown)
    {
        if (!isAcademy && !isHomegrown)
            return query;

        return query.Where(m =>
            (isAcademy && (
                m.Region.Division.TournamentId == TournamentConstants.AcademyTournamentId ||
                m.Region.Division.TournamentId == TournamentConstants.NjCupQualifierTournamentId ||
                m.Competition.Name.StartsWith("AD"))) ||
            (isHomegrown && (
                new[] { TournamentConstants.HomegrownTournamentId, TournamentConstants.FestTournamentId }.Contains(m.Region.Division.TournamentId) &&
                !m.Competition.Name.StartsWith("AD"))));
    }

    public static IEnumerable<Match> FilterByProgram(
        this IEnumerable<Match> query, bool isAcademy, bool isHomegrown)
    {
        if (!isAcademy && !isHomegrown)
            return query;

        return query.Where(m =>
            (isAcademy && (
                m.Region.Division.TournamentId == TournamentConstants.AcademyTournamentId ||
                m.Region.Division.TournamentId == TournamentConstants.NjCupQualifierTournamentId ||
                m.Competition.Name.StartsWith("AD"))) ||
            (isHomegrown && (
                new[] { TournamentConstants.HomegrownTournamentId, TournamentConstants.FestTournamentId }.Contains(m.Region.Division.TournamentId) &&
                !m.Competition.Name.StartsWith("AD"))));
    }
}
