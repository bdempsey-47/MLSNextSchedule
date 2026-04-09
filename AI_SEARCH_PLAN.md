# Plan: Integrate Azure AI Search for Team Name Search

## Context
Currently, the Schedules page team search works by:
1. Fetching ALL teams from `/api/teams` (filtered by program/season/region)
2. Client-side substring filtering as the user types
3. Selecting a team name string, which is passed to `/api/matches?team=...`

We want to replace the team suggestion/autocomplete with Azure AI Search (`yss-ai-search-prod`, index `search-teamnames`) for better fuzzy/wildcard search. The API key stays server-side via a proxy Azure Function.

## Changes Required

### 1. New Azure Function: `SearchTeams.cs`
**File:** `YSS.Functions/Triggers/SearchTeams.cs` (new file)

- HTTP GET trigger at route `/api/search-teams`
- Query param: `q` (the search text)
- Calls Azure AI Search REST API:
  ```
  POST https://yss-ai-search-prod.search.windows.net/indexes/search-teamnames/docs/search?api-version=2024-07-01
  Headers: api-key: <from app setting>, Content-Type: application/json
  Body: { "search": "<q>*", "queryType": "full" }
  ```
- Returns the `value` array from the AI Search response (array of `{ Id, Name, Program }`)
- Store the API key in an app setting (e.g., `AzureSearchApiKey`) — add to `local.settings.json` for local dev, and Azure Function App Configuration for prod
- Use `HttpClient` (injected via DI or `IHttpClientFactory`) to make the outbound call

### 2. Update FilterBar Component
**File:** `YSS.Web/src/components/FilterBar.tsx`

Current behavior (lines ~86-130): When program/season/region change, fetches all teams from `/api/teams`, stores in state, then filters client-side as user types.

**Replace with:**
- Remove the bulk team fetch on filter change (remove the `useEffect` that calls `/api/teams`)
- Remove the `teams` state array and client-side filtering logic
- Instead, when the user types in the team search input, **debounce** (300ms) and call `/api/search-teams?q=<input>`
- Display the returned results as autocomplete suggestions
- Keep the existing selection behavior (set team name string on click)
- Keep the AbortController pattern for canceling in-flight requests
- Keep the clear (×) button behavior

**Key details:**
- Add a debounce (300ms recommended) so you don't call the API on every keystroke
- Append `*` wildcard on the frontend OR in the backend (pick one place — backend is cleaner)
- Minimum 2 characters before searching (avoid overly broad queries)
- The response shape `{ Id, Name, Program }` matches what FilterBar already uses for suggestions

### 3. App Settings / Configuration
- **`local.settings.json`** (`YSS.Functions/local.settings.json`): Add `"AzureSearchApiKey": "<your-query-key>"`
- **Azure Portal**: Add `AzureSearchApiKey` to Function App → Configuration → Application Settings
- Use a **query key** (read-only), not the admin key

### 4. CORS (if needed)
No CORS changes needed — the frontend calls your own Azure Function, not AI Search directly.

## Files Summary

| File | Action | What to do |
|------|--------|------------|
| `YSS.Functions/Triggers/SearchTeams.cs` | **Create** | New proxy endpoint for Azure AI Search |
| `YSS.Functions/local.settings.json` | **Edit** | Add `AzureSearchApiKey` setting |
| `YSS.Web/src/components/FilterBar.tsx` | **Edit** | Replace bulk team fetch + client filter with debounced API search |

## Implementation Tips
- For debounce, you can use a simple `setTimeout`/`clearTimeout` pattern in the `useEffect` or extract a small `useDebounce` hook — no need for a library
- The AI Search response wraps results in `{ value: [...] }` — unwrap in the Azure Function before returning, so the frontend gets a clean array
- Test with wildcard queries like `slam*` and also partial matches like `galaxy`

## Verification
1. **Local test (backend):** Run `func start` with the new function, hit `http://localhost:7071/api/search-teams?q=slam` — should return matching teams
2. **Local test (frontend):** Run `npm run dev`, go to Schedules, type a team name — suggestions should appear after debounce
3. **Edge cases:** Empty input (no call), 1 character (no call), clear button, selecting a suggestion, rapid typing (only last request fires)
4. **Existing tests:** Run `dotnet test YSS.Tests` to make sure nothing broke
