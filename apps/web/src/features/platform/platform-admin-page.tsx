"use client";

import Link from "next/link";
import {
  Check,
  Loader2,
  LogOut,
  Pencil,
  Settings2,
  ShieldOff,
  Save,
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
  type BulkUpdateToolImprovementSuggestionsRequest,
  type CreateToolImprovementSuggestionRequest,
  type AdminUserListItem,
  type BillingCycle,
  type PlatformSettings,
  type PlanDefinition,
  type ToolImprovementSuggestion,
  type ToolImprovementSuggestionPriority,
  type ToolImprovementSuggestionStatus,
  type UpdateToolImprovementSuggestionRequest,
  type UserSubscription,
  type UserSubscriptionStatus,
  type UpdatePlatformSettingsRequest,
  apiFetch,
  clearSession,
} from "@/lib/api";
import { uiStorageKeys } from "@/features/projects/project-dashboard.constants";
import { AccountStateGate } from "@/features/workspace/account-state-gate";
import { DeleteConfirmationDialog } from "@/features/workspace/delete-confirmation-dialog";
import { OrganizaClubAuth } from "@/features/workspace/organiza-club-auth";
import { OrganizaClubWorkspaceShell, Notice } from "@/features/workspace/organiza-club-workspace-shell";
import { useProjectDashboard } from "@/features/projects/use-project-dashboard";
import { cn } from "@/lib/utils";

type PlatformTab = "users" | "plans" | "subscriptions" | "suggestions" | "settings";
type UserFilter = "all" | "Active" | "PendingSelfDeletion" | "DisabledBySuperAdmin";
type SuggestionStatusFilter = "all" | ToolImprovementSuggestionStatus;
type SuggestionPriorityFilter = "all" | ToolImprovementSuggestionPriority;

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

