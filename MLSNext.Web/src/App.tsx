import { useState } from 'react'
import './App.css'
import ProgramSelector from './components/ProgramSelector'
import MatchList from './components/MatchList'
import FilterBar from './components/FilterBar'
import { Match, Program } from './types'
import { mockMatches } from './mockData'

function App() {
  const [selectedProgram, setSelectedProgram] = useState<Program>('homegrown')
  const [selectedRegion, setSelectedRegion] = useState<string>('')
  const [selectedTeam, setSelectedTeam] = useState<string>('')
  const [selectedAgeGroups, setSelectedAgeGroups] = useState<string[]>([])
  const [matches, setMatches] = useState<Match[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string>('')

  const handleProgramChange = (program: Program) => {
    setSelectedProgram(program)
    setSelectedRegion('')
    setSelectedTeam('')
    setMatches([])
  }

  const handleFilterChange = (region: string, team: string, ageGroups: string[]) => {
    setSelectedRegion(region)
    setSelectedTeam(team)
    setSelectedAgeGroups(ageGroups)
    fetchMatches(region, team, ageGroups)
  }

  const fetchMatches = async (region: string, team: string, ageGroups: string[]) => {
    try {
      setLoading(true)
      setError('')
      
      const apiBase = import.meta.env.VITE_API_BASE_URL
      
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
      
      if (team) params.append('team', team)
      if (region) params.append('division', region)
      ageGroups.forEach(ag => params.append('ageGroup', ag))

      try {
        const response = await fetch(`${apiBase}/matches?${params.toString()}`)
        if (!response.ok) throw new Error('Failed to fetch matches')
        
        const data = await response.json()
        setMatches(data || [])
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
        
        <FilterBar 
          program={selectedProgram}
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