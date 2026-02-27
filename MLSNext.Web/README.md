# MLS Next Schedule - React Frontend

A responsive React application for browsing MLS Next soccer schedules by division, team, and age group.

## Overview

This project serves as the frontend for the MLS Next Schedule system, providing a user-friendly interface to search and view soccer match schedules. Built with React 18, TypeScript, and Vite for optimal development experience and build performance.

## Tech Stack

- **React 18.2.0** - UI library
- **TypeScript 5.2.2** - Type-safe development
- **Vite 5.0.8** - Modern build tool and dev server
- **CSS3** - Mobile-first responsive styling
- **Node.js** - JavaScript runtime

## Project Structure

```
mlsnext-web/
├── index.html                 # Single-page app entry point
├── vite.config.ts            # Vite configuration
├── tsconfig.json             # TypeScript configuration
├── package.json              # Dependencies and scripts
├── .env.example              # Environment variables template
├── .env.development          # Development environment settings
├── .gitignore               # Git ignore rules
└── src/
    ├── main.tsx             # React entry point
    ├── App.tsx              # Root component with state management
    ├── App.css              # App styling (header, layout)
    ├── index.css            # Global styles and resets
    ├── types.ts             # TypeScript interfaces
    └── components/
        ├── ProgramSelector/
        │   ├── ProgramSelector.tsx      # Homegrown/Academy selection
        │   └── ProgramSelector.css
        ├── FilterBar/
        │   ├── FilterBar.tsx            # Region, team, age group filters
        │   └── FilterBar.css
        ├── MatchList/
        │   ├── MatchList.tsx            # Match list container
        │   └── MatchList.css
        └── MatchCard/
            ├── MatchCard.tsx            # Individual match display
            └── MatchCard.css
```

## Components

### ProgramSelector
**Purpose:** Allow users to choose between Homegrown (Tournament 12) and Academy (Tournament 35) programs

**Features:**
- Two-button interface with visual feedback
- Icons and descriptions for each program
- Active state styling

### FilterBar
**Purpose:** Provide filtering options for schedules

**Filters:**
- **Region** - Dropdown selecting NorthEast, Southeast, Mountain, Frontier, South, Midwest, or West
- **Team Search** - Text input to find matches by team name
- **Age Groups** - Multi-select checkboxes for U13, U14, U15, U16, U17, U18

**Features:**
- Real-time filter updates via URLSearchParams
- Responsive grid layout
- Accessible form controls

### MatchList
**Purpose:** Display collection of matches

**Features:**
- Sorts matches chronologically by matchDateUtc
- Displays match count
- Responsive grid layout (380px cards on desktop, 1 column on mobile)

### MatchCard
**Purpose:** Display individual match information

**Displays:**
- Home vs Away teams
- Match score (or "TBD" if not yet played)
- Date and time (UTC converted to local timezone)
- Venue name
- Competition name
- Age group and gender badges

**Features:**
- Hover elevation effect
- Responsive stacking on mobile
- Visual score badge with gradient background

## Getting Started

### Prerequisites
- Node.js 18+ (npm 9+)

### Installation

1. Navigate to project directory:
```bash
cd mlsnext-web
```

2. Install dependencies:
```bash
npm install
```

### Development

Start the development server:
```bash
npm run dev
```

Server runs on `http://localhost:5173` with hot module reloading enabled.

### Type Checking

Run TypeScript compiler without emitting:
```bash
npm run type-check
```

### Linting

Check for code quality issues:
```bash
npm run lint
```

### Building for Production

Create optimized production build:
```bash
npm run build
```

Output is in the `dist/` directory.

### Preview Production Build

Test the production build locally:
```bash
npm run preview
```

## Environment Configuration

### Development (.env.development)
```
VITE_API_BASE_URL=http://localhost:7071/api
```

### Production (.env.production)
```
VITE_API_BASE_URL=https://your-production-api.azurewebsites.net/api
```

Use `.env.example` as a template for creating environment files.

## API Integration

### Endpoint: /api/matches

**Query Parameters:**
- `team` - Filter by team name (optional)
- `division` - Filter by region/division (optional)
- `ageGroup` - Filter by age group, can be repeated for multiple values (optional)

