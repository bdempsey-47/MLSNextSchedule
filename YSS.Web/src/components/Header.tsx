import { useLocation, useNavigate } from 'react-router-dom'
import { Menu } from 'lucide-react'
import ysiLogo from '../../images/ysi_logo.png'
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
      case '/Analytics':
        return 'Analytics'
      default:
        return 'Home'
    }
  }

  const pageTitle = getPageTitle()
  const navigate = useNavigate()

  return (
    <header className="app-header">
      <div className="app-header-inner">
        <button className="header-menu-btn" onClick={onMenuClick} aria-label="Open navigation menu">
          <Menu size={24} />
        </button>
        <h1 className="header-title">
          <img src={ysiLogo} alt="YSI" title="Youth Soccer Intelligence" className="header-logo" onClick={() => navigate('/')} />
          {pageTitle && <span className="header-subtitle"> - {pageTitle}</span>}
        </h1>
      </div>
    </header>
  )
}

export default Header
