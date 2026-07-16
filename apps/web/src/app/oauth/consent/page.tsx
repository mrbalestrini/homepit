import { OAuthConsentPage } from "@/features/oauth/oauth-consent-page";

export default async function OAuthConsentRoute({ searchParams }: { searchParams: Promise<{ interaction?: string }> }) {
  const { interaction } = await searchParams;
  return <OAuthConsentPage interaction={interaction} />;
}
