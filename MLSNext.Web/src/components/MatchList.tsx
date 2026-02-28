import { Match, Program } from '../types'
import MatchCard from './MatchCard'
import './MatchList.css'

interface MatchListProps {
  matches: Match[]
  program: Program
}

export default function MatchList({ matches, program }: MatchListProps) {
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
        {sortedMatches.map(match => (
          <MatchCard key={match.matchId} match={match} program={program} />
        ))}
      </div>
    </div>
  )
}