import { Match, Program } from '../types'
import MatchCard from './MatchCard'
import './MatchList.css'

interface MatchListProps {
  matches: Match[]
  programs: Program[]
  onBadgeClick?: (type: 'region' | 'ageGroup' | 'team', value: string) => void
}

export default function MatchList({ matches, programs, onBadgeClick }: MatchListProps) {
  const sortedMatches = [...matches].sort((a, b) =>
    new Date(a.matchDateUtc).getTime() - new Date(b.matchDateUtc).getTime()
  )

  return (
    <div className="match-list">
      <div className="match-list-header">
        <span className="match-count">{matches.length}</span>
        <span className="match-count-label">
          Match{matches.length !== 1 ? 'es' : ''}
        </span>
      </div>
      <div className="match-grid">
        {sortedMatches.map(match => {
          // Determine which program this match belongs to (homegrown=12, academy=35)
          const matchProgram = match.division?.tournamentId === 12 ? 'homegrown' : 'academy'
          return (
            <MatchCard key={match.matchId} match={match} program={matchProgram} onBadgeClick={onBadgeClick} />
          )
        })}
      </div>
    </div>
  )
}