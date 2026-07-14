"use client";

import { AlertTriangle, Camera, Check, Loader2, Save, Settings2, Trash2, X } from "lucide-react";
import { type ChangeEvent, FormEvent, useEffect, useMemo, useRef, useState } from "react";
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
import { ProtectedUserAvatar } from "@/features/workspace/protected-user-avatar";
import { DeleteConfirmationDialog } from "@/features/workspace/delete-confirmation-dialog";
import {
  type AuthResponse,
  type CurrentUserPlanSummary,
  type DeleteOwnAccountResult,
  type PlanCreationItem,
  type PlanCreationScope,
  type PlanDefinition,
  type PublicPlatformSettings,
  apiFetch,
  clearSession,
  updateStoredSession,
} from "@/lib/api";
import { useProjectDashboard } from "@/features/projects/use-project-dashboard";
import { COMMON_IMAGE_ACCEPT } from "@/lib/image-upload";
import { ProfilePhotoCropDialog, type ProfilePhotoCropDraft } from "@/features/profile/profile-photo-crop-dialog";
import { cropProfilePhotoFile } from "@/features/profile/profile-photo-utils";
import { ConnectionTab } from "@/features/profile/connection-tab";
import { cn } from "@/lib/utils";

type ProfileTab = "profile" | "connection";

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
      subtitle="Atualize sua foto, seus dados e acompanhe os limites da conta"
      visibleCount={session.households.length}
      visibleLabel="casas"
      headerStats={[
        { label: "casas", value: session.households.length },
        { label: "próprias", value: session.households.filter((household) => household.isOwnedByCurrentUser).length },
      ]}
      requireHousehold={false}
    >
      <ProfilePanel key={session.user.id} dashboard={dashboard} />
    </HomePitWorkspaceShell>
  );
}

