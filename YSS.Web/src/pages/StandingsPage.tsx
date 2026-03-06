import { useState, useEffect } from 'react'
import { AlertCircle } from 'lucide-react'
import ProgramSelector from '../components/ProgramSelector'
import { Program, AgeGroup, StandingsGroup } from '../types'
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
  const [loading, setLoading]     = useState(false)
  const [error, setError]         = useState<string>('')

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
            pts:      row.Pts      ?? row.pts      ?? 0,
            ppm:      row.PPM      ?? row.ppm      ?? 0,
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
  }

  // Region options come from the fetched data
  const regionOptions = allGroups.map(g => g.regionName)

  // Filter displayed groups by selected region
  const displayedGroups = selectedRegion
    ? allGroups.filter(g => g.regionName === selectedRegion)
    : allGroups

  const isFiltersComplete = selectedProgram && selectedAgeGroup

  return (
    <div className="standings-page">
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
            onChange={e => { setSelectedAgeGroup(e.target.value); setSelectedRegion('') }}
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
                  <th className="col-pts">Pts</th>
                  <th className="col-ppm">PPM</th>
                </tr>
              </thead>
              <tbody>
                {group.standings.map((row, idx) => (
                  <tr key={`${group.regionName}-${row.rank}`} className={idx % 2 === 0 ? 'even' : 'odd'}>
                    <td className="col-rank">{row.rank}</td>
                    <td className="col-team">
                      {row.logoUrl && (
                        <img src={row.logoUrl} alt={row.teamName} className="standings-team-logo" />
                      )}
                      <span className="standings-team-name">{row.teamName}</span>
                    </td>
                    <td className="col-gp">{row.gp}</td>
                    <td className="col-record">{row.w}-{row.d}-{row.l}</td>
                    <td className="col-pts">{row.pts}</td>
                    <td className="col-ppm">{typeof row.ppm === 'number' ? row.ppm.toFixed(3) : row.ppm}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      ))}
    </div>
  )
}

export default StandingsPage
