import { Link } from 'react-router-dom'
import './HomePage.css'

function HomePage() {
  return (
    <div className="home-page">
      <div className="home-container">
        <div className="home-content">
          <h2 className="home-title">Welcome to YSI</h2>
          <p className="home-subtitle">Youth Soccer Intelligence</p>

          <div className="home-cards">
            <Link to="/Schedules" className="home-card schedules-card">
              <div className="card-icon">📅</div>
              <h3>Schedules</h3>
              <p>Browse match schedules and results</p>
            </Link>

            <Link to="/Standings" className="home-card standings-card">
              <div className="card-icon">🏆</div>
              <h3>Standings</h3>
              <p>View league standings and rankings</p>
            </Link>
          </div>
        </div>
      </div>
    </div>
  )
}

export default HomePage
