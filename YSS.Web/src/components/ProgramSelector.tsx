import { Trophy, Zap } from 'lucide-react'
import { Program } from '../types'
import './ProgramSelector.css'

interface ProgramSelectorProps {
  selected: Program[]
  onChange: (programs: Program[]) => void
  singleSelect?: boolean
}

export default function ProgramSelector({ selected, onChange, singleSelect = false }: ProgramSelectorProps) {
  const toggleProgram = (program: Program) => {
    if (singleSelect) {
      onChange([program])
    } else if (selected.includes(program)) {
      onChange(selected.filter(p => p !== program))
    } else {
      onChange([...selected, program])
    }
  }

  return (
    <div className="program-selector">
      <span className="selector-label">Program</span>
      <div className="program-buttons">
        <button
          className={`program-btn ${selected.includes('homegrown') ? 'active' : ''}`}
          onClick={() => toggleProgram('homegrown')}
        >
          <Trophy size={15} />
          Homegrown
        </button>

        <button
          className={`program-btn ${selected.includes('academy') ? 'active' : ''}`}
          onClick={() => toggleProgram('academy')}
        >
          <Zap size={15} />
          Academy
        </button>
      </div>
    </div>
  )
}
