using FluentAssertions;
using Xunit;
using YSS.Functions.Services;
using static YSS.Functions.Services.EloCalculator;

namespace YSS.Tests.Unit;

public class EloCalculatorTests
{
    [Fact]
    public void TwoEqualTeams_OneWins_SymmetricRatingChange()
    {
        var matches = new List<EloMatchInput>
        {
            new(HomeTeamId: 1, AwayTeamId: 2, HomeScore: 1, AwayScore: 0, Date: new DateTime(2026, 1, 1))
        };

        var result = ComputeRankings(matches);

        result[1].Rating.Should().BeGreaterThan(1500);
        result[2].Rating.Should().BeLessThan(1500);
        // Symmetric: gains equal losses
        (result[1].Rating + result[2].Rating).Should().BeApproximately(3000, 0.001);
    }

    [Fact]
    public void Upset_WeakerBeatsStronger_LargerSwing()
    {
        // First give team 1 a higher rating by winning
        var matches = new List<EloMatchInput>
        {
            new(1, 2, 3, 0, new DateTime(2026, 1, 1)),
            new(1, 2, 3, 0, new DateTime(2026, 1, 2)),
            new(1, 2, 3, 0, new DateTime(2026, 1, 3)),
            // Now team 2 (weaker) beats team 1 (stronger) — upset
            new(2, 1, 1, 0, new DateTime(2026, 1, 4)),
        };

        var result = ComputeRankings(matches);

        // The upset delta (last delta for team 2) should be larger than normal
        var team2LastDelta = result[2].RecentDeltas.Last();
        team2LastDelta.Should().BeGreaterThan(15, "upset win should yield a large positive delta");
    }

    [Fact]
    public void MarginMultiplier_ThreeGoalWin_GivesLargerDelta()
    {
        var matchesNarrow = new List<EloMatchInput>
        {
            new(1, 2, 1, 0, new DateTime(2026, 1, 1))
        };
        var matchesBlowout = new List<EloMatchInput>
        {
            new(1, 2, 3, 0, new DateTime(2026, 1, 1))
        };

        var resultNarrow = ComputeRankings(matchesNarrow);
        var resultBlowout = ComputeRankings(matchesBlowout);

        var narrowDelta = resultNarrow[1].Rating - 1500;
        var blowoutDelta = resultBlowout[1].Rating - 1500;

        blowoutDelta.Should().BeApproximately(narrowDelta * 1.75, 0.001,
            "3-0 win should give 1.75x the delta of 1-0 win between equal teams");
    }

    [Fact]
    public void Draw_BetweenEqualTeams_NoRatingChange()
    {
        var matches = new List<EloMatchInput>
        {
            new(1, 2, 1, 1, new DateTime(2026, 1, 1))
        };

        var result = ComputeRankings(matches);

        result[1].Rating.Should().BeApproximately(1500, 0.001);
        result[2].Rating.Should().BeApproximately(1500, 0.001);
    }

    [Fact]
    public void GamesPlayed_TrackedCorrectly()
    {
        var matches = new List<EloMatchInput>
        {
            new(1, 2, 1, 0, new DateTime(2026, 1, 1)),
            new(1, 3, 2, 1, new DateTime(2026, 1, 2)),
            new(2, 3, 0, 0, new DateTime(2026, 1, 3)),
        };

        var result = ComputeRankings(matches);

        result[1].GP.Should().Be(2);
        result[2].GP.Should().Be(2);
        result[3].GP.Should().Be(2);
    }

    [Fact]
    public void RecentDeltas_CappedAtFive()
    {
        var matches = new List<EloMatchInput>();
        for (int i = 0; i < 7; i++)
        {
            matches.Add(new(1, 2, 1, 0, new DateTime(2026, 1, 1).AddDays(i)));
        }

        var result = ComputeRankings(matches);

        result[1].RecentDeltas.Should().HaveCount(5);
        result[2].RecentDeltas.Should().HaveCount(5);
    }

    [Fact]
    public void GetExpectedScore_EqualRatings_Returns50Percent()
    {
        var expected = EloCalculator.GetExpectedScore(1500, 1500);
        expected.Should().BeApproximately(0.5, 0.001);
    }

    [Fact]
    public void GetExpectedScore_400PointAdvantage_Returns91Percent()
    {
        var expected = EloCalculator.GetExpectedScore(1900, 1500);
        expected.Should().BeApproximately(0.909, 0.01);
    }

    [Fact]
    public void GetMarginMultiplier_VariousGoalDiffs()
    {
        EloCalculator.GetMarginMultiplier(0).Should().Be(1.0);
        EloCalculator.GetMarginMultiplier(1).Should().Be(1.0);
        EloCalculator.GetMarginMultiplier(2).Should().Be(1.5);
        EloCalculator.GetMarginMultiplier(3).Should().Be(1.75);
        EloCalculator.GetMarginMultiplier(5).Should().Be(1.75);
    }

    [Fact]
    public void TwoGoalWin_GetsOnePointFiveMultiplier()
    {
        var matchesOneGoal = new List<EloMatchInput>
        {
            new(1, 2, 1, 0, new DateTime(2026, 1, 1))
        };
        var matchesTwoGoal = new List<EloMatchInput>
        {
            new(1, 2, 2, 0, new DateTime(2026, 1, 1))
        };

        var resultOne = ComputeRankings(matchesOneGoal);
        var resultTwo = ComputeRankings(matchesTwoGoal);

        var deltaOne = resultOne[1].Rating - 1500;
        var deltaTwo = resultTwo[1].Rating - 1500;

        deltaTwo.Should().BeApproximately(deltaOne * 1.5, 0.001);
    }
}
