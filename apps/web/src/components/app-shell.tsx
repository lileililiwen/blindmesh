import Link from "next/link";

import { ThemeToggle } from "@/components/theme-toggle";

export function AppShell({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex min-h-dvh flex-col">
      <a className="skip-link" href="#main-content">
        Skip to the main content
      </a>

      <header className="border-b border-border bg-surface">
        <div className="page-width flex flex-wrap items-center justify-between gap-3 py-3">
          <div className="flex items-center gap-6">
            <Link href="/" className="text-large font-semibold">
              Blindmesh
            </Link>

            <nav aria-label="Primary">
              <ul className="flex items-center gap-4">
                <li>
                  <Link
                    href="/"
                    aria-current="page"
                    className="text-small underline underline-offset-4"
                  >
                    Home
                  </Link>
                </li>
              </ul>
            </nav>
          </div>

          <ThemeToggle />
        </div>
      </header>

      <main id="main-content" tabIndex={-1} className="flex-1 py-8 sm:py-12">
        <div className="page-width flex flex-col gap-8">{children}</div>
      </main>

      <footer className="border-t border-border bg-surface">
        <div className="page-width py-6 text-small text-muted-foreground">
          <p>Blindmesh is a peer testing marketplace for independent developers.</p>
        </div>
      </footer>
    </div>
  );
}
