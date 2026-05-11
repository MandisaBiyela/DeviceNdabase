# DeviceDesk React frontend

Bundled with **esbuild** (no Vite). Output goes to `dist/`, which ASP.NET publishes under `wwwroot/app`.

## Commands

- `npm install` — install dependencies
- `npm run build` — production bundle (`dist/assets/bundle.js`, `bundle.css`)
- `npm run dev` — rebuild on file changes (`esbuild --watch`)

Open the UI from the backend: `http://localhost:5170/app/` (or your configured host).
