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
  logoUrl?: string
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
  teamId: number
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
  gfm: number
  gam: number
  gdm: number
}
