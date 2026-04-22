namespace YSS.Constants;

/// <summary>
/// ELO rating system constants.
/// </summary>
public static class EloConstants
{
    /// <summary>
    /// Default ELO rating for new teams with no match history.
    /// </summary>
    public const int DefaultEloRating = 1500;

    /// <summary>
    /// K-factor for ELO rating calculations. Higher values = more volatile ratings.
    /// Typical range: 20-40. Youth sports often use higher values.
    /// </summary>
    public const int KFactor = 30;

    /// <summary>
    /// Home field advantage bonus added to home team ELO during match prediction.
    /// Not persisted, only used for win probability calculations.
    /// </summary>
    public const int HomeFieldAdvantage = 100;
}

/// <summary>
/// Tournament identification constants.
/// </summary>
public static class TournamentConstants
{
    /// <summary>
    /// Modular11 tournament ID for MLS Next Homegrown program.
    /// </summary>
    public const string HomegrownTournamentId = "12";

    /// <summary>
    /// Modular11 tournament ID for MLS Next Academy program.
    /// </summary>
    public const string AcademyTournamentId = "35";

    /// <summary>
    /// Modular11 tournament ID for NJ Cup Qualifier.
    /// </summary>
    public const int NjCupQualifierTournamentId = 84;
}

/// <summary>
/// Modular11 API age group codes and their mappings to standard age group names.
/// These codes are used in the age[] query parameter when fetching match data.
/// </summary>
public static class AgeGroupConstants
{
    /// <summary>
    /// Modular11 API code for U13 age group.
    /// </summary>
    public const string U13Code = "21";

    /// <summary>
    /// Modular11 API code for U14 age group.
    /// </summary>
    public const string U14Code = "22";

    /// <summary>
    /// Modular11 API code for U15 age group.
    /// </summary>
    public const string U15Code = "33";

    /// <summary>
    /// Modular11 API code for U16 age group.
    /// </summary>
    public const string U16Code = "14";

    /// <summary>
    /// Modular11 API code for U17 age group.
    /// </summary>
    public const string U17Code = "15";

    /// <summary>
    /// Modular11 API code for U19 age group.
    /// </summary>
    public const string U19Code = "26";

    /// <summary>
    /// Standard age group names.
    /// </summary>
    public static class Names
    {
        public const string U13 = "U13";
        public const string U14 = "U14";
        public const string U15 = "U15";
        public const string U16 = "U16";
        public const string U17 = "U17";
        public const string U19 = "U19";
    }

    /// <summary>
    /// All configured Modular11 age group codes (current season).
    /// </summary>
    public static readonly string[] AllCodes = new[]
    {
        U13Code,
        U14Code,
        U15Code,
        U16Code,
        U17Code,
        U19Code
    };
}

/// <summary>
/// Modular11 API configuration constants.
/// </summary>
public static class Modular11ApiConstants
{
    /// <summary>
    /// Gender code for male athletes.
    /// </summary>
    public const string GenderMale = "1";

    /// <summary>
    /// Match type code (specific meaning defined by Modular11 API).
    /// </summary>
    public const string MatchType = "2";

    /// <summary>
    /// Status filter for scheduled matches.
    /// </summary>
    public const string StatusScheduled = "scheduled";

    /// <summary>
    /// Status filter for completed matches.
    /// </summary>
    public const string StatusCompleted = "completed";

    /// <summary>
    /// Minimum throttle delay between API requests (milliseconds).
    /// </summary>
    public const int MinThrottleMilliseconds = 1000;

    /// <summary>
    /// Maximum throttle delay between API requests (milliseconds).
    /// </summary>
    public const int MaxThrottleMilliseconds = 3000;
}

/// <summary>
/// Program identification constants.
/// </summary>
public static class ProgramConstants
{
    /// <summary>
    /// Academy program identifier (MLS Next Academy).
    /// </summary>
    public const string Academy = "AG";

    /// <summary>
    /// Homegrown program identifier (MLS Next Homegrown).
    /// </summary>
    public const string Homegrown = "HG";
}

/// <summary>
/// API and validation constants.
/// </summary>
public static class ValidationConstants
{
    /// <summary>
    /// Minimum length for search query terms (team name search, etc.).
    /// Prevents overly broad queries that could impact performance.
    /// </summary>
    public const int MinSearchQueryLength = 2;

    /// <summary>
    /// Maximum string length for match scores (e.g., "3-2", "1-0 (PK)", etc.).
    /// </summary>
    public const int MaxScoreLength = 20;

    /// <summary>
    /// Maximum string length for gender field.
    /// </summary>
    public const int MaxGenderLength = 20;

    /// <summary>
    /// Maximum string length for match ID (natural key from Modular11).
    /// </summary>
    public const int MaxMatchIdLength = 50;
}
