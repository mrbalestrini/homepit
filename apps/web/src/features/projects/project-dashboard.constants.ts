import type { ActivityStatus, AuthResponse, Space, Priority } from "@/lib/api";
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

export const roleLabels: Record<Space["role"], string> = {
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
  { value: "relevance", label: "Mais relevantes" },
  { value: "size", label: "Esforço" },
  { value: "project", label: "Projeto" },
  { value: "responsible", label: "Responsável" },
  { value: "title", label: "Título" },
];

export const modules = [
  { key: "projects", label: "Projetos", state: "active" as const },
  { key: "market", label: "Mercado", state: "roadmap" as const },
  { key: "finance", label: "Financeiro", state: "active" as const },
  { key: "routines", label: "Rotinas", state: "roadmap" as const },
];

export const uiStorageKeys = {
  projectViewMode: "organizaclub.projects.view-mode",
  projectActivitySort: "organizaclub.projects.activity-sort",
  promptImagesHidden: "organizaclub.prompts.images-hidden",
  platformSuggestionFilters: "organizaclub.platform.suggestion-filters",
  sidebarCollapsed: "organizaclub.ui.sidebar-collapsed",
  theme: "organizaclub.ui.theme",
};

export const defaultAppTheme: AppTheme = "system";

export const themeOptions: Array<{ value: AppTheme; label: string }> = [
  { value: "system", label: "Sistema" },
  { value: "light", label: "Claro" },
  { value: "dark", label: "Escuro" },
];

export const defaultActivityFilters: ActivityFilterState = {
  search: "",
  status: "all",
  priority: "all",
  responsibleMemberId: "all",
  sort: "priority",
};
