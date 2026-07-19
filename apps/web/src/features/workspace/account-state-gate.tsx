"use client";

import { AlertTriangle, Loader2, LogOut, RotateCcw } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { type AuthResponse, apiFetch, clearSession, updateStoredSession } from "@/lib/api";

export function AccountStateGate({
  session,
  children,
}: {
  session: AuthResponse;
  children: React.ReactNode;
}) {
  if ((session.user.accountState ?? "Active") === "Active") {
    return <>{children}</>;
  }

  return <BlockedAccountScreen session={session} />;
}

function BlockedAccountScreen({ session }: { session: AuthResponse }) {
  const [loading, setLoading] = useState(false);

  async function reactivateAccount() {
    setLoading(true);

    try {
      const user = await apiFetch<AuthResponse["user"]>("/api/users/me/reactivate", {
        method: "POST",
        token: session.accessToken,
      });

      updateStoredSession((currentSession) =>
        currentSession
          ? {
              ...currentSession,
              user,
            }
          : currentSession,
      );

      toast.success("Cancelamento desfeito e conta reativada.");
    } catch (exception) {
      toast.error(exception instanceof Error ? exception.message : "Não foi possível reativar a conta.");
    } finally {
      setLoading(false);
    }
  }

  const scheduledDeletionLabel = session.user.scheduledDeletionAt
    ? new Intl.DateTimeFormat("pt-BR", {
        dateStyle: "full",
        timeStyle: "short",
      }).format(new Date(session.user.scheduledDeletionAt))
    : null;

  const isPendingSelfDeletion = (session.user.accountState ?? "Active") === "PendingSelfDeletion";

  return (
    <main className="grid min-h-screen place-items-center bg-background px-4 py-10">
      <Card className="w-full max-w-2xl overflow-hidden">
        <div className="h-2 bg-gradient-to-r from-amber-500 via-rose-500 to-primary" />
        <CardHeader className="space-y-4 pb-4">
          <div className="flex size-14 items-center justify-center rounded-[18px] bg-status-warning-soft text-warning">
            <AlertTriangle className="size-7" />
          </div>
          <div className="space-y-2">
            <CardTitle className="text-3xl">
              {isPendingSelfDeletion ? "Conta desativada por você" : "Conta desativada"}
            </CardTitle>
            <CardDescription className="max-w-xl text-sm leading-6">
              {isPendingSelfDeletion
                ? `Seu acesso está pausado. Seus dados serão apagados em ${scheduledDeletionLabel ?? "data não informada"} caso o cancelamento não seja desfeito.`
                : `Seu acesso foi desativado pelo superadmin. Entre em contato por ${session.user.supportEmail ?? "e-mail de suporte não informado"}.`}
            </CardDescription>
          </div>
        </CardHeader>
        <CardContent className="space-y-5">
          {isPendingSelfDeletion ? (
            <div className="rounded-[18px] border border-border/70 bg-surface-muted p-4 text-sm leading-6 text-muted-foreground">
              Você pode sair agora e manter o cancelamento agendado, ou desfazer o cancelamento para recuperar suas espaço(s)
              e continuar usando o sistema normalmente.
            </div>
          ) : (
            <div className="rounded-[18px] border border-border/70 bg-surface-muted p-4 text-sm leading-6 text-muted-foreground">
              O login foi concluído apenas para mostrar este aviso. Operações comuns permanecem bloqueadas enquanto a conta
              estiver desativada.
            </div>
          )}
          <div className="flex flex-col gap-3 sm:flex-row">
            <Button variant="secondary" className="sm:min-w-40" onClick={() => clearSession()} disabled={loading}>
              <LogOut />
              Sair
            </Button>
            {isPendingSelfDeletion ? (
              <Button className="sm:min-w-72" onClick={() => void reactivateAccount()} disabled={loading}>
                {loading ? <Loader2 className="animate-spin" /> : <RotateCcw />}
                Desfazer cancelamento da conta e recuperar espaço(s)
              </Button>
            ) : null}
          </div>
        </CardContent>
      </Card>
    </main>
  );
}
