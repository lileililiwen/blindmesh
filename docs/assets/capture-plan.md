# Documentation capture plan

Status: the runtime foundation is captured. The images below are the real rendered home page of the compose stack. No screenshot is fabricated and no mock is used.

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

The `playwright-cli` tool of the plan is used for the captures of the later packages, after the identity and task delivery, with the local test OAuth provider and the seeded fictional accounts only.

Screenshots supplement the interaction and accessibility evidence; they do not replace it. The evidence of the runtime foundation is in `openspec/changes/runtime-foundation/verification.md` and in the browser journeys under `tests/e2e/runtime-foundation`.
