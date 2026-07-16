"use client";

import { Loader2, ShieldCheck } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";
import {
  type OAuthConsentInteraction,
  apiFetch,
  readSession,
} from "@/lib/api";

type AccessMode = "ReadOnly" | "ReadWrite";

export function OAuthConsentPage({ interaction }: { interaction?: string }) {
  // Keep a narrowed copy for callbacks declared below the early return. TypeScript
  // cannot retain the narrowing of an optional prop inside that closure.
  const interactionId = interaction ?? "";
  const [details, setDetails] = useState<OAuthConsentInteraction | null>(null);
  const [token, setToken] = useState<string | null>(null);
  const [householdId, setHouseholdId] = useState("");
  const [accessMode, setAccessMode] = useState<AccessMode>("ReadOnly");
  const [expiresAt, setExpiresAt] = useState(() => dateInputValue(90));
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const session = useMemo(() => readSession(), []);
  const canWrite = details?.requestedScopes.includes("homepit.write") ?? false;

  useEffect(() => {
    if (!interactionId || !session?.accessToken) {
      return;
    }
    setToken(session.accessToken);
    setHouseholdId(session.households[0]?.id ?? "");
    void apiFetch<OAuthConsentInteraction>(`/api/oauth/consent/${encodeURIComponent(interactionId)}`, { token: session.accessToken })
      .then(setDetails)
      .catch((reason: unknown) => setError(reason instanceof Error ? reason.message : "Não foi possível abrir a autorização."));
  }, [interactionId, session]);

  if (!interaction) {
    return <ConsentShell title="Autorização indisponível" description="A solicitação de conexão não foi encontrada." />;
  }

  if (!session) {
    return (
      <ConsentShell title="Entre para continuar" description="Faça login no HomePit e volte a esta página para escolher o acesso da conexão.">
        <Button asChild><a href={`/?returnTo=${encodeURIComponent(`/oauth/consent?interaction=${interactionId}`)}`}>Entrar no HomePit</a></Button>
      </ConsentShell>
    );
  }

  if (error) {
    return <ConsentShell title="Não foi possível autorizar" description={error} />;
  }

  if (!details || !token) {
    return <ConsentShell title="Preparando autorização" description="Verificando a solicitação de conexão."><Loader2 className="size-5 animate-spin" /></ConsentShell>;
  }

  async function decide(decision: "approve" | "deny") {
    setSubmitting(true);
    setError(null);
    try {
      const path = `/api/oauth/consent/${encodeURIComponent(interactionId)}/${decision}`;
      const result = decision === "approve"
        ? await apiFetch<{ continueUrl: string }>(path, {
            method: "POST",
            token: token ?? undefined,
            body: JSON.stringify({ householdId, accessMode, expiresAt: new Date(`${expiresAt}T23:59:59`).toISOString() }),
          })
        : await apiFetch<{ continueUrl: string }>(path, { method: "POST", token: token ?? undefined });
      window.location.assign(result.continueUrl);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Não foi possível concluir a autorização.");
      setSubmitting(false);
    }
  }

  return (
    <main className="mx-auto flex min-h-screen max-w-2xl items-center px-5 py-10">
      <Card className="w-full">
        <CardHeader>
          <div className="mb-2 flex size-11 items-center justify-center rounded-full bg-primary/10 text-primary"><ShieldCheck className="size-6" /></div>
          <CardTitle>Autorizar conexão</CardTitle>
          <CardDescription><strong className="text-foreground">{details.clientName}</strong> poderá usar o HomePit conforme as escolhas abaixo.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-5">
          <div className="rounded-[18px] border border-border/70 bg-surface-muted p-4 text-sm text-muted-foreground">
            Permissões solicitadas: {details.requestedScopes.map(scopeLabel).join(", ")}.
          </div>
          <label className="grid gap-2 text-sm font-semibold text-foreground">
            Casa
            <Select value={householdId} onChange={(event) => setHouseholdId(event.target.value)}>
              {session.households.map((household) => <option key={household.id} value={household.id}>{household.name}</option>)}
            </Select>
          </label>
          <label className="grid gap-2 text-sm font-semibold text-foreground">
            Acesso
            <Select value={accessMode} onChange={(event) => setAccessMode(event.target.value as AccessMode)}>
              <option value="ReadOnly">Somente leitura</option>
              {canWrite ? <option value="ReadWrite">Leitura e escrita</option> : null}
            </Select>
          </label>
          <label className="grid gap-2 text-sm font-semibold text-foreground">
            Válida até
            <Input type="date" value={expiresAt} min={dateInputValue(1)} max={dateInputValue(365)} onChange={(event) => setExpiresAt(event.target.value)} />
          </label>
          {error ? <p className="text-sm text-danger">{error}</p> : null}
          <div className="flex flex-wrap justify-end gap-3">
            <Button type="button" variant="secondary" disabled={submitting} onClick={() => void decide("deny")}>Recusar</Button>
            <Button type="button" disabled={submitting || !householdId} onClick={() => void decide("approve")}>{submitting ? <Loader2 className="animate-spin" /> : null}Autorizar</Button>
          </div>
        </CardContent>
      </Card>
    </main>
  );
}

function ConsentShell({ title, description, children }: { title: string; description: string; children?: React.ReactNode }) {
  return <main className="mx-auto flex min-h-screen max-w-xl items-center px-5 py-10"><Card className="w-full"><CardHeader><CardTitle>{title}</CardTitle><CardDescription>{description}</CardDescription></CardHeader>{children ? <CardContent>{children}</CardContent> : null}</Card></main>;
}

function dateInputValue(days: number) {
  const value = new Date();
  value.setDate(value.getDate() + days);
  return value.toISOString().slice(0, 10);
}

function scopeLabel(scope: string) {
  if (scope === "homepit.read") return "leitura";
  if (scope === "homepit.write") return "leitura e escrita";
  if (scope === "offline_access") return "manter a conexão ativa";
  return scope;
}
