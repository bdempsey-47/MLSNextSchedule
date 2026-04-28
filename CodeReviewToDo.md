# YSI Code Review — To-Do List

Generated: 2026-04-27

---

## ✅ Completed

### 1. Extract program filter into shared extension method
**Status:** DONE (April 27, 2026)

Extracted to `MatchQueryExtensions.FilterByProgram()` with overloads for `IQueryable<Match>` and `IEnumerable<Match>`. Updated all 6 call sites:
- GetMatches.cs (2 locations)
- GetAnalytics.cs
- GetPowerRankings.cs
- GetTeams.cs
- HomepageSnapshotService.cs

All 47 tests passing.

---

## High Priority

### 2. Fix `TournamentConstants` string/int inconsistency
**Status:** DONE (April 27, 2026)

Converted `HomegrownTournamentId` (12) and `AcademyTournamentId` (35) from `string` to `int` in `YSS.Constants/Constants.cs`. Added `FestTournamentId = 75`. Updated all call sites:
- `MatchUpsertService.cs`: Removed `int.Parse()` calls
- `MatchQueryExtensions.cs`: Replaced literal 35, 84, 12, 75 with constants
- `GetStandings.cs`: Replaced literal 35, 12, 75 with constants
- `Modular11Client.cs`: Updated `TournamentId` property and `BuildQueryParams()` to handle int
- `TournamentSeason.cs`: Changed `TournamentId` parameter from string to int
- Test fixtures & YSS.Verification: Updated to parse string config to int
- All 47 tests passing.

---

### 3. Fix sync-over-async in `GetStandings`
**Files:** `GetStandings.cs` lines 137–138, 249

`.Result` on async task inside async method = thread-pool starvation risk.

```csharp
// Before
var document = context.OpenAsync(req => req.Content(html)).Result;

// After
var document = await context.OpenAsync(req => req.Content(html));
```

Make `ParseStandings` and `ParseQoPRankings` return `async Task<List<...>>`.

---

### 4. Switch URL param management to `useSearchParams`
**Files:** `SchedulesPage.tsx` lines 13/46, `StandingsPage.tsx` lines 11/39, `AnalyticsPage.tsx` lines 51/78

All 3 pages use `new URLSearchParams(window.location.search)` + `history.replaceState`. Reads URL only at mount (not reactive), bypasses router history management, copy-pasted 3 times.

```tsx
// Before
const params = new URLSearchParams(window.location.search)
history.replaceState(null, '', `?${params.toString()}`)

// After
const [searchParams, setSearchParams] = useSearchParams()
setSearchParams(params, { replace: true })
```

---

### 5. Store ELO rank in `TeamAgeGroupElo` table
**Files:** `GetMatches.cs` lines 163–216, `EloRecomputeService.cs`

`GetMatches` runs full ELO rank computation scan on every request — loads all team IDs + all ELO records per request. Also produces different rank values than `GetPowerRankings` (different scope filters), which is the rank-mismatch bug.

**Fix:** Add `Rank` column to `TeamAgeGroupElo`, compute+store nightly in `EloRecomputeService`. `GetMatches` reads stored rank via single join instead of full scan.

---

## Medium Priority

### 6. Fix JSON serialization inconsistency
**Files:** `YSS.Functions/Program.cs`, all frontend transform functions

`GetMatches` uses `JsonNamingPolicy.CamelCase`; other endpoints use default (PascalCase). Results in dual-path fallbacks everywhere: `t.TeamName ?? t.teamName ?? ''`. 

**Fix:** Set camelCase globally in `Program.cs` DI config. Remove all dual-path fallbacks from frontend transforms.

---

### 7. Fix `FilterBar` JSON.stringify deduplication
**File:** `FilterBar.tsx` lines 111–125

`new Set<any>()` of JSON-serialized objects — fragile (property order), uses `any`.

```tsx
// Replace with:
const regionMap = new Map<number, Region>()
regionsData.forEach((r: any) => {
  const region = { id: r.Id ?? r.id, name: r.Name ?? r.name }
  regionMap.set(region.id, region)
})
setRegions([...regionMap.values()].sort((a, b) => a.name.localeCompare(b.name)))
```

---

### 8. Remove `useEffect` double-fire on team search
**File:** `FilterBar.tsx` lines 224–226

Effect fires `onFiltersChange` on every `teamSearch` change including parent-synced changes. Missing deps (`region`, `selectedAgeGroups`). Causes `fetchMatches` to trigger twice on badge click.

**Fix:** Remove the effect. Notify parent explicitly in `onChange` and `handleSelectTeam` handlers instead.

---

### 9. Fix `Season` type — empty string sentinel
**File:** `types.ts` line 2

```ts
// Before
export type Season = 'fall2025' | 'spring2026' | ''

// After
export type Season = 'fall2025' | 'spring2026'
// Represent "none" as Season[] being empty (already how it's used)
```

Empty string leaks into API calls as `params.append('season', '')`.

---

### 10. Extract `U18/19` exclusion to shared constant
**Files:** `FilterBar.tsx` line 313, `AnalyticsPage.tsx` line 292, `StandingsPage.tsx` line 261

`ag.name !== 'U18/19'` hardcoded in 3 files. Add to shared constants.

---

## Low Priority

### 11. Remove 28 debug `console.log` calls
**Files:** `SchedulesPage.tsx` (10 — emoji-prefixed), `FilterBar.tsx` (11 — `[SearchEffect]` traces fire every keystroke), `AnalyticsPage.tsx`, `HomePage.tsx`, `StandingsPage.tsx`

Remove or wrap in `if (import.meta.env.DEV)`.

---

### 12. `Load More` button inline styles
**File:** `SchedulesPage.tsx` lines 350–367

~7 inline style props while rest of codebase uses CSS classes. Inconsistency will make mobile compact-mode work harder.

---

### 13. Fix or delete `getProgramFromMatch`
**File:** `SchedulesPage.tsx` line 134

```tsx
return match.division.tournamentId === 12 ? 'homegrown' : 'academy'
```

Uses magic number `12`. Misclassifies FEST (75) and NJ Cup (84) as academy. No call sites found — likely dead code. Delete or fix.

---

### 14. Fix `getInitials` empty string crash
**File:** `MatchCard.tsx` line 77

```tsx
// Before
const getInitials = (name: string) =>
  name.split(' ').map(w => w[0]).slice(0, 3).join('').toUpperCase()

// After
const getInitials = (name: string) =>
  name.split(' ').map(w => w[0] ?? '').slice(0, 3).join('').toUpperCase()
```

Empty name → `w[0]` returns `undefined` → garbled output.

---

### 15. Fix wrong exception type caught in `MatchUpsertService`
**File:** `MatchUpsertService.cs` lines 125–130

Catches `InvalidOperationException` to detect duplicate match IDs, but actual duplicate key violation throws `DbUpdateException`. Risks silently swallowing unrelated EF configuration errors.

---

## Open Questions — RESOLVED

1. **Is `getProgramFromMatch` dead code?** — UNKNOWN. Needs grep for call sites before deleting. If no callers found, delete it.
2. **MatchCard rank vs Analytics rank** — SAME RANK. Both should show same stored rank. Confirms issue #5 (store `Rank` in `TeamAgeGroupElo`) is correct fix.
3. **Snapshot timing** — SHOULD RUN AFTER INGESTION. Move `ComputeHomepageSnapshot` trigger to 3 AM UTC (after `DailyIngestion` at 2 AM). Add to issue #5 work or as separate small fix in `DailyIngestion.cs` / `WeeklyIngestion.cs`.
