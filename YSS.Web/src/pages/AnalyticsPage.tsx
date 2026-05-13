import React, { useState, useEffect } from 'react'
import { useSearchParams } from 'react-router-dom'
import { AlertCircle } from 'lucide-react'
import ProgramSelector from '../components/ProgramSelector'
import { Program, AgeGroup, TeamAnalytics, PowerRanking, Match } from '../types'
import '../components/SeasonSelector.css'
import './AnalyticsPage.css'

interface CombinedTeamRow {
  rank: number
  teamName: string
  logoUrl?: string
  regionName: string
  regionNames: string[]
  gp: number
  sos: number
  last5: string[]
  momentumScore: number
  eloRating: number | null
  eloDelta: number | null
  rankChange: number | null | undefined
}

function EloInfoModal({ onClose }: { onClose: () => void }) {
  return (
    <div className="elo-modal-backdrop" onClick={onClose}>
      <div className="elo-modal" onClick={e => e.stopPropagation()}>
        <button className="elo-modal-close" onClick={onClose}>×</button>
        <h3>How ELO Rating Works</h3>
        <p>
          ELO is a rating system that measures relative team strength based on match results.
          Every team starts at <strong>1500</strong>. After each match, the winner gains points
          and the loser loses points.
        </p>
        <h4>Key factors</h4>
        <ul>
          <li><strong>Upset bonus:</strong> Beating a higher-rated team earns more points than beating a lower-rated one.</li>
          <li><strong>Margin of victory:</strong> A 1-goal win uses a 1.0x multiplier, 2-goal win uses 1.5x, and 3+ goals uses 1.75x.</li>
          <li><strong>Rolling window:</strong> Only matches from the last 12 months are included.</li>
          <li><strong>Minimum games:</strong> Teams need at least 3 matches to appear in rankings.</li>
        </ul>
        <h4>Reading the table</h4>
        <ul>
          <li><strong>ELO</strong> — Current rating. Higher is better. 1500 is average.</li>
          <li><strong>Δ (Delta)</strong> — Net rating change over the last 5 matches. Green = trending up, red = trending down.</li>
        </ul>
      </div>
    </div>
  )
}

