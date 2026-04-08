import { useState, useEffect, useRef} from 'react'
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
    if (initialTeam !== teamSearch) {
      parentSyncRef.current = true
      setTeamSearch(initialTeam)
    }
  }, [initialTeam])
  
  const [searchSuggestions, setSearchSuggestions] = useState<Team[]>([])
  const [searchLoading, setSearchLoading] = useState(false)
  const debounceTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  const parentSyncRef = useRef(false)
  const abortControllerRef = useRef<AbortController | null>(null)
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

  // When user types in team search, debounce and call the API
useEffect(() => {
  console.log('[SearchEffect] fired', { teamSearch, parentSync: parentSyncRef.current, showSuggestions })

  // Clear the previous debounce timer
  if (debounceTimerRef.current) {
    clearTimeout(debounceTimerRef.current)
  }

  // Skip search when change came from parent sync (not user typing)
  if (parentSyncRef.current) {
    console.log('[SearchEffect] skipping — parent sync')
    parentSyncRef.current = false
    return
  }

  // If input is too short, clear suggestions
  if (teamSearch.length < 2) {
    console.log('[SearchEffect] skipping — too short')
    setSearchSuggestions([])
    setSearchLoading(false)
    return
  }

  // Abort any in-flight request
  if (abortControllerRef.current) {
    abortControllerRef.current.abort()
  }

  // Set up new debounce (300ms)
  const timer = setTimeout(async () => {
    const apiBase = import.meta.env.VITE_API_BASE_URL
    if (!apiBase) {
      // Fallback to mock data if no API
      setSearchSuggestions(Array.from(new Set(
        mockMatches.flatMap(m => [m.homeTeam, m.awayTeam])
      )).filter(t => t.name.toLowerCase().includes(teamSearch.toLowerCase())).sort((a, b) => a.name.localeCompare(b.name)))
      return
    }

    setSearchLoading(true)
    const newController = new AbortController()
    abortControllerRef.current = newController

    const params = new URLSearchParams({ q: teamSearch })
    // Map frontend program names to search index codes
    if (programs.length === 1) {
      params.set('program', programs[0] === 'homegrown' ? 'HG' : 'AG')
    }

    console.log('[SearchEffect] fetching', `${apiBase}/search-teams?${params}`)
    try {
      const response = await fetch(
        `${apiBase}/search-teams?${params}`,
        { signal: newController.signal }
      )

      console.log('[SearchEffect] response status', response.status)
      if (response.ok) {
        const results = await response.json()
        console.log('[SearchEffect] results', results)
        // Transform the response to match Team type
        const teams = results.map((t: any) => ({
          id: t.Id || t.id,
          name: t.Name || t.name
        }))
        console.log('[SearchEffect] setting suggestions', teams)
        setSearchSuggestions(teams)
      }
    } catch (err: any) {
      if (err.name !== 'AbortError') {
        console.error('Error searching teams:', err)
      }
    } finally {
      setSearchLoading(false)
    }
  }, 300) // 300ms debounce

  debounceTimerRef.current = timer

  return () => {
    clearTimeout(timer)
  }
}, [teamSearch])

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
            placeholder={loading ? "Loading filters..." : "Team name..."}
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
          {showSuggestions && !searchLoading && searchSuggestions.length > 0 && (
            <div className="autocomplete-suggestions">
              {searchSuggestions.map((team) => (
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
          {ageGroups.filter(ag => ag.name !== 'U18/19').map(ageGroup => (
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