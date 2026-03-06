import { useState, useEffect } from 'react'
import { AlertCircle, Loader2, SearchX } from 'lucide-react'
import LeagueSelector from '../components/LeagueSelector'
import ProgramSelector from '../components/ProgramSelector'
import SeasonSelector from '../components/SeasonSelector'
import MatchList from '../components/MatchList'
import FilterBar from '../components/FilterBar'
import { Match, Program, Season } from '../types'
import { mockMatches } from '../mockData'

function SchedulesPage() {
  // Parse URL query params so bookmarked/shared links restore filter state
  const urlParams = new URLSearchParams(window.location.search)

  const [selectedPrograms, setSelectedPrograms] = useState<Program[]>(() => {
    const programs = urlParams.getAll('program') as Program[]
    return programs.length > 0 ? programs : ['homegrown']
  })
  const [selectedSeasons, setSelectedSeasons] = useState<Season[]>(() => {
    const seasons = urlParams.getAll('season') as Season[]
    return seasons.length > 0 ? seasons : ['fall2025']
  })
  const [selectedRegion, setSelectedRegion] = useState<string>(urlParams.get('region') || '')
  const [selectedTeam, setSelectedTeam] = useState<string>(urlParams.get('team') || '')
  const [selectedAgeGroups, setSelectedAgeGroups] = useState<string[]>(urlParams.getAll('ageGroup'))
  const [matches, setMatches] = useState<Match[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string>('')

  // Load matches whenever program or season changes, preserving current filter state
  useEffect(() => {
    fetchMatches(selectedRegion, selectedTeam, selectedAgeGroups)
  }, [selectedSeasons, selectedPrograms])

  // Keep URL in sync with filter state so the page can be bookmarked or shared
  useEffect(() => {
    const params = new URLSearchParams()
    selectedPrograms.forEach(p => params.append('program', p))
    selectedSeasons.forEach(s => params.append('season', s))
    if (selectedRegion) params.set('region', selectedRegion)
    if (selectedTeam) params.set('team', selectedTeam)
    selectedAgeGroups.forEach(ag => params.append('ageGroup', ag))
    history.replaceState(null, '', `?${params.toString()}`)
  }, [selectedPrograms, selectedSeasons, selectedRegion, selectedTeam, selectedAgeGroups])

  const handleProgramChange = (programs: Program[]) => {
    setSelectedPrograms(programs)
    setSelectedRegion('')
    setMatches([])
  }

  const handleSeasonChange = (seasons: Season[]) => {
    setSelectedSeasons(seasons)
    setMatches([])
  }

  const handleFilterChange = (region: string, team: string, ageGroups: string[]) => {
    setSelectedRegion(region)
    setSelectedTeam(team)
    setSelectedAgeGroups(ageGroups)
    fetchMatches(region, team, ageGroups)
  }

  const handleBadgeClick = (type: 'region' | 'ageGroup' | 'team', value: string) => {
    if (type === 'region') {
      handleFilterChange(value, selectedTeam, selectedAgeGroups)
    } else if (type === 'ageGroup') {
      handleFilterChange(selectedRegion, selectedTeam, [value])
    } else if (type === 'team') {
      handleFilterChange(selectedRegion, value, selectedAgeGroups)
    }
  }

  // Transform API response from PascalCase to camelCase
  const transformApiMatch = (apiData: any): Match => ({
    matchId: apiData.MatchId || apiData.matchId,
    homeTeam: {
      id: apiData.HomeTeam?.Id || apiData.homeTeam?.id,
      name: apiData.HomeTeam?.Name || apiData.homeTeam?.name,
      logoUrl: apiData.HomeTeam?.LogoUrl || apiData.homeTeam?.logoUrl || undefined
    },
    awayTeam: {
      id: apiData.AwayTeam?.Id || apiData.awayTeam?.id,
      name: apiData.AwayTeam?.Name || apiData.awayTeam?.name,
      logoUrl: apiData.AwayTeam?.LogoUrl || apiData.awayTeam?.logoUrl || undefined
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

  const fetchMatches = async (region: string, team: string, ageGroups: string[]) => {
    // If no programs or seasons selected, show no matches
    if (selectedPrograms.length === 0 || selectedSeasons.length === 0) {
      setMatches([])
      setLoading(false)
      setError('')
      return
    }

    try {
      setLoading(true)
      setError('')

      const apiBase = import.meta.env.VITE_API_BASE_URL
      console.log('🔍 API Base from env:', apiBase)
      console.log('🎯 Program filters:', selectedPrograms)
      console.log('📅 Season filters:', selectedSeasons)

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

      selectedSeasons.forEach(s => params.append('season', s))
      selectedPrograms.forEach(p => params.append('program', p))
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

  return (
    <div className="schedules-page">
      <div className="controls-bar">
        <LeagueSelector />
        <div className="controls-divider" />
        <ProgramSelector
          selected={selectedPrograms}
          onChange={handleProgramChange}
        />
        <div className="controls-divider" />
        <SeasonSelector
          selected={selectedSeasons}
          onChange={handleSeasonChange}
        />
      </div>

      <FilterBar
        programs={selectedPrograms}
        seasons={selectedSeasons}
        region={selectedRegion}
        selectedAgeGroups={selectedAgeGroups}
        initialTeam={selectedTeam}
        onFiltersChange={handleFilterChange}
      />

      {error && (
        <div className={`error-message ${error.includes('mock') ? 'info' : 'error'}`}>
          <AlertCircle size={16} />
          {error}
        </div>
      )}

      {loading && (
        <div className="loading-state">
          <div className="loading-spinner" />
          <p>Loading matches…</p>
        </div>
      )}

      {!loading && matches.length === 0 && !error && (
        <div className="no-matches">
          <SearchX size={48} />
          <p>Select filters to view matches</p>
          <span className="no-matches-hint">Choose a program and season above to get started</span>
        </div>
      )}

      {!loading && matches.length === 0 && error && (
        <div className="no-matches">
          <SearchX size={48} />
          <p>No matches found</p>
          <span className="no-matches-hint">Try adjusting your filters</span>
        </div>
      )}

      {matches.length > 0 && (
        <MatchList
          matches={matches}
          programs={selectedPrograms}
          onBadgeClick={handleBadgeClick}
        />
      )}
    </div>
  )
}

export default SchedulesPage
