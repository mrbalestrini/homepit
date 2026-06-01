import type { Metadata } from "next";
import { Fraunces, Manrope } from "next/font/google";
import Script from "next/script";
import "./globals.css";
import { Toaster } from "@/components/ui/toaster";

const manrope = Manrope({
  subsets: ["latin"],
  variable: "--font-manrope",
});

const fraunces = Fraunces({
  subsets: ["latin"],
  variable: "--font-fraunces",
});

export const metadata: Metadata = {
  title: "HomePit",
  description: "Projetos de casa com universos, projetos, atividades e pendências.",
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <html
      lang="pt-BR"
      className={`${manrope.variable} ${fraunces.variable}`}
      data-theme="earthy"
      suppressHydrationWarning
    >
      <head>
        <Script
          id="homepit-theme-init"
          strategy="beforeInteractive"
          dangerouslySetInnerHTML={{
            __html: `
              try {
                var theme = window.localStorage.getItem("homepit.ui.theme");
                if (theme === "cozy" || theme === "earthy" || theme === "dark") {
                  document.documentElement.dataset.theme = theme;
                }
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
