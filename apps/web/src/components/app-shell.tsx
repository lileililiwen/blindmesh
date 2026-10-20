"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useState } from "react";

import { useSession } from "@/components/session-provider";
import { ThemeToggle } from "@/components/theme-toggle";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/cn";

/**
 * The shell of the product. The navigation shows the state of the session, so
 * a visitor always knows whether they are signed in and where the next action
 * is.
 */
export function AppShell({ children }: { children: React.ReactNode }) {
  const { state, signOut } = useSession();
  const pathname = usePathname();
  const [menuOpen, setMenuOpen] = useState(false);
  const [signingOut, setSigningOut] = useState(false);

  const signedIn = state.kind === "authenticated";
  const user = signedIn ? state.user : null;

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

            <button
              type="button"
              className="rounded-md border border-border px-3 py-2 text-small md:hidden"
              aria-expanded={menuOpen}
              aria-controls="primary-navigation"
              onClick={() => setMenuOpen((open) => !open)}
            >
              Menu
            </button>

            <nav
              id="primary-navigation"
              aria-label="Primary"
              className={cn(
                "w-full flex-col gap-3 md:flex md:w-auto md:flex-row md:items-center md:gap-4",
                menuOpen ? "flex" : "hidden",
              )}
            >
              <ul className="flex flex-col gap-3 md:flex-row md:items-center md:gap-4">
                <li>
                  <Link
                    href="/"
                    aria-current={pathname === "/" ? "page" : undefined}
                    className="text-small underline underline-offset-4"
                  >
                    Home
                  </Link>
                </li>

                {signedIn && user ? (
                  <>
                    <li>
                      <Link
                        href="/projects"
                        aria-current={pathname === "/projects" ? "page" : undefined}
                        className="text-small underline underline-offset-4"
                      >
                        My projects
                      </Link>
                    </li>
                    <li>
                      <Link
                        href={`/people/${user.username}`}
                        aria-current={pathname === `/people/${user.username}` ? "page" : undefined}
                        className="text-small underline underline-offset-4"
                      >
                        My profile
                      </Link>
                    </li>
                    <li>
                      <Link
                        href="/settings/profile"
                        aria-current={pathname === "/settings/profile" ? "page" : undefined}
                        className="text-small underline underline-offset-4"
                      >
                        Profile settings
                      </Link>
                    </li>
                    <li>
                      <Button
                        type="button"
                        variant="ghost"
                        size="small"
                        disabled={signingOut}
                        onClick={() => {
                          setSigningOut(true);
                          void signOut().finally(() => setSigningOut(false));
                        }}
                      >
                        {signingOut ? "Signing out" : "Sign out"}
                      </Button>
                    </li>
                  </>
                ) : (
                  <li>
                    <Link
                      href="/sign-in"
                      aria-current={pathname === "/sign-in" ? "page" : undefined}
                      className="text-small underline underline-offset-4"
                    >
                      Sign in
                    </Link>
                  </li>
                )}
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
