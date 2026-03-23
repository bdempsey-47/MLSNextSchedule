import { useState, useEffect } from 'react'
import { Link } from 'react-router-dom'
import type { HomepageStats, MiniRanking, RegionDominance, UpsetInfo, MatchOfWeek, QuickStats } from '../types'
import './HomePage.css'

const AGE_GROUPS = ['U13', 'U14', 'U15', 'U16', 'U17', 'U18/19']
const API_BASE = import.meta.env.VITE_API_BASE_URL || ''

function transformMiniRanking(r: any): MiniRanking {
  return {
    rank:       r.Rank       ?? r.rank       ?? 0,
    teamName:   r.TeamName   ?? r.teamName   ?? '',
    logoUrl:    r.LogoUrl    ?? r.logoUrl,
    regionName: r.RegionName ?? r.regionName ?? '',
    eloRating:  r.EloRating  ?? r.eloRating  ?? 0,
    eloDelta:   r.EloDelta   ?? r.eloDelta   ?? 0,
  }
}

function transformRegionDominance(r: any): RegionDominance {
  return {
    rank:           r.Rank           ?? r.rank           ?? 0,
    regionName:     r.RegionName     ?? r.regionName     ?? '',
    wins:           r.Wins           ?? r.wins           ?? 0,
    losses:         r.Losses         ?? r.losses         ?? 0,
    goalsFor:       r.GoalsFor       ?? r.goalsFor       ?? 0,
    goalsAgainst:   r.GoalsAgainst   ?? r.goalsAgainst   ?? 0,
    goalDifference: r.GoalDifference ?? r.goalDifference ?? 0,
  }
}

function transformUpset(u: any): UpsetInfo {
  return {
    winnerName:    u.WinnerName    ?? u.winnerName    ?? '',
    winnerLogoUrl: u.WinnerLogoUrl ?? u.winnerLogoUrl,
    winnerElo:     u.WinnerElo     ?? u.winnerElo     ?? 0,
    loserName:     u.LoserName     ?? u.loserName     ?? '',
    loserLogoUrl:  u.LoserLogoUrl  ?? u.loserLogoUrl,
    loserElo:      u.LoserElo      ?? u.loserElo      ?? 0,
    score:         u.Score         ?? u.score         ?? '',
    eloDiff:       u.EloDiff       ?? u.eloDiff       ?? 0,
    matchDate:     u.MatchDate     ?? u.matchDate     ?? '',
    program:       u.Program       ?? u.program       ?? '',
  }
}

function transformMatchOfWeek(m: any): MatchOfWeek {
  return {
    homeTeamName: m.HomeTeamName ?? m.homeTeamName ?? '',
    homeLogoUrl:  m.HomeLogoUrl  ?? m.homeLogoUrl,
    homeElo:      m.HomeElo      ?? m.homeElo      ?? 0,
    awayTeamName: m.AwayTeamName ?? m.awayTeamName ?? '',
    awayLogoUrl:  m.AwayLogoUrl  ?? m.awayLogoUrl,
    awayElo:      m.AwayElo      ?? m.awayElo      ?? 0,
    matchDate:    m.MatchDate    ?? m.matchDate    ?? '',
    combinedElo:  m.CombinedElo  ?? m.combinedElo  ?? 0,
    program:      m.Program      ?? m.program      ?? '',
  }
}

function transformStats(raw: any): HomepageStats {
  const topElo = (obj: any): Record<string, MiniRanking[]> => {
    const result: Record<string, MiniRanking[]> = {}
    const source = obj ?? {}
    for (const key of Object.keys(source)) {
      result[key] = (source[key] as any[]).map(transformMiniRanking)
    }
    return result
  }

  const upsets = (obj: any): Record<string, UpsetInfo> => {
    const result: Record<string, UpsetInfo> = {}
    const source = obj ?? {}
    for (const key of Object.keys(source)) {
      result[key] = transformUpset(source[key])
    }
    return result
  }

  const motw = (obj: any): Record<string, MatchOfWeek> => {
    const result: Record<string, MatchOfWeek> = {}
    const source = obj ?? {}
    for (const key of Object.keys(source)) {
      result[key] = transformMatchOfWeek(source[key])
    }
    return result
  }

  const qs = raw.QuickStats ?? raw.quickStats ?? {}

  return {
    academyTopElo:       topElo(raw.AcademyTopElo ?? raw.academyTopElo),
    homegrownTopElo:     topElo(raw.HomegrownTopElo ?? raw.homegrownTopElo),
    festHomegrownRegions: (raw.FestHomegrownRegions ?? raw.festHomegrownRegions ?? []).map(transformRegionDominance),
    festAcademyRegions:   (raw.FestAcademyRegions ?? raw.festAcademyRegions ?? []).map(transformRegionDominance),
    biggestUpsets:        upsets(raw.BiggestUpsets ?? raw.biggestUpsets),
    matchesOfTheWeek:     motw(raw.MatchesOfTheWeek ?? raw.matchesOfTheWeek),
    quickStats: {
      totalMatches:     qs.TotalMatches     ?? qs.totalMatches     ?? 0,
      totalTeams:       qs.TotalTeams       ?? qs.totalTeams       ?? 0,
      totalRegions:     qs.TotalRegions     ?? qs.totalRegions     ?? 0,
      completedMatches: qs.CompletedMatches ?? qs.completedMatches ?? 0,
    },
  }
}

