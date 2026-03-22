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
