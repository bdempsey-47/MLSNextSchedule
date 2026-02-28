import { Trophy, Zap } from 'lucide-react'
import { Program } from '../types'
import './ProgramSelector.css'

interface ProgramSelectorProps {
  selected: Program
  onChange: (program: Program) => void
}

export default function ProgramSelector({ selected, onChange }: ProgramSelectorProps) {
  return (
    <div className="program-selector">
      <span className="selector-label">Program</span>
      <div className="program-buttons">
        <button
          className={`program-btn ${selected === 'homegrown' ? 'active' : ''}`}
          onClick={() => onChange('homegrown')}
        >
          <Trophy size={15} />
          Homegrown
        </button>

        <button
          className={`program-btn ${selected === 'academy' ? 'active' : ''}`}
          onClick={() => onChange('academy')}
        >
          <Zap size={15} />
          Academy
        </button>
      </div>
    </div>
  )
}
