import type { Metadata } from "next";

import { AppShell } from "@/components/app-shell";
import { SessionProvider } from "@/components/session-provider";
import "./globals.css";

export const metadata: Metadata = {
  title: "Blindmesh",
  description:
    "Blindmesh connects independent developers who test each other's software and settle the work in credits.",
};

const themeScript = `(function(){try{var stored=window.localStorage.getItem("theme");var prefers=window.matchMedia("(prefers-color-scheme: dark)").matches;var theme=(stored==="dark"||stored==="light")?stored:(prefers?"dark":"light");document.documentElement.setAttribute("data-theme",theme);}catch(error){document.documentElement.setAttribute("data-theme","light");}})();`;

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" suppressHydrationWarning>
      <head>
        <script dangerouslySetInnerHTML={{ __html: themeScript }} />
      </head>
      <body>
        <SessionProvider>
          <AppShell>{children}</AppShell>
        </SessionProvider>
      </body>
    </html>
  );
}
