# Project Todos

## Standings Page
- [ ] Add "Strength of Remaining Schedule" stat to Standings table
  - Calculate average ELO of remaining opponents
  - Display per team in standings view

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

## UI/UX
- [ ] Mobile UI improvements
  - Compact collapse mode for MatchCard on screens < 600px
  - Show: Date | Home | Score | Away
