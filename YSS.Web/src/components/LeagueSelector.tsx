import './LeagueSelector.css'

export default function LeagueSelector() {
  return (
    <div className="league-selector">
      <span className="selector-label">League</span>
      <div className="league-buttons">
        <button className="league-btn active" disabled title="League selection coming soon">
          MLS Next
        </button>
      </div>
    </div>
  )
}
