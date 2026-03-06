import { useNavigate } from 'react-router-dom'
import { X } from 'lucide-react'
import './NavMenu.css'

interface NavMenuProps {
  isOpen: boolean
  onClose: () => void
}

function NavMenu({ isOpen, onClose }: NavMenuProps) {
  const navigate = useNavigate()

  const handleNavClick = (path: string) => {
    navigate(path)
    onClose()
  }

  return (
    <>
      {/* Backdrop */}
      {isOpen && (
        <div className="nav-backdrop" onClick={onClose} aria-hidden="true" />
      )}

      {/* Menu */}
      <nav className={`nav-menu ${isOpen ? 'open' : ''}`}>
        <div className="nav-header">
          <h2>Navigation</h2>
          <button className="nav-close-btn" onClick={onClose} aria-label="Close menu">
            <X size={24} />
          </button>
        </div>

        <ul className="nav-links">
          <li>
            <button className="nav-link" onClick={() => handleNavClick('/')}>
              Home
            </button>
          </li>
          <li>
            <button className="nav-link" onClick={() => handleNavClick('/Schedules')}>
              Schedules
            </button>
          </li>
          <li>
            <button className="nav-link" onClick={() => handleNavClick('/Standings')}>
              Standings
            </button>
          </li>
          <li>
            <button className="nav-link" onClick={() => handleNavClick('/Analytics')}>
              Analytics
            </button>
          </li>
        </ul>
      </nav>
    </>
  )
}

export default NavMenu
