export interface Announcement {
  id: string
  message: string
  url: string
  expiresAt: string // 'YYYY-MM-DD' — bar hides after midnight on this date
  enabled: boolean  // set false to disable without removing
}

export const announcements: Announcement[] = [
  {
    id: 'mls-next-cup-2026',
    message: 'Follow the MLS Next Cup matches here',
    url: 'https://www.youthsoccerintelligence.com/Schedules?program=homegrown&program=academy&season=spring2026&region=MLS+Next+Cup',
    expiresAt: '2026-06-01',
    enabled: true,
  },
]
