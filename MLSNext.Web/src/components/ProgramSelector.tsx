import { Program } from '../types'
import './ProgramSelector.css'

interface ProgramSelectorProps {
  selected: Program
  onChange: (program: Program) => void
}

export default function ProgramSelector({ selected, onChange }: ProgramSelectorProps) {
  return (
    <div className="program-selector">
      <h2>Select Program</h2>
      <div className="program-buttons">
        <button
          className={`program-btn ${selected === 'homegrown' ? 'active' : ''}`}
          onClick={() => onChange('homegrown')}
        >
          <span className="program-icon">🏆</span>
          <span className="program-name">Homegrown</span>
          <span className="program-desc">Tournament 12</span>
        </button>
        
        <button
          className={`program-btn ${selected === 'academy' ? 'active' : ''}`}
          onClick={() => onChange('academy')}
        >
          <span className="program-icon">⚽</span>
          <span className="program-name">Academy</span>
          <span className="program-desc">Tournament 35</span>
        </button>
      </div>
    </div>
  )
}