import type { ActivityStatus, AuthResponse, Household, Priority } from "@/lib/api";
import type { ActivityFilterState, ActivitySortState, AppTheme, ProjectViewMode } from "./project-dashboard.types";

export const activityColumns: Array<{ status: ActivityStatus; label: string; hint: string }> = [
  { status: "NaoIniciada", label: "Não iniciadas", hint: "Aguardando ação" },
  { status: "EmAndamento", label: "Em andamento", hint: "Em execução" },
  { status: "Concluido", label: "Concluídas", hint: "Finalizadas" },
];

export const priorityLabels: Record<Priority, string> = {
  Baixa: "Baixa",
  Media: "Média",
  Alta: "Alta",
  Urgente: "Urgente",
};

export const priorityRank: Record<Priority, number> = {
  Urgente: 0,
  Alta: 1,
  Media: 2,
  Baixa: 3,
};

export const roleLabels: Record<Household["role"], string> = {
  Owner: "Proprietário",
  Admin: "Administrador",
  Member: "Membro",
};

export const systemRoleLabels: Record<AuthResponse["user"]["systemRole"], string> = {
  Admin: "Admin do sistema",
  SuperAdmin: "Superadmin",
  User: "Usuário",
};

export const viewModeOptions: Array<{ value: ProjectViewMode; label: string }> = [
  { value: "list", label: "Lista" },
  { value: "kanban", label: "Kanban" },
];

export const activitySortOptions: Array<{ value: ActivitySortState; label: string }> = [
  { value: "priority", label: "Prioridade" },
  { value: "size", label: "Tamanho" },
  { value: "project", label: "Projeto" },
  { value: "responsible", label: "Responsável" },
  { value: "title", label: "Título" },
];

export const modules = [
  { key: "projects", label: "Projetos", state: "active" as const },
  { key: "market", label: "Mercado", state: "roadmap" as const },
  { key: "finance", label: "Financeiro", state: "roadmap" as const },
  { key: "routines", label: "Rotinas", state: "roadmap" as const },
];

export const uiStorageKeys = {
  projectViewMode: "homepit.projects.view-mode",
  projectActivitySort: "homepit.projects.activity-sort",
  sidebarCollapsed: "homepit.ui.sidebar-collapsed",
  theme: "homepit.ui.theme",
};

export const defaultAppTheme: AppTheme = "earthy";

export const themeOptions: Array<{ value: AppTheme; label: string }> = [
  { value: "cozy", label: "Atual" },
  { value: "earthy", label: "Terroso" },
  { value: "dark", label: "Escuro" },
];

export const defaultActivityFilters: ActivityFilterState = {
  search: "",
  status: "all",
  priority: "all",
  responsibleMemberId: "all",
  sort: "priority",
};
