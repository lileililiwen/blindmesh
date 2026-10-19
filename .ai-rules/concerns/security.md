# Security and privacy concerns

Use deny-by-default object authorization. Return 404 for private objects outside the actor's visibility and 403 for forbidden mutations of visible objects. Keep GitHub login separate from third-party resource delegation; collect no resource credentials.

Do not fetch arbitrary task targets server-side. Browser links require a visible external-target notice. Evidence is private by default and is downloaded only after parent-object authorization; no public buckets or permanent public URLs.

Use secure session cookies, one-time OAuth state, CSRF defenses, rate limits, audit redaction, upload quarantine, and bounded user input. Logs exclude cookies, OAuth codes, credentials, private evidence, and raw request bodies.
