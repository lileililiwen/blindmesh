# UI concerns

Use the vendored shared tokens with Tailwind semantic mappings and shadcn/ui primitives. Prefer native semantics. Single main landmark, skip link, visible focus, names and descriptions, field error association, live status announcements, focus restoration, and reduced motion are required.

All human-facing changes cover initial/loading/empty/validation/error/unavailable/permission/success/recovery states; destructive actions have explicit confirmation. Verify light/dark contrast and 360/768/1440px layouts through real browser interactions. Token contrast evidence alone does not prove rendered accessibility.
