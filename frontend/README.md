# Frontend

The frontend is a React 19 and TypeScript application built with Vite and Material UI. It renders the dashboard, status board, administration screens, line detail views, and reports UI.

## Main Areas

- [src/README.md](src/README.md): top-level source layout
- [src/components/README.md](src/components/README.md): reusable UI pieces
- [src/pages/README.md](src/pages/README.md): route-level screens
- [src/services/README.md](src/services/README.md): API and data access layer

## Development

```powershell
Set-Location frontend
npm install
npm run dev
```

## Build

```powershell
npm run build
```

## Test

```powershell
npm run test
```

The frontend depends on the backend API at `http://localhost:5265/api` unless `VITE_API_BASE_URL` is set.

## Settings And Persistence

The frontend stores display preferences in browser local storage and scopes them
to the currently signed-in user.

- Theme preference defaults to light mode when no saved preference exists.
- Appearance settings (monitor name and colors) persist across reloads.
- Preferences do not sync automatically across browsers or machines.
