import { useState, useEffect } from 'react'
import './App.css'
import ProgramSelector from './components/ProgramSelector'
import SeasonSelector from './components/SeasonSelector'
import MatchList from './components/MatchList'
import FilterBar from './components/FilterBar'
import { Match, Program, Season } from './types'
import { mockMatches } from './mockData'

function App() {
  // Parse URL query params so bookmarked/shared links restore filter state
  const urlParams = new URLSearchParams(window.location.search)

  const [selectedProgram, setSelectedProgram] = useState<Program>(
    (urlParams.get('program') as Program) || 'homegrown'
  )
  const [selectedSeason, setSelectedSeason] = useState<Season>(
    (urlParams.get('season') as Season) || 'fall2025'
  )
  const [selectedRegion, setSelectedRegion] = useState<string>(urlParams.get('region') || '')
  const [selectedTeam, setSelectedTeam] = useState<string>(urlParams.get('team') || '')
  const [selectedAgeGroups, setSelectedAgeGroups] = useState<string[]>(urlParams.getAll('ageGroup'))
  const [matches, setMatches] = useState<Match[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string>('')

  // Load matches whenever program or season changes, preserving current filter state
  useEffect(() => {
    fetchMatches(selectedRegion, selectedTeam, selectedAgeGroups, selectedSeason)
  }, [selectedSeason, selectedProgram])

  // Keep URL in sync with filter state so the page can be bookmarked or shared
  useEffect(() => {
    const params = new URLSearchParams()
    params.set('program', selectedProgram)
    params.set('season', selectedSeason)
    if (selectedRegion) params.set('region', selectedRegion)
    if (selectedTeam) params.set('team', selectedTeam)
    selectedAgeGroups.forEach(ag => params.append('ageGroup', ag))
    history.replaceState(null, '', `?${params.toString()}`)
  }, [selectedProgram, selectedSeason, selectedRegion, selectedTeam, selectedAgeGroups])

  const handleProgramChange = (program: Program) => {
    if (program === selectedProgram) return
    setSelectedProgram(program)
    setSelectedRegion('')
    setSelectedTeam('')
    setMatches([])
  }

  const handleSeasonChange = (season: Season) => {
    if (season === selectedSeason) return
    setSelectedSeason(season)
    setMatches([])
  }

  const handleFilterChange = (region: string, team: string, ageGroups: string[]) => {
    setSelectedRegion(region)
    setSelectedTeam(team)
    setSelectedAgeGroups(ageGroups)
    fetchMatches(region, team, ageGroups, selectedSeason)
  }

  // Transform API response from PascalCase to camelCase
  const transformApiMatch = (apiData: any): Match => ({
    matchId: apiData.MatchId || apiData.matchId,
    homeTeam: {
      id: apiData.HomeTeam?.Id || apiData.homeTeam?.id,
      name: apiData.HomeTeam?.Name || apiData.homeTeam?.name
    },
    awayTeam: {
      id: apiData.AwayTeam?.Id || apiData.awayTeam?.id,
      name: apiData.AwayTeam?.Name || apiData.awayTeam?.name
    },
    matchDateUtc: apiData.MatchDateUtc || apiData.matchDateUtc,
    venue: {
      id: apiData.Venue?.Id || apiData.venue?.id,
      name: apiData.Venue?.Name || apiData.venue?.name
    },
    ageGroup: {
      id: apiData.AgeGroup?.Id || apiData.ageGroup?.id,
      name: apiData.AgeGroup?.Name || apiData.ageGroup?.name
    },
    region: {
      id: apiData.Region?.Id || apiData.region?.id,
      name: apiData.Region?.Name || apiData.region?.name
    },
    competition: {
      id: apiData.Competition?.Id || apiData.competition?.id,
      name: apiData.Competition?.Name || apiData.competition?.name
    },
    // Division comes through Region.Division relationship
    division: apiData.Region?.Division || apiData.region?.division ? {
      id: apiData.Region?.Division?.Id || apiData.region?.division?.id,
      name: apiData.Region?.Division?.Name || apiData.region?.division?.name,
      leagueId: apiData.Region?.Division?.LeagueId || apiData.region?.division?.leagueId,
      tournamentId: apiData.Region?.Division?.TournamentId || apiData.region?.division?.tournamentId
    } : undefined,
    score: apiData.Score || apiData.score || 'TBD',
    gender: apiData.Gender || apiData.gender
  })

  // Helper to determine program from division tournament ID
  const getProgramFromMatch = (match: Match): Program => {
    if (!match.division?.tournamentId) return 'academy' // default to academy
    return match.division.tournamentId === 12 ? 'homegrown' : 'academy'
  }

  const fetchMatches = async (region: string, team: string, ageGroups: string[], season?: Season) => {
    const activeSeason = season || selectedSeason
    try {
      setLoading(true)
      setError('')
      
      const apiBase = import.meta.env.VITE_API_BASE_URL
      console.log('🔍 API Base from env:', apiBase)
      console.log('🎯 Program filter:', selectedProgram)
      console.log('📅 Season filter:', activeSeason)
      
      if (!apiBase) {
        console.warn('API URL not configured, using mock data')
        let filteredMock = [...mockMatches]
        
        if (team) {
          filteredMock = filteredMock.filter(m => 
            m.homeTeam.name.toLowerCase().includes(team.toLowerCase()) ||
            m.awayTeam.name.toLowerCase().includes(team.toLowerCase())
          )
        }
        
        if (region) {
          filteredMock = filteredMock.filter(m => m.region.name.toLowerCase() === region.toLowerCase())
        }
        
        if (ageGroups.length > 0) {
          filteredMock = filteredMock.filter(m => ageGroups.includes(m.ageGroup.name))
        }
        
        setMatches(filteredMock)
        setError('(Using mock data - API not configured)')
        setLoading(false)
        return
      }
      
      const params = new URLSearchParams()
      
      if (activeSeason) params.append('season', activeSeason)
      if (selectedProgram) params.append('program', selectedProgram)
      if (team) params.append('team', team)
      if (region) params.append('division', region)
      ageGroups.forEach(ag => params.append('ageGroup', ag))

      console.log('📡 Fetching from:', `${apiBase}/matches?${params.toString()}`)
      try {
        const response = await fetch(`${apiBase}/matches?${params.toString()}`)
        console.log('✅ API Response status:', response.status)
        
        if (!response.ok) throw new Error('Failed to fetch matches')
        
        const data = await response.json()
        console.log('🎯 Data received:', data.length, 'matches')
        
        // Transform API response to match expected format
        let transformedMatches = data.map((match: any) => transformApiMatch(match))
        
        console.log('📊 After transform:', transformedMatches.length, 'matches')
        setMatches(transformedMatches || [])
      } catch (fetchErr) {
        console.warn('API unavailable, using mock data:', fetchErr)
        let filteredMock = [...mockMatches]
        
        if (team) {
          filteredMock = filteredMock.filter(m => 
            m.homeTeam.name.toLowerCase().includes(team.toLowerCase()) ||
            m.awayTeam.name.toLowerCase().includes(team.toLowerCase())
          )
        }
        
        if (region) {
          filteredMock = filteredMock.filter(m => m.region.name.toLowerCase() === region.toLowerCase())
        }
        
        if (ageGroups.length > 0) {
          filteredMock = filteredMock.filter(m => ageGroups.includes(m.ageGroup.name))
        }
        
        setMatches(filteredMock)
        setError('(Using mock data - API unavailable)')
      }
    } catch (err) {
      console.error('Error in fetchMatches:', err)
      setError(err instanceof Error ? err.message : 'Unknown error')
      setMatches([])
    } finally {
      setLoading(false)
    }
  }

  const handleLoadDemoData = () => {
    setMatches(mockMatches)
    setError('')
    setLoading(false)
  }

  return (
    <div className="app">
      <header className="app-header">
        <h1>Soccer Schedules</h1>
        <p className="subtitle">MLS Next</p>
        <button className="demo-button" onClick={handleLoadDemoData}>
          📊 Load Demo Data
        </button>
      </header>
      
      <main className="app-main">
        <ProgramSelector 
          selected={selectedProgram} 
          onChange={handleProgramChange}
        />
        
        <SeasonSelector 
          selected={selectedSeason}
          onChange={handleSeasonChange}
        />
        
        <FilterBar 
          program={selectedProgram}
          season={selectedSeason}
          region={selectedRegion}
          initialTeam={selectedTeam}
          initialAgeGroups={selectedAgeGroups}
          onFiltersChange={handleFilterChange}
        />
        
        {error && <div className={`error-message ${error.includes('mock') ? 'info' : 'error'}`}>{error}</div>}
        {loading && <div className="loading">Loading matches...</div>}
        {!loading && matches.length === 0 && !error && (
          <div className="no-matches">Select filters to view matches</div>
        )}
        {!loading && matches.length === 0 && error && (
          <div className="no-matches">No matches found. Try adjusting your filters.</div>
        )}
        
        {matches.length > 0 && (
          <MatchList 
            matches={matches}
            program={selectedProgram}
          />
        )}
      </main>
    </div>
  )
}

export default App