import { useState, useEffect } from 'react'
import { Program, Region, AgeGroup, Team } from '../types'
import { mockMatches } from '../mockData'
import resetIcon from '../../images/reset_icon.png'
import './FilterBar.css'

interface FilterBarProps {
  programs: Program[]
  seasons: string[]
  region: string
  selectedAgeGroups: string[]
  initialTeam?: string
  onFiltersChange: (region: string, team: string, ageGroups: string[]) => void
}

export default function FilterBar({ programs, seasons, region, selectedAgeGroups, initialTeam = '', onFiltersChange }: FilterBarProps) {
  const [teamSearch, setTeamSearch] = useState(initialTeam)
  const [showSuggestions, setShowSuggestions] = useState(false)

  // Keep the text input in sync when parent sets the team externally
  // (e.g. clicking a team name on a match card)
  useEffect(() => {
    setTeamSearch(initialTeam)
  }, [initialTeam])
  
  const [teams, setTeams] = useState<Team[]>([])
  const [teamsLoading, setTeamsLoading] = useState(false)
  const [divisions, setDivisions] = useState<any[]>([])
  const [regions, setRegions] = useState<any[]>([])
  const [ageGroups, setAgeGroups] = useState<AgeGroup[]>([])
  const [loading, setLoading] = useState(true)

  // Map programs to division names for filtering
  const getDivisionNamesForPrograms = (progs: Program[]): string[] => {
    return progs.map(p => p === 'homegrown' ? 'Homegrown' : 'Academy')
  }

  // Fetch age groups and divisions once on mount (not program/season dependent)
  useEffect(() => {
    const fetchStaticOptions = async () => {
      const apiBase = import.meta.env.VITE_API_BASE_URL
      
      try {
        if (!apiBase) {
          setAgeGroups(Array.from(new Set(
            mockMatches.map(m => m.ageGroup)
          )).sort((a, b) => a.name.localeCompare(b.name)))
          setDivisions([])
          setLoading(false)
          return
        }

        // Fetch divisions from API
        const divisionsRes = await fetch(`${apiBase}/divisions`)
        if (divisionsRes.ok) {
          const divisionsData = await divisionsRes.json()
          const transformedDivisions = divisionsData.map((d: any) => ({
            id: d.Id || d.id,
            name: d.Name || d.name,
            tournamentId: d.TournamentId || d.tournamentId
          }))
          setDivisions(transformedDivisions)
        }

        // Fetch age groups from API
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
        console.error('Error fetching static filter options:', err)
        setAgeGroups(Array.from(new Set(
          mockMatches.map(m => m.ageGroup)
        )).sort((a, b) => a.name.localeCompare(b.name)))
      } finally {
        setLoading(false)
      }
    }

    fetchStaticOptions()
  }, [])

  // Re-fetch teams whenever program, season, or region changes — list must reflect current context
  useEffect(() => {
    const controller = new AbortController()

    // Clear stale list immediately so old suggestions never flash while the new fetch is in flight
    setTeams([])
    setTeamsLoading(true)

    const fetchTeams = async () => {
      const apiBase = import.meta.env.VITE_API_BASE_URL
      if (!apiBase) {
        setTeams(Array.from(new Set(
          mockMatches.flatMap(m => [m.homeTeam, m.awayTeam])
        )).sort((a, b) => a.name.localeCompare(b.name)))
        setTeamsLoading(false)
        return
      }
      try {
        const params = new URLSearchParams()
        programs.forEach(p => params.append('program', p === 'homegrown' ? 'Homegrown' : 'Academy'))
        seasons.forEach(s => params.append('season', s))
        if (region)  params.set('region', region)
        console.log(`🔍 fetchTeams → programs=${programs.join('+')} seasons=${seasons.join('+')} region=${region || '(all)'}`)
        const teamsRes = await fetch(`${apiBase}/teams?${params.toString()}`, { signal: controller.signal })
        if (teamsRes.ok) {
          const teamsData = await teamsRes.json()
          const mapped = teamsData.map((t: any) => ({
            id: t.Id || t.id,
            name: t.Name || t.name
          })).sort((a: Team, b: Team) => a.name.localeCompare(b.name))
          console.log(`✅ fetchTeams → ${mapped.length} teams loaded`)
          setTeams(mapped)
        }
      } catch (err: any) {
        if (err.name !== 'AbortError') {
          console.error('Error fetching teams:', err)
        }
      } finally {
        if (!controller.signal.aborted) {
          setTeamsLoading(false)
        }
      }
    }

    fetchTeams()
    return () => controller.abort()
  }, [programs, seasons, region])

  // Fetch regions when programs change
  useEffect(() => {
    const fetchRegions = async () => {
      const apiBase = import.meta.env.VITE_API_BASE_URL
      
      if (!apiBase) {
        setRegions([])
        return
      }

      try {
        // If both programs selected, fetch regions for both; otherwise fetch for the selected one(s)
        const divisionNames = getDivisionNamesForPrograms(programs)
        const allRegions = new Set<any>()
        
        for (const divisionName of divisionNames) {
          const regionsRes = await fetch(`${apiBase}/regions?division=${encodeURIComponent(divisionName)}`)
          if (regionsRes.ok) {
            const regionsData = await regionsRes.json()
            regionsData.forEach((r: any) => {
              allRegions.add(JSON.stringify({ id: r.Id || r.id, name: r.Name || r.name }))
            })
          }
        }
        
        const transformedRegions = Array.from(allRegions)
          .map(str => JSON.parse(str as string))
          .sort((a: any, b: any) => a.name.localeCompare(b.name))
        
        console.log(`📍 Regions for ${programs.join('+')}:`, transformedRegions)
        setRegions(transformedRegions)
      } catch (err) {
        console.error('Error fetching regions:', err)
        setRegions([])
      }
    }

    fetchRegions()
  }, [programs])

  // Notify parent when team filter changes; region and ageGroups are controlled by parent
  useEffect(() => {
    onFiltersChange(region, teamSearch, selectedAgeGroups)
  }, [teamSearch])

  const handleAgeGroupToggle = (ageGroup: string) => {
    const next = selectedAgeGroups.includes(ageGroup)
      ? selectedAgeGroups.filter(ag => ag !== ageGroup)
      : [...selectedAgeGroups, ageGroup]
    onFiltersChange(region, teamSearch, next)
  }

  // Filter team suggestions based on current input
  const filteredSuggestions = teamSearch.length > 0
    ? teams.filter(team =>
        team.name.toLowerCase().includes(teamSearch.toLowerCase())
      )
    : teams

  const handleSelectTeam = (teamName: string) => {
    setTeamSearch(teamName)
    setShowSuggestions(false)
  }

  const handleReset = () => {
    setTeamSearch('')
    onFiltersChange('', '', [])
  }

  // Are any filters currently active?
  const hasActiveFilters = region !== '' || teamSearch !== '' || selectedAgeGroups.length > 0

  return (
    <div className="filter-bar">
      <div className="filter-section">
        <label htmlFor="region-select">Region</label>
        <select
          id="region-select"
          value={region}
          onChange={(e) => onFiltersChange(e.target.value, teamSearch, selectedAgeGroups)}
          className="filter-select"
        >
          <option value="">All Regions</option>
          {regions.map(region => (
            <option key={region.id} value={region.name}>
              {region.name}
            </option>
          ))}
        </select>
      </div>

      <div className="filter-section">
        <label htmlFor="team-search">Team</label>
        <div className="team-search-container">
          <input
            id="team-search"
            type="text"
            placeholder={loading || teamsLoading ? "Loading teams..." : "Team name..."}
            value={teamSearch}
            onChange={(e) => {
              setTeamSearch(e.target.value)
              setShowSuggestions(true)
            }}
            onFocus={() => setShowSuggestions(true)}
            onBlur={() => setTimeout(() => setShowSuggestions(false), 150)}
            className="filter-input"
            disabled={loading}
          />
          {teamSearch && (
            <button
              className="team-clear-button"
              onClick={() => setTeamSearch('')}
              title="Clear team filter"
              type="button"
            >
              ×
            </button>
          )}
          {showSuggestions && !teamsLoading && filteredSuggestions.length > 0 && (
            <div className="autocomplete-suggestions">
              {filteredSuggestions.map((team) => (
                <div
                  key={`${team.id}-${team.name}`}
                  className="suggestion-item"
                  onMouseDown={() => handleSelectTeam(team.name)}
                >
                  {team.name}
                </div>
              ))}
            </div>
          )}
        </div>
      </div>

      <div className="filter-section">
        <label>Age</label>
        <div className="age-group-checkboxes">
          {ageGroups.map(ageGroup => (
            <label key={ageGroup.id} className="checkbox-label">
              <input
                type="checkbox"
                checked={selectedAgeGroups.includes(ageGroup.name)}
                onChange={() => handleAgeGroupToggle(ageGroup.name)}
              />
              {ageGroup.name}
            </label>
          ))}
        </div>
      </div>

      <button
        className={`reset-button ${hasActiveFilters ? 'reset-button--active' : ''}`}
        onClick={handleReset}
        title="Reset all filters"
        disabled={!hasActiveFilters}
      >
        <img src={resetIcon} alt="Reset filters" className="reset-icon" />
      </button>
    </div>
  )
}