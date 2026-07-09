"use client";

import { AlertTriangle, Camera, Loader2, ShieldAlert, Sparkles } from "lucide-react";
import { FormEvent, useEffect, useMemo, useState } from "react";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { AccountStateGate } from "@/features/workspace/account-state-gate";
import { HomePitAuth } from "@/features/workspace/homepit-auth";
import { HomePitWorkspaceShell, Notice } from "@/features/workspace/homepit-workspace-shell";
import { AvatarCircle, ProtectedUserAvatar } from "@/features/workspace/protected-user-avatar";
import { COMMON_IMAGE_ACCEPT, COMMON_IMAGE_HELP_TEXT } from "@/lib/image-upload";
import { type AuthResponse, type DeleteOwnAccountResult, apiFetch, clearSession, updateStoredSession } from "@/lib/api";
import { useProjectDashboard } from "@/features/projects/use-project-dashboard";

export function ProfilePage() {
  const dashboard = useProjectDashboard();

  if (!dashboard.session) {
    return <HomePitAuth onAuthenticated={dashboard.handleAuthenticated} />;
  }

  return (
    <AccountStateGate session={dashboard.session}>
      <ProfileWorkspace dashboard={dashboard} />
    </AccountStateGate>
  );
}

function ProfileWorkspace({ dashboard }: { dashboard: ReturnType<typeof useProjectDashboard> }) {
  const session = dashboard.session;
  if (!session) {
    return null;
  }

  const profileKey = [
    session.user.id,
    session.user.displayName,
    session.user.phoneNumber ?? "",
    session.user.profilePhotoUpdatedAt ?? "",
    session.user.hasProfilePhoto ? "1" : "0",
  ].join(":");

  return (
    <HomePitWorkspaceShell
      controller={{
        session,
        activeHouseholdId: dashboard.activeHouseholdId,
        activeHousehold: dashboard.activeHousehold,
        members: dashboard.members,
        theme: dashboard.theme,
        sidebarCollapsed: dashboard.sidebarCollapsed,
        loading: dashboard.loading,
        error: dashboard.error,
        canShareHousehold: dashboard.canShareHousehold,
        canManageHousehold: dashboard.canManageHousehold,
        editingHousehold: dashboard.editingHousehold,
        isHouseholdDialogOpen: dashboard.activeModal === "household",
        isShareDialogOpen: dashboard.activeModal === "share",
        setError: dashboard.setError,
        setSidebarCollapsed: dashboard.setSidebarCollapsed,
        setTheme: dashboard.setTheme,
        handleHouseholdChange: dashboard.handleHouseholdChange,
        handleLogout: dashboard.handleLogout,
        refreshHouseholds: dashboard.refreshHouseholds,
        refreshWorkspace: async () => dashboard.loadWorkspace(),
        openCreateHousehold: dashboard.openCreateHousehold,
        openEditHousehold: dashboard.openEditHousehold,
        openShareHousehold: dashboard.openShareHousehold,
        closeCommonModal: dashboard.closeModal,
        createHousehold: dashboard.createHousehold,
        updateHousehold: dashboard.updateHousehold,
        deleteHousehold: dashboard.deleteHousehold,
        shareHousehold: dashboard.shareHousehold,
      }}
      activeModule="profile"
      subtitle="Gerencie sua identidade, foto, contato e o ciclo de vida da conta"
      visibleCount={session.households.length}
      visibleLabel="casas"
      headerStats={[
        { label: "casas", value: session.households.length },
        { label: "próprias", value: session.households.filter((household) => household.role === "Owner").length },
      ]}
      requireHousehold={false}
    >
      <ProfilePanel key={profileKey} dashboard={dashboard} />
    </HomePitWorkspaceShell>
  );
}

