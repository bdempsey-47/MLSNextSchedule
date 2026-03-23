export type Program = 'homegrown' | 'academy'
export type Season = 'fall2025' | 'spring2026' | ''

export interface Match {
  matchId: string
  homeTeam: Team
  awayTeam: Team
  matchDateUtc: string
  venue: Venue
  ageGroup: AgeGroup
  region: Region
  competition: Competition
  division?: Division
  score?: string
  gender: string
}

export interface Team {
  id: number
  name: string
  program?: string  // "AG" | "HG"
  logoUrl?: string
  eloRating?: number
}

export interface Venue {
  id: number
  name: string
  latitude?: number
  longitude?: number
}

export interface AgeGroup {
  id: number
  name: string
}

export interface Region {
  id: number
  name: string
}

export interface Competition {
  id: number
  name: string
}

export interface Division {
  id: number
  leagueId?: number
  name: string
  tournamentId?: number
}

export interface FilterOptions {
  regions: Region[]
  ageGroups: AgeGroup[]
  teams: Team[]
}

export interface StandingRow {
  rank: number
  teamName: string
  logoUrl?: string
  gp: number
  w: number
  d: number
  l: number
  gf: number
  ga: number
  gd: number
  pts: number
  ppm: number
  wpm: number
  gdpm: number
  gpm: number
}

export interface StandingsGroup {
  regionName: string
  standings: StandingRow[]
}

export interface PowerRanking {
  rank: number
  teamName: string
  logoUrl?: string
  regionName: string
  regionNames: string[]
  eloRating: number
  eloDelta: number
  gp: number
}

export interface TeamAnalytics {
  teamName: string
  logoUrl?: string
  regionName: string
  regionNames: string[]
  momentumScore: number
  momentumLabel: string
  last8: string[]
  gp: number
  sos: number
}

// Homepage Stats types
export interface HomepageStats {
  academyTopElo: Record<string, MiniRanking[]>
  homegrownTopElo: Record<string, MiniRanking[]>
  festHomegrownRegions: RegionDominance[]
  festAcademyRegions: RegionDominance[]
  biggestUpsets: Record<string, UpsetInfo>
  matchesOfTheWeek: Record<string, MatchOfWeek>
  quickStats: QuickStats
}

export interface MiniRanking {
  rank: number
  teamName: string
  logoUrl?: string
  regionName: string
  eloRating: number
  eloDelta: number
}

export interface RegionDominance {
  rank: number
  regionName: string
  wins: number
  losses: number
  goalsFor: number
  goalsAgainst: number
  goalDifference: number
}

export interface UpsetInfo {
  winnerName: string
  winnerLogoUrl?: string
  winnerElo: number
  loserName: string
  loserLogoUrl?: string
  loserElo: number
  score: string
  eloDiff: number
  matchDate: string
  program: string
}

export interface MatchOfWeek {
  homeTeamName: string
  homeLogoUrl?: string
  homeElo: number
  awayTeamName: string
  awayLogoUrl?: string
  awayElo: number
  matchDate: string
  combinedElo: number
  program: string
}

export interface QuickStats {
  totalMatches: number
  totalTeams: number
  totalRegions: number
  completedMatches: number
}
