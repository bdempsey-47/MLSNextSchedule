import { useState, useEffect } from 'react'
import { AlertCircle } from 'lucide-react'
import ProgramSelector from '../components/ProgramSelector'
import { Program, AgeGroup, TeamAnalytics } from '../types'
import '../components/SeasonSelector.css'
import './AnalyticsPage.css'

function AnalyticsPage() {
  const urlParams = new URLSearchParams(window.location.search)

  const [selectedProgram, setSelectedProgram] = useState<Program>(() => {
    const p = urlParams.get('program') as Program
    return p === 'homegrown' || p === 'academy' ? p : 'homegrown'
  })

  const [selectedAgeGroup, setSelectedAgeGroup] = useState<string>(urlParams.get('ageGroup') || '')
  const [selectedRegion, setSelectedRegion]     = useState<string>(urlParams.get('region') || '')

  const [ageGroups, setAgeGroups]   = useState<AgeGroup[]>([])
  const [allTeams, setAllTeams]     = useState<TeamAnalytics[]>([])
  const [loading, setLoading]       = useState(false)
  const [error, setError]           = useState<string>('')

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
      })
      .catch(err => console.error('Error fetching age groups:', err))
  }, [])

  // Fetch analytics when program + ageGroup selected
  useEffect(() => {
    if (!selectedAgeGroup) {
      setAllTeams([])
      setError('')
      return
    }

    const fetchAnalytics = async () => {
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
        if (selectedRegion) params.set('region', selectedRegion)

        const response = await fetch(`${apiBase}/analytics?${params.toString()}`)
        if (!response.ok) throw new Error(`HTTP ${response.status}`)

        const data: any[] = await response.json()
        const teams: TeamAnalytics[] = data.map((t: any) => ({
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

        if (selectedRegion && !teams.some(t => t.regionNames.includes(selectedRegion))) {
          setSelectedRegion('')
        }
      } catch (err) {
        console.error('Error fetching analytics:', err)
        setError(err instanceof Error ? err.message : 'Failed to load analytics')
        setAllTeams([])
      } finally {
        setLoading(false)
      }
    }

    fetchAnalytics()
  }, [selectedProgram, selectedAgeGroup, selectedRegion])

  const handleProgramChange = (programs: Program[]) => {
    setSelectedProgram(programs[0] || 'homegrown')
    setAllTeams([])
    setSelectedRegion('')
  }

  // Region options derived from all teams fetched (before region filter applied client-side)
  const regionOptions = [...new Set(allTeams.flatMap(t => t.regionNames))].sort((a, b) => a.localeCompare(b))

  // Apply region filter client-side on the current data
  const displayedTeams = selectedRegion
    ? allTeams.filter(t => t.regionNames.includes(selectedRegion))
    : allTeams

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
            {ageGroups.map(ag => (
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
                <th className="col-last8">Last 8</th>
                <th className="col-momentum">Momentum</th>
                <th className="col-form">Form</th>
              </tr>
            </thead>
            <tbody>
              {displayedTeams.map((team, idx) => (
                <tr key={`${team.teamName}-${idx}`} className={idx % 2 === 0 ? 'even' : 'odd'}>
                  <td className="col-rank">{idx + 1}</td>
                  <td className="col-team">
                    <div className="team-cell-inner">
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
                  <td className="col-last8">
                    <div className="last5-badges">
                      {team.last8.map((result, i) => (
                        <span key={i} className={`result-badge ${result === 'W' ? 'win' : result === 'D' ? 'draw' : 'loss'} ${i >= 5 ? 'deemphasized' : ''}`}>
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
                  <td className="col-form">
                    {team.momentumLabel}
                    {team.gp < 5 && (
                      <span className="limited-data-note">⚠ {team.gp} match{team.gp !== 1 ? 'es' : ''}</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

function getMomentumArrow(score: number): string {
  if (score >= 60) return '↗'
  if (score >= 40) return '→'
  return '↘'
}

export default AnalyticsPage