function ProfilePanel({ dashboard }: { dashboard: ReturnType<typeof useProjectDashboard> }) {
  const session = dashboard.session!;
  const user = session.user;
  const token = session.accessToken;
  const ownedHouseholdCount = session.households.filter((household) => household.isOwnedByCurrentUser).length;
  const profilePhotoInputRef = useRef<HTMLInputElement | null>(null);

  const [displayName, setDisplayName] = useState(user.displayName);
  const [phoneNumber, setPhoneNumber] = useState(user.phoneNumber ?? "");
  const [savingProfile, setSavingProfile] = useState(false);
  const [dangerDialogOpen, setDangerDialogOpen] = useState(false);
  const [cancelAccountDialogOpen, setCancelAccountDialogOpen] = useState(false);
  const [deletingAccount, setDeletingAccount] = useState(false);
  const [photoCropDraft, setPhotoCropDraft] = useState<ProfilePhotoCropDraft | null>(null);
  const [planSummary, setPlanSummary] = useState<CurrentUserPlanSummary | null>(null);
  const [planLoading, setPlanLoading] = useState(true);
  const [planCatalog, setPlanCatalog] = useState<PlanDefinition[]>([]);
  const [planCatalogLoading, setPlanCatalogLoading] = useState(true);
  const [publicPlatformSettings, setPublicPlatformSettings] = useState<PublicPlatformSettings | null>(null);
  const [subscriptionDialogOpen, setSubscriptionDialogOpen] = useState(false);
  const [creationScope, setCreationScope] = useState<PlanCreationScope | null>(null);
  const [creationItems, setCreationItems] = useState<PlanCreationItem[]>([]);
  const [creationLoading, setCreationLoading] = useState(false);
  const [deletingCreation, setDeletingCreation] = useState<PlanCreationItem | null>(null);
  const [activeTab, setActiveTab] = useState<ProfileTab>(getProfileTab);

  useEffect(() => {
    const handlePopState = () => setActiveTab(getProfileTab());
    window.addEventListener("popstate", handlePopState);
    return () => window.removeEventListener("popstate", handlePopState);
  }, []);

  function selectTab(tab: ProfileTab) {
    const url = new URL(window.location.href);
    url.searchParams.set("tab", tab);
    window.history.pushState({}, "", url);
    setActiveTab(tab);
  }

  async function saveProfile(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSavingProfile(true);

    try {
      const updatedUser = await apiFetch<AuthResponse["user"]>("/api/users/me", {
        method: "PUT",
        token,
        body: JSON.stringify({
          displayName,
          phoneNumber: phoneNumber || null,
        }),
      });
      applyUpdatedUser(updatedUser);
      toast.success("Perfil atualizado.");
    } catch (exception) {
      toast.error(exception instanceof Error ? exception.message : "Não foi possível atualizar o perfil.");
    } finally {
      setSavingProfile(false);
    }
  }

  function closeSubscriptionDialog() {
    setSubscriptionDialogOpen(false);
  }

  function openSubscriptionDialog() {
    setCancelAccountDialogOpen(false);
    setDangerDialogOpen(false);
    setSubscriptionDialogOpen(true);
  }

  function closeCancelAccountDialog() {
    setCancelAccountDialogOpen(false);
  }

  function openCancelAccountDialog() {
    setSubscriptionDialogOpen(false);
    setDangerDialogOpen(false);
    setCancelAccountDialogOpen(true);
  }

  function closeDangerDialog() {
    setDangerDialogOpen(false);
  }

  function openDangerDialog() {
    setSubscriptionDialogOpen(false);
    setCancelAccountDialogOpen(false);
    setDangerDialogOpen(true);
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
      closeDangerDialog();
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

  function openPhotoPicker() {
    profilePhotoInputRef.current?.click();
  }

  function handlePhotoSelection(event: ChangeEvent<HTMLInputElement>) {
    const file = event.currentTarget.files?.[0];
    event.currentTarget.value = "";

    if (!file) {
      return;
    }

    setPhotoCropDraft({
      file,
      previewUrl: URL.createObjectURL(file),
    });
  }

  const pendingCopy = useMemo(() => {
    if (ownedHouseholdCount === 0) {
      return "Se você excluir a conta agora, o acesso será encerrado imediatamente. Para voltar ao sistema no futuro, será necessário criar uma nova conta.";
    }

    return `Você é proprietário de ${ownedHouseholdCount} casa(s). Ao continuar, sua conta será desativada agora e apagada automaticamente em 30 dias com essas casa(s) e todos os vínculos delas, caso o cancelamento não seja desfeito.`;
  }, [ownedHouseholdCount]);

  useEffect(() => {
    return () => {
      if (photoCropDraft) {
        URL.revokeObjectURL(photoCropDraft.previewUrl);
      }
    };
  }, [photoCropDraft]);

  async function loadPlanSummary() {
    setPlanLoading(true);

    try {
      const nextSummary = await apiFetch<CurrentUserPlanSummary>("/api/users/me/plan", {
        token,
      });
      setPlanSummary(nextSummary);
    } catch (exception) {
      toast.error(exception instanceof Error ? exception.message : "Não foi possível carregar o plano.");
    } finally {
      setPlanLoading(false);
    }
  }

  async function openCreationModal(scope: PlanCreationScope) {
    setCreationScope(scope);
    setCreationLoading(true);

    try {
      const items = await apiFetch<PlanCreationItem[]>(`/api/users/me/plan/creations/${scope}`, {
        token,
      });
      setCreationItems(items);
    } catch (exception) {
      toast.error(exception instanceof Error ? exception.message : "Não foi possível carregar a listagem.");
      setCreationScope(null);
    } finally {
      setCreationLoading(false);
    }
  }

  async function deleteCreationItem() {
    if (!deletingCreation) {
      return;
    }

    try {
      if (creationScope === "households") {
        await apiFetch<void>(`/api/households/${deletingCreation.id}`, {
          method: "DELETE",
          token,
          householdId: deletingCreation.householdId,
        });
        await dashboard.refreshHouseholds();
      } else if (creationScope === "universes") {
        await apiFetch<void>(`/api/universes/${deletingCreation.id}`, {
          method: "DELETE",
          token,
          householdId: deletingCreation.householdId,
        });
        if (dashboard.activeHouseholdId === deletingCreation.householdId) {
          await dashboard.loadWorkspace();
        }
      } else if (creationScope === "projects") {
        await apiFetch<void>(`/api/projects/${deletingCreation.id}`, {
          method: "DELETE",
          token,
          householdId: deletingCreation.householdId,
        });
        if (dashboard.activeHouseholdId === deletingCreation.householdId) {
          await dashboard.loadWorkspace();
        }
      }

      setCreationItems((current) => current.filter((item) => item.id !== deletingCreation.id));
      await loadPlanSummary();
      toast.success(
        `${capitalize(getCreationScopeLabel(creationScope ?? "households", true))} ${getCreationPastParticiple(creationScope ?? "households")}.`,
      );
      setDeletingCreation(null);
    } catch (exception) {
      toast.error(exception instanceof Error ? exception.message : "Não foi possível excluir a criação.");
    }
  }

  useEffect(() => {
    let cancelled = false;

    const timer = window.setTimeout(() => {
      void (async () => {
        setPlanLoading(true);

        try {
          const nextSummary = await apiFetch<CurrentUserPlanSummary>("/api/users/me/plan", {
            token,
          });

          if (!cancelled) {
            setPlanSummary(nextSummary);
          }
        } catch (exception) {
          if (!cancelled) {
            toast.error(exception instanceof Error ? exception.message : "Não foi possível carregar o plano.");
          }
        } finally {
          if (!cancelled) {
            setPlanLoading(false);
          }
        }
      })();
    }, 0);

    return () => {
      cancelled = true;
      window.clearTimeout(timer);
    };
  }, [token]);

  useEffect(() => {
    let cancelled = false;

    const timer = window.setTimeout(() => {
      void (async () => {
        setPlanCatalogLoading(true);

        try {
          const nextPlans = await apiFetch<PlanDefinition[]>("/api/plans");

          if (!cancelled) {
            setPlanCatalog(nextPlans);
          }
        } catch (exception) {
          if (!cancelled) {
            toast.error(exception instanceof Error ? exception.message : "Não foi possível carregar os planos.");
          }
        } finally {
          if (!cancelled) {
            setPlanCatalogLoading(false);
          }
        }
      })();
    }, 0);

    return () => {
      cancelled = true;
      window.clearTimeout(timer);
    };
  }, []);

  useEffect(() => {
    let cancelled = false;

    const timer = window.setTimeout(() => {
      void (async () => {
        try {
          const nextSettings = await apiFetch<PublicPlatformSettings>("/api/platform-settings");

          if (!cancelled) {
            setPublicPlatformSettings(nextSettings);
          }
        } catch {
          if (!cancelled) {
            setPublicPlatformSettings(null);
          }
        }
      })();
    }, 0);

    return () => {
      cancelled = true;
      window.clearTimeout(timer);
    };
  }, []);

  const quotaCards = planSummary
    ? [
        {
          label: "Casas",
          current: planSummary.usage.ownedHouseholdCount,
          limit: planSummary.plan.maxOwnedHouseholds,
          scope: "households" as const,
        },
        {
          label: "Universos",
          current: planSummary.usage.universeCount,
          limit: planSummary.plan.maxUniverses,
          scope: "universes" as const,
        },
        {
          label: "Projetos",
          current: planSummary.usage.projectCount,
          limit: planSummary.plan.maxProjects,
          scope: "projects" as const,
        },
        {
          label: "Membros convidados",
          current: planSummary.usage.invitedMemberCount,
          limit: planSummary.plan.maxInvitedMembers ?? null,
          scope: null,
        },
        {
          label: "Imagens originais",
          current: planSummary.usage.managedOriginalImageCount,
          limit: planSummary.plan.maxOriginalImages,
          scope: null,
        },
      ]
    : [];
  const requestContact = resolveSubscriptionRequestContact(
    publicPlatformSettings,
    session.user.supportEmail ?? null,
  );

  return (
    <div className="space-y-4">
      <Card className="overflow-hidden">
        <div className="bg-[radial-gradient(circle_at_top_left,var(--highlight),transparent_42%),linear-gradient(135deg,rgba(255,255,255,0.03),rgba(0,0,0,0))] p-6 sm:p-8">
          <div className="flex flex-col gap-5 lg:flex-row lg:items-end lg:justify-between">
            <div className="max-w-2xl">
              <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">Gestão pessoal</p>
              <h1 className="mt-2 text-3xl font-semibold text-foreground sm:text-4xl">
                {activeTab === "profile" ? "Sua identidade no HomePit" : "Conecte suas ferramentas"}
              </h1>
              <p className="mt-3 text-sm leading-6 text-muted-foreground">
                {activeTab === "profile"
                  ? "Atualize sua foto, revise seus dados e acompanhe os limites da conta em um só lugar."
                  : "Crie chaves com o acesso necessário para usar suas automações com segurança."}
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
              className={cn(
                "rounded-full px-4 py-2 text-sm font-semibold transition",
                activeTab === "profile"
                  ? "bg-primary text-primary-foreground shadow-sm"
                  : "border border-border/70 text-muted-foreground hover:bg-surface-muted hover:text-foreground",
              )}
              onClick={() => selectTab("profile")}
              aria-current={activeTab === "profile" ? "page" : undefined}
            >
              Perfil
            </button>
            <button
              type="button"
              className={cn(
                "rounded-full px-4 py-2 text-sm font-semibold transition",
                activeTab === "connection"
                  ? "bg-primary text-primary-foreground shadow-sm"
                  : "border border-border/70 text-muted-foreground hover:bg-surface-muted hover:text-foreground",
              )}
              onClick={() => selectTab("connection")}
              aria-current={activeTab === "connection" ? "page" : undefined}
            >
              Conexão
            </button>
          </div>
        </div>
      </Card>

      {activeTab === "profile" ? (
        <>
      <div className="grid gap-4 xl:grid-cols-[360px_minmax(0,1fr)]">
        <Card className="overflow-hidden">
          <CardContent className="space-y-5 p-6">
            <div className="rounded-[26px] border border-border/70 bg-surface-muted p-5">
              <div className="flex items-start justify-between gap-4">
                <div className="space-y-3">
                  <div className="relative">
                    <ProtectedUserAvatar user={user} token={token} className="size-24 text-xl" />
                  </div>
                </div>
                <button
                  type="button"
                  className="absolute -bottom-1 -right-1 grid size-10 place-items-center rounded-full border border-border/70 bg-background text-muted-foreground shadow-sm transition hover:bg-surface-muted hover:text-foreground"
                  onClick={openPhotoPicker}
                  aria-label="Alterar foto de perfil"
                  title="Alterar foto de perfil"
                  ><Camera className="size-4" />
                </button>
              </div>
              <input
                ref={profilePhotoInputRef}
                className="hidden"
                type="file"
                accept={COMMON_IMAGE_ACCEPT}
                onChange={handlePhotoSelection}
              />
              <div className="mt-4 space-y-1">
                <p className="text-xl font-semibold text-foreground">{user.displayName}</p>
                <p className="text-sm text-muted-foreground">{user.email}</p>
              </div>
            </div>

            <div className="space-y-3 rounded-[22px] border border-border/70 bg-surface-muted p-5">
              <div>
                <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">Resumo rápido</p>
                <p className="mt-2 text-sm leading-6 text-muted-foreground">
                  Sua foto aparece nas interações da casa, nos comentários e nos módulos compartilhados.
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
              <CardDescription>Revise seu nome e seu contato principal sem complicação.</CardDescription>
            </CardHeader>
            <CardContent>
              <form className="grid gap-5" onSubmit={saveProfile}>
                <div className="grid gap-5 lg:grid-cols-2">
                  <Field label="Nome" description="Como você quer aparecer para as outras pessoas.">
                    <Input value={displayName} onChange={(event) => setDisplayName(event.target.value)} required />
                  </Field>
                  <Field label="WhatsApp" description="O contato que acompanha sua conta no HomePit.">
                    <Input value={phoneNumber} onChange={(event) => setPhoneNumber(event.target.value)} autoComplete="tel" />
                  </Field>
                </div>
                <div className="flex justify-end">
                  <Button type="submit" disabled={savingProfile}>
                    {savingProfile ? <Loader2 className="animate-spin" /> : <Save />}
                    Salvar perfil
                  </Button>
                </div>
              </form>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                <div>
                  <CardTitle>Plano</CardTitle>
                  <CardDescription>Veja o que sua conta pode usar e o que já está em uso.</CardDescription>
                </div>
                <div className="flex flex-wrap gap-2">
                  <Button type="button" variant="secondary" onClick={openSubscriptionDialog}>
                    <Settings2 />
                    Assinatura
                  </Button>
                  {user.systemRole !== "SuperAdmin" ? (
                    <Button type="button" variant="danger" onClick={openCancelAccountDialog}>
                      <Trash2 />
                      Excluir conta
                    </Button>
                  ) : null}
                </div>
              </div>
            </CardHeader>
            <CardContent className="space-y-4">
              {planLoading ? (
                <div className="flex items-center gap-2 text-sm text-muted-foreground">
                  <Loader2 className="size-4 animate-spin" />
                  Carregando plano...
                </div>
              ) : planSummary ? (
                <>
                  <div className="flex flex-wrap items-center gap-2">
                    <Badge variant="outline">{planSummary.plan.name}</Badge>
                    <Badge variant="neutral">
                      {planSummary.activeSubscription ? "Assinatura ativa" : "Plano padrão"}
                    </Badge>
                    {planSummary.activeSubscription ? (
                      <Badge variant="neutral">
                        {formatSubscriptionStatus(planSummary.activeSubscription.status)}
                      </Badge>
                    ) : null}
                  </div>
                  <p className="text-sm leading-6 text-muted-foreground">
                    {planSummary.activeSubscription
                      ? `Assinatura ${formatSubscriptionStatus(planSummary.activeSubscription.status)} de ${formatDateTime(planSummary.activeSubscription.startsAt)} até ${formatDateTime(planSummary.activeSubscription.endsAt)}.`
                      : "Você está usando o plano padrão porque não há assinatura ativa no momento."}
                  </p>
                  <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
                    {quotaCards.map((card) => (
                      <QuotaOverviewCard
                        key={card.label}
                        label={card.label}
                        current={card.current}
                        limit={card.limit}
                        clickable={card.scope !== null}
                        helperText={card.scope ? "Clique para ver as criações." : undefined}
                        onClick={card.scope ? () => void openCreationModal(card.scope) : undefined}
                      />
                    ))}
                  </div>
                  <div className="rounded-[18px] border border-border/70 bg-surface-muted p-4 text-sm leading-6 text-muted-foreground">
                    Seu plano define quantas criações totais sua conta pode manter e quantas imagens privadas seguem
                    em qualidade original. Ao atingir o limite, novas criações ficam bloqueadas até o uso voltar a
                    caber na cota.
                  </div>
                </>
              ) : (
                <Notice tone="warning">Não foi possível carregar os dados do plano.</Notice>
              )}
            </CardContent>
          </Card>
          {user.systemRole === "SuperAdmin" ? (
            <Notice tone="warning">A conta do superadmin é protegida e não pode ser cancelada por esta interface.</Notice>
          ) : null}
        </div>
      </div>

      <SubscriptionPlansDialog
        open={subscriptionDialogOpen}
        onOpenChange={(open) => {
          if (!open) {
            closeSubscriptionDialog();
          } else {
            openSubscriptionDialog();
          }
        }}
        planSummary={planSummary}
        planCatalog={planCatalog}
        planCatalogLoading={planCatalogLoading}
        requestContact={requestContact}
      />

      <Dialog
        open={cancelAccountDialogOpen}
        onOpenChange={(open) => {
          if (!open) {
            closeCancelAccountDialog();
          } else {
            openCancelAccountDialog();
          }
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{ownedHouseholdCount === 0 ? "Excluir conta" : "Cancelar conta"}</DialogTitle>
            <DialogDescription>{pendingCopy}</DialogDescription>
          </DialogHeader>
          <div className="rounded-[18px] border border-danger/20 bg-status-danger-soft p-4 text-sm leading-6 text-foreground">
            {ownedHouseholdCount === 0
              ? "Sem casas próprias, a exclusão acontece na hora. Seus vínculos atuais serão removidos e um novo acesso no futuro exigirá nova conta."
              : "Com casas próprias, o acesso é bloqueado imediatamente. A exclusão final apaga as casas que você criou e todos os vínculos delas após 30 dias."}
          </div>
          <DialogFooter>
            <Button variant="secondary" onClick={closeCancelAccountDialog}>
              Voltar
            </Button>
            <Button
              variant="danger"
              onClick={() => {
                closeCancelAccountDialog();
                openDangerDialog();
              }}
            >
              <AlertTriangle />
              {ownedHouseholdCount === 0 ? "Excluir conta" : "Desativar conta"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ProfilePhotoCropDialog
        key={photoCropDraft?.previewUrl ?? "profile-crop-closed"}
        draft={photoCropDraft}
        onCancel={() => setPhotoCropDraft(null)}
        onConfirm={async (crop) => {
          if (!photoCropDraft) {
            return;
          }

          try {
            const croppedFile = await cropProfilePhotoFile(photoCropDraft.file, crop);
            const formData = new FormData();
            formData.append("file", croppedFile);
            const updatedUser = await apiFetch<AuthResponse["user"]>("/api/users/me/profile-photo", {
              method: "POST",
              token,
              body: formData,
            });

            applyUpdatedUser(updatedUser);
            toast.success("Foto de perfil atualizada.");
            setPhotoCropDraft(null);
          } catch (exception) {
            toast.error(exception instanceof Error ? exception.message : "Não foi possível atualizar a foto de perfil.");
          }
        }}
      />

      <Dialog
        open={dangerDialogOpen}
        onOpenChange={(open) => {
          if (!open) {
            closeDangerDialog();
          } else {
            openDangerDialog();
          }
        }}
      >
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
            <Button variant="secondary" onClick={closeDangerDialog} disabled={deletingAccount}>
              Voltar
            </Button>
            <Button variant="danger" onClick={() => void deleteAccount()} disabled={deletingAccount}>
              {deletingAccount ? <Loader2 className="animate-spin" /> : <AlertTriangle />}
              Confirmar
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={creationScope !== null}
        onOpenChange={(open) => {
          if (!open) {
            setCreationScope(null);
            setCreationItems([]);
          }
        }}
      >
          <DialogContent className="w-[min(94vw,48rem)] max-h-[88vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>
              {creationScope
                ? `${capitalize(getCreationScopeLabel(creationScope))} ${getCreationPastParticiple(creationScope, true)} por você`
                : "Suas criações"}
            </DialogTitle>
            <DialogDescription>
              {creationScope
                ? "Revise a lista abaixo e exclua com segurança quando precisar liberar cota."
                : "Revise suas criações."}
            </DialogDescription>
          </DialogHeader>

          {creationLoading ? (
            <div className="flex items-center gap-2 text-sm text-muted-foreground">
              <Loader2 className="size-4 animate-spin" />
              Carregando listagem...
            </div>
          ) : creationItems.length === 0 ? (
            <Notice tone="warning">Nenhuma criação encontrada para este tipo.</Notice>
          ) : (
            <div className="space-y-3">
              {creationItems.map((item) => (
                <div key={item.id} className="rounded-[18px] border border-border/70 bg-surface-muted p-4">
                  <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                    <div className="space-y-1">
                      <p className="text-sm font-semibold text-foreground">{item.name}</p>
                      <p className="text-xs text-muted-foreground">{buildCreationContext(item, creationScope ?? "households")}</p>
                      <p className="text-xs text-muted-foreground">Criado em {formatDateTime(item.createdAt)}</p>
                    </div>
                    <Button
                      variant="danger"
                      size="sm"
                      onClick={() => setDeletingCreation(item)}
                      disabled={!item.canDelete}
                    >
                      <Trash2 />
                      Excluir
                    </Button>
                  </div>
                  {!item.canDelete ? (
                    <p className="mt-3 text-xs text-muted-foreground">
                      Esta criação continua no seu histórico, mas a exclusão depende de acesso ativo a essa casa.
                    </p>
                  ) : null}
                </div>
              ))}
            </div>
          )}
        </DialogContent>
      </Dialog>

      <DeleteConfirmationDialog
        open={Boolean(deletingCreation)}
        title={deletingCreation ? `Excluir ${getCreationDeleteLabel(creationScope ?? "households")}` : "Excluir criação"}
        description={
          deletingCreation
            ? `Essa ação remove ${getCreationDeleteLabel(creationScope ?? "households")} ${deletingCreation.name} e ajuda a liberar cota da sua conta.`
            : "Essa ação remove a criação selecionada."
        }
        confirmationTarget={deletingCreation?.name}
        confirmationLabel={`Digite o nome ${deletingCreation?.name ?? ""} para confirmar`}
        confirmLabel={`Excluir ${getCreationDeleteLabel(creationScope ?? "households")}`}
        impactItems={buildCreationImpactItems(creationScope ?? "households")}
        onOpenChange={(open) => {
          if (!open) {
            setDeletingCreation(null);
          }
        }}
        onConfirm={deleteCreationItem}
      />
        </>
      ) : (
        <ConnectionTab token={token} households={session.households} />
      )}
    </div>
  );
}

function getProfileTab(): ProfileTab {
  if (typeof window === "undefined") {
    return "profile";
  }

  return new URLSearchParams(window.location.search).get("tab") === "connection" ? "connection" : "profile";
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

function Metric({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="rounded-[18px] border border-border/70 bg-background px-4 py-3">
      <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">{label}</p>
      <p className="mt-2 text-2xl font-semibold text-foreground">{value}</p>
    </div>
  );
}

function QuotaOverviewCard({
  label,
  current,
  limit,
  clickable,
  helperText,
  onClick,
}: {
  label: string;
  current: number;
  limit: number | null;
  clickable?: boolean;
  helperText?: string;
  onClick?: () => void;
}) {
  const isOverLimit = limit !== null && current > limit;
  const remaining = limit === null ? null : Math.max(limit - current, 0);
  const content = (
    <>
      <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">{label}</p>
      <p className={cn("mt-2 text-2xl font-semibold", isOverLimit ? "text-danger" : "text-foreground")}>{current} usados</p>
      <p className={cn("mt-2 text-xs leading-5", isOverLimit ? "text-danger" : "text-muted-foreground")}>
        {remaining === null ? "Restante ilimitado" : `${remaining} restante(s)`}
      </p>
      {helperText ? <p className="mt-2 text-xs leading-5 text-muted-foreground">{helperText}</p> : null}
    </>
  );

  if (clickable && onClick) {
    return (
      <button
        type="button"
        onClick={onClick}
        className={cn(
          "rounded-[18px] border px-4 py-3 text-left transition hover:border-primary/40 hover:bg-surface-muted",
          isOverLimit ? "border-danger/30 bg-status-danger-soft" : "border-border/70 bg-background",
        )}
      >
        {content}
      </button>
    );
  }

  return (
    <div
      className={cn(
        "rounded-[18px] border px-4 py-3 transition",
        isOverLimit ? "border-danger/30 bg-status-danger-soft" : "border-border/70 bg-background",
      )}
    >
      {content}
    </div>
  );
}

function SubscriptionPlansDialog({
  open,
  onOpenChange,
  planSummary,
  planCatalog,
  planCatalogLoading,
  requestContact,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  planSummary: CurrentUserPlanSummary | null;
  planCatalog: PlanDefinition[];
  planCatalogLoading: boolean;
  requestContact: ReturnType<typeof resolveSubscriptionRequestContact>;
}) {
  const currentPlanId = planSummary?.plan.id ?? null;
  const activeSubscription = planSummary?.activeSubscription ?? null;
  const planCatalogWithCurrent = currentPlanId
    ? planCatalog.some((plan) => plan.id === currentPlanId)
      ? planCatalog
      : [planSummary?.plan ?? null, ...planCatalog].filter((plan): plan is PlanDefinition => plan !== null)
    : planCatalog;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="w-[min(96vw,82rem)] max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Assinatura</DialogTitle>
          <DialogDescription>Veja os planos disponíveis e a assinatura atual da conta.</DialogDescription>
        </DialogHeader>

        {planSummary ? (
          <div className="rounded-[22px] border border-border/70 bg-surface-muted p-5">
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div className="space-y-2">
                <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">Plano em uso</p>
                <p className="text-2xl font-semibold text-foreground">{planSummary.plan.name}</p>
                <p className="text-sm leading-6 text-muted-foreground">
                  {activeSubscription
                    ? `Assinatura ${formatSubscriptionStatus(activeSubscription.status)} ${formatBillingCycle(activeSubscription.billingCycle).toLowerCase()} de ${formatDateTime(activeSubscription.startsAt)} até ${formatDateTime(activeSubscription.endsAt)}.`
                    : "Você está no plano padrão no momento."}
                </p>
              </div>
              <div className="flex flex-wrap justify-end gap-2">
                <Badge variant="neutral">{requestContact.label}</Badge>
                <Badge variant="neutral">Plano atual</Badge>
                {planSummary.plan.isPopular ? <Badge variant="default">Popular</Badge> : null}
              </div>
            </div>
          </div>
        ) : null}

        {planCatalogLoading ? (
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <Loader2 className="size-4 animate-spin" />
            Carregando planos...
          </div>
        ) : planCatalogWithCurrent.length === 0 ? (
          <Notice tone="warning">Nenhum plano público foi encontrado para solicitar no momento.</Notice>
        ) : (
          <div className="grid gap-4 lg:grid-cols-2 xl:grid-cols-3">
            {planCatalogWithCurrent.map((plan) => (
              <PlanRequestCard
                key={plan.id}
                plan={plan}
                requestHref={buildSubscriptionRequestLink(plan, requestContact)}
                requestLabel="Solicitar"
                isCurrent={currentPlanId === plan.id}
              />
            ))}
          </div>
        )}

        <DialogFooter>
          <Button variant="secondary" onClick={() => onOpenChange(false)}>
            Fechar
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function PlanRequestCard({
  plan,
  requestHref,
  requestLabel,
  isCurrent,
}: {
  plan: PlanDefinition;
  requestHref: string;
  requestLabel: string;
  isCurrent: boolean;
}) {
  const features = buildPlanFeatureHighlights(plan);

  return (
    <div
      className={cn(
        "flex h-full flex-col rounded-[24px] border p-5 shadow-sm transition",
        isCurrent ? "border-primary/50 bg-highlight/35" : "border-border/70 bg-surface-muted",
        plan.isPopular && !isCurrent ? "border-primary/45 bg-surface-muted" : "",
      )}
    >
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <p className="text-sm font-semibold text-foreground">{plan.name}</p>
          <p className="mt-1 text-xs text-muted-foreground">{plan.slug}</p>
        </div>
        <div className="flex flex-wrap justify-end gap-2">
          {plan.isPopular ? <Badge variant="default">Popular</Badge> : null}
          {isCurrent ? <Badge variant="neutral">Atual</Badge> : null}
        </div>
      </div>
      <div className="mt-4">
        <p className="text-3xl font-semibold text-foreground">
          {formatCurrency(plan.monthlyPrice, plan.currencyCode)}
          <span className="ml-2 text-sm font-medium text-muted-foreground">/mês</span>
        </p>
        <p className="mt-1 text-sm text-muted-foreground">{formatCurrency(plan.annualPrice, plan.currencyCode)}/ano</p>
      </div>
      <div className="mt-5 space-y-2">
        {features.map((feature) => (
          <div key={feature.text} className="flex items-center gap-2 text-sm text-foreground">
            <span
              className={cn(
                "grid size-5 shrink-0 place-items-center rounded-full",
                feature.enabled ? "bg-status-success-soft text-success" : "bg-status-danger-soft text-danger",
              )}
            >
              {feature.enabled ? <Check className="size-3.5" /> : <X className="size-3.5" />}
            </span>
            <span>{feature.text}</span>
          </div>
        ))}
      </div>
      <p className="mt-5 text-sm leading-6 text-muted-foreground">{plan.imagePolicyDescription}</p>
      <div className="mt-5">
        {isCurrent ? (
          <Button variant="secondary" className="w-full" disabled>
            Plano atual
          </Button>
        ) : (
          <Button asChild className="w-full">
            <a href={requestHref}>{requestLabel}</a>
          </Button>
        )}
      </div>
    </div>
  );
}

function getCreationScopeLabel(scope: PlanCreationScope, singular = false) {
  switch (scope) {
    case "households":
      return singular ? "casa" : "casas";
    case "universes":
      return singular ? "universo" : "universos";
    case "projects":
      return singular ? "projeto" : "projetos";
    default:
      return scope;
  }
}

function getCreationDeleteLabel(scope: PlanCreationScope) {
  return getCreationScopeLabel(scope, true);
}

function getCreationPastParticiple(scope: PlanCreationScope, plural = false) {
  if (scope === "households") {
    return plural ? "criadas" : "excluída";
  }

  return plural ? "criados" : "excluído";
}

function buildCreationContext(item: PlanCreationItem, scope: PlanCreationScope) {
  if (scope === "projects") {
    return `Casa: ${item.householdName} • Universo: ${item.universeName ?? "Sem universo"}`;
  }

  if (scope === "universes") {
    return `Casa: ${item.householdName}`;
  }

  return `Casa: ${item.householdName}`;
}

function buildCreationImpactItems(scope: PlanCreationScope) {
  switch (scope) {
    case "households":
      return [
        "A casa, seus universos, projetos, atividades, prompts e membros vinculados.",
        "Comentários, preferências e histórico operacional ligados a essa casa.",
        "Parte da cota total usada por essa criação.",
      ];
    case "universes":
      return [
        "O universo e os projetos, atividades e pendências vinculados a ele.",
        "Associações do banco de prompts com esse universo.",
        "Parte da cota total usada por essa criação.",
      ];
    case "projects":
      return [
        "O projeto e as atividades, comentários e pendências vinculados a ele.",
        "Referências desse projeto em áreas relacionadas da casa.",
        "Parte da cota total usada por essa criação.",
      ];
    default:
      return ["Parte da cota total usada por essa criação."];
  }
}

function capitalize(value: string) {
  return value.charAt(0).toUpperCase() + value.slice(1);
}

function formatCurrency(value: number, currencyCode: string) {
  return new Intl.NumberFormat("pt-BR", {
    style: "currency",
    currency: currencyCode,
  }).format(value);
}

function formatDateTime(value: string) {
  return new Intl.DateTimeFormat("pt-BR", {
    dateStyle: "short",
    timeStyle: "short",
  }).format(new Date(value));
}

function formatSubscriptionStatus(value: "Scheduled" | "Active" | "Expired" | "Cancelled") {
  switch (value) {
    case "Active":
      return "ativa";
    case "Scheduled":
      return "agendada";
    case "Expired":
      return "expirada";
    case "Cancelled":
      return "cancelada";
    default:
      return value;
  }
}

function formatBillingCycle(value: "Monthly" | "Annual" | "Custom") {
  switch (value) {
    case "Monthly":
      return "Mensal";
    case "Annual":
      return "Anual";
    case "Custom":
      return "Personalizado";
    default:
      return value;
  }
}

function resolveSubscriptionRequestContact(
  platformSettings: PublicPlatformSettings | null,
  supportEmail: string | null,
) {
  const contactPhone = normalizePhoneNumber(platformSettings?.contactPhone ?? "");
  if (contactPhone) {
    return {
      kind: "whatsapp" as const,
      label: "WhatsApp",
      destination: contactPhone,
    };
  }

  const contactEmail = normalizeContactEmail(platformSettings?.contactEmail ?? "") ?? normalizeContactEmail(supportEmail ?? "");
  if (contactEmail) {
    return {
      kind: "email" as const,
      label: "E-mail",
      destination: contactEmail,
    };
  }

  return {
    kind: "email" as const,
    label: "E-mail",
    destination: supportEmail ?? "",
  };
}

function buildSubscriptionRequestLink(
  plan: PlanDefinition,
  contact: ReturnType<typeof resolveSubscriptionRequestContact>,
) {
  const message = buildSubscriptionRequestMessage(plan);

  if (contact.kind === "whatsapp") {
    return `https://wa.me/${contact.destination}?text=${encodeURIComponent(message)}`;
  }

  return `mailto:${contact.destination}?subject=${encodeURIComponent(`Interesse no plano ${plan.name} - HomePit`)}&body=${encodeURIComponent(message)}`;
}

function buildSubscriptionRequestMessage(plan: PlanDefinition) {
  return [
    `Olá! Tenho interesse no plano ${plan.name} do HomePit.`,
    "",
    "Valores:",
    `- Mensal: ${formatCurrency(plan.monthlyPrice, plan.currencyCode)}`,
    `- Anual: ${formatCurrency(plan.annualPrice, plan.currencyCode)}`,
    "",
    "Limites:",
    `- Casas: ${plan.maxOwnedHouseholds}`,
    `- Universos: ${plan.maxUniverses}`,
    `- Projetos: ${plan.maxProjects}`,
    `- Membros convidados: ${plan.maxInvitedMembers ?? "ilimitados"}`,
    `- Imagens originais: ${plan.maxOriginalImages}`,
    "",
    "Gostaria de receber as próximas orientações para contratar esse plano.",
  ].join("\n");
}

function buildPlanFeatureHighlights(plan: PlanDefinition) {
  return [
    {
      text: formatPlanQuotaCount(plan.maxOwnedHouseholds, "Casa", "Casas"),
      enabled: plan.maxOwnedHouseholds > 0,
    },
    {
      text: formatPlanQuotaCount(plan.maxUniverses, "Universo", "Universos"),
      enabled: plan.maxUniverses > 0,
    },
    {
      text: formatPlanQuotaCount(plan.maxProjects, "Projeto", "Projetos"),
      enabled: plan.maxProjects > 0,
    },
    {
      text:
        plan.maxInvitedMembers == null
          ? "Membros convidados ilimitados"
          : formatPlanQuotaCount(plan.maxInvitedMembers, "Membro convidado", "Membros convidados"),
      enabled: plan.maxInvitedMembers == null || plan.maxInvitedMembers > 0,
    },
    {
      text: formatPlanQuotaCount(plan.maxOriginalImages, "Imagem original", "Imagens originais"),
      enabled: plan.maxOriginalImages > 0,
    },
  ];
}

function formatPlanQuotaCount(value: number, singular: string, plural: string) {
  return `${value} ${value === 1 ? singular : plural}`;
}

function normalizePhoneNumber(value: string) {
  const digits = value.replace(/\D/g, "");
  return digits.length > 0 ? digits : "";
}

function normalizeContactEmail(value: string) {
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
}
