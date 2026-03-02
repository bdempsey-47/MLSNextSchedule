import { Season } from '../types'
import './SeasonSelector.css'

interface SeasonSelectorProps {
  selected: Season[]
  onChange: (seasons: Season[]) => void
}

export default function SeasonSelector({ selected, onChange }: SeasonSelectorProps) {
  const seasons: { value: Season; label: string }[] = [
    { value: 'fall2025', label: 'Fall 2025' },
    { value: 'spring2026', label: 'Spring 2026' }
  ]

  const toggleSeason = (season: Season) => {
    if (selected.includes(season)) {
      // Deselect: remove from array
      onChange(selected.filter(s => s !== season))
    } else {
      // Select: add to array
      onChange([...selected, season])
    }
  }

  return (
    <div className="season-selector">
      <span className="selector-label">Season</span>
      <div className="season-buttons">
        {seasons.map(season => (
          <button
            key={season.value}
            className={`season-button ${selected.includes(season.value) ? 'active' : ''}`}
            onClick={() => toggleSeason(season.value)}
          >
            {season.label}
          </button>
        ))}
      </div>
    </div>
  )
}