type PlatformSettingsFormState = UpdatePlatformSettingsRequest;
type SuggestionEditorState = {
  status: ToolImprovementSuggestionStatus;
  priority: ToolImprovementSuggestionPriority;
  internalComment: string;
};
type SuggestionFiltersState = {
  search: string;
  status: SuggestionStatusFilter;
  priority: SuggestionPriorityFilter;
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

const defaultPlatformSettingsForm: PlatformSettingsFormState = {
  adminName: "",
  contactEmail: "",
  contactPhone: "",
  managementPhone: "",
  instagram: "",
  addressLine1: "",
  addressLine2: "",
  city: "",
  state: "",
  postalCode: "",
};

const defaultSuggestionFilters: SuggestionFiltersState = {
  search: "",
  status: "all",
  priority: "all",
};

const suggestionStatusOptions: Array<{ value: ToolImprovementSuggestionStatus; label: string }> = [
  { value: "NaoLido", label: "Não lido" },
  { value: "EmExecucao", label: "Em execução" },
  { value: "Postergado", label: "Postergado" },
  { value: "Feito", label: "Feito" },
];

const suggestionPriorityOptions: Array<{ value: ToolImprovementSuggestionPriority; label: string }> = [
  { value: "Baixa", label: "Baixa" },
  { value: "Media", label: "Média" },
  { value: "Alta", label: "Alta" },
  { value: "Urgente", label: "Urgente" },
];

function buildSuggestionDrafts(items: ToolImprovementSuggestion[]) {
  return Object.fromEntries(
    items.map((item) => [
      item.id,
      {
        status: item.status,
        priority: item.priority,
        internalComment: item.internalComment ?? "",
      } satisfies SuggestionEditorState,
    ]),
  );
}

function readStoredSuggestionFilters(): SuggestionFiltersState {
  if (typeof window === "undefined") {
    return defaultSuggestionFilters;
  }

  try {
    const rawValue = window.localStorage.getItem(uiStorageKeys.platformSuggestionFilters);
    if (!rawValue) {
      return defaultSuggestionFilters;
    }

    const parsed = JSON.parse(rawValue) as Partial<SuggestionFiltersState>;
    return {
      search: typeof parsed.search === "string" ? parsed.search : "",
      status: isSuggestionStatusFilter(parsed.status) ? parsed.status : "all",
      priority: isSuggestionPriorityFilter(parsed.priority) ? parsed.priority : "all",
    };
  } catch {
    return defaultSuggestionFilters;
  }
}

function isSuggestionStatusFilter(value: unknown): value is SuggestionStatusFilter {
  return value === "all" || suggestionStatusOptions.some((option) => option.value === value);
}

function isSuggestionPriorityFilter(value: unknown): value is SuggestionPriorityFilter {
  return value === "all" || suggestionPriorityOptions.some((option) => option.value === value);
}

export function PlatformAdminPage() {
  const dashboard = useProjectDashboard();

  if (!dashboard.session) {
    return <OrganizaClubAuth onAuthenticated={dashboard.handleAuthenticated} />;
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
      <OrganizaClubWorkspaceShell
      controller={{
        session,
        activeSpaceId: dashboard.activeSpaceId,
        activeSpace: dashboard.activeSpace,
        members: dashboard.members,
        theme: dashboard.theme,
        sidebarCollapsed: dashboard.sidebarCollapsed,
        loading: dashboard.loading,
        error: dashboard.error,
        canShareSpace: dashboard.canShareSpace,
        canManageSpace: dashboard.canManageSpace,
        editingSpace: dashboard.editingSpace,
        isSpaceDialogOpen: dashboard.activeModal === "space",
        isShareDialogOpen: dashboard.activeModal === "share",
        setError: dashboard.setError,
        setSidebarCollapsed: dashboard.setSidebarCollapsed,
        setTheme: dashboard.setTheme,
        handleSpaceChange: dashboard.handleSpaceChange,
        handleLogout: dashboard.handleLogout,
        refreshSpaces: dashboard.refreshSpaces,
        refreshWorkspace: async () => dashboard.loadWorkspace(),
        openCreateSpace: dashboard.openCreateSpace,
        openEditSpace: dashboard.openEditSpace,
        openShareSpace: dashboard.openShareSpace,
        closeCommonModal: dashboard.closeModal,
        createSpace: dashboard.createSpace,
        updateSpace: dashboard.updateSpace,
        deleteSpace: dashboard.deleteSpace,
        shareSpace: dashboard.shareSpace,
      }}
      activeModule="platform"
      subtitle="Usuários, planos, assinaturas e configurações globais em um hub do SuperAdmin"
      visibleCount={session.spaces.length}
      visibleLabel="espaços visíveis"
      headerStats={[{ label: "espaços", value: session.spaces.length }]}
      requireSpace={false}
    >
      <PlatformAdminPanel token={session.accessToken} />
    </OrganizaClubWorkspaceShell>
  );
}

function PlatformAdminPanel({ token }: { token: string }) {
  const [activeTab, setActiveTab] = useState<PlatformTab>("users");
  const [users, setUsers] = useState<AdminUserListItem[]>([]);
  const [plans, setPlans] = useState<PlanDefinition[]>([]);
  const [subscriptions, setSubscriptions] = useState<UserSubscription[]>([]);
  const [suggestions, setSuggestions] = useState<ToolImprovementSuggestion[]>([]);
  const [platformSettings, setPlatformSettings] = useState<PlatformSettings>({
    ...defaultPlatformSettingsForm,
    canShowAddressOnLanding: false,
  });
  const [loading, setLoading] = useState(true);
  const [savingPlanId, setSavingPlanId] = useState<string | null>(null);
  const [savingSettings, setSavingSettings] = useState(false);
  const [busyUserId, setBusyUserId] = useState<string | null>(null);
  const [userFilter, setUserFilter] = useState<UserFilter>("all");
  const [userToDelete, setUserToDelete] = useState<AdminUserListItem | null>(null);
  const [editingSubscriptionId, setEditingSubscriptionId] = useState<string | null>(null);
  const [savingSubscription, setSavingSubscription] = useState(false);
  const [subscriptionForm, setSubscriptionForm] = useState<SubscriptionFormState>(defaultSubscriptionForm);
  const [planDrafts, setPlanDrafts] = useState<Record<string, PlanDefinition>>({});
  const [suggestionDrafts, setSuggestionDrafts] = useState<Record<string, SuggestionEditorState>>({});
  const [savingSuggestionIds, setSavingSuggestionIds] = useState<string[]>([]);
  const [selectedSuggestionIds, setSelectedSuggestionIds] = useState<string[]>([]);
  const [savingBulkSuggestionUpdate, setSavingBulkSuggestionUpdate] = useState(false);
  const [bulkSuggestionStatus, setBulkSuggestionStatus] = useState<"" | ToolImprovementSuggestionStatus>("");
  const [bulkSuggestionPriority, setBulkSuggestionPriority] = useState<"" | ToolImprovementSuggestionPriority>("");
  const [suggestionFilters, setSuggestionFilters] = useState<SuggestionFiltersState>(() => readStoredSuggestionFilters());
  const addressCanBePublic =
    platformSettings.addressLine1.trim() !== "" &&
    platformSettings.addressLine2.trim() !== "" &&
    platformSettings.city.trim() !== "" &&
    platformSettings.state.trim() !== "" &&
    platformSettings.postalCode.trim() !== "";

  const loadData = useCallback(async () => {
    setLoading(true);

    try {
      const [nextUsers, nextPlans, nextSubscriptions, nextSuggestions, nextSettings] = await Promise.all([
        apiFetch<AdminUserListItem[]>("/api/admin/users", { token }),
        apiFetch<PlanDefinition[]>("/api/admin/platform/plans", { token }),
        apiFetch<UserSubscription[]>("/api/admin/platform/subscriptions", { token }),
        apiFetch<ToolImprovementSuggestion[]>("/api/admin/platform/tool-improvement-suggestions", { token }),
        apiFetch<PlatformSettings>("/api/admin/platform/settings", { token }),
      ]);

      setUsers(nextUsers);
      setPlans(nextPlans);
      setSubscriptions(nextSubscriptions);
      setSuggestions(nextSuggestions);
      setPlatformSettings(nextSettings);
      setPlanDrafts(Object.fromEntries(nextPlans.map((plan) => [plan.id, plan])));
      setSuggestionDrafts(buildSuggestionDrafts(nextSuggestions));
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

  useEffect(() => {
    try {
      window.localStorage.setItem(uiStorageKeys.platformSuggestionFilters, JSON.stringify(suggestionFilters));
    } catch {
      // Ignore storage failures so filters do not block the UI.
    }
  }, [suggestionFilters]);

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

  const filteredSuggestions = useMemo(() => {
    const normalizedSearch = suggestionFilters.search.trim().toLowerCase();

    return suggestions.filter((suggestion) => {
      const matchesStatus = suggestionFilters.status === "all" || suggestion.status === suggestionFilters.status;
      const matchesPriority = suggestionFilters.priority === "all" || suggestion.priority === suggestionFilters.priority;
      const matchesSearch =
        normalizedSearch === "" ||
        suggestion.userDisplayName.toLowerCase().includes(normalizedSearch) ||
        suggestion.userEmail.toLowerCase().includes(normalizedSearch) ||
        suggestion.suggestionText.toLowerCase().includes(normalizedSearch);

      return matchesStatus && matchesPriority && matchesSearch;
    });
  }, [suggestionFilters, suggestions]);

  const suggestionSummary = useMemo(
    () => ({
      total: suggestions.length,
      unread: suggestions.filter((suggestion) => suggestion.status === "NaoLido").length,
      inProgress: suggestions.filter((suggestion) => suggestion.status === "EmExecucao").length,
      postponed: suggestions.filter((suggestion) => suggestion.status === "Postergado").length,
      done: suggestions.filter((suggestion) => suggestion.status === "Feito").length,
    }),
    [suggestions],
  );

  const allVisibleSuggestionsSelected =
    filteredSuggestions.length > 0 && filteredSuggestions.every((suggestion) => selectedSuggestionIds.includes(suggestion.id));

  function updatePlanDraft(
    planId: string,
    field:
      | "monthlyPrice"
      | "annualPrice"
      | "maxOwnedSpaces"
      | "maxCores"
      | "maxProjects"
      | "maxInvitedMembers"
      | "maxOriginalImages",
    value: string,
  ) {
    setPlanDrafts((current) => ({
      ...current,
      [planId]: {
        ...current[planId],
        [field]: field === "maxInvitedMembers" && value.trim() === "" ? null : Number(value),
      },
    }));
  }

  function updatePlanVisibility(planId: string, showInCatalog: boolean) {
    setPlanDrafts((current) => ({
      ...current,
      [planId]: {
        ...current[planId],
        showInCatalog,
      },
    }));
  }

  function togglePopularPlan(planId: string) {
    setPlanDrafts((current) =>
      Object.fromEntries(
        Object.entries(current).map(([currentPlanId, plan]) => [
          currentPlanId,
          {
            ...plan,
            isPopular: currentPlanId === planId ? !plan.isPopular : false,
          },
        ]),
      ),
    );
  }

  function updateSettingsField(field: keyof PlatformSettingsFormState, value: string) {
    setPlatformSettings((current) => ({
      ...current,
      [field]: value,
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
          maxOwnedSpaces: draft.maxOwnedSpaces,
          maxCores: draft.maxCores,
          maxProjects: draft.maxProjects,
          maxInvitedMembers: draft.maxInvitedMembers ?? null,
          maxOriginalImages: draft.maxOriginalImages,
          showInCatalog: draft.showInCatalog,
          isPopular: draft.isPopular,
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

  async function savePlatformSettings() {
    setSavingSettings(true);

    try {
      const saved = await apiFetch<PlatformSettings>("/api/admin/platform/settings", {
        method: "PUT",
        token,
        body: JSON.stringify(platformSettings),
      });

      setPlatformSettings(saved);
      toast.success("Configurações da plataforma atualizadas.");
    } catch (exception) {
      toast.error(exception instanceof Error ? exception.message : "Não foi possível salvar as configurações.");
    } finally {
      setSavingSettings(false);
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

  function updateSuggestionFilter(field: keyof SuggestionFiltersState, value: string) {
    setSuggestionFilters((current) => ({
      ...current,
      [field]: value,
    }));
  }

  function updateSuggestionDraft(
    suggestionId: string,
    field: keyof SuggestionEditorState,
    value: string,
  ) {
    setSuggestionDrafts((current) => ({
      ...current,
      [suggestionId]: {
        ...(current[suggestionId] ?? { status: "NaoLido", priority: "Media", internalComment: "" }),
        [field]: value,
      },
    }));
  }

  function toggleSuggestionSelection(suggestionId: string) {
    setSelectedSuggestionIds((current) =>
      current.includes(suggestionId) ? current.filter((id) => id !== suggestionId) : [...current, suggestionId],
    );
  }

  function toggleVisibleSuggestionSelection() {
    if (filteredSuggestions.length === 0) {
      return;
    }

    setSelectedSuggestionIds((current) => {
      const visibleIds = filteredSuggestions.map((suggestion) => suggestion.id);
      if (visibleIds.every((id) => current.includes(id))) {
        return current.filter((id) => !visibleIds.includes(id));
      }

      return Array.from(new Set([...current, ...visibleIds]));
    });
  }

  async function saveSuggestion(suggestionId: string) {
    const draft = suggestionDrafts[suggestionId];
    if (!draft) {
      return;
    }

    setSavingSuggestionIds((current) => [...current, suggestionId]);

    try {
      const payload: UpdateToolImprovementSuggestionRequest = {
        status: draft.status,
        priority: draft.priority,
        internalComment: draft.internalComment.trim() === "" ? null : draft.internalComment.trim(),
      };

      const updated = await apiFetch<ToolImprovementSuggestion>(`/api/admin/platform/tool-improvement-suggestions/${suggestionId}`, {
        method: "PUT",
        token,
        body: JSON.stringify(payload),
      });

      setSuggestions((current) => current.map((suggestion) => (suggestion.id === updated.id ? updated : suggestion)));
      setSuggestionDrafts((current) => ({
        ...current,
        [updated.id]: {
          status: updated.status,
          priority: updated.priority,
          internalComment: updated.internalComment ?? "",
        },
      }));
      toast.success("Sugestão atualizada.");
    } catch (exception) {
      toast.error(exception instanceof Error ? exception.message : "Não foi possível salvar a sugestão.");
    } finally {
      setSavingSuggestionIds((current) => current.filter((id) => id !== suggestionId));
    }
  }

  async function applyBulkSuggestionUpdate() {
    if (selectedSuggestionIds.length === 0 || (!bulkSuggestionStatus && !bulkSuggestionPriority)) {
      return;
    }

    setSavingBulkSuggestionUpdate(true);

    try {
      const payload: BulkUpdateToolImprovementSuggestionsRequest = {
        suggestionIds: selectedSuggestionIds,
        status: bulkSuggestionStatus || null,
        priority: bulkSuggestionPriority || null,
      };

      const updated = await apiFetch<ToolImprovementSuggestion[]>("/api/admin/platform/tool-improvement-suggestions/bulk-update", {
        method: "POST",
        token,
        body: JSON.stringify(payload),
      });

      const updatedById = new Map(updated.map((item) => [item.id, item]));
      setSuggestions((current) => current.map((suggestion) => updatedById.get(suggestion.id) ?? suggestion));
      setSuggestionDrafts((current) => ({
        ...current,
        ...buildSuggestionDrafts(updated),
      }));
      setBulkSuggestionStatus("");
      setBulkSuggestionPriority("");
      setSelectedSuggestionIds([]);
      toast.success("Sugestões atualizadas em massa.");
    } catch (exception) {
      toast.error(exception instanceof Error ? exception.message : "Não foi possível aplicar a atualização em massa.");
    } finally {
      setSavingBulkSuggestionUpdate(false);
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
            <h1 className="mt-2 text-3xl font-semibold text-foreground">Plataforma Organiza Club</h1>
            <p className="mt-2 text-sm leading-6 text-muted-foreground">
              Centralize a gestão global de contas, catálogo comercial e histórico manual de assinaturas com uma visão
              única da plataforma.
            </p>
          </div>
          <div className="flex flex-wrap gap-2" role="tablist" aria-label="Abas da plataforma">
            <TabButton tab="users" activeTab={activeTab} onSelect={setActiveTab} label="Usuários" />
            <TabButton tab="plans" activeTab={activeTab} onSelect={setActiveTab} label="Planos" />
            <TabButton tab="subscriptions" activeTab={activeTab} onSelect={setActiveTab} label="Assinaturas" />
            <TabButton tab="suggestions" activeTab={activeTab} onSelect={setActiveTab} label="Sugestões" />
            <TabButton tab="settings" activeTab={activeTab} onSelect={setActiveTab} label="Configurações" />
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
            O superadmin protegido segue somente leitura. Excluir um proprietário apaga seus espaços próprios e os dados
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
                    <div className="flex flex-wrap items-center gap-2">
                      {draft.isPopular ? <Badge variant="default">Popular</Badge> : null}
                      <Button
                        type="button"
                        variant={draft.isPopular ? "default" : "secondary"}
                        size="sm"
                        onClick={() => togglePopularPlan(plan.id)}
                        aria-pressed={draft.isPopular}
                        aria-label={`${draft.isPopular ? "Remover" : "Marcar"} ${plan.name} como popular`}
                      >
                        {draft.isPopular ? "Popular" : "Marcar popular"}
                      </Button>
                      <label className="inline-flex items-center gap-2 rounded-full border border-border/70 px-3 py-2 text-sm font-medium text-foreground">
                        <input
                          type="checkbox"
                          checked={draft.showInCatalog}
                          onChange={(event) => updatePlanVisibility(plan.id, event.target.checked)}
                          aria-label={`Mostrar plano ${plan.name}`}
                        />
                        Mostrar plano
                      </label>
                      <Badge variant="outline">{plan.currencyCode}</Badge>
                    </div>
                  </div>
                </CardHeader>
                <CardContent className="space-y-4">
                  <div
                    className={cn(
                      "rounded-[18px] border px-4 py-3 text-sm",
                      draft.isPopular ? "border-primary/30 bg-highlight/35 text-foreground" : "border-border/70 bg-surface-muted text-muted-foreground",
                    )}
                  >
                    {draft.isPopular ? "Este é o plano popular exibido com destaque para os clientes." : "Marque este plano como popular para destacá-lo na modal de assinatura."}
                  </div>
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
                    <Field label="Espaços próprios">
                      <Input
                        type="number"
                        value={draft.maxOwnedSpaces}
                        onChange={(event) => updatePlanDraft(plan.id, "maxOwnedSpaces", event.target.value)}
                      />
                    </Field>
                    <Field label="Núcleos totais">
                      <Input
                        type="number"
                        value={draft.maxCores}
                        onChange={(event) => updatePlanDraft(plan.id, "maxCores", event.target.value)}
                      />
                    </Field>
                    <Field label="Projetos totais">
                      <Input
                        type="number"
                        value={draft.maxProjects}
                        onChange={(event) => updatePlanDraft(plan.id, "maxProjects", event.target.value)}
                      />
                    </Field>
                    <Field label="Membros convidados">
                      <Input
                        type="number"
                        value={draft.maxInvitedMembers ?? ""}
                        onChange={(event) => updatePlanDraft(plan.id, "maxInvitedMembers", event.target.value)}
                        placeholder="Ilimitado"
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
                      {savingPlanId === plan.id ? <Loader2 className="animate-spin" /> : <Save />}
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
                  {savingSubscription ? <Loader2 className="animate-spin" /> : <Save />}
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

      {!loading && activeTab === "suggestions" ? (
        <div className="space-y-4" role="tabpanel" aria-label="Sugestões">
          <div className="grid gap-3 md:grid-cols-5">
            <MetricCard label="Total" value={suggestionSummary.total} />
            <MetricCard label="Não lidas" value={suggestionSummary.unread} />
            <MetricCard label="Em execução" value={suggestionSummary.inProgress} />
            <MetricCard label="Postergadas" value={suggestionSummary.postponed} />
            <MetricCard label="Feitas" value={suggestionSummary.done} />
          </div>

          <Card>
            <CardHeader className="gap-3">
              <div>
                <CardTitle>Sugestões de melhorias</CardTitle>
                <CardDescription>Centralize a triagem, a prioridade e os comentários internos do time.</CardDescription>
              </div>
              <div className="grid gap-3 lg:grid-cols-[minmax(0,1fr)_220px_220px]">
                <Input
                  value={suggestionFilters.search}
                  onChange={(event) => updateSuggestionFilter("search", event.target.value)}
                  placeholder="Buscar por usuário, e-mail ou conteúdo"
                  aria-label="Buscar sugestões"
                />
                <Select
                  value={suggestionFilters.status}
                  onChange={(event) => updateSuggestionFilter("status", event.target.value)}
                  aria-label="Filtrar sugestões por status"
                >
                  <option value="all">Todos os status</option>
                  {suggestionStatusOptions.map((option) => (
                    <option key={option.value} value={option.value}>
                      {option.label}
                    </option>
                  ))}
                </Select>
                <Select
                  value={suggestionFilters.priority}
                  onChange={(event) => updateSuggestionFilter("priority", event.target.value)}
                  aria-label="Filtrar sugestões por prioridade"
                >
                  <option value="all">Todas as prioridades</option>
                  {suggestionPriorityOptions.map((option) => (
                    <option key={option.value} value={option.value}>
                      {option.label}
                    </option>
                  ))}
                </Select>
              </div>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="flex flex-col gap-3 rounded-[18px] border border-border/70 bg-surface-muted p-4 lg:flex-row lg:items-end">
                <label className="inline-flex items-center gap-2 text-sm font-medium text-foreground">
                  <input
                    type="checkbox"
                    checked={allVisibleSuggestionsSelected}
                    onChange={() => toggleVisibleSuggestionSelection()}
                    aria-label="Selecionar sugestões visíveis"
                  />
                  Selecionar visíveis
                </label>
                <div className="grid flex-1 gap-3 sm:grid-cols-2">
                  <Field label="Status em massa">
                    <Select
                      value={bulkSuggestionStatus}
                      onChange={(event) => setBulkSuggestionStatus(event.target.value as "" | ToolImprovementSuggestionStatus)}
                      aria-label="Status em massa"
                    >
                      <option value="">Sem alteração</option>
                      {suggestionStatusOptions.map((option) => (
                        <option key={option.value} value={option.value}>
                          {option.label}
                        </option>
                      ))}
                    </Select>
                  </Field>
                  <Field label="Prioridade em massa">
                    <Select
                      value={bulkSuggestionPriority}
                      onChange={(event) => setBulkSuggestionPriority(event.target.value as "" | ToolImprovementSuggestionPriority)}
                      aria-label="Prioridade em massa"
                    >
                      <option value="">Sem alteração</option>
                      {suggestionPriorityOptions.map((option) => (
                        <option key={option.value} value={option.value}>
                          {option.label}
                        </option>
                      ))}
                    </Select>
                  </Field>
                </div>
                <div className="flex items-center justify-between gap-3">
                  <span className="text-sm text-muted-foreground">{selectedSuggestionIds.length} selecionada(s)</span>
                  <Button
                    onClick={() => void applyBulkSuggestionUpdate()}
                    disabled={savingBulkSuggestionUpdate || selectedSuggestionIds.length === 0 || (!bulkSuggestionStatus && !bulkSuggestionPriority)}
                  >
                    {savingBulkSuggestionUpdate ? <Loader2 className="animate-spin" /> : <Check />}
                    Aplicar em massa
                  </Button>
                </div>
              </div>

              {filteredSuggestions.length === 0 ? (
                <Notice tone="warning">Nenhuma sugestão encontrada para os filtros atuais.</Notice>
              ) : (
                <div className="space-y-3">
                  {filteredSuggestions.map((suggestion) => {
                    const draft = suggestionDrafts[suggestion.id] ?? {
                      status: suggestion.status,
                      priority: suggestion.priority,
                      internalComment: suggestion.internalComment ?? "",
                    };
                    const saving = savingSuggestionIds.includes(suggestion.id);

                    return (
                      <Card key={suggestion.id}>
                        <CardContent className="space-y-4 p-4">
                          <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
                            <div className="flex items-start gap-3">
                              <input
                                type="checkbox"
                                checked={selectedSuggestionIds.includes(suggestion.id)}
                                onChange={() => toggleSuggestionSelection(suggestion.id)}
                                aria-label={`Selecionar sugestão ${suggestion.userDisplayName}`}
                                className="mt-1"
                              />
                              <div className="space-y-2">
                                <div className="flex flex-wrap items-center gap-2">
                                  <p className="font-semibold text-foreground">{suggestion.userDisplayName}</p>
                                  <Badge variant="outline">{formatSuggestionStatus(suggestion.status)}</Badge>
                                  <Badge variant="neutral">{formatSuggestionPriority(suggestion.priority)}</Badge>
                                </div>
                                <div className="flex flex-wrap gap-x-4 gap-y-1 text-sm text-muted-foreground">
                                  <span>{suggestion.userEmail}</span>
                                  <span>Enviada em {formatDateTime(suggestion.submittedAt)}</span>
                                  {suggestion.lastReviewedAt ? (
                                    <span>
                                      Última revisão em {formatDateTime(suggestion.lastReviewedAt)}
                                      {suggestion.lastReviewedByDisplayName ? ` por ${suggestion.lastReviewedByDisplayName}` : ""}
                                    </span>
                                  ) : null}
                                </div>
                              </div>
                            </div>
                          </div>

                          <div className="rounded-[18px] border border-border/70 bg-surface-muted p-4 text-sm leading-6 text-foreground">
                            {suggestion.suggestionText}
                          </div>

                          <div className="grid gap-4 lg:grid-cols-[220px_220px_minmax(0,1fr)]">
                            <Field label="Status">
                              <Select
                                value={draft.status}
                                onChange={(event) => updateSuggestionDraft(suggestion.id, "status", event.target.value)}
                                aria-label={`Status da sugestão ${suggestion.userDisplayName}`}
                              >
                                {suggestionStatusOptions.map((option) => (
                                  <option key={option.value} value={option.value}>
                                    {option.label}
                                  </option>
                                ))}
                              </Select>
                            </Field>
                            <Field label="Prioridade">
                              <Select
                                value={draft.priority}
                                onChange={(event) => updateSuggestionDraft(suggestion.id, "priority", event.target.value)}
                                aria-label={`Prioridade da sugestão ${suggestion.userDisplayName}`}
                              >
                                {suggestionPriorityOptions.map((option) => (
                                  <option key={option.value} value={option.value}>
                                    {option.label}
                                  </option>
                                ))}
                              </Select>
                            </Field>
                            <Field label="Comentário interno">
                              <Textarea
                                value={draft.internalComment}
                                onChange={(event) => updateSuggestionDraft(suggestion.id, "internalComment", event.target.value)}
                                rows={4}
                                aria-label={`Comentário interno da sugestão ${suggestion.userDisplayName}`}
                              />
                            </Field>
                          </div>

                          <div className="flex justify-end">
                            <Button onClick={() => void saveSuggestion(suggestion.id)} disabled={saving}>
                              {saving ? <Loader2 className="animate-spin" /> : <Pencil />}
                              Salvar sugestão
                            </Button>
                          </div>
                        </CardContent>
                      </Card>
                    );
                  })}
                </div>
              )}
            </CardContent>
          </Card>
        </div>
      ) : null}

      {!loading && activeTab === "settings" ? (
        <div className="grid gap-4 xl:grid-cols-[minmax(0,1fr)_320px]" role="tabpanel" aria-label="Configurações">
          <Card>
            <CardHeader>
              <CardTitle>Configurações da plataforma</CardTitle>
              <CardDescription>Dados globais usados pelo contato da marca, pela operação e pela landing.</CardDescription>
            </CardHeader>
            <CardContent className="space-y-6">
              <section className="space-y-4">
                <SectionHeading
                  title="Identificação"
                  description="Nome e presença social que ajudam a reconhecer a plataforma."
                />
                <div className="grid gap-4 sm:grid-cols-2">
                  <Field label="Nome administrador">
                    <Input
                      value={platformSettings.adminName}
                      onChange={(event) => updateSettingsField("adminName", event.target.value)}
                    />
                  </Field>
                  <Field label="Instagram">
                    <Input
                      value={platformSettings.instagram}
                      onChange={(event) => updateSettingsField("instagram", event.target.value)}
                    />
                  </Field>
                </div>
              </section>

              <section className="space-y-4">
                <SectionHeading
                  title="Contato público"
                  description="Meios que podem aparecer para clientes na landing."
                />
                <div className="grid gap-4 sm:grid-cols-2">
                  <Field label="E-mail contato">
                    <Input
                      type="email"
                      value={platformSettings.contactEmail}
                      onChange={(event) => updateSettingsField("contactEmail", event.target.value)}
                    />
                  </Field>
                  <Field label="Telefone contato">
                    <Input
                      value={platformSettings.contactPhone}
                      onChange={(event) => updateSettingsField("contactPhone", event.target.value)}
                    />
                  </Field>
                </div>
              </section>

              <section className="space-y-4">
                <SectionHeading
                  title="Contato interno"
                  description="Canal que recebe notificações operacionais da plataforma."
                />
                <div className="grid gap-4 sm:grid-cols-2">
                  <Field label="Telefone gestão">
                    <Input
                      value={platformSettings.managementPhone}
                      onChange={(event) => updateSettingsField("managementPhone", event.target.value)}
                    />
                  </Field>
                </div>
              </section>

              <section className="space-y-4">
                <SectionHeading
                  title="Endereço institucional"
                  description="Preencha todos os campos para liberar a exibição na landing."
                />
                <div className="grid gap-4 sm:grid-cols-2">
                  <Field label="Endereço linha 1">
                    <Input
                      value={platformSettings.addressLine1}
                      onChange={(event) => updateSettingsField("addressLine1", event.target.value)}
                    />
                  </Field>
                  <Field label="Endereço linha 2">
                    <Input
                      value={platformSettings.addressLine2}
                      onChange={(event) => updateSettingsField("addressLine2", event.target.value)}
                    />
                  </Field>
                  <Field label="Cidade">
                    <Input
                      value={platformSettings.city}
                      onChange={(event) => updateSettingsField("city", event.target.value)}
                    />
                  </Field>
                  <Field label="Estado">
                    <Input
                      value={platformSettings.state}
                      onChange={(event) => updateSettingsField("state", event.target.value)}
                    />
                  </Field>
                  <Field label="CEP">
                    <Input
                      value={platformSettings.postalCode}
                      onChange={(event) => updateSettingsField("postalCode", event.target.value)}
                    />
                  </Field>
                </div>
                <div
                  className={cn(
                    "rounded-[18px] border px-4 py-3 text-sm",
                    platformSettings.canShowAddressOnLanding
                      ? "border-emerald-200 bg-emerald-50 text-emerald-900"
                      : "border-border/70 bg-surface-muted text-muted-foreground",
                  )}
                >
                  <p className="font-semibold text-foreground">
                    {addressCanBePublic
                      ? "Endereço pronto para a landing"
                      : "Endereço ainda não visível na landing"}
                  </p>
                  <p className="mt-1">
                    {addressCanBePublic
                      ? "Os campos de endereço já estão completos."
                      : "Preencha linha 1, linha 2, cidade, estado e CEP para liberar a exibição."}
                  </p>
                </div>
              </section>

              <div className="flex justify-end">
                <Button onClick={() => void savePlatformSettings()} disabled={savingSettings}>
                  {savingSettings ? <Loader2 className="animate-spin" /> : <Settings2 />}
                  Salvar configurações
                </Button>
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Resumo</CardTitle>
              <CardDescription>Visão rápida do que já pode ser usado na landing.</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4 text-sm">
              <SummaryRow label="Contato público" value={platformSettings.contactEmail || "Não informado"} />
              <SummaryRow label="Telefone contato" value={platformSettings.contactPhone || "Não informado"} />
              <SummaryRow label="Telefone gestão" value={platformSettings.managementPhone || "Não informado"} />
              <SummaryRow label="Instagram" value={platformSettings.instagram || "Não informado"} />
              <SummaryRow
                label="Endereço"
                value={
                  addressCanBePublic
                    ? `${platformSettings.addressLine1}, ${platformSettings.addressLine2} - ${platformSettings.city}/${platformSettings.state} - ${platformSettings.postalCode}`
                    : "Aguardando preenchimento completo"
                }
              />
            </CardContent>
          </Card>
        </div>
      ) : null}

      <DeleteConfirmationDialog
        open={Boolean(userToDelete)}
        title="Excluir usuário"
        description="Essa ação remove a conta e todos os vínculos do usuário. Se ele possuir espaços próprios, elas também serão apagadas com seus dados vinculados."
        confirmationTarget={userToDelete?.email}
        confirmationLabel={`Digite o e-mail ${userToDelete?.email ?? ""} para confirmar`}
        confirmLabel="Excluir usuário"
        impactItems={[
          "A conta, os tokens e os vínculos ativos do usuário.",
          "Comentários autorados por ele em outros espaços.",
          "Espaços próprios do usuário e todos os vínculos desses espaços, quando existirem.",
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

function SectionHeading({ title, description }: { title: string; description: string }) {
  return (
    <div>
      <p className="text-sm font-semibold text-foreground">{title}</p>
      <p className="mt-1 text-sm leading-6 text-muted-foreground">{description}</p>
    </div>
  );
}

function SummaryRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-[16px] border border-border/70 bg-surface-muted px-4 py-3">
      <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">{label}</p>
      <p className="mt-1 break-words text-sm text-foreground">{value}</p>
    </div>
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
            <span>{user.ownedSpaceCount} espaço(s) própria(s)</span>
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

function formatSuggestionStatus(value: ToolImprovementSuggestionStatus) {
  switch (value) {
    case "NaoLido":
      return "Não lido";
    case "EmExecucao":
      return "Em execução";
    case "Postergado":
      return "Postergado";
    case "Feito":
      return "Feito";
    default:
      return value;
  }
}

function formatSuggestionPriority(value: ToolImprovementSuggestionPriority) {
  switch (value) {
    case "Baixa":
      return "Baixa";
    case "Media":
      return "Média";
    case "Alta":
      return "Alta";
    case "Urgente":
      return "Urgente";
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
