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
