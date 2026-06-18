import type { Metadata } from "next";
import { API_BASE_URL, type InstitutionalPageContent } from "@/lib/api";

export function buildInstitutionalMetadata(page: InstitutionalPageContent): Metadata {
  const sharedImageUrl = page.hasSeoImage
    ? institutionalImageUrl("seo", page.seoImageUpdatedAt)
    : page.hasHeroImage
      ? institutionalImageUrl("hero", page.heroImageUpdatedAt)
      : undefined;

  const openGraphImages = sharedImageUrl ? [{ url: sharedImageUrl, alt: page.seoTitle }] : undefined;

  return {
    title: page.seoTitle,
    description: page.seoDescription,
    openGraph: {
      title: page.seoTitle,
      description: page.seoDescription,
      type: "website",
      images: openGraphImages,
    },
    twitter: {
      card: sharedImageUrl ? "summary_large_image" : "summary",
      title: page.seoTitle,
      description: page.seoDescription,
      images: sharedImageUrl ? [sharedImageUrl] : undefined,
    },
  };
}

function institutionalImageUrl(slot: "hero" | "seo", updatedAt?: string | null) {
  return `${API_BASE_URL}/api/institutional-page/images/${slot}?v=${encodeURIComponent(updatedAt ?? "")}`;
}
