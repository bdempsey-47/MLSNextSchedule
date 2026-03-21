namespace YSS.Functions.Services;

public static class EloCalculator
{
    private const double InitialRating = 1500.0;
    private const double KFactor = 30.0;

    public record EloMatchInput(int HomeTeamId, int AwayTeamId, int HomeScore, int AwayScore, DateTime Date);

    public class EloTeamState
    {
        public double Rating { get; set; } = InitialRating;
        public int GP { get; set; }
        public List<double> RecentDeltas { get; } = new();

        public void AddDelta(double delta)
        {
            RecentDeltas.Add(delta);
            if (RecentDeltas.Count > 5)
                RecentDeltas.RemoveAt(0);
        }
    }

    /// <summary>
    /// Compute ELO rankings from a list of matches (must be sorted by date ascending).
    /// </summary>
    public static Dictionary<int, EloTeamState> ComputeRankings(List<EloMatchInput> matches)
    {
        var teams = new Dictionary<int, EloTeamState>();

        foreach (var match in matches)
        {
            if (!teams.ContainsKey(match.HomeTeamId))
                teams[match.HomeTeamId] = new EloTeamState();
            if (!teams.ContainsKey(match.AwayTeamId))
                teams[match.AwayTeamId] = new EloTeamState();

            var home = teams[match.HomeTeamId];
            var away = teams[match.AwayTeamId];

            var expectedHome = GetExpectedScore(home.Rating, away.Rating);
            var expectedAway = 1.0 - expectedHome;

            double actualHome, actualAway;
            if (match.HomeScore > match.AwayScore)
            {
                actualHome = 1.0;
                actualAway = 0.0;
            }
            else if (match.HomeScore < match.AwayScore)
            {
                actualHome = 0.0;
                actualAway = 1.0;
            }
            else
            {
                actualHome = 0.5;
                actualAway = 0.5;
            }

            var goalDiff = Math.Abs(match.HomeScore - match.AwayScore);
            var multiplier = GetMarginMultiplier(goalDiff);

            var homeDelta = KFactor * multiplier * (actualHome - expectedHome);
            var awayDelta = KFactor * multiplier * (actualAway - expectedAway);

            home.Rating += homeDelta;
            away.Rating += awayDelta;
            home.GP++;
            away.GP++;
            home.AddDelta(homeDelta);
            away.AddDelta(awayDelta);
        }

        return teams;
    }

    public static double GetExpectedScore(double ratingA, double ratingB)
    {
        return 1.0 / (1.0 + Math.Pow(10.0, (ratingB - ratingA) / 400.0));
    }

    public static double GetMarginMultiplier(int goalDiff)
    {
        return goalDiff switch
        {
            <= 1 => 1.0,
            2 => 1.5,
            _ => 1.75
        };
    }
}
