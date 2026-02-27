import { useState, useEffect } from 'react'
import { Program, Region, AgeGroup, Team } from '../types'
import { mockMatches } from '../mockData'
import './FilterBar.css'

interface FilterBarProps {
  program: Program
  onFiltersChange: (region: string, team: string, ageGroups: string[]) => void
}

const REGIONS = [
  'NorthEast', 'Southeast', 'Mountain', 'Frontier', 'South', 'Midwest', 'West'
]

const AGE_GROUPS = ['U13', 'U14', 'U15', 'U16', 'U17', 'U18']

// Extract unique team names from mock data
const TEAM_SUGGESTIONS = Array.from(
  new Set(
    mockMatches.flatMap(match => [
      match.homeTeam.name,
      match.awayTeam.name
    ])
  )
).sort()

export default function FilterBar({ program, onFiltersChange }: FilterBarProps) {
  const [selectedRegion, setSelectedRegion] = useState('')
  const [teamSearch, setTeamSearch] = useState('')
  const [selectedAgeGroups, setSelectedAgeGroups] = useState<string[]>([])
  const [showSuggestions, setShowSuggestions] = useState(false)

  useEffect(() => {
    onFiltersChange(selectedRegion, teamSearch, selectedAgeGroups)
  }, [selectedRegion, teamSearch, selectedAgeGroups])

  const handleAgeGroupToggle = (ageGroup: string) => {
    setSelectedAgeGroups(prev =>
      prev.includes(ageGroup)
        ? prev.filter(ag => ag !== ageGroup)
        : [...prev, ageGroup]
    )
  }

  // Filter suggestions based on current input
  const filteredSuggestions = teamSearch.length > 0
    ? TEAM_SUGGESTIONS.filter(team =>
        team.toLowerCase().includes(teamSearch.toLowerCase())
      )
    : TEAM_SUGGESTIONS

  const handleSelectTeam = (teamName: string) => {
    setTeamSearch(teamName)
    setShowSuggestions(false)
  }

  return (
    <div className="filter-bar">
      <div className="filter-section">
        <label htmlFor="region-select">Region</label>
        <select
          id="region-select"
          value={selectedRegion}
          onChange={(e) => setSelectedRegion(e.target.value)}
          className="filter-select"
        >
          <option value="">All Regions</option>
          {REGIONS.map(region => (
            <option key={region} value={region}>
              {region}
            </option>
          ))}
        </select>
      </div>

      <div className="filter-section">
        <label htmlFor="team-search">Search by Team</label>
        <div className="team-search-container">
          <input
            id="team-search"
            type="text"
            placeholder="Team name..."
            value={teamSearch}
            onChange={(e) => {
              setTeamSearch(e.target.value)
              setShowSuggestions(true)
            }}
            onFocus={() => setShowSuggestions(true)}
            onBlur={() => setTimeout(() => setShowSuggestions(false), 150)}
            className="filter-input"
          />
          {showSuggestions && filteredSuggestions.length > 0 && (
            <div className="autocomplete-suggestions">
              {filteredSuggestions.map((team, index) => (
                <div
                  key={`${team}-${index}`}
                  className="suggestion-item"
                  onMouseDown={() => handleSelectTeam(team)}
                >
                  {team}
                </div>
              ))}
            </div>
          )}
        </div>
      </div>

      <div className="filter-section">
        <label>Age Groups</label>
        <div className="age-group-checkboxes">
          {AGE_GROUPS.map(ageGroup => (
            <label key={ageGroup} className="checkbox-label">
              <input
                type="checkbox"
                checked={selectedAgeGroups.includes(ageGroup)}
                onChange={() => handleAgeGroupToggle(ageGroup)}
              />
              {ageGroup}
            </label>
          ))}
        </div>
      </div>
    </div>
  )
}