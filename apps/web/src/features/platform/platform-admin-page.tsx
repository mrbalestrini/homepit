"use client";

import Link from "next/link";
import {
  Loader2,
  LogOut,
  Pencil,
  ShieldOff,
  Sparkles,
  Trash2,
  UserCheck,
} from "lucide-react";
import { useCallback, useEffect, useMemo, useState } from "react";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Textarea } from "@/components/ui/textarea";
import {
  type AdminUserListItem,
  type BillingCycle,
  type PlanDefinition,
  type UserSubscription,
  type UserSubscriptionStatus,
  apiFetch,
  clearSession,
} from "@/lib/api";
import { AccountStateGate } from "@/features/workspace/account-state-gate";
import { DeleteConfirmationDialog } from "@/features/workspace/delete-confirmation-dialog";
import { HomePitAuth } from "@/features/workspace/homepit-auth";
import { HomePitWorkspaceShell, Notice } from "@/features/workspace/homepit-workspace-shell";
import { useProjectDashboard } from "@/features/projects/use-project-dashboard";

type PlatformTab = "users" | "plans" | "subscriptions";
type UserFilter = "all" | "Active" | "PendingSelfDeletion" | "DisabledBySuperAdmin";

type SubscriptionFormState = {
  userId: string;
  planDefinitionId: string;
  billingCycle: BillingCycle;
  startsAt: string;
  endsAt: string;
  amountPaid: string;
  currencyCode: string;
  status: UserSubscriptionStatus;
  adminNote: string;
};

const defaultSubscriptionForm: SubscriptionFormState = {
  userId: "",
  planDefinitionId: "",
  billingCycle: "Monthly",
  startsAt: "",
  endsAt: "",
  amountPaid: "0.00",
  currencyCode: "BRL",
  status: "Active",
  adminNote: "",
};

export function PlatformAdminPage() {
  const dashboard = useProjectDashboard();

  if (!dashboard.session) {
    return <HomePitAuth onAuthenticated={dashboard.handleAuthenticated} />;
  }

  return (
    <AccountStateGate session={dashboard.session}>
      <PlatformWorkspace dashboard={dashboard} />
    </AccountStateGate>
  );
}

function PlatformWorkspace({ dashboard }: { dashboard: ReturnType<typeof useProjectDashboard> }) {
  const session = dashboard.session!;

  if (session.user.systemRole !== "SuperAdmin") {
    return <AdminAccessDenied />;
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
      activeModule="platform"
      subtitle="Usuários, planos e assinaturas em um hub global do SuperAdmin"
      visibleCount={session.households.length}
      visibleLabel="casas visíveis"
      headerStats={[{ label: "casas", value: session.households.length }]}
      requireHousehold={false}
    >
      <PlatformAdminPanel token={session.accessToken} />
    </HomePitWorkspaceShell>
  );
}

