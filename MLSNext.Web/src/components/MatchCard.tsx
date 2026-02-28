import { MapPin, Clock, Trophy } from 'lucide-react'
import { Match, Program } from '../types'
import './MatchCard.css'

interface MatchCardProps {
  match: Match
  program?: Program
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

const getInitials = (name: string) =>
  name.split(' ').map(w => w[0]).slice(0, 3).join('').toUpperCase()

export default function MatchCard({ match, program }: MatchCardProps) {
  const isScored = match.score && match.score !== 'TBD'
  const isAcademy = program === 'academy'

  return (
    <div className="match-card">
      <div className={`match-card-accent${isAcademy ? ' accent-academy' : ''}`} />

      <div className="match-header">
        <div className="match-badges">
          <span className="badge badge-age">{match.ageGroup.name}</span>
          <span className="badge badge-gender">{match.gender}</span>
        </div>
        {match.region?.name && (
          <span className="badge badge-region">{match.region.name}</span>
        )}
      </div>

      <div className="match-teams">
        <div className="team home-team">
          <div className="team-crest">{getInitials(match.homeTeam.name)}</div>
          <span className="team-name">{match.homeTeam.name}</span>
        </div>

        <div className={`match-score ${isScored ? 'scored' : 'tbd'}`}>
          {isScored ? (
            <>
              <span className="score-value">{match.score}</span>
              <span className="score-label">Final</span>
            </>
          ) : (
            <span className="score-tbd">VS</span>
          )}
        </div>

        <div className="team away-team">
          <div className="team-crest">{getInitials(match.awayTeam.name)}</div>
          <span className="team-name">{match.awayTeam.name}</span>
        </div>
      </div>

      <div className="match-footer">
        <div className="detail">
          <MapPin size={13} />
          <span className="detail-text">{match.venue.name}</span>
        </div>
        <div className="detail">
          <Clock size={13} />
          <span className="detail-text">{formatDate(match.matchDateUtc)}</span>
        </div>
        {match.competition?.name && (
          <div className="detail">
            <Trophy size={13} />
            <span className="detail-text">{match.competition.name}</span>
          </div>
        )}
      </div>
    </div>
  )
}