function ProfilePanel({ dashboard }: { dashboard: ReturnType<typeof useProjectDashboard> }) {
  const session = dashboard.session!;
  const user = session.user;
  const token = session.accessToken;
  const householdId = dashboard.activeHouseholdId || undefined;
  const ownedHouseholdCount = session.households.filter((household) => household.role === "Owner").length;

  const [displayName, setDisplayName] = useState(user.displayName);
  const [phoneNumber, setPhoneNumber] = useState(user.phoneNumber ?? "");
  const [profilePhoto, setProfilePhoto] = useState<File | null>(null);
  const previewUrl = useObjectUrl(profilePhoto);
  const [savingProfile, setSavingProfile] = useState(false);
  const [dangerDialogOpen, setDangerDialogOpen] = useState(false);
  const [deletingAccount, setDeletingAccount] = useState(false);

  async function saveProfile(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSavingProfile(true);

    let profileSaved = false;

    try {
      let updatedUser = await apiFetch<AuthResponse["user"]>("/api/users/me", {
        method: "PUT",
        token,
        body: JSON.stringify({
          displayName,
          phoneNumber: phoneNumber || null,
        }),
      });
      profileSaved = true;
      applyUpdatedUser(updatedUser);

      if (profilePhoto) {
        const formData = new FormData();
        formData.append("file", profilePhoto);
        updatedUser = await apiFetch<AuthResponse["user"]>("/api/users/me/profile-photo", {
          method: "POST",
          token,
          body: formData,
        });
        applyUpdatedUser(updatedUser);
      }

      toast.success(profilePhoto ? "Perfil e foto atualizados." : "Perfil atualizado.");
    } catch (exception) {
      toast.error(
        exception instanceof Error
          ? exception.message
          : profileSaved
            ? "Os dados do perfil foram salvos, mas não foi possível concluir o envio da foto."
            : "Não foi possível atualizar o perfil.",
      );
    } finally {
      setSavingProfile(false);
    }
  }

  async function deleteAccount() {
    setDeletingAccount(true);

    try {
      const result = await apiFetch<DeleteOwnAccountResult>("/api/users/me", {
        method: "DELETE",
        token,
      });

      if (result.deletedImmediately) {
        clearSession();
        toast.success("Conta excluída com sucesso.");
        return;
      }

      const scheduledDeletionAt = result.scheduledDeletionAt ?? null;
      updateStoredSession((currentSession) =>
        currentSession
          ? {
              ...currentSession,
              user: {
                ...currentSession.user,
                accountState: "PendingSelfDeletion",
                scheduledDeletionAt,
              },
            }
          : currentSession,
      );

      toast.success("Conta desativada e exclusão agendada.");
      setDangerDialogOpen(false);
    } catch (exception) {
      toast.error(exception instanceof Error ? exception.message : "Não foi possível cancelar a conta.");
    } finally {
      setDeletingAccount(false);
    }
  }

  function applyUpdatedUser(updatedUser: AuthResponse["user"]) {
    updateStoredSession((currentSession) =>
      currentSession
        ? {
            ...currentSession,
            user: updatedUser,
          }
        : currentSession,
    );
  }

  const pendingCopy = useMemo(() => {
    if (ownedHouseholdCount === 0) {
      return "Se você excluir a conta agora, o acesso será encerrado imediatamente. Para voltar ao sistema no futuro, será necessário criar uma nova conta.";
    }

    return `Você é proprietário de ${ownedHouseholdCount} casa(s). Ao continuar, sua conta será desativada agora e apagada automaticamente em 30 dias com essas casa(s) e todos os vínculos delas, caso o cancelamento não seja desfeito.`;
  }, [ownedHouseholdCount]);

  return (
    <div className="space-y-4">
      <Card className="overflow-hidden">
        <div className="bg-[radial-gradient(circle_at_top_left,rgba(22,163,74,0.18),transparent_42%),linear-gradient(135deg,rgba(255,255,255,0.02),rgba(0,0,0,0))] p-6 sm:p-8">
          <div className="flex flex-col gap-5 lg:flex-row lg:items-end lg:justify-between">
            <div className="max-w-2xl">
              <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">Gestão pessoal</p>
              <h1 className="mt-2 text-3xl font-semibold text-foreground sm:text-4xl">Sua identidade no HomePit</h1>
              <p className="mt-3 text-sm leading-6 text-muted-foreground">
                A nova área de perfil já nasce preparada para futuras abas, com foco em foto, dados principais e
                controle completo do ciclo da conta.
              </p>
            </div>
            <div className="flex flex-wrap gap-2">
              <Badge variant="outline">{user.systemRole}</Badge>
              <Badge variant="neutral">{session.households.length} casa(s) vinculada(s)</Badge>
            </div>
          </div>
          <div className="mt-6 flex flex-wrap gap-2">
            <button
              type="button"
              className="rounded-full bg-primary px-4 py-2 text-sm font-semibold text-primary-foreground shadow-sm"
            >
              Perfil
            </button>
            <button
              type="button"
              className="rounded-full border border-border/70 px-4 py-2 text-sm font-semibold text-muted-foreground"
              disabled
            >
              Segurança
            </button>
            <button
              type="button"
              className="rounded-full border border-border/70 px-4 py-2 text-sm font-semibold text-muted-foreground"
              disabled
            >
              Preferências
            </button>
          </div>
        </div>
      </Card>

      <div className="grid gap-4 xl:grid-cols-[360px_minmax(0,1fr)]">
        <Card className="overflow-hidden">
          <CardContent className="space-y-5 p-6">
            <div className="rounded-[26px] border border-border/70 bg-surface-muted p-5">
              <div className="flex items-start justify-between gap-3">
                {previewUrl ? (
                  <AvatarCircle name={displayName || user.displayName} imageUrl={previewUrl} className="size-24 text-xl" />
                ) : (
                  <ProtectedUserAvatar user={user} token={token} householdId={householdId} className="size-24 text-xl" />
                )}
                <div className="rounded-full border border-border/70 bg-background p-2 text-muted-foreground">
                  <Camera className="size-4" />
                </div>
              </div>
              <div className="mt-4 space-y-1">
                <p className="text-xl font-semibold text-foreground">{user.displayName}</p>
                <p className="text-sm text-muted-foreground">{user.email}</p>
              </div>
            </div>

            <div className="space-y-3 rounded-[22px] border border-border/70 bg-surface-muted p-5">
              <div>
                <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">Resumo rápido</p>
                <p className="mt-2 text-sm leading-6 text-muted-foreground">
                  Sua foto é usada na colaboração da casa, comentários e módulos compartilhados.
                </p>
              </div>
              <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-1">
                <Metric label="Casas" value={session.households.length} />
                <Metric label="Próprias" value={ownedHouseholdCount} />
              </div>
            </div>
          </CardContent>
        </Card>

        <div className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Perfil</CardTitle>
              <CardDescription>Campos organizados para crescer junto com futuras abas desta área.</CardDescription>
            </CardHeader>
            <CardContent>
              <form className="grid gap-5" onSubmit={saveProfile}>
                <Field label="Foto de perfil" description={COMMON_IMAGE_HELP_TEXT}>
                  <Input type="file" accept={COMMON_IMAGE_ACCEPT} onChange={(event) => setProfilePhoto(event.target.files?.[0] ?? null)} />
                </Field>
                <div className="grid gap-5 lg:grid-cols-2">
                  <Field label="Nome" description="Nome exibido nos módulos e interações da casa.">
                    <Input value={displayName} onChange={(event) => setDisplayName(event.target.value)} required />
                  </Field>
                  <Field label="WhatsApp" description="Contato usado no contexto da sua conta.">
                    <Input value={phoneNumber} onChange={(event) => setPhoneNumber(event.target.value)} autoComplete="tel" />
                  </Field>
                </div>
                <div className="flex justify-end">
                  <Button type="submit" disabled={savingProfile}>
                    {savingProfile ? <Loader2 className="animate-spin" /> : <Sparkles />}
                    Salvar perfil
                  </Button>
                </div>
              </form>
            </CardContent>
          </Card>

          {user.systemRole !== "SuperAdmin" ? (
            <Card className="border-danger/30">
              <CardHeader>
                <div className="flex items-center gap-3">
                  <div className="rounded-[16px] bg-status-danger-soft p-3 text-danger">
                    <ShieldAlert className="size-5" />
                  </div>
                  <div>
                    <CardTitle>Cancelar conta</CardTitle>
                    <CardDescription>Área sensível para encerramento definitivo do acesso.</CardDescription>
                  </div>
                </div>
              </CardHeader>
              <CardContent className="space-y-4">
                <Notice tone={ownedHouseholdCount === 0 ? "warning" : "danger"}>{pendingCopy}</Notice>
                <div className="rounded-[18px] border border-border/70 bg-surface-muted p-4 text-sm leading-6 text-muted-foreground">
                  {ownedHouseholdCount === 0
                    ? "Sem casas próprias, a exclusão acontece na hora. Seus vínculos atuais serão removidos e um novo acesso no futuro exigirá nova conta."
                    : "Com casas próprias, o acesso é bloqueado imediatamente. A exclusão final apaga as casas que você criou e todos os vínculos delas após 30 dias."}
                </div>
                <Button variant="danger" onClick={() => setDangerDialogOpen(true)}>
                  <AlertTriangle />
                  {ownedHouseholdCount === 0 ? "Excluir conta agora" : "Desativar conta e agendar exclusão"}
                </Button>
              </CardContent>
            </Card>
          ) : (
            <Notice tone="warning">A conta do superadmin é protegida e não pode ser cancelada por esta interface.</Notice>
          )}
        </div>
      </div>

      <Dialog open={dangerDialogOpen} onOpenChange={setDangerDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{ownedHouseholdCount === 0 ? "Excluir conta" : "Desativar conta"}</DialogTitle>
            <DialogDescription>{pendingCopy}</DialogDescription>
          </DialogHeader>
          <div className="rounded-[18px] border border-border/70 bg-surface-muted p-4 text-sm leading-6 text-muted-foreground">
            {ownedHouseholdCount === 0
              ? "Ao confirmar, sua sessão será encerrada e o acesso não poderá ser recuperado por este mesmo cadastro."
              : "Ao confirmar, o próximo login mostrará o aviso de conta desativada com a data exata da exclusão programada."}
          </div>
          <DialogFooter>
            <Button variant="secondary" onClick={() => setDangerDialogOpen(false)} disabled={deletingAccount}>
              Voltar
            </Button>
            <Button variant="danger" onClick={() => void deleteAccount()} disabled={deletingAccount}>
              {deletingAccount ? <Loader2 className="animate-spin" /> : <AlertTriangle />}
              Confirmar
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function Field({
  label,
  description,
  children,
}: {
  label: string;
  description: string;
  children: React.ReactNode;
}) {
  return (
    <label className="grid gap-2">
      <span className="text-sm font-semibold text-foreground">{label}</span>
      {children}
      <span className="text-xs leading-5 text-muted-foreground">{description}</span>
    </label>
  );
}

function Metric({ label, value }: { label: string; value: number }) {
  return (
    <div className="rounded-[18px] border border-border/70 bg-background px-4 py-3">
      <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">{label}</p>
      <p className="mt-2 text-2xl font-semibold text-foreground">{value}</p>
    </div>
  );
}

function useObjectUrl(file: File | null) {
  const objectUrl = useMemo(() => (file ? URL.createObjectURL(file) : null), [file]);

  useEffect(() => {
    return () => {
      if (objectUrl) {
        URL.revokeObjectURL(objectUrl);
      }
    };
  }, [objectUrl]);

  return objectUrl;
}