**Example Requests:**
```
GET /api/matches?team=FC%20Dallas
GET /api/matches?division=NorthEast&ageGroup=U17&ageGroup=U18
GET /api/matches?team=Real%20Salt%20Lake&division=Mountain
```

**Response Format:**
```json
[
  {
    "matchId": "uuid",
    "homeTeam": { "teamId": "uuid", "name": "FC Dallas U17" },
    "awayTeam": { "teamId": "uuid", "name": "Real Salt Lake U17" },
    "matchDateUtc": "2024-03-15T18:30:00Z",
    "score": "2-1",
    "venue": { "venueId": "uuid", "name": "Toyota Soccer Center" },
    "ageGroup": { "ageGroupId": "uuid", "name": "U17" },
    "gender": "Male",
    "competition": { "competitionId": "uuid", "name": "Spring Tournament" },
    "region": { "regionId": "uuid", "name": "NorthEast" }
  }
]
```

## TypeScript Types

See `src/types.ts` for complete type definitions including:
- `Match` - Complete match data
- `Team`, `Venue`, `AgeGroup`, `Region`, `Competition` - Entity types
- `Program` - Union type for program selection
- `FilterOptions` - Filter state type

## Responsive Design

### Breakpoints

- **Mobile First** - Base styles for small screens (375px+)
- **Tablet** - `@media (min-width: 600px)`
- **Desktop** - `@media (min-width: 768px)`

### Key Features

- Full-width layouts on mobile
- Multi-column grids on desktop
- Touch-friendly button sizing (44px minimum)
- Stacked filter form on mobile, grid layout on desktop
- Single-column match cards on mobile, grid on desktop

## Performance Optimizations

- **Code Splitting** - Vite automatically splits component imports
- **CSS Modules** - Scoped styling prevents conflicts
- **Lazy Loading** - Components loaded on demand
- **Production Build** - Minified CSS/JS with tree-shaking

## Styling Architecture

### Color Palette
- **Primary Gradient** - Purple #667eea → #764ba2
- **Accent** - White/light backgrounds for contrast
- **Text** - Dark gray (#333) on light, white on dark

### CSS Conventions
- BEM-inspired class naming
- Flexbox for layouts
- CSS Grid for component layouts
- CSS variables ready (can be added to :root in index.css)

## Browser Support

- Chrome/Edge 90+
- Firefox 88+
- Safari 14+
- Mobile browsers (iOS Safari 12+, Android Chrome 90+)

## Development Workflow

1. **Start Dev Server** - `npm run dev`
2. **Create/Edit Components** - Run type-check to catch errors early
3. **Test Filters** - Ensure filters generate correct API queries
4. **Build Check** - `npm run build` to verify production build succeeds
5. **Production Deploy** - Deploy `dist/` folder to CDN or static hosting

## Common Issues

### Module not found
Ensure import paths match file locations. Check `tsconfig.json` path mappings if using aliases.

### API calls failing
Verify `VITE_API_BASE_URL` in `.env.development` points to correct backend URL. Check browser DevTools Network tab for actual requests.

### Styles not applying
Check CSS file imports in component. Ensure CSS filename matches component (e.g., `MatchCard.tsx` → `MatchCard.css`).

### Build errors
Run `npm run type-check` to identify TypeScript errors before build.

## Future Enhancements

- Error boundary for graceful error handling
- Service worker for offline caching
- Advanced filtering (date range, specific venues)
- Match details modal with full information
- Team profiles and statistics
- User preferences and favorites
- Dark mode toggle
- Accessibility improvements (ARIA labels, keyboard navigation)

## Contributing

1. Keep components focused and reusable
2. Add TypeScript types for all props and state
3. Include descriptive comments for complex logic
4. Test responsive design at 375px, 768px, and 1200px breakpoints
5. Run `npm run type-check` before committing

## Deployment

### Quick Deploy to Vercel

```bash
npm install -g vercel
vercel
```

### Deploy to GitHub Pages

```bash
# Add to vite.config.ts: base: '/repository-name/'
npm run build
# Push dist/ folder to gh-pages branch
```

### Deploy to Static Host (Azure, AWS S3, etc.)

```bash
npm run build
# Upload contents of dist/ folder
```

Ensure environment variables are configured in deployment platform (e.g., GitHub Secrets, Vercel Environment Variables).

## License

Proprietary - MLS Next Schedule Project
