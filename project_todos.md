# Project Todos

## Standings Page
- [x] Add "Strength of Remaining Schedule" (SORS) column — done April 22, 2026

## Analytics & Rankings
- [ ] ELO Power Rankings (cross-region leaderboard)
  - Top 10 U17 teams in the US aggregated across all regions
  - See ELO_Specs.txt for implementation details (K=30, home +100, margin multipliers)
  
## Data & Ingestion
- [ ] NJ Cup Qualifier integration (tournament 84, group phase discovery)

## Infrastructure
- [ ] Copy team logos to Azure Blob Storage
  - Remove dependency on Modular11 CDN
  - Update Team.LogoUrl to point to blob URLs

## Homepage Stats
- [ ] Road Warriors leaderboard
  - Teams that win away more than at home
  - Metric: away GD minus home GD (per match to normalize game counts)
  - Filter: only teams with meaningful away sample (e.g. >= 3 away games played)
  - Backend: compute from match data in GetHomepageStats or dedicated section
  - Display: ranked list on homepage, similar to ELO leaderboard cards

## UI/UX
- [ ] Mobile UI improvements
  - Compact collapse mode for MatchCard on screens < 600px
  - Show: Date | Home | Score | Away
