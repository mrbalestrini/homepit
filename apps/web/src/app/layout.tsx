import type { Metadata } from "next";
import { DM_Sans } from "next/font/google";
import Script from "next/script";
import "./globals.css";
import { Toaster } from "@/components/ui/toaster";

const dmSans = DM_Sans({
  subsets: ["latin"],
  variable: "--font-dm-sans",
});

export const metadata: Metadata = {
  metadataBase: new URL("https://organiza.club"),
  title: "Organiza Club | Controle tranquilo para cuidar da vida",
  description: "Seu espaço para organizar finanças, projetos, estudos, vida e muito mais.",
  applicationName: "Organiza Club",
  manifest: "/manifest.webmanifest",
  icons: {
    icon: [
      { url: "/brand/favicon-32.png", type: "image/png", sizes: "32x32" },
      { url: "/brand/pwa-192.png", type: "image/png", sizes: "192x192" },
    ],
    apple: [{ url: "/brand/apple-touch-icon.png", sizes: "180x180", type: "image/png" }],
  },
  openGraph: {
    type: "website",
    locale: "pt_BR",
    siteName: "Organiza Club",
    images: [{ url: "/brand/open-graph-1200x630.png", width: 1200, height: 630, alt: "Organiza Club" }],
  },
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <html
      lang="pt-BR"
      className={dmSans.variable}
      data-theme="light"
      data-theme-preference="system"
      suppressHydrationWarning
    >
      <head>
        <Script
          id="organiza-club-theme-init"
          strategy="beforeInteractive"
          dangerouslySetInnerHTML={{
            __html: `
              try {
                (function () {
                  var key = "organizaclub.ui.theme";
                  var media = window.matchMedia("(prefers-color-scheme: dark)");
                  var clearLegacyStorage = function (storage) {
                    for (var index = storage.length - 1; index >= 0; index -= 1) {
                      var storedKey = storage.key(index);
                      if (storedKey && /^homepit(?:[._:-]|$)/i.test(storedKey)) storage.removeItem(storedKey);
                    }
                  };
                  clearLegacyStorage(window.localStorage);
                  clearLegacyStorage(window.sessionStorage);
                  if ("caches" in window) {
                    window.caches.keys().then(function (names) {
                      names.filter(function (name) { return /^homepit(?:[._:-]|$)/i.test(name); }).forEach(function (name) { window.caches.delete(name); });
                    });
                  }
                  var apply = function () {
                    var stored = window.localStorage.getItem(key);
                    var preference = stored === "light" || stored === "dark" || stored === "system" ? stored : "system";
                    document.documentElement.dataset.themePreference = preference;
                    document.documentElement.dataset.theme = preference === "system" ? (media.matches ? "dark" : "light") : preference;
                  };
                  apply();
                  media.addEventListener("change", apply);
                  window.addEventListener("storage", function (event) { if (event.key === key) apply(); });
                })();
              } catch {}
            `,
          }}
        />
      </head>
      <body className="bg-background text-foreground font-sans antialiased">
        {children}
        <Toaster />
      </body>
    </html>
  );
}
