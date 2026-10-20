"use client";

import { createContext, useCallback, useContext, useEffect, useMemo, useState } from "react";

import { loadSession, signOut, type SessionState } from "@/lib/identity";

type SessionContextValue = {
  state: SessionState;
  /** The CSRF token of the server session. It is only used for unsafe requests. */
  csrfToken: string;
  refresh: () => Promise<void>;
  signOut: () => Promise<boolean>;
};

const SessionContext = createContext<SessionContextValue | null>(null);

/**
 * The session of the visitor. The state lives in the browser only; the
 * session cookie stays with the API and is never readable by the interface.
 */
export function SessionProvider({ children }: { children: React.ReactNode }) {
  const [state, setState] = useState<SessionState>({ kind: "loading" });

  const refresh = useCallback(async () => {
    setState(await loadSession());
  }, []);

  useEffect(() => {
    let cancelled = false;

    void (async () => {
      const next = await loadSession();

      if (!cancelled) {
        setState(next);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, []);

  const value = useMemo<SessionContextValue>(
    () => ({
      state,
      csrfToken: state.kind === "authenticated" || state.kind === "anonymous" ? state.csrfToken : "",
      refresh,
      signOut: async () => {
        const token = state.kind === "authenticated" || state.kind === "anonymous" ? state.csrfToken : "";

        if (!token) {
          return false;
        }

        const removed = await signOut(token);

        // The private state of the visitor is dropped with the session.
        setState({ kind: "loading" });
        await refresh();

        return removed;
      },
    }),
    [state, refresh],
  );

  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>;
}

/** The session of the visitor. */
export function useSession(): SessionContextValue {
  const value = useContext(SessionContext);

  if (value === null) {
    throw new Error("The session provider is missing above this component.");
  }

  return value;
}
