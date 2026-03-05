import { useLocation } from 'react-router-dom'
import { Menu } from 'lucide-react'
import './Header.css'

interface HeaderProps {
  onMenuClick: () => void
}

function Header({ onMenuClick }: HeaderProps) {
  const location = useLocation()

  const getPageTitle = () => {
    switch (location.pathname) {
      case '/Schedules':
        return 'Schedules'
      case '/Standings':
        return 'Standings'
      default:
        return ''
    }
  }

  const pageTitle = getPageTitle()

  return (
    <header className="app-header">
      <div className="app-header-inner">
        <button className="header-menu-btn" onClick={onMenuClick} aria-label="Open navigation menu">
          <Menu size={24} />
        </button>
        <h1 className="header-title">
          YSI
          {pageTitle && <span className="header-subtitle"> - {pageTitle}</span>}
        </h1>
      </div>
    </header>
  )
}

export default Header
