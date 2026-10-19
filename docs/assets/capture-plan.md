# Documentation capture plan

Status: BLOCKED — no application runtime exists. No screenshot has been fabricated.

After runtime-foundation: start the actual Compose stack, use globally installed shot-scraper against http://localhost:3000/ with --width 1440 --height 1000 --wait 1000 --output docs/assets/runtime-desktop.png; repeat at 360x800 as runtime-mobile.png. Capture the real loading/unavailable state with the API stopped and record the health status.

After identity and task delivery: use globally installed playwright-cli to sign in using the local test OAuth provider, create a real project/task, and capture the dashboard/task detail after visible success. Use seeded fictional accounts only. Never capture OAuth codes, cookies, resource identities, private logs, or actual customer data.

Record exact command, source route, viewport, selector/wait, timestamp, application commit, backend status, artifact checksum and privacy review in this file. Commit images only after checking their contents. Screenshots supplement interaction and accessibility evidence; they do not replace it.
