import { Match } from '../types'
import './MatchCard.css'

interface MatchCardProps {
  match: Match
}

const formatDate = (dateString: string) => {
  const date = new Date(dateString)
  return date.toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

export default function MatchCard({ match }: MatchCardProps) {
  const isScored = match.score && match.score !== 'TBD'

  return (
    <div className="match-card">
      <div className="match-meta">
        <span className="age-group">{match.ageGroup.name}</span>
        <span className="gender">{match.gender}</span>
      </div>

      <div className="match-teams">
        <div className="team home-team">
          <span className="team-name">{match.homeTeam.name}</span>
        </div>
        
        <div className="match-score">
          {isScored ? (
            <span className="score-value">{match.score}</span>
          ) : (
            <span className="score-tbd">TBD</span>
          )}
        </div>
        
        <div className="team away-team">
          <span className="team-name">{match.awayTeam.name}</span>
        </div>
      </div>

      <div className="match-details">
        <div className="detail">
          <span className="detail-icon">📍</span>
          <span className="detail-text">{match.venue.name}</span>
        </div>
        <div className="detail">
          <span className="detail-icon">🕐</span>
          <span className="detail-text">{formatDate(match.matchDateUtc)}</span>
        </div>
        <div className="detail">
          <span className="detail-icon">🏅</span>
          <span className="detail-text">{match.competition.name}</span>
        </div>
      </div>
    </div>
  )
}