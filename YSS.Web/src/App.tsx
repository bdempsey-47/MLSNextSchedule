import { useState } from 'react'
import { BrowserRouter as Router, Routes, Route } from 'react-router-dom'
import './App.css'
import Header from './components/Header'
import NavMenu from './components/NavMenu'
import HomePage from './pages/HomePage'
import SchedulesPage from './pages/SchedulesPage'
import StandingsPage from './pages/StandingsPage'
import AnalyticsPage from './pages/AnalyticsPage'

function App() {
  const [menuOpen, setMenuOpen] = useState(false)

  return (
    <Router>
      <div className="app">
        <Header onMenuClick={() => setMenuOpen(true)} />
        <NavMenu isOpen={menuOpen} onClose={() => setMenuOpen(false)} />

        <main className="app-main">
          <Routes>
            <Route path="/" element={<HomePage />} />
            <Route path="/Schedules" element={<SchedulesPage />} />
            <Route path="/Standings" element={<StandingsPage />} />
            <Route path="/Analytics" element={<AnalyticsPage />} />
          </Routes>
        </main>
      </div>
    </Router>
  )
}

export default App
