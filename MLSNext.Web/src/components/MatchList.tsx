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
      <h2 className="match-list-title">
        {matches.length} Match{matches.length !== 1 ? 'es' : ''}
      </h2>
      <div className="match-grid">
        {sortedMatches.map(match => (
          <MatchCard key={match.matchId} match={match} />
        ))}
      </div>
    </div>
  )
}