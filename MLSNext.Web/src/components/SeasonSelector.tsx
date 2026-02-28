import { Season } from '../types'
import './SeasonSelector.css'

interface SeasonSelectorProps {
  selected: Season
  onChange: (season: Season) => void
}

export default function SeasonSelector({ selected, onChange }: SeasonSelectorProps) {
  const seasons: { value: Season; label: string }[] = [
    { value: 'fall2025', label: 'Fall 2025' },
    { value: 'spring2026', label: 'Spring 2026' }
  ]

  return (
    <div className="season-selector">
      <span className="selector-label">Season</span>
      <div className="season-buttons">
        {seasons.map(season => (
          <button
            key={season.value}
            className={`season-button ${selected === season.value ? 'active' : ''}`}
            onClick={() => onChange(season.value)}
          >
            {season.label}
          </button>
        ))}
      </div>
    </div>
  )
}

