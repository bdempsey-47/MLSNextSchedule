import { MapPin, Clock, Trophy } from 'lucide-react'
import { Match, Program } from '../types'
import './MatchCard.css'

interface MatchCardProps {
  match: Match
  program?: Program
  onBadgeClick?: (type: 'region' | 'ageGroup' | 'team', value: string) => void
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

// Formats a Date to ICS UTC datetime string: YYYYMMDDTHHMMSSZ
const toIcsDate = (date: Date) =>
  date.toISOString().replace(/[-:]/g, '').replace(/\.\d{3}/, '')

const addToCalendar = (match: Match) => {
  const start = new Date(match.matchDateUtc)
  const end = new Date(start.getTime() + 90 * 60 * 1000) // assume 90-min match
  const title = `${match.homeTeam.name} vs ${match.awayTeam.name}`
  const description = `${match.ageGroup.name} ${match.gender} — ${match.region?.name ?? ''}`
  const location = match.venue.name !== 'TBD' ? match.venue.name : ''

  const ics = [
    'BEGIN:VCALENDAR',
    'VERSION:2.0',
    'PRODID:-//MLS Next Schedule//EN',
    'BEGIN:VEVENT',
    `UID:match-${match.matchId}@mlsnextschedule`,
    `DTSTART:${toIcsDate(start)}`,
    `DTEND:${toIcsDate(end)}`,
    `SUMMARY:${title}`,
    `DESCRIPTION:${description}`,
    `LOCATION:${location}`,
    'END:VEVENT',
    'END:VCALENDAR',
  ].join('\r\n')

  const blob = new Blob([ics], { type: 'text/calendar;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `match-${match.matchId}.ics`
  a.click()
  URL.revokeObjectURL(url)
}

const getInitials = (name: string) =>
  name.split(' ').map(w => w[0]).slice(0, 3).join('').toUpperCase()

export default function MatchCard({ match, program, onBadgeClick }: MatchCardProps) {
  const isScored = match.score && match.score !== 'TBD'
  const isAcademy = program === 'academy'

  return (
    <div className="match-card">
      <div className={`match-card-accent${isAcademy ? ' accent-academy' : ''}`} />

      <div className="match-header">
        <div className="match-badges">
          <span
            className={`badge badge-age${onBadgeClick ? ' badge-clickable' : ''}`}
            onClick={() => onBadgeClick?.('ageGroup', match.ageGroup.name)}
            title={onBadgeClick ? `Filter by ${match.ageGroup.name}` : undefined}
          >
            {match.ageGroup.name}
          </span>
          <span className="badge badge-gender">{match.gender}</span>
        </div>
        {match.region?.name && (
          <span
            className={`badge badge-region${onBadgeClick ? ' badge-clickable' : ''}`}
            onClick={() => onBadgeClick?.('region', match.region.name)}
            title={onBadgeClick ? `Filter by ${match.region.name}` : undefined}
          >
            {match.region.name}
          </span>
        )}
      </div>

      <div className="match-teams">
        <div className="team home-team">
          <div
            className={`team-crest${match.homeTeam.logoUrl ? ' team-crest--logo' : ''}${onBadgeClick ? ' team-crest-clickable' : ''}`}
            onClick={() => onBadgeClick?.('team', match.homeTeam.name)}
            title={onBadgeClick ? `Filter by ${match.homeTeam.name}` : undefined}
          >
            {match.homeTeam.logoUrl
              ? <img src={match.homeTeam.logoUrl} alt={match.homeTeam.name} className="team-logo" />
              : getInitials(match.homeTeam.name)
            }
          </div>
          <span
            className={`team-name${onBadgeClick ? ' team-name-clickable' : ''}`}
            onClick={() => onBadgeClick?.('team', match.homeTeam.name)}
            title={onBadgeClick ? `Filter by ${match.homeTeam.name}` : undefined}
          >
            {match.homeTeam.name}
          </span>
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
          <div
            className={`team-crest${match.awayTeam.logoUrl ? ' team-crest--logo' : ''}${onBadgeClick ? ' team-crest-clickable' : ''}`}
            onClick={() => onBadgeClick?.('team', match.awayTeam.name)}
            title={onBadgeClick ? `Filter by ${match.awayTeam.name}` : undefined}
          >
            {match.awayTeam.logoUrl
              ? <img src={match.awayTeam.logoUrl} alt={match.awayTeam.name} className="team-logo" />
              : getInitials(match.awayTeam.name)
            }
          </div>
          <span
            className={`team-name${onBadgeClick ? ' team-name-clickable' : ''}`}
            onClick={() => onBadgeClick?.('team', match.awayTeam.name)}
            title={onBadgeClick ? `Filter by ${match.awayTeam.name}` : undefined}
          >
            {match.awayTeam.name}
          </span>
        </div>
      </div>

      <div className="match-footer">
        <div className="detail">
          <MapPin size={13} />
          {match.venue.name && match.venue.name !== 'TBD' ? (
            <a
              className="detail-text venue-link"
              href={`https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(match.venue.name)}`}
              target="_blank"
              rel="noopener noreferrer"
              title={`Search "${match.venue.name}" on Google Maps (approximate — may not show exact field location)`}
            >
              {match.venue.name}
            </a>
          ) : (
            <span className="detail-text">{match.venue.name}</span>
          )}
        </div>
        <button
          className="detail detail-calendar"
          onClick={() => addToCalendar(match)}
          title="Add to calendar"
          type="button"
        >
          <Clock size={13} />
          <span className="detail-text">{formatDate(match.matchDateUtc)}</span>
        </button>
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