function AnalyticsPage() {
  const [searchParams, setSearchParams] = useSearchParams()

  const [selectedProgram, setSelectedProgram] = useState<Program>(() => {
    const p = searchParams.get('program') as Program
    return p === 'homegrown' || p === 'academy' ? p : 'homegrown'
  })

  const [selectedAgeGroup, setSelectedAgeGroup] = useState<string>(searchParams.get('ageGroup') || '')
  const [selectedRegion, setSelectedRegion]     = useState<string>(searchParams.get('region') || '')

  const [ageGroups, setAgeGroups]   = useState<AgeGroup[]>([])
  const [allTeams, setAllTeams]     = useState<TeamAnalytics[]>([])
  const [powerRankings, setPowerRankings] = useState<PowerRanking[]>([])
  const [loading, setLoading]       = useState(false)
  const [error, setError]           = useState<string>('')
  const [showEloInfo, setShowEloInfo] = useState(false)

  const [expandedTeam, setExpandedTeam]           = useState<string | null>(null)
  const [teamMatchesCache, setTeamMatchesCache]   = useState<Record<string, Match[]>>({})
  const [teamMatchesLoading, setTeamMatchesLoading] = useState<string | null>(null)

  // Sync URL params
  useEffect(() => {
    const params = new URLSearchParams()
    params.set('program', selectedProgram)
    if (selectedAgeGroup) params.set('ageGroup', selectedAgeGroup)
    if (selectedRegion)   params.set('region', selectedRegion)
    setSearchParams(params, { replace: true })
  }, [selectedProgram, selectedAgeGroup, selectedRegion])

  // Fetch age groups on mount
  useEffect(() => {
    const apiBase = import.meta.env.VITE_API_BASE_URL
    if (!apiBase) return
    fetch(`${apiBase}/agegroups`)
      .then(r => r.ok ? r.json() : [])
      .then(data => {
        const transformed: AgeGroup[] = data
          .map((ag: any) => ({ id: ag.Id ?? ag.id, name: ag.Name ?? ag.name }))
          .sort((a: AgeGroup, b: AgeGroup) => a.name.localeCompare(b.name))
        setAgeGroups(transformed)
      })
      .catch(err => console.error('Error fetching age groups:', err))
  }, [])

  // Fetch both analytics + power rankings in parallel when program + ageGroup selected
  useEffect(() => {
    if (!selectedAgeGroup) {
      setAllTeams([])
      setPowerRankings([])
      setError('')
      return
    }

    const fetchData = async () => {
      try {
        setLoading(true)
        setError('')

        const apiBase = import.meta.env.VITE_API_BASE_URL
        if (!apiBase) {
          setError('API not configured')
          setLoading(false)
          return
        }

        setExpandedTeam(null)
        setTeamMatchesCache({})

        const analyticsParams = new URLSearchParams()
        analyticsParams.set('program', selectedProgram)
        analyticsParams.set('ageGroup', selectedAgeGroup)

        const prParams = new URLSearchParams()
        prParams.set('program', selectedProgram)
        prParams.set('ageGroup', selectedAgeGroup)
        if (selectedRegion) prParams.set('region', selectedRegion)

        const [analyticsRes, prRes] = await Promise.all([
          fetch(`${apiBase}/analytics?${analyticsParams.toString()}`),
          fetch(`${apiBase}/powerrankings?${prParams.toString()}`)
        ])

        if (!analyticsRes.ok) throw new Error(`Analytics HTTP ${analyticsRes.status}`)

        const analyticsData: any[] = await analyticsRes.json()
        const teams: TeamAnalytics[] = analyticsData.map((t: any) => ({
          teamName:      t.TeamName      ?? t.teamName      ?? '',
          logoUrl:       t.LogoUrl       ?? t.logoUrl,
          regionName:    t.RegionName    ?? t.regionName    ?? '',
          regionNames:   t.RegionNames   ?? t.regionNames   ?? [],
          momentumScore: t.MomentumScore ?? t.momentumScore ?? 0,
          momentumLabel: t.MomentumLabel ?? t.momentumLabel ?? '',
          last8:         t.Last8         ?? t.last8         ?? [],
          gp:            t.GP            ?? t.gp            ?? 0,
          sos:           t.Sos           ?? t.sos           ?? 0,
        }))
        setAllTeams(teams)

        if (prRes.ok) {
          const prData: any[] = await prRes.json()
          const rankings: PowerRanking[] = prData.map((t: any) => ({
            rank:        t.Rank        ?? t.rank        ?? 0,
            teamName:    t.TeamName    ?? t.teamName    ?? '',
            logoUrl:     t.LogoUrl     ?? t.logoUrl,
            regionName:  t.RegionName  ?? t.regionName  ?? '',
            regionNames: t.RegionNames ?? t.regionNames ?? [],
            eloRating:   t.EloRating   ?? t.eloRating   ?? 0,
            eloDelta:    t.EloDelta    ?? t.eloDelta    ?? 0,
            rankChange:  t.RankChange  !== undefined ? (t.RankChange ?? t.rankChange) : null,
            gp:          t.GP          ?? t.gp          ?? 0,
          }))
          setPowerRankings(rankings)
        } else {
          setPowerRankings([])
        }

        if (selectedRegion && !teams.some(t => t.regionNames.includes(selectedRegion))) {
          setSelectedRegion('')
        }
      } catch (err) {
        console.error('Error fetching analytics:', err)
        setError(err instanceof Error ? err.message : 'Failed to load analytics')
        setAllTeams([])
        setPowerRankings([])
      } finally {
        setLoading(false)
      }
    }

    fetchData()
  }, [selectedProgram, selectedAgeGroup])

  const handleProgramChange = (programs: Program[]) => {
    setSelectedProgram(programs[0] || 'homegrown')
    setAllTeams([])
    setPowerRankings([])
    setSelectedRegion('')
    setExpandedTeam(null)
    setTeamMatchesCache({})
  }

  const handleTeamClick = async (teamName: string) => {
    if (expandedTeam === teamName) { setExpandedTeam(null); return }
    setExpandedTeam(teamName)
    if (teamMatchesCache[teamName]) return

    try {
      setTeamMatchesLoading(teamName)
      const apiBase = import.meta.env.VITE_API_BASE_URL
      const params = new URLSearchParams()
      params.set('program', selectedProgram)
      params.set('ageGroup', selectedAgeGroup)
      params.set('team', teamName)
      const res = await fetch(`${apiBase}/matches?${params.toString()}`)
      if (!res.ok) throw new Error(`HTTP ${res.status}`)
      const json = await res.json()
      const data: any[] = Array.isArray(json) ? json : (json.matches ?? [])
      const matches: Match[] = data.map((m: any) => ({
        matchId:      m.matchId      ?? m.MatchId,
        matchDateUtc: m.matchDateUtc ?? m.MatchDateUtc,
        score:        m.score        ?? m.Score        ?? null,
        gender:       m.gender       ?? m.Gender       ?? '',
        homeTeam:    { id: m.HomeTeam?.Id    || m.homeTeam?.id    || 0, name: m.HomeTeam?.Name    || m.homeTeam?.name    || '', logoUrl: m.HomeTeam?.LogoUrl || m.homeTeam?.logoUrl },
        awayTeam:    { id: m.AwayTeam?.Id    || m.awayTeam?.id    || 0, name: m.AwayTeam?.Name    || m.awayTeam?.name    || '', logoUrl: m.AwayTeam?.LogoUrl || m.awayTeam?.logoUrl },
        venue:       { id: m.Venue?.Id       || m.venue?.id       || 0, name: m.Venue?.Name       || m.venue?.name       || '' },
        ageGroup:    { id: m.AgeGroup?.Id    || m.ageGroup?.id    || 0, name: m.AgeGroup?.Name    || m.ageGroup?.name    || '' },
        region:      { id: m.Region?.Id      || m.region?.id      || 0, name: m.Region?.Name      || m.region?.name      || '' },
        competition: { id: m.Competition?.Id || m.competition?.id || 0, name: m.Competition?.Name || m.competition?.name || '' },
      }))
      setTeamMatchesCache(prev => ({ ...prev, [teamName]: matches }))
    } catch (e) {
      console.error('Failed to fetch team matches', e)
      setTeamMatchesCache(prev => ({ ...prev, [teamName]: [] }))
    } finally {
      setTeamMatchesLoading(null)
    }
  }

  // Region options derived from all teams fetched
  const regionOptions = [...new Set(allTeams.flatMap(t => t.regionNames))].sort((a, b) => a.localeCompare(b))

  // Build ELO lookup by team name
  const eloByTeam = new Map(powerRankings.map(pr => [pr.teamName, pr]))

  // Combine momentum + ELO into unified rows, apply region filter, sort by ELO
  const displayedTeams: CombinedTeamRow[] = (selectedRegion
    ? allTeams.filter(t => t.regionNames.includes(selectedRegion))
    : allTeams
  ).map(team => {
    const elo = eloByTeam.get(team.teamName)
    return {
      rank: elo?.rank ?? 0,
      teamName: team.teamName,
      logoUrl: team.logoUrl,
      regionName: team.regionName,
      regionNames: team.regionNames,
      gp: team.gp,
      sos: team.sos,
      last5: team.last8.slice(0, 5),
      momentumScore: team.momentumScore,
      eloRating: elo?.eloRating ?? null,
      eloDelta: elo?.eloDelta ?? null,
      rankChange: elo?.rankChange ?? null,
    }
  }).sort((a, b) => {
    // Teams with ELO first, sorted descending; teams without ELO at the bottom by momentum
    if (a.eloRating != null && b.eloRating != null) return b.eloRating - a.eloRating
    if (a.eloRating != null) return -1
    if (b.eloRating != null) return 1
    return b.momentumScore - a.momentumScore
  })

  const isFiltersComplete = selectedProgram && selectedAgeGroup

  return (
    <div className="analytics-page">
      <div className="controls-bar">
        <ProgramSelector
          selected={[selectedProgram]}
          onChange={handleProgramChange}
          singleSelect
        />
        <div className="controls-divider" />
        <div className="season-selector">
          <span className="selector-label">Season</span>
          <div className="season-buttons">
            <span className="season-button active">2025–2026</span>
          </div>
        </div>
      </div>

      <div className="analytics-filters">
        <div className="analytics-filter-group">
          <label htmlFor="agegroup-select">Age Group</label>
          <select
            id="agegroup-select"
            value={selectedAgeGroup}
            onChange={e => { setSelectedAgeGroup(e.target.value); setSelectedRegion('') }}
            className="analytics-filter-select"
          >
            <option value="">Select an age group</option>
            {ageGroups.filter(ag => ag.name !== 'U18/19').map(ag => (
              <option key={ag.id} value={ag.name}>{ag.name}</option>
            ))}
          </select>
        </div>

        <div className="analytics-filter-group">
          <label htmlFor="region-select">Region</label>
          <select
            id="region-select"
            value={selectedRegion}
            onChange={e => setSelectedRegion(e.target.value)}
            className="analytics-filter-select"
            disabled={regionOptions.length === 0}
          >
            <option value="">All regions</option>
            {regionOptions.map(name => (
              <option key={name} value={name}>{name}</option>
            ))}
          </select>
        </div>

      </div>

      {!isFiltersComplete && !loading && (
        <div className="no-results">
          <p>Select an age group to view analytics</p>
        </div>
      )}

      {error && (
        <div className="analytics-error">
          <AlertCircle size={16} />
          {error}
        </div>
      )}

      {loading && (
        <div className="analytics-loading">
          <div className="analytics-spinner" />
          <p>Loading analytics…</p>
        </div>
      )}

      {isFiltersComplete && !loading && allTeams.length === 0 && !error && (
        <div className="no-results">
          <p>No analytics data available yet</p>
        </div>
      )}

      {displayedTeams.length > 0 && (
        <div className="analytics-table-wrapper">
          <table className="analytics-table">
            <thead>
              <tr>
                <th className="col-rank">#</th>
                <th className="col-team">Team</th>
                <th className="col-region">Region</th>
                <th className="col-gp">GP</th>
                <th className="col-sos" title="Strength of Schedule">SOS</th>
                <th className="col-last5">Last 5</th>
                <th className="col-momentum">Momentum</th>
                <th className="col-elo">ELO <button className="elo-info-btn" onClick={() => setShowEloInfo(true)} title="How ELO works">?</button></th>
                <th className="col-delta">Δ</th>
                <th className="col-rank-change">↑↓</th>
              </tr>
            </thead>
            <tbody>
              {displayedTeams.map((team, idx) => (
                <React.Fragment key={`${team.teamName}-${idx}`}>
                  <tr
                    className={`${idx % 2 === 0 ? 'even' : 'odd'} analytics-team-row${expandedTeam === team.teamName ? ' expanded' : ''}`}
                    onClick={() => handleTeamClick(team.teamName)}
                  >
                    <td className="col-rank">{team.rank || '—'}</td>
                    <td className="col-team">
                      <div className="team-cell-inner">
                        <span className={`analytics-chevron${expandedTeam === team.teamName ? ' open' : ''}`}>▶</span>
                        {team.logoUrl && (
                          <img src={team.logoUrl} alt={team.teamName} className="analytics-team-logo" />
                        )}
                        <span className="analytics-team-name">{team.teamName}</span>
                      </div>
                    </td>
                    <td className="col-region">
                      {team.regionNames.length > 0
                        ? team.regionNames.join(', ')
                        : team.regionName}
                    </td>
                    <td className="col-gp">{team.gp}</td>
                    <td className="col-sos">{team.sos.toFixed(2)}</td>
                    <td className="col-last5">
                      <div className="last5-badges">
                        {team.last5.map((result, i) => (
                          <span key={i} className={`result-badge ${result === 'W' ? 'win' : result === 'D' ? 'draw' : 'loss'}`}>
                            {result}
                          </span>
                        ))}
                      </div>
                    </td>
                    <td className="col-momentum">
                      <span className="momentum-badge">
                        <span className="momentum-arrow">{getMomentumArrow(team.momentumScore)}</span>
                        {team.momentumScore.toFixed(1)}
                      </span>
                    </td>
                    <td className="col-elo">
                      {team.eloRating != null ? (
                        <span className="elo-rating">{team.eloRating}</span>
                      ) : (
                        <span className="elo-na">—</span>
                      )}
                    </td>
                    <td className="col-delta">
                      {team.eloDelta != null ? (
                        <span className={`elo-delta ${team.eloDelta > 2 ? 'positive' : team.eloDelta < -2 ? 'negative' : 'neutral'}`}>
                          {team.eloDelta > 2 ? '↗' : team.eloDelta < -2 ? '↘' : '→'}{' '}
                          {team.eloDelta > 0 ? '+' : ''}{team.eloDelta.toFixed(1)}
                        </span>
                      ) : (
                        <span className="elo-na">—</span>
                      )}
                    </td>
                    <td className="col-rank-change">
                      {team.rankChange !== null && team.rankChange !== undefined ? (
                        <span className={`rank-change ${team.rankChange > 0 ? 'positive' : team.rankChange < 0 ? 'negative' : 'neutral'}`}>
                          {team.rankChange > 0 ? '↑' : team.rankChange < 0 ? '↓' : '–'}{' '}
                          {team.rankChange > 0 ? '+' : ''}{team.rankChange}
                        </span>
                      ) : (
                        <span className="elo-na">—</span>
                      )}
                    </td>
                  </tr>

                  {expandedTeam === team.teamName && (
                    <tr className="team-matches-expansion">
                      <td colSpan={10}>
                        {teamMatchesLoading === team.teamName ? (
                          <div className="team-matches-loading">
                            <div className="analytics-spinner small" /> Loading matches…
                          </div>
                        ) : (teamMatchesCache[team.teamName] ?? []).filter(m => m.score && m.score !== 'TBD').length === 0 ? (
                          <div className="team-matches-empty">No completed matches found</div>
                        ) : (
                          <ul className="team-matches-list">
                            {(teamMatchesCache[team.teamName] ?? [])
                              .filter(m => m.score && m.score !== 'TBD')
                              .sort((a, b) => new Date(a.matchDateUtc).getTime() - new Date(b.matchDateUtc).getTime())
                              .map(m => {
                                const date = new Date(m.matchDateUtc).toLocaleDateString('en-US', { month: 'short', day: 'numeric' })
                                return (
                                  <li key={m.matchId} className="team-match-row">
                                    <span className="match-date">{date}</span>
                                    <span className="match-region">{m.region.name}</span>
                                    <span className="match-home">
                                      <span className="match-name">{m.homeTeam.name.trim()}</span>
                                      {m.homeTeam.logoUrl && <img src={m.homeTeam.logoUrl} alt="" className="match-logo" />}
                                    </span>
                                    <span className="match-score">{m.score}</span>
                                    <span className="match-away">
                                      {m.awayTeam.logoUrl && <img src={m.awayTeam.logoUrl} alt="" className="match-logo" />}
                                      <span className="match-name">{m.awayTeam.name.trim()}</span>
                                    </span>
                                  </li>
                                )
                              })}
                          </ul>
                        )}
                      </td>
                    </tr>
                  )}
                </React.Fragment>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {showEloInfo && <EloInfoModal onClose={() => setShowEloInfo(false)} />}
    </div>
  )
}

function getMomentumArrow(score: number): string {
  if (score >= 60) return '↗'
  if (score >= 40) return '→'
  return '↘'
}

export default AnalyticsPage