function HomePage() {
  const [stats, setStats] = useState<HomepageStats | null>(null)
  const [loading, setLoading] = useState(true)
  const [eloTab, setEloTab] = useState('U17')
  const [spotlightTab, setSpotlightTab] = useState('U17')

  useEffect(() => {
    if (!API_BASE) { setLoading(false); return }
    fetch(`${API_BASE}/homepagestats`)
      .then(r => r.ok ? r.json() : null)
      .then(data => { if (data) setStats(transformStats(data)) })
      .catch(err => console.error('Error fetching homepage stats:', err))
      .finally(() => setLoading(false))
  }, [])

  const hasEloData = stats && (
    Object.keys(stats.academyTopElo).length > 0 ||
    Object.keys(stats.homegrownTopElo).length > 0
  )

  return (
    <div className="home-page">
      <div className="home-container">
        <div className="home-content">
          <h2 className="home-title">Welcome to YSI</h2>
          <p className="home-subtitle">Youth Soccer Intelligence</p>

          <div className="home-cards">
            <Link to="/Schedules" className="home-card schedules-card">
              <div className="card-icon">📅</div>
              <h3>Schedules</h3>
              <p>Browse match schedules and results</p>
            </Link>

            <Link to="/Standings" className="home-card standings-card">
              <div className="card-icon">🏆</div>
              <h3>Standings</h3>
              <p>View league standings and rankings</p>
            </Link>

            <Link to="/Analytics" className="home-card analytics-card">
              <div className="card-icon">📊</div>
              <h3>Analytics</h3>
              <p>Team form, momentum, and performance metrics</p>
            </Link>
          </div>

          {/* Quick Stats Bar */}
          {stats && (
            <div className="quick-stats-bar">
              <div className="stat-pill">
                <span className="stat-value">{stats.quickStats.totalMatches.toLocaleString()}</span>
                <span className="stat-label">Matches</span>
              </div>
              <div className="stat-pill">
                <span className="stat-value">{stats.quickStats.totalTeams.toLocaleString()}</span>
                <span className="stat-label">Teams</span>
              </div>
              <div className="stat-pill">
                <span className="stat-value">{stats.quickStats.totalRegions}</span>
                <span className="stat-label">Regions</span>
              </div>
              <div className="stat-pill">
                <span className="stat-value">{stats.quickStats.completedMatches.toLocaleString()}</span>
                <span className="stat-label">Completed</span>
              </div>
            </div>
          )}

          {loading && <div className="home-loading">Loading analytics...</div>}

          {/* Top 5 ELO Leaderboards */}
          {hasEloData && (
            <div className="home-section">
              <div className="age-tabs">
                {AGE_GROUPS.map(ag => (
                  <button
                    key={ag}
                    className={`age-tab ${eloTab === ag ? 'active' : ''}`}
                    onClick={() => setEloTab(ag)}
                  >{ag}</button>
                ))}
              </div>

              <div className="elo-leaderboards">
                <EloTable
                  title="MLS Next Homegrown Top 5"
                  data={stats!.homegrownTopElo[eloTab] ?? []}
                  program="homegrown"
                  ageGroup={eloTab}
                />
                <EloTable
                  title="MLS Next Academy Top 5"
                  data={stats!.academyTopElo[eloTab] ?? []}
                  program="academy"
                  ageGroup={eloTab}
                />
              </div>
            </div>
          )}

          {/* Biggest Upset + Match of the Week */}
          {stats && (Object.keys(stats.biggestUpsets).length > 0 || Object.keys(stats.matchesOfTheWeek).length > 0) && (
            <div className="home-section">
              <div className="age-tabs">
                {AGE_GROUPS.map(ag => (
                  <button
                    key={ag}
                    className={`age-tab ${spotlightTab === ag ? 'active' : ''}`}
                    onClick={() => setSpotlightTab(ag)}
                  >{ag}</button>
                ))}
              </div>

              <div className="spotlight-row">
                {stats.biggestUpsets[spotlightTab] && (
                  <UpsetCard upset={stats.biggestUpsets[spotlightTab]} />
                )}
                {stats.matchesOfTheWeek[spotlightTab] && (
                  <MatchOfWeekCard match={stats.matchesOfTheWeek[spotlightTab]} />
                )}
              </div>
              {!stats.biggestUpsets[spotlightTab] && !stats.matchesOfTheWeek[spotlightTab] && (
                <p className="no-data-msg">No spotlight data for {spotlightTab}</p>
              )}
            </div>
          )}

          {/* FEST Region Dominance */}
          {stats && (stats.festHomegrownRegions.length > 0 || stats.festAcademyRegions.length > 0) && (
            <div className="home-section">
              <h3 className="section-title">2025 MLS Next Fest - Most Dominant Regions</h3>
              <div className="fest-tables">
                {stats.festHomegrownRegions.length > 0 && (
                  <FestTable title="Homegrown" data={stats.festHomegrownRegions} />
                )}
                {stats.festAcademyRegions.length > 0 && (
                  <FestTable title="Academy" data={stats.festAcademyRegions} />
                )}
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}

function EloTable({ title, data, program, ageGroup }: {
  title: string
  data: MiniRanking[]
  program: string
  ageGroup: string
}) {
  if (data.length === 0) return null
  return (
    <div className="elo-table-wrapper">
      <div className="elo-table-header">
        <h4>{title}</h4>
        <Link to={`/Analytics?program=${program}&ageGroup=${encodeURIComponent(ageGroup)}`} className="view-all-link">
          View Full Rankings
        </Link>
      </div>
      <table className="elo-table">
        <thead>
          <tr>
            <th>#</th>
            <th>Team</th>
            <th>Region</th>
            <th>ELO</th>
            <th>Delta</th>
          </tr>
        </thead>
        <tbody>
          {data.map(r => (
            <tr key={r.teamName}>
              <td className="rank-cell">{r.rank}</td>
              <td className="team-cell">
                {r.logoUrl && <img src={r.logoUrl} alt="" className="team-logo-sm" />}
                <span>{r.teamName}</span>
              </td>
              <td className="region-cell">{r.regionName}</td>
              <td className="elo-cell">{r.eloRating}</td>
              <td className={`delta-cell ${r.eloDelta > 0 ? 'positive' : r.eloDelta < 0 ? 'negative' : ''}`}>
                {r.eloDelta > 0 ? '+' : ''}{r.eloDelta}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function UpsetCard({ upset }: { upset: UpsetInfo }) {
  return (
    <div className="spotlight-card upset-card">
      <div className="spotlight-label">Biggest Upset (Last 30 Days)</div>
      <div className="spotlight-program">{upset.program}</div>
      <div className="matchup">
        <div className="matchup-team winner">
          {upset.winnerLogoUrl && <img src={upset.winnerLogoUrl} alt="" className="team-logo-md" />}
          <span className="team-name">{upset.winnerName}</span>
          <span className="elo-badge">{upset.winnerElo}</span>
        </div>
        <div className="matchup-score">{upset.score}</div>
        <div className="matchup-team loser">
          {upset.loserLogoUrl && <img src={upset.loserLogoUrl} alt="" className="team-logo-md" />}
          <span className="team-name">{upset.loserName}</span>
          <span className="elo-badge">{upset.loserElo}</span>
        </div>
      </div>
      <div className="upset-diff">+{upset.eloDiff} ELO gap</div>
      <div className="spotlight-date">{upset.matchDate}</div>
    </div>
  )
}

function MatchOfWeekCard({ match }: { match: MatchOfWeek }) {
  return (
    <div className="spotlight-card motw-card">
      <div className="spotlight-label">Match of the Week</div>
      <div className="spotlight-program">{match.program}</div>
      <div className="matchup">
        <div className="matchup-team">
          {match.homeLogoUrl && <img src={match.homeLogoUrl} alt="" className="team-logo-md" />}
          <span className="team-name">{match.homeTeamName}</span>
          <span className="elo-badge">{match.homeElo}</span>
        </div>
        <div className="matchup-vs">VS</div>
        <div className="matchup-team">
          {match.awayLogoUrl && <img src={match.awayLogoUrl} alt="" className="team-logo-md" />}
          <span className="team-name">{match.awayTeamName}</span>
          <span className="elo-badge">{match.awayElo}</span>
        </div>
      </div>
      <div className="motw-combined">Combined ELO: {match.combinedElo}</div>
      <div className="spotlight-date">{match.matchDate}</div>
    </div>
  )
}

function FestTable({ title, data }: { title: string; data: RegionDominance[] }) {
  return (
    <div className="fest-table-wrapper">
      <h4>{title}</h4>
      <table className="fest-table">
        <thead>
          <tr>
            <th>#</th>
            <th>Region</th>
            <th>W-L</th>
            <th>GF-GA</th>
            <th>GD</th>
          </tr>
        </thead>
        <tbody>
          {data.map(r => (
            <tr key={r.regionName}>
              <td>{r.rank}</td>
              <td>{r.regionName}</td>
              <td>{r.wins}-{r.losses}</td>
              <td>{r.goalsFor}-{r.goalsAgainst}</td>
              <td className={r.goalDifference > 0 ? 'positive' : r.goalDifference < 0 ? 'negative' : ''}>
                {r.goalDifference > 0 ? '+' : ''}{r.goalDifference}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

export default HomePage
