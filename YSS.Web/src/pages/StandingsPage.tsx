import { useState, useEffect } from 'react'
import { AlertCircle, Loader2 } from 'lucide-react'
import ProgramSelector from '../components/ProgramSelector'
import SeasonSelector from '../components/SeasonSelector'
import { Program, Season, AgeGroup, StandingRow } from '../types'
import './StandingsPage.css'

function StandingsPage() {
  const urlParams = new URLSearchParams(window.location.search)

  const [selectedProgram, setSelectedProgram] = useState<Program>(() => {
    const p = urlParams.get('program') as Program
    return p === 'homegrown' || p === 'academy' ? p : 'homegrown'
  })

  const [selectedSeason, setSelectedSeason] = useState<Season>(() => {
    const s = urlParams.get('season') as Season
    return s === 'fall2025' || s === 'spring2026' ? s : 'fall2025'
  })

  const [selectedRegion, setSelectedRegion] = useState<string>(urlParams.get('region') || '')
  const [selectedAgeGroup, setSelectedAgeGroup] = useState<string>(urlParams.get('ageGroup') || '')

  const [regions, setRegions] = useState<{ id: number; name: string }[]>([])
  const [ageGroups, setAgeGroups] = useState<AgeGroup[]>([])
  const [standings, setStandings] = useState<StandingRow[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string>('')

  // Update URL when filters change
  useEffect(() => {
    const params = new URLSearchParams()
    params.set('program', selectedProgram)
    params.set('season', selectedSeason)
    if (selectedRegion) params.set('region', selectedRegion)
    if (selectedAgeGroup) params.set('ageGroup', selectedAgeGroup)
    history.replaceState(null, '', `?${params.toString()}`)
  }, [selectedProgram, selectedSeason, selectedRegion, selectedAgeGroup])

  // Fetch regions and ageGroups on mount
  useEffect(() => {
    const fetchStaticOptions = async () => {
      const apiBase = import.meta.env.VITE_API_BASE_URL

      try {
        if (!apiBase) {
          setRegions([])
          setAgeGroups([])
          return
        }

        // Fetch regions (filtered by division)
        const divisionName = selectedProgram === 'homegrown' ? 'Homegrown' : 'Academy'
        const regionsRes = await fetch(`${apiBase}/regions?division=${encodeURIComponent(divisionName)}`)
        if (regionsRes.ok) {
          const regionsData = await regionsRes.json()
          const transformedRegions = regionsData.map((r: any) => ({
            id: r.Id || r.id,
            name: r.Name || r.name
          }))
          setRegions(transformedRegions)
        }

        // Fetch age groups
        const ageGroupsRes = await fetch(`${apiBase}/agegroups`)
        if (ageGroupsRes.ok) {
          const ageGroupsData = await ageGroupsRes.json()
          const transformedAgeGroups = ageGroupsData.map((ag: any) => ({
            id: ag.Id || ag.id,
            name: ag.Name || ag.name
          })).sort((a: AgeGroup, b: AgeGroup) => a.name.localeCompare(b.name))
          setAgeGroups(transformedAgeGroups)
        }
      } catch (err) {
        console.error('Error fetching filter options:', err)
      }
    }

    fetchStaticOptions()
  }, [selectedProgram])

  // Fetch standings when all filters are selected
  useEffect(() => {
    if (!selectedRegion || !selectedAgeGroup) {
      setStandings([])
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
          setStandings([])
          setLoading(false)
          return
        }

        const params = new URLSearchParams()
        params.set('program', selectedProgram)
        params.set('season', selectedSeason)
        params.set('region', selectedRegion)
        params.set('ageGroup', selectedAgeGroup)

        const response = await fetch(`${apiBase}/standings?${params.toString()}`)
        if (!response.ok) {
          throw new Error(`HTTP ${response.status}`)
        }

        const data = await response.json()
        // Transform PascalCase to camelCase
        const transformed = data.map((row: any) => ({
          rank: row.Rank ?? row.rank,
          teamId: row.TeamId ?? row.teamId,
          teamName: row.TeamName ?? row.teamName,
          logoUrl: row.LogoUrl ?? row.logoUrl,
          gp: row.GP ?? row.gp ?? 0,
          w: row.W ?? row.w ?? 0,
          d: row.D ?? row.d ?? 0,
          l: row.L ?? row.l ?? 0,
          gf: row.GF ?? row.gf ?? 0,
          ga: row.GA ?? row.ga ?? 0,
          gd: row.GD ?? row.gd ?? 0,
          pts: row.Pts ?? row.pts ?? 0,
          ppm: row.PPM ?? row.ppm ?? 0,
          gfm: row.GFM ?? row.gfm ?? 0,
          gam: row.GAM ?? row.gam ?? 0,
          gdm: row.GDM ?? row.gdm ?? 0
        }))

        setStandings(transformed)
      } catch (err) {
        console.error('Error fetching standings:', err)
        setError(err instanceof Error ? err.message : 'Failed to load standings')
        setStandings([])
      } finally {
        setLoading(false)
      }
    }

    fetchStandings()
  }, [selectedProgram, selectedSeason, selectedRegion, selectedAgeGroup])

  const handleProgramChange = (programs: Program[]) => {
    // For standings, only single program selection
    setSelectedProgram(programs[0] || 'homegrown')
    setSelectedRegion('')
  }

  const handleSeasonChange = (seasons: Season[]) => {
    // For standings, only single season selection
    setSelectedSeason(seasons[0] || 'fall2025')
  }

  const isFiltersComplete = selectedProgram && selectedSeason && selectedRegion && selectedAgeGroup

  return (
    <div className="standings-page">
      <div className="controls-bar">
        <ProgramSelector
          selected={[selectedProgram]}
          onChange={handleProgramChange}
        />
        <div className="controls-divider" />
        <SeasonSelector
          selected={[selectedSeason]}
          onChange={handleSeasonChange}
        />
      </div>

      <div className="standings-filters">
        <div className="standings-filter-group">
          <label htmlFor="region-select">Region</label>
          <select
            id="region-select"
            value={selectedRegion}
            onChange={(e) => setSelectedRegion(e.target.value)}
            className="standings-filter-select"
          >
            <option value="">Select a region</option>
            {regions.map((r) => (
              <option key={r.id} value={r.name}>
                {r.name}
              </option>
            ))}
          </select>
        </div>

        <div className="standings-filter-group">
          <label htmlFor="agegroup-select">Age Group</label>
          <select
            id="agegroup-select"
            value={selectedAgeGroup}
            onChange={(e) => setSelectedAgeGroup(e.target.value)}
            className="standings-filter-select"
          >
            <option value="">Select an age group</option>
            {ageGroups.map((ag) => (
              <option key={ag.id} value={ag.name}>
                {ag.name}
              </option>
            ))}
          </select>
        </div>
      </div>

      {!isFiltersComplete && !loading && (
        <div className="no-results">
          <p>Select all filters to view standings</p>
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

      {isFiltersComplete && !loading && standings.length === 0 && !error && (
        <div className="no-results">
          <p>No standings available yet</p>
        </div>
      )}

      {standings.length > 0 && (
        <div className="standings-table-wrapper">
          <table className="standings-table">
            <thead>
              <tr>
                <th className="col-rank">#</th>
                <th className="col-team">Team</th>
                <th className="col-gp">GP</th>
                <th className="col-record">W-D-L</th>
                <th className="col-goals">GF</th>
                <th className="col-goals">GA</th>
                <th className="col-goals">GD</th>
                <th className="col-gpm">GF/M</th>
                <th className="col-gpm">GA/M</th>
                <th className="col-gpm">GD/M</th>
                <th className="col-pts">Pts</th>
                <th className="col-ppm">PPM</th>
              </tr>
            </thead>
            <tbody>
              {standings.map((row, idx) => (
                <tr key={row.teamId} className={idx % 2 === 0 ? 'even' : 'odd'}>
                  <td className="col-rank">{row.rank}</td>
                  <td className="col-team">
                    {row.logoUrl && (
                      <img src={row.logoUrl} alt={row.teamName} className="standings-team-logo" />
                    )}
                    <span className="standings-team-name">{row.teamName}</span>
                  </td>
                  <td className="col-gp">{row.gp}</td>
                  <td className="col-record">
                    {row.w}-{row.d}-{row.l}
                  </td>
                  <td className="col-goals">{row.gf}</td>
                  <td className="col-goals">{row.ga}</td>
                  <td className="col-goals">{row.gd}</td>
                  <td className="col-gpm">{row.gfm.toFixed(2)}</td>
                  <td className="col-gpm">{row.gam.toFixed(2)}</td>
                  <td className="col-gpm">{row.gdm.toFixed(2)}</td>
                  <td className="col-pts">{row.pts}</td>
                  <td className="col-ppm">{row.ppm.toFixed(3)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

export default StandingsPage
