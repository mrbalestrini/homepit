import type { Metadata } from "next";
import { API_BASE_URL, type InstitutionalPageContent } from "@/lib/api";
import { InstitutionalLanding } from "@/features/institutional/institutional-landing";

export const dynamic = "force-dynamic";

async function loadInstitutionalPage(): Promise<InstitutionalPageContent> {
  const response = await fetch(`${API_BASE_URL}/api/institutional-page`, {
    cache: "no-store",
  });

  if (!response.ok) {
    throw new Error("Não foi possível carregar a página institucional.");
  }

  return (await response.json()) as InstitutionalPageContent;
}

export async function generateMetadata(): Promise<Metadata> {
  const page = await loadInstitutionalPage();
  const heroImageUrl = page.hasHeroImage
    ? `${API_BASE_URL}/api/institutional-page/images/hero?v=${encodeURIComponent(page.heroImageUpdatedAt ?? "")}`
    : undefined;

  return {
    title: page.seoTitle,
    description: page.seoDescription,
    openGraph: {
      title: page.seoTitle,
      description: page.seoDescription,
      type: "website",
      images: heroImageUrl ? [{ url: heroImageUrl, alt: page.heroImageAlt }] : undefined,
    },
  };
}

export default async function Home() {
  return <InstitutionalLanding page={await loadInstitutionalPage()} />;
}
