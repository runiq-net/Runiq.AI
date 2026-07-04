# Studio And Embedded Dashboard Context

Use this context before changing dashboard, embedded dashboard, Studio, UI, pages, routes, navigation, or dashboard client behavior.

## Ownership

- `src/Runiq.Dashboard.Client` is the dashboard/studio client area.
- It is a Vite/React/TypeScript client folder with `package.json`, Vite config, TypeScript config, pages, routes, layouts, components, API client modules, theme code, and dashboard configuration.
- Dashboard UI code belongs in `src/Runiq.Dashboard.Client`.
- Reusable dashboard pages, components, layouts, routes, and navigation behavior should live in the dashboard client area.
- React, UI, and client-side code should not be placed in C# library or host projects.

## Studio Assumption

- Assumption from repository structure: Studio and embedded dashboard appear to be the same client area in this repository because `src/Runiq.Dashboard.Client` contains dashboard pages, workflow studio layouts, routes, and client-side app entry points.
- If an execution unit depends on a distinction between Studio and embedded dashboard, inspect existing code before changing behavior.
- Do not invent a separate `Runiq.Studio` project.

## Host And Runtime Boundaries

- Host and sample projects may wire, serve, or demonstrate embedded dashboard assets.
- Host and sample projects should not duplicate reusable UI.
- Server/runtime behavior should not be added to `src/Runiq.Dashboard.Client`.
- Authentication, backend APIs, runtime bridges, provider logic, and dependency injection registration are out of scope unless an execution unit explicitly asks for them.
- Dashboard runtime hosting belongs outside the client area, typically in the relevant C# host/runtime project.

## Before Adding Pages Or Routes

- Inspect existing route definitions.
- Inspect route rendering.
- Inspect navigation and layout conventions.
- Inspect nearby page and component naming/style conventions.
- Reuse existing client-side API modules, layout patterns, and components when they fit.

## Current Client Structure

- `src/Runiq.Dashboard.Client/src/routes.ts` owns route definitions.
- `src/Runiq.Dashboard.Client/src/routeRendering.tsx` participates in route rendering.
- `src/Runiq.Dashboard.Client/src/layouts/` contains dashboard and workflow studio layouts.
- `src/Runiq.Dashboard.Client/src/pages/` contains page-level UI.
- `src/Runiq.Dashboard.Client/src/components/` contains reusable UI components.
- `src/Runiq.Dashboard.Client/src/api/` contains client-side API wrappers.
- `src/Runiq.Dashboard.Client/src/theme/` contains theme support.

## Validation

- Use client commands from `src/Runiq.Dashboard.Client/package.json` when dashboard client files change.
- Current discovered commands include `npm run build` and `npm run lint`.
- Do not invent dashboard validation commands that are not present in the package scripts.
