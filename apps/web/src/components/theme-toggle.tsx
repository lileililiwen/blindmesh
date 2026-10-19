"use client";

import { useSyncExternalStore } from "react";

import { Button } from "@/components/ui/button";

type Theme = "light" | "dark";

function currentTheme(): Theme {
  return document.documentElement.getAttribute("data-theme") === "dark" ? "dark" : "light";
}

function subscribe(onChange: () => void): () => void {
  const observer = new MutationObserver(onChange);
  observer.observe(document.documentElement, { attributes: true, attributeFilter: ["data-theme"] });

  return () => observer.disconnect();
}

/**
 * The theme lives in the document element, because it is set before the page is
 * painted. The component reads that source instead of keeping a second copy of
 * the state.
 */
export function ThemeToggle() {
  const theme = useSyncExternalStore(subscribe, currentTheme, () => "light" as Theme);

  function toggle() {
    const next: Theme = currentTheme() === "dark" ? "light" : "dark";

    document.documentElement.setAttribute("data-theme", next);
    window.localStorage.setItem("theme", next);
  }

  return (
    <Button
      type="button"
      variant="ghost"
      size="small"
      onClick={toggle}
      aria-label="Switch between the light and the dark theme"
    >
      <span aria-hidden="true">{theme === "dark" ? "Dark" : "Light"}</span>
      <span className="sr-only">The {theme} theme is active.</span>
    </Button>
  );
}
