import React, { useState, useEffect } from 'react'
import { AlertCircle } from 'lucide-react'
import ProgramSelector from '../components/ProgramSelector'
import { Program, AgeGroup, StandingsGroup, Match } from '../types'
import '../components/SeasonSelector.css'
import './StandingsPage.css'

function StandingsPage() {
  const urlParams = new URLSearchParams(window.location.search)

  const [selectedProgram, setSelectedProgram] = useState<Program>(() => {
    const p = urlParams.get('program') as Program
    return p === 'homegrown' || p === 'academy' ? p : 'homegrown'
  })

  const [selectedAgeGroup, setSelectedAgeGroup] = useState<string>(urlParams.get('ageGroup') || '')
  const [selectedRegion, setSelectedRegion]     = useState<string>(urlParams.get('region') || '')

  const [ageGroups, setAgeGroups] = useState<AgeGroup[]>([])
  const [allGroups, setAllGroups] = useState<StandingsGroup[]>([])
  const [ageGroupsLoading, setAgeGroupsLoading] = useState(true)
  const [loading, setLoading]     = useState(false)
  const [error, setError]         = useState<string>('')

  const [expandedTeam, setExpandedTeam]             = useState<string | null>(null)
  const [teamMatchesCache, setTeamMatchesCache]     = useState<Record<string, Match[]>>({})
  const [teamMatchesLoading, setTeamMatchesLoading] = useState<string | null>(null)

  // Sync URL params
  useEffect(() => {
    const params = new URLSearchParams()
    params.set('program', selectedProgram)
    if (selectedAgeGroup) params.set('ageGroup', selectedAgeGroup)
    if (selectedRegion)   params.set('region', selectedRegion)
    history.replaceState(null, '', `?${params.toString()}`)
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
        setAgeGroupsLoading(false)
      })
      .catch(err => {
        console.error('Error fetching age groups:', err)
        setAgeGroupsLoading(false)
      })
  }, [])

  // Fetch standings when program + ageGroup are selected
  useEffect(() => {
    if (!selectedAgeGroup) {
      setAllGroups([])
      setError('')
      return
    }

    const fetchStandings = async () => {
      try {
        setLoading(true)
        setError('')

        const apiBase = import.meta.env.VITE_API_BASE_URL
        if (!apiBase) {
          setError('API not configured')
          setLoading(false)
          return
        }

        const params = new URLSearchParams()
        params.set('program', selectedProgram)
        params.set('ageGroup', selectedAgeGroup)

        const response = await fetch(`${apiBase}/standings?${params.toString()}`)
        if (!response.ok) throw new Error(`HTTP ${response.status}`)

        const data: any[] = await response.json()

        const groups: StandingsGroup[] = data.map((g: any) => ({
          regionName: g.RegionName ?? g.regionName,
          standings: (g.Standings ?? g.standings ?? []).map((row: any) => ({
            rank:     row.Rank     ?? row.rank     ?? 0,
            teamName: row.TeamName ?? row.teamName ?? '',
            logoUrl:  row.LogoUrl  ?? row.logoUrl,
            gp:       row.GP       ?? row.gp       ?? 0,
            w:        row.W        ?? row.w        ?? 0,
            d:        row.D        ?? row.d        ?? 0,
            l:        row.L        ?? row.l        ?? 0,
            gf:       row.GF       ?? row.gf       ?? 0,
            ga:       row.GA       ?? row.ga       ?? 0,
            gd:       row.GD       ?? row.gd       ?? 0,
            pts:      row.Pts      ?? row.pts      ?? 0,
            ppm:      row.PPM      ?? row.ppm      ?? 0,
            wpm:      row.WPM      ?? row.wpm      ?? 0,
            gdpm:     row.GDPM     ?? row.gdpm     ?? 0,
            gpm:      row.GPM      ?? row.gpm      ?? 0,
          }))
        }))

        setAllGroups(groups)

        // If the region from URL is no longer present in the new data, clear it
        if (selectedRegion && !groups.some(g => g.regionName === selectedRegion)) {
          setSelectedRegion('')
        }
      } catch (err) {
        console.error('Error fetching standings:', err)
        setError(err instanceof Error ? err.message : 'Failed to load standings')
        setAllGroups([])
      } finally {
        setLoading(false)
      }
    }

    fetchStandings()
  }, [selectedProgram, selectedAgeGroup])

  const handleProgramChange = (programs: Program[]) => {
    setSelectedProgram(programs[0] || 'homegrown')
    setAllGroups([])
    setSelectedRegion('')
    setExpandedTeam(null)
    setTeamMatchesCache({})
  }

  const handleTeamClick = async (teamName: string) => {
    if (expandedTeam === teamName) {
      setExpandedTeam(null)
      return
    }
    setExpandedTeam(teamName)
    if (teamMatchesCache[teamName]) return // already fetched

    try {
      setTeamMatchesLoading(teamName)
      const apiBase = import.meta.env.VITE_API_BASE_URL
      const params = new URLSearchParams()
      params.set('program', selectedProgram)
      params.set('ageGroup', selectedAgeGroup)
      params.set('team', teamName)
      const res = await fetch(`${apiBase}/matches?${params.toString()}`)
      if (!res.ok) throw new Error(`HTTP ${res.status}`)
      const data: any[] = await res.json()
      const matches: Match[] = data.map((m: any) => ({
        matchId:      m.matchId      ?? m.MatchId,
        matchDateUtc: m.matchDateUtc ?? m.MatchDateUtc,
        score:        m.score        ?? m.Score ?? null,
        gender:       m.gender       ?? m.Gender ?? '',
        homeTeam: {
          id:      m.HomeTeam?.Id      || m.homeTeam?.id      || 0,
          name:    m.HomeTeam?.Name    || m.homeTeam?.name    || '',
          logoUrl: m.HomeTeam?.LogoUrl || m.homeTeam?.logoUrl,
        },
        awayTeam: {
          id:      m.AwayTeam?.Id      || m.awayTeam?.id      || 0,
          name:    m.AwayTeam?.Name    || m.awayTeam?.name    || '',
          logoUrl: m.AwayTeam?.LogoUrl || m.awayTeam?.logoUrl,
        },
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

  // Region options come from the fetched data
  const regionOptions = allGroups.map(g => g.regionName).sort((a, b) => a.localeCompare(b))

  // Filter displayed groups by selected region
  const displayedGroups = selectedRegion
    ? allGroups.filter(g => g.regionName === selectedRegion)
    : allGroups

  const isFiltersComplete = selectedProgram && selectedAgeGroup

  return (
    <div className="standings-page">
      {ageGroupsLoading ? (
        <div className="standings-loading">
          <div className="standings-spinner" />
          <p>Loading…</p>
        </div>
      ) : (
        <>
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

          <div className="standings-filters">
            <div className="standings-filter-group">
              <label htmlFor="agegroup-select">Age Group</label>
              <select
                id="agegroup-select"
                value={selectedAgeGroup}
                onChange={e => {
                  setSelectedAgeGroup(e.target.value)
                  setSelectedRegion('')
                  setExpandedTeam(null)
                  setTeamMatchesCache({})
                }}
                className="standings-filter-select"
              >
                <option value="">Select an age group</option>
                {ageGroups.map(ag => (
                  <option key={ag.id} value={ag.name}>{ag.name}</option>
                ))}
              </select>
            </div>

            <div className="standings-filter-group">
              <label htmlFor="region-select">Region</label>
              <select
                id="region-select"
                value={selectedRegion}
                onChange={e => setSelectedRegion(e.target.value)}
                className="standings-filter-select"
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
              <p>Select an age group to view standings</p>
            </div>
          )}

          {error && (
            <div className="standings-error">
              <AlertCircle size={16} />
              {error}
            </div>
          )}

          {loading && (
            <div className="standings-loading">
              <div className="standings-spinner" />
              <p>Loading standings…</p>
            </div>
          )}

          {isFiltersComplete && !loading && allGroups.length === 0 && !error && (
            <div className="no-results">
              <p>No standings available yet</p>
            </div>
          )}

          {displayedGroups.map(group => (
            <div key={group.regionName} className="standings-group">
              <h3 className="standings-region-heading">{group.regionName}</h3>
              <div className="standings-table-wrapper">
                <table className="standings-table">
                  <thead>
                    <tr>
                      <th className="col-rank">#</th>
                      <th className="col-team">Team</th>
                      <th className="col-gp">GP</th>
                      <th className="col-record">W-D-L</th>
                      <th className="col-goals" title="Goals For">GF</th>
                      <th className="col-goals" title="Goals Against">GA</th>
                      <th className="col-goals" title="Goal Differential">GD</th>
                      <th className="col-pts">Pts</th>
                      <th className="col-ppm" title="Points Per Match">PPM</th>
                      <th className="col-wpm" title="Wins Per Match">WPM</th>
                      <th className="col-gdpm" title="Goal Differential Per Match">GDPM</th>
                      <th className="col-gpm" title="Goals For Per Match">GPM</th>
                    </tr>
                  </thead>
                  <tbody>
                    {group.standings.map((row, idx) => (
                      <React.Fragment key={`${group.regionName}-${row.rank}`}>
                        <tr
                          className={`${idx % 2 === 0 ? 'even' : 'odd'} standings-team-row${expandedTeam === row.teamName ? ' expanded' : ''}`}
                          onClick={() => handleTeamClick(row.teamName)}
                        >
                          <td className="col-rank">{row.rank}</td>
                          <td className="col-team">
                            <span className={`standings-chevron${expandedTeam === row.teamName ? ' open' : ''}`}>▶</span>
                            {row.logoUrl && (
                              <img src={row.logoUrl} alt={row.teamName} className="standings-team-logo" />
                            )}
                            <span className="standings-team-name">{row.teamName}</span>
                          </td>
                          <td className="col-gp">{row.gp}</td>
                          <td className="col-record">{row.w}-{row.d}-{row.l}</td>
                          <td className="col-goals">{row.gf}</td>
                          <td className="col-goals">{row.ga}</td>
                          <td className="col-goals">{row.gd > 0 ? `+${row.gd}` : row.gd}</td>
                          <td className="col-pts">{row.pts}</td>
                          <td className="col-ppm">{typeof row.ppm === 'number' ? row.ppm.toFixed(3) : row.ppm}</td>
                          <td className="col-wpm">{typeof row.wpm === 'number' ? row.wpm.toFixed(3) : row.wpm}</td>
                          <td className="col-gdpm">{typeof row.gdpm === 'number' ? row.gdpm.toFixed(3) : row.gdpm}</td>
                          <td className="col-gpm">{typeof row.gpm === 'number' ? row.gpm.toFixed(3) : row.gpm}</td>
                        </tr>

                        {expandedTeam === row.teamName && (
                          <tr className="team-matches-expansion">
                            <td colSpan={12}>
                              {teamMatchesLoading === row.teamName ? (
                                <div className="team-matches-loading">
                                  <div className="standings-spinner small" /> Loading matches…
                                </div>
                              ) : (teamMatchesCache[row.teamName] ?? []).length === 0 ? (
                                <div className="team-matches-empty">No matches found</div>
                              ) : (
                                <ul className="team-matches-list">
                                  {(teamMatchesCache[row.teamName] ?? [])
                                    .filter(m => m.score && m.score !== 'TBD')
                                    .sort((a, b) => new Date(a.matchDateUtc).getTime() - new Date(b.matchDateUtc).getTime())
                                    .map(m => {
                                      const date = new Date(m.matchDateUtc).toLocaleDateString('en-US', { month: 'short', day: 'numeric' })
                                      return (
                                        <li key={m.matchId} className="team-match-row">
                                          <span className="match-date">{date}</span>
                                          <span className="match-home">
                                            {m.homeTeam.logoUrl && <img src={m.homeTeam.logoUrl} alt="" className="match-logo" />}
                                            <span className="match-name">{m.homeTeam.name}</span>
                                          </span>
                                          <span className="match-score">{m.score}</span>
                                          <span className="match-away">
                                            {m.awayTeam.logoUrl && <img src={m.awayTeam.logoUrl} alt="" className="match-logo" />}
                                            <span className="match-name">{m.awayTeam.name}</span>
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
            </div>
          ))}
        </>
      )}
    </div>
  )
}

export default StandingsPage
