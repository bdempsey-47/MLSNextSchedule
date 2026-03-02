import { Match } from './types'

export const mockMatches: Match[] = [
  {
    matchId: '1',
    homeTeam: {
      id: 1,
      name: 'United FC'
    },
    awayTeam: {
      id: 2,
      name: 'City Stars'
    },
    matchDateUtc: new Date(Date.now() + 86400000).toISOString(),
    score: 'TBD',
    venue: {
      id: 1,
      name: 'Central Park Field',
      latitude: 40.7829,
      longitude: -73.9654
    },
    ageGroup: {
      id: 15,
      name: 'U15'
    },
    gender: 'Male',
    competition: {
      id: 1,
      name: 'MLS Next Cup'
    },
    region: {
      id: 1,
      name: 'Northeast'
    }
  },
  {
    matchId: '2',
    homeTeam: {
      id: 3,
      name: 'Phoenix Rising'
    },
    awayTeam: {
      id: 4,
      name: 'Denver United'
    },
    matchDateUtc: new Date(Date.now() + 172800000).toISOString(),
    score: '2-1',
    venue: {
      id: 2,
      name: 'Riverside Soccer Complex',
      latitude: 33.4484,
      longitude: -112.0742
    },
    ageGroup: {
      id: 16,
      name: 'U16'
    },
    gender: 'Female',
    competition: {
      id: 1,
      name: 'MLS Next Cup'
    },
    region: {
      id: 2,
      name: 'Southwest'
    }
  },
  {
    matchId: '3',
    homeTeam: {
      id: 5,
      name: 'Seattle Sounders'
    },
    awayTeam: {
      id: 6,
      name: 'Portland Timbers'
    },
    matchDateUtc: new Date(Date.now() + 259200000).toISOString(),
    score: 'TBD',
    venue: {
      id: 3,
      name: 'Pacific Northwest Academy',
      latitude: 47.6062,
      longitude: -122.3321
    },
    ageGroup: {
      id: 17,
      name: 'U17'
    },
    gender: 'Male',
    competition: {
      id: 2,
      name: 'Regional Championship'
    },
    region: {
      id: 3,
      name: 'West'
    }
  },
  {
    matchId: '4',
    homeTeam: {
      id: 7,
      name: 'Chicago Fire'
    },
    awayTeam: {
      id: 8,
      name: 'Minnesota United'
    },
    matchDateUtc: new Date(Date.now() + 345600000).toISOString(),
    score: 'TBD',
    venue: {
      id: 4,
      name: 'Midwest Training Center',
      latitude: 41.8781,
      longitude: -87.6298
    },
    ageGroup: {
      id: 18,
      name: 'U18'
    },
    gender: 'Female',
    competition: {
      id: 2,
      name: 'Regional Championship'
    },
    region: {
      id: 4,
      name: 'Midwest'
    }
  }
]