function PlatformAdminPanel({ token }: { token: string }) {
  const [activeTab, setActiveTab] = useState<PlatformTab>("users");
  const [users, setUsers] = useState<AdminUserListItem[]>([]);
  const [plans, setPlans] = useState<PlanDefinition[]>([]);
  const [subscriptions, setSubscriptions] = useState<UserSubscription[]>([]);
  const [loading, setLoading] = useState(true);
  const [savingPlanId, setSavingPlanId] = useState<string | null>(null);
  const [busyUserId, setBusyUserId] = useState<string | null>(null);
  const [userFilter, setUserFilter] = useState<UserFilter>("all");
  const [userToDelete, setUserToDelete] = useState<AdminUserListItem | null>(null);
  const [editingSubscriptionId, setEditingSubscriptionId] = useState<string | null>(null);
  const [savingSubscription, setSavingSubscription] = useState(false);
  const [subscriptionForm, setSubscriptionForm] = useState<SubscriptionFormState>(defaultSubscriptionForm);
  const [planDrafts, setPlanDrafts] = useState<Record<string, PlanDefinition>>({});

  const loadData = useCallback(async () => {
    setLoading(true);

    try {
      const [nextUsers, nextPlans, nextSubscriptions] = await Promise.all([
        apiFetch<AdminUserListItem[]>("/api/admin/users", { token }),
        apiFetch<PlanDefinition[]>("/api/admin/platform/plans", { token }),
        apiFetch<UserSubscription[]>("/api/admin/platform/subscriptions", { token }),
      ]);

      setUsers(nextUsers);
      setPlans(nextPlans);
      setSubscriptions(nextSubscriptions);
      setPlanDrafts(Object.fromEntries(nextPlans.map((plan) => [plan.id, plan])));
    } catch (exception) {
      toast.error(exception instanceof Error ? exception.message : "Não foi possível carregar a plataforma.");
    } finally {
      setLoading(false);
    }
  }, [token]);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadData();
    }, 0);

    return () => window.clearTimeout(timer);
  }, [loadData]);

  const filteredUsers = useMemo(
    () => (userFilter === "all" ? users : users.filter((user) => user.accountState === userFilter)),
    [userFilter, users],
  );

  const userSummary = useMemo(
    () => ({
      total: users.length,
      active: users.filter((user) => user.accountState === "Active").length,
      pending: users.filter((user) => user.accountState === "PendingSelfDeletion").length,
      disabled: users.filter((user) => user.accountState === "DisabledBySuperAdmin").length,
    }),
    [users],
  );

  function updatePlanDraft(planId: string, field: keyof PlanDefinition, value: string) {
    setPlanDrafts((current) => ({
      ...current,
      [planId]: {
        ...current[planId],
        [field]: Number(value),
      },
    }));
  }

  async function savePlan(planId: string) {
    const draft = planDrafts[planId];
    if (!draft) {
      return;
    }

    setSavingPlanId(planId);

    try {
      const updated = await apiFetch<PlanDefinition>(`/api/admin/platform/plans/${planId}`, {
        method: "PUT",
        token,
        body: JSON.stringify({
          monthlyPrice: draft.monthlyPrice,
          annualPrice: draft.annualPrice,
          maxOwnedHouseholds: draft.maxOwnedHouseholds,
          maxUniversesPerHousehold: draft.maxUniversesPerHousehold,
          maxProjectsPerUniverse: draft.maxProjectsPerUniverse,
          maxOriginalImages: draft.maxOriginalImages,
        }),
      });

      setPlans((current) => current.map((plan) => (plan.id === updated.id ? updated : plan)));
      setPlanDrafts((current) => ({ ...current, [updated.id]: updated }));
      toast.success(`Plano ${updated.name} atualizado.`);
    } catch (exception) {
      toast.error(exception instanceof Error ? exception.message : "Não foi possível salvar o plano.");
    } finally {
      setSavingPlanId(null);
    }
  }

  async function deactivateUser(userId: string) {
    setBusyUserId(userId);

    try {
      const updated = await apiFetch<AdminUserListItem>(`/api/admin/users/${userId}/deactivate`, {
        method: "POST",
        token,
      });
      setUsers((current) => current.map((user) => (user.id === updated.id ? updated : user)));
      toast.success("Usuário desativado.");
    } catch (exception) {
      toast.error(exception instanceof Error ? exception.message : "Não foi possível desativar o usuário.");
    } finally {
      setBusyUserId(null);
    }
  }

  async function reactivateUser(userId: string) {
    setBusyUserId(userId);

    try {
      const updated = await apiFetch<AdminUserListItem>(`/api/admin/users/${userId}/reactivate`, {
        method: "POST",
        token,
      });
      setUsers((current) => current.map((user) => (user.id === updated.id ? updated : user)));
      toast.success("Usuário reativado.");
    } catch (exception) {
      toast.error(exception instanceof Error ? exception.message : "Não foi possível reativar o usuário.");
    } finally {
      setBusyUserId(null);
    }
  }

  async function deleteUser(userId: string) {
    setBusyUserId(userId);

    try {
      await apiFetch<void>(`/api/admin/users/${userId}`, {
        method: "DELETE",
        token,
      });
      setUsers((current) => current.filter((user) => user.id !== userId));
      setSubscriptions((current) => current.filter((subscription) => subscription.userId !== userId));
      setUserToDelete(null);
      toast.success("Usuário excluído definitivamente.");
    } catch (exception) {
      toast.error(exception instanceof Error ? exception.message : "Não foi possível excluir o usuário.");
    } finally {
      setBusyUserId(null);
    }
  }

  function beginCreateSubscription() {
    setEditingSubscriptionId(null);
    setSubscriptionForm({
      ...defaultSubscriptionForm,
      userId: users.find((user) => !user.isProtected)?.id ?? "",
      planDefinitionId: plans[0]?.id ?? "",
      startsAt: toLocalInputValue(new Date()),
      endsAt: toLocalInputValue(new Date(Date.now() + 30 * 24 * 60 * 60 * 1000)),
    });
  }

  function beginEditSubscription(subscription: UserSubscription) {
    setEditingSubscriptionId(subscription.id);
    setSubscriptionForm({
      userId: subscription.userId,
      planDefinitionId: subscription.planDefinitionId,
      billingCycle: subscription.billingCycle,
      startsAt: toLocalInputValue(subscription.startsAt),
      endsAt: toLocalInputValue(subscription.endsAt),
      amountPaid: subscription.amountPaid.toFixed(2),
      currencyCode: subscription.currencyCode,
      status: subscription.status,
      adminNote: subscription.adminNote ?? "",
    });
    setActiveTab("subscriptions");
  }

  function updateSubscriptionField(field: keyof SubscriptionFormState, value: string) {
    setSubscriptionForm((current) => ({ ...current, [field]: value }));
  }

  async function saveSubscription() {
    setSavingSubscription(true);

    try {
      const payload = {
        userId: subscriptionForm.userId,
        planDefinitionId: subscriptionForm.planDefinitionId,
        billingCycle: subscriptionForm.billingCycle,
        startsAt: fromLocalInputValue(subscriptionForm.startsAt),
        endsAt: fromLocalInputValue(subscriptionForm.endsAt),
        amountPaid: Number(subscriptionForm.amountPaid),
        currencyCode: subscriptionForm.currencyCode,
        status: subscriptionForm.status,
        adminNote: subscriptionForm.adminNote || null,
      };

      const path = editingSubscriptionId
        ? `/api/admin/platform/subscriptions/${editingSubscriptionId}`
        : "/api/admin/platform/subscriptions";
      const method = editingSubscriptionId ? "PUT" : "POST";

      const saved = await apiFetch<UserSubscription>(path, {
        method,
        token,
        body: JSON.stringify(payload),
      });

      setSubscriptions((current) =>
        editingSubscriptionId
          ? current.map((subscription) => (subscription.id === saved.id ? saved : subscription))
          : [saved, ...current],
      );
      setUsers(await apiFetch<AdminUserListItem[]>("/api/admin/users", { token }));
      setEditingSubscriptionId(null);
      beginCreateSubscription();
      toast.success(editingSubscriptionId ? "Assinatura atualizada." : "Assinatura criada.");
    } catch (exception) {
      toast.error(exception instanceof Error ? exception.message : "Não foi possível salvar a assinatura.");
    } finally {
      setSavingSubscription(false);
    }
  }

  useEffect(() => {
    if (plans.length > 0 && users.length > 0 && !subscriptionForm.userId && !editingSubscriptionId) {
      beginCreateSubscription();
    }
  }, [editingSubscriptionId, plans, subscriptionForm.userId, users]);

  return (
    <div className="space-y-4">
      <Card>
        <CardContent className="flex flex-col gap-4 p-5 sm:p-6 lg:flex-row lg:items-end lg:justify-between">
          <div className="max-w-3xl">
            <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">SuperAdmin</p>
            <h1 className="mt-2 text-3xl font-semibold text-foreground">Plataforma HomePit</h1>
            <p className="mt-2 text-sm leading-6 text-muted-foreground">
              Centralize a gestão global de contas, catálogo comercial e histórico manual de assinaturas com uma visão
              única da plataforma.
            </p>
          </div>
          <div className="flex flex-wrap gap-2" role="tablist" aria-label="Abas da plataforma">
            <TabButton tab="users" activeTab={activeTab} onSelect={setActiveTab} label="Usuários" />
            <TabButton tab="plans" activeTab={activeTab} onSelect={setActiveTab} label="Planos" />
            <TabButton tab="subscriptions" activeTab={activeTab} onSelect={setActiveTab} label="Assinaturas" />
          </div>
        </CardContent>
      </Card>

      {loading ? (
        <Card>
          <CardContent className="grid min-h-[240px] place-items-center text-muted-foreground">
            <div className="flex items-center gap-2 text-sm">
              <Loader2 className="size-4 animate-spin" />
              Carregando dados da plataforma...
            </div>
          </CardContent>
        </Card>
      ) : null}

      {!loading && activeTab === "users" ? (
        <div className="space-y-4" role="tabpanel" aria-label="Usuários">
          <div className="grid gap-3 md:grid-cols-4">
            <MetricCard label="Total" value={userSummary.total} />
            <MetricCard label="Ativos" value={userSummary.active} />
            <MetricCard label="Pendentes" value={userSummary.pending} />
            <MetricCard label="Desativados" value={userSummary.disabled} />
          </div>

          <Notice tone="warning">
            O superadmin protegido segue somente leitura. Excluir um proprietário apaga suas casas próprias e os dados
            vinculados a elas.
          </Notice>

          <Card>
            <CardHeader className="gap-3 lg:flex-row lg:items-end lg:justify-between">
              <div>
                <CardTitle>Contas da plataforma</CardTitle>
                <CardDescription>Estado atual das contas e resumo comercial de cada usuário.</CardDescription>
              </div>
              <div className="w-full max-w-xs">
                <Select value={userFilter} onChange={(event) => setUserFilter(event.target.value as UserFilter)}>
                  <option value="all">Todos os estados</option>
                  <option value="Active">Ativos</option>
                  <option value="PendingSelfDeletion">Cancelamento pendente</option>
                  <option value="DisabledBySuperAdmin">Desativados pelo superadmin</option>
                </Select>
              </div>
            </CardHeader>
            <CardContent className="space-y-3">
              {filteredUsers.length === 0 ? (
                <Notice tone="warning">Nenhum usuário encontrado para o filtro atual.</Notice>
              ) : (
                filteredUsers.map((user) => (
                  <UserRow
                    key={user.id}
                    user={user}
                    busy={busyUserId === user.id}
                    onDeactivate={deactivateUser}
                    onReactivate={reactivateUser}
                    onDelete={setUserToDelete}
                  />
                ))
              )}
            </CardContent>
          </Card>
        </div>
      ) : null}

      {!loading && activeTab === "plans" ? (
        <div className="grid gap-4 xl:grid-cols-2" role="tabpanel" aria-label="Planos">
          {plans.map((plan) => {
            const draft = planDrafts[plan.id] ?? plan;
            return (
              <Card key={plan.id}>
                <CardHeader>
                  <div className="flex items-center justify-between gap-3">
                    <div>
                      <CardTitle>{plan.name}</CardTitle>
                      <CardDescription>Slug fixo: {plan.slug}</CardDescription>
                    </div>
                    <Badge variant="outline">{plan.currencyCode}</Badge>
                  </div>
                </CardHeader>
                <CardContent className="space-y-4">
                  <div className="grid gap-4 sm:grid-cols-2">
                    <Field label="Preço mensal">
                      <Input
                        type="number"
                        step="0.01"
                        value={draft.monthlyPrice}
                        onChange={(event) => updatePlanDraft(plan.id, "monthlyPrice", event.target.value)}
                      />
                    </Field>
                    <Field label="Preço anual">
                      <Input
                        type="number"
                        step="0.01"
                        value={draft.annualPrice}
                        onChange={(event) => updatePlanDraft(plan.id, "annualPrice", event.target.value)}
                      />
                    </Field>
                    <Field label="Casas próprias">
                      <Input
                        type="number"
                        value={draft.maxOwnedHouseholds}
                        onChange={(event) => updatePlanDraft(plan.id, "maxOwnedHouseholds", event.target.value)}
                      />
                    </Field>
                    <Field label="Universos por casa">
                      <Input
                        type="number"
                        value={draft.maxUniversesPerHousehold}
                        onChange={(event) => updatePlanDraft(plan.id, "maxUniversesPerHousehold", event.target.value)}
                      />
                    </Field>
                    <Field label="Projetos por universo">
                      <Input
                        type="number"
                        value={draft.maxProjectsPerUniverse}
                        onChange={(event) => updatePlanDraft(plan.id, "maxProjectsPerUniverse", event.target.value)}
                      />
                    </Field>
                    <Field label="Imagens originais">
                      <Input
                        type="number"
                        value={draft.maxOriginalImages}
                        onChange={(event) => updatePlanDraft(plan.id, "maxOriginalImages", event.target.value)}
                      />
                    </Field>
                  </div>
                  <div className="rounded-[18px] border border-border/70 bg-surface-muted p-4 text-sm leading-6 text-muted-foreground">
                    {draft.imagePolicyDescription}
                  </div>
                  <div className="flex justify-end">
                    <Button onClick={() => void savePlan(plan.id)} disabled={savingPlanId === plan.id}>
                      {savingPlanId === plan.id ? <Loader2 className="animate-spin" /> : <Sparkles />}
                      Salvar plano
                    </Button>
                  </div>
                </CardContent>
              </Card>
            );
          })}
        </div>
      ) : null}

      {!loading && activeTab === "subscriptions" ? (
        <div className="grid gap-4 xl:grid-cols-[380px_minmax(0,1fr)]" role="tabpanel" aria-label="Assinaturas">
          <Card>
            <CardHeader>
              <CardTitle>{editingSubscriptionId ? "Editar assinatura" : "Nova assinatura"}</CardTitle>
              <CardDescription>Assinaturas manuais suportam valor pago zero para testes, vouchers e descontos.</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <Field label="Cliente">
                <Select value={subscriptionForm.userId} onChange={(event) => updateSubscriptionField("userId", event.target.value)}>
                  {users.filter((user) => !user.isProtected).map((user) => (
                    <option key={user.id} value={user.id}>
                      {user.displayName} ({user.email})
                    </option>
                  ))}
                </Select>
              </Field>
              <Field label="Plano">
                <Select
                  value={subscriptionForm.planDefinitionId}
                  onChange={(event) => updateSubscriptionField("planDefinitionId", event.target.value)}
                >
                  {plans.map((plan) => (
                    <option key={plan.id} value={plan.id}>
                      {plan.name}
                    </option>
                  ))}
                </Select>
              </Field>
              <Field label="Ciclo">
                <Select value={subscriptionForm.billingCycle} onChange={(event) => updateSubscriptionField("billingCycle", event.target.value)}>
                  <option value="Monthly">Mensal</option>
                  <option value="Annual">Anual</option>
                  <option value="Custom">Personalizado</option>
                </Select>
              </Field>
              <div className="grid gap-4 sm:grid-cols-2">
                <Field label="Início">
                  <Input
                    type="datetime-local"
                    value={subscriptionForm.startsAt}
                    onChange={(event) => updateSubscriptionField("startsAt", event.target.value)}
                  />
                </Field>
                <Field label="Fim">
                  <Input
                    type="datetime-local"
                    value={subscriptionForm.endsAt}
                    onChange={(event) => updateSubscriptionField("endsAt", event.target.value)}
                  />
                </Field>
              </div>
              <div className="grid gap-4 sm:grid-cols-2">
                <Field label="Valor pago">
                  <Input
                    type="number"
                    step="0.01"
                    value={subscriptionForm.amountPaid}
                    onChange={(event) => updateSubscriptionField("amountPaid", event.target.value)}
                  />
                </Field>
                <Field label="Moeda">
                  <Input value={subscriptionForm.currencyCode} onChange={(event) => updateSubscriptionField("currencyCode", event.target.value.toUpperCase())} />
                </Field>
              </div>
              <Field label="Status">
                <Select value={subscriptionForm.status} onChange={(event) => updateSubscriptionField("status", event.target.value)}>
                  <option value="Active">Ativa</option>
                  <option value="Scheduled">Agendada</option>
                  <option value="Expired">Expirada</option>
                  <option value="Cancelled">Cancelada</option>
                </Select>
              </Field>
              <Field label="Observação">
                <Textarea value={subscriptionForm.adminNote} onChange={(event) => updateSubscriptionField("adminNote", event.target.value)} />
              </Field>
              <div className="flex flex-wrap justify-end gap-2">
                {editingSubscriptionId ? (
                  <Button variant="secondary" onClick={beginCreateSubscription}>
                    Cancelar edição
                  </Button>
                ) : null}
                <Button onClick={() => void saveSubscription()} disabled={savingSubscription}>
                  {savingSubscription ? <Loader2 className="animate-spin" /> : <Sparkles />}
                  {editingSubscriptionId ? "Salvar assinatura" : "Criar assinatura"}
                </Button>
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Histórico de assinaturas</CardTitle>
              <CardDescription>Vigência, valor pago e status administrativo por usuário.</CardDescription>
            </CardHeader>
            <CardContent>
              {subscriptions.length === 0 ? (
                <Notice tone="warning">Nenhuma assinatura registrada.</Notice>
              ) : (
                <div className="overflow-x-auto">
                  <Table>
                    <TableHeader>
                      <TableRow className="border-b border-border/60 bg-surface-muted hover:bg-surface-muted">
                        <TableHead>Cliente</TableHead>
                        <TableHead>Plano</TableHead>
                        <TableHead>Vigência</TableHead>
                        <TableHead>Valor pago</TableHead>
                        <TableHead>Status</TableHead>
                        <TableHead className="text-right">Ações</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {subscriptions.map((subscription) => (
                        <TableRow key={subscription.id}>
                          <TableCell>
                            <div className="min-w-[220px]">
                              <p className="font-semibold text-foreground">{subscription.userDisplayName}</p>
                              <p className="text-xs text-muted-foreground">{subscription.userEmail}</p>
                            </div>
                          </TableCell>
                          <TableCell className="whitespace-nowrap">{subscription.planName}</TableCell>
                          <TableCell className="min-w-[220px] text-sm text-muted-foreground">
                            {formatDateTime(subscription.startsAt)} até {formatDateTime(subscription.endsAt)}
                            <div className="text-xs">{formatBillingCycle(subscription.billingCycle)}</div>
                          </TableCell>
                          <TableCell className="whitespace-nowrap font-medium text-foreground">
                            {formatCurrency(subscription.amountPaid, subscription.currencyCode)}
                          </TableCell>
                          <TableCell>
                            <Badge variant="neutral">{formatSubscriptionStatus(subscription.status)}</Badge>
                          </TableCell>
                          <TableCell className="text-right">
                            <Button variant="ghost" size="sm" onClick={() => beginEditSubscription(subscription)}>
                              <Pencil />
                              Editar
                            </Button>
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              )}
            </CardContent>
          </Card>
        </div>
      ) : null}

      <DeleteConfirmationDialog
        open={Boolean(userToDelete)}
        title="Excluir usuário"
        description="Essa ação remove a conta e todos os vínculos do usuário. Se ele possuir casas próprias, elas também serão apagadas com seus dados vinculados."
        confirmationTarget={userToDelete?.email}
        confirmationLabel={`Digite o e-mail ${userToDelete?.email ?? ""} para confirmar`}
        confirmLabel="Excluir usuário"
        impactItems={[
          "A conta, os tokens e os vínculos ativos do usuário.",
          "Comentários autorados por ele em outras casas.",
          "Casas próprias do usuário e todos os vínculos dessas casas, quando existirem.",
        ]}
        onOpenChange={(open) => {
          if (!open) {
            setUserToDelete(null);
          }
        }}
        onConfirm={async () => {
          if (!userToDelete) {
            return;
          }

          await deleteUser(userToDelete.id);
        }}
      />
    </div>
  );
}

function TabButton({
  tab,
  activeTab,
  onSelect,
  label,
}: {
  tab: PlatformTab;
  activeTab: PlatformTab;
  onSelect: (tab: PlatformTab) => void;
  label: string;
}) {
  const active = tab === activeTab;

  return (
    <button
      type="button"
      role="tab"
      aria-selected={active}
      className={
        active
          ? "rounded-full bg-primary px-4 py-2 text-sm font-semibold text-primary-foreground shadow-sm"
          : "rounded-full border border-border/70 px-4 py-2 text-sm font-semibold text-muted-foreground"
      }
      onClick={() => onSelect(tab)}
    >
      {label}
    </button>
  );
}

function UserRow({
  user,
  busy,
  onDeactivate,
  onReactivate,
  onDelete,
}: {
  user: AdminUserListItem;
  busy: boolean;
  onDeactivate: (userId: string) => Promise<void>;
  onReactivate: (userId: string) => Promise<void>;
  onDelete: (user: AdminUserListItem) => void;
}) {
  const stateLabel =
    user.accountState === "Active"
      ? "Ativa"
      : user.accountState === "PendingSelfDeletion"
        ? "Cancelamento pendente"
        : "Desativada pelo superadmin";

  return (
    <div className="rounded-[20px] border border-border/70 bg-surface-muted p-4">
      <div className="flex flex-col gap-4 xl:flex-row xl:items-center xl:justify-between">
        <div className="min-w-0 space-y-2">
          <div className="flex flex-wrap items-center gap-2">
            <p className="truncate text-base font-semibold text-foreground">{user.displayName}</p>
            <Badge variant={user.isProtected ? "default" : "outline"}>{user.systemRole}</Badge>
            <Badge variant="neutral">{stateLabel}</Badge>
            <Badge variant="outline">{user.effectivePlanName}</Badge>
          </div>
          <div className="flex flex-wrap gap-x-4 gap-y-1 text-sm text-muted-foreground">
            <span>{user.email}</span>
            <span>{user.membershipCount} vínculo(s)</span>
            <span>{user.ownedHouseholdCount} casa(s) própria(s)</span>
          </div>
          <p className="text-xs text-muted-foreground">
            {user.activeSubscriptionStatus
              ? `Assinatura ${formatSubscriptionStatus(user.activeSubscriptionStatus)} até ${user.activeSubscriptionEndsAt ? formatDateTime(user.activeSubscriptionEndsAt) : "sem fim"}`
              : "Sem assinatura ativa; usa o plano padrão atual."}
          </p>
        </div>

        <div className="flex flex-wrap gap-2">
          {user.isProtected ? (
            <Badge variant="neutral">Conta protegida</Badge>
          ) : user.accountState === "DisabledBySuperAdmin" ? (
            <Button variant="secondary" onClick={() => void onReactivate(user.id)} disabled={busy}>
              {busy ? <Loader2 className="animate-spin" /> : <UserCheck />}
              Reativar
            </Button>
          ) : (
            <Button variant="secondary" onClick={() => void onDeactivate(user.id)} disabled={busy}>
              {busy ? <Loader2 className="animate-spin" /> : <ShieldOff />}
              Desativar
            </Button>
          )}
          {!user.isProtected ? (
            <Button variant="danger" onClick={() => onDelete(user)} disabled={busy}>
              <Trash2 />
              Excluir
            </Button>
          ) : null}
        </div>
      </div>
    </div>
  );
}

function Field({
  label,
  children,
}: {
  label: string;
  children: React.ReactNode;
}) {
  return (
    <label className="grid gap-2">
      <span className="text-sm font-semibold text-foreground">{label}</span>
      {children}
    </label>
  );
}

function MetricCard({ label, value }: { label: string; value: number }) {
  return (
    <Card>
      <CardContent className="p-4">
        <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">{label}</p>
        <p className="mt-2 text-3xl font-semibold text-foreground">{value}</p>
      </CardContent>
    </Card>
  );
}

function AdminAccessDenied() {
  return (
    <main className="grid min-h-screen place-items-center bg-background px-4">
      <Card className="w-full max-w-lg">
        <CardHeader>
          <CardTitle>Acesso restrito</CardTitle>
          <CardDescription>Somente o perfil SuperAdmin pode acessar a gestão global da plataforma.</CardDescription>
        </CardHeader>
        <CardContent className="flex flex-wrap gap-2">
          <Button asChild>
            <Link href="/projects">Voltar ao sistema</Link>
          </Button>
          <Button variant="ghost" onClick={() => clearSession()}>
            <LogOut />
            Sair
          </Button>
        </CardContent>
      </Card>
    </main>
  );
}

function formatCurrency(value: number, currencyCode: string) {
  return new Intl.NumberFormat("pt-BR", {
    style: "currency",
    currency: currencyCode || "BRL",
  }).format(value);
}

function formatDateTime(value: string) {
  return new Intl.DateTimeFormat("pt-BR", {
    dateStyle: "short",
    timeStyle: "short",
  }).format(new Date(value));
}

function formatBillingCycle(value: BillingCycle) {
  return value === "Monthly" ? "Mensal" : value === "Annual" ? "Anual" : "Personalizado";
}

function formatSubscriptionStatus(value: UserSubscriptionStatus) {
  switch (value) {
    case "Active":
      return "Ativa";
    case "Scheduled":
      return "Agendada";
    case "Expired":
      return "Expirada";
    case "Cancelled":
      return "Cancelada";
    default:
      return value;
  }
}

function toLocalInputValue(value: Date | string) {
  const date = value instanceof Date ? value : new Date(value);
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  const hours = String(date.getHours()).padStart(2, "0");
  const minutes = String(date.getMinutes()).padStart(2, "0");

  return `${year}-${month}-${day}T${hours}:${minutes}`;
}

function fromLocalInputValue(value: string) {
  return new Date(value).toISOString();
}
