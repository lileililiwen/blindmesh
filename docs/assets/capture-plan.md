# Documentation capture plan

Status: the runtime foundation is captured, and the scoped matching package is captured after its milestone. The images below are the real rendered pages of the compose stack and of the local fixture. No screenshot is fabricated and no mock is used.

## Captures of the runtime foundation

Source: the compose stack started with `docker compose -f infra/compose.dev.yaml up -d --build`, the migration applied with `dotnet run --project src/Blindmesh.Api -- migrate`. The proxy answers on `http://127.0.0.1:58080` in the recorded session, the documented default is `http://localhost:8080`.

| Artifact | Command | Viewport | State |
|---|---|---|---|
| `docs/assets/runtime-desktop.png` | `npx --prefix apps/web playwright screenshot --viewport-size="1440,1000" --wait-for-timeout=2500 --wait-for-selector="text=Runtime availability" http://127.0.0.1:58080/ docs/assets/runtime-desktop.png` | 1440x1000 | Available, light theme |
| `docs/assets/runtime-mobile.png` | `npx --prefix apps/web playwright screenshot --viewport-size="360,800" --wait-for-timeout=2500 --wait-for-selector="text=Runtime availability" http://127.0.0.1:58080/ docs/assets/runtime-mobile.png` | 360x800 | Available, light theme |
| `docs/assets/runtime-dark.png` | `npx --prefix apps/web playwright screenshot --viewport-size="1440,1000" --color-scheme=dark --wait-for-timeout=2500 --wait-for-selector="text=Runtime availability" http://127.0.0.1:58080/ docs/assets/runtime-dark.png` | 1440x1000 | Available, dark theme |
| `docs/assets/runtime-unavailable.png` | the same command with `--wait-for-selector="text=Unavailable"` after `docker compose -f infra/compose.dev.yaml stop postgres` | 1440x1000 | Unavailable, the database is down |

The `shot-scraper` binary of the capture plan is installed but its Python module is missing in this environment, so the Playwright screenshot command of the installed browser tooling is used. The recorded route, viewport, wait and selector are listed above for every artifact.

Privacy review: the captures show the public home page with the availability of the runtime. They contain no cookie, no token, no OAuth code, no private object, no customer data and no fictional metric. The dependency names are the technical names of the local services.

## Captures of the scoped matching package

Source: the local stack of the capture, on the database `bm_capture` with the migrated schema, the API on `http://127.0.0.1:58099`, the worker and the web application on `http://127.0.0.1:53120`, the local OAuth fixture enabled with `APP_ENV=test`. The page of the candidates belongs to the owner of a task, so a screenshot of a URL cannot reach it: the capture runs the real flow of the interface and writes the three artifacts below. The command is:

```
cd apps/web && WEB=http://127.0.0.1:53120 OUT=../../docs/assets node capture-matching.mjs
```

The script is `apps/web/capture-matching.mjs`. It signs in as alice with the local fixture, claims the grant, creates the public project "Browser rendering check", writes and publishes the task "Check the rendering of the candidate list" with the hard condition `region: eu-central-<timestamp>`, signs in as bob, declares the matching region, signs in as alice again and opens `/tasks/<id>/candidates`.

| Artifact | Viewport | State |
|---|---|---|
| `docs/assets/matching-candidates-desktop.png` | 1440x1000 | The owner reads the list of the candidates, light theme |
| `docs/assets/matching-candidates-mobile.png` | 360x800 | The same page in one column, light theme |
| `docs/assets/matching-candidates-dark.png` | 1440x1000 | The same page, dark theme, set as the stored preference before the page loads |

Privacy review: the captures show the page of the candidates of a task of the seeded fictional account alice, with the seeded fictional contributor bob, the public name of the account, the public declaration that satisfied the condition and the reason of the order. They contain no cookie, no token, no OAuth code, no private object, no email address, no real data and no fictional metric. The region value carries the timestamp of the capture, so the artifact is not linked to a person.

The `playwright-cli` tool of the plan is used for the captures of the later packages, after the identity and task delivery, with the local test OAuth provider and the seeded fictional accounts only.

Screenshots supplement the interaction and accessibility evidence; they do not replace it. The evidence of the runtime foundation is in `openspec/changes/runtime-foundation/verification.md` and in the browser journeys under `tests/e2e/runtime-foundation`; the evidence of the scoped matching package is in `openspec/changes/archive/2026-10-22-scoped-matching/verification.md` and in the browser journeys under `tests/e2e/scoped-matching`.
