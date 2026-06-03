export type Household = {
  id: string;
  name: string;
  role: "Owner" | "Admin" | "Member";
};

export type HouseholdMember = {
  id: string;
  userId: string;
  displayName: string;
  email: string;
  phoneNumber?: string | null;
  role: "Owner" | "Admin" | "Member";
  isCurrentUser: boolean;
};

export type User = {
  id: string;
  email: string;
  displayName: string;
  phoneNumber?: string | null;
  systemRole: "User" | "Admin";
  hasProfilePhoto: boolean;
  profilePhotoUpdatedAt?: string | null;
};

export type AuthResponse = {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  user: User;
  households: Household[];
};

export type Universe = {
  id: string;
  name: string;
  imageUrl?: string | null;
  createdByMemberId?: string | null;
  projectCount: number;
  canEdit: boolean;
  canDelete: boolean;
};

export type Project = {
  id: string;
  universeId: string;
  universeName: string;
  universeImageUrl?: string | null;
  name: string;
  createdByMemberId?: string | null;
  activityCount: number;
  canEdit: boolean;
  canDelete: boolean;
};

export type ActivityStatus = "NaoIniciada" | "EmAndamento" | "Concluido";
export type Priority = "Baixa" | "Media" | "Alta" | "Urgente";

export type Activity = {
  id: string;
  projectId: string;
  projectName: string;
  universeId: string;
  universeName: string;
  universeImageUrl?: string | null;
  createdByMemberId?: string | null;
  title: string;
  description?: string | null;
  status: ActivityStatus;
  priority: Priority;
  size?: number | null;
  responsibleMemberId?: string | null;
  responsibleName?: string | null;
  pendingCount: number;
  commentCount: number;
  canEdit: boolean;
  canDelete: boolean;
};

export type ActivityComment = {
  id: string;
  activityId: string;
  authorMemberId: string;
  authorName: string;
  body: string;
  createdAt: string;
  isEdited: boolean;
  canEdit: boolean;
  canDelete: boolean;
};

export type PromptCategoryReference = {
  id: string;
  name: string;
};

export type PromptCategory = {
  id: string;
  name: string;
  createdByMemberId?: string | null;
  usageCount: number;
  replacementRequiredCount: number;
  canEdit: boolean;
  canDelete: boolean;
};

export type PromptListItem = {
  id: string;
  universeId?: string | null;
  universeName?: string | null;
  title: string;
  description?: string | null;
  promptText: string;
  categories: PromptCategoryReference[];
  linkUrl?: string | null;
  linkTitle?: string | null;
  createdByMemberId?: string | null;
  hasImage: boolean;
  imageUpdatedAt?: string | null;
  updatedAt: string;
  canEdit: boolean;
  canDelete: boolean;
};

export type PromptListResponse = {
  items: PromptListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
};

export type PromptDetail = {
  id: string;
  universeId?: string | null;
  universeName?: string | null;
  title: string;
  description?: string | null;
  promptText: string;
  categories: PromptCategoryReference[];
  linkUrl?: string | null;
  linkTitle?: string | null;
  createdByMemberId?: string | null;
  hasImage: boolean;
  imageUpdatedAt?: string | null;
  createdAt: string;
  updatedAt: string;
  canEdit: boolean;
  canDelete: boolean;
};

const API_BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080";

export class ApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
  ) {
    super(message);
  }
}

export async function apiFetch<T>(
  path: string,
  options: RequestInit & { token?: string; householdId?: string } = {},
): Promise<T> {
  const headers = new Headers(options.headers);
  const hasBody = options.body !== undefined && options.body !== null;
  const isFormData = typeof FormData !== "undefined" && options.body instanceof FormData;
  const isBlob = typeof Blob !== "undefined" && options.body instanceof Blob;
  const shouldSetJsonContentType = hasBody && !headers.has("Content-Type") && !isFormData && !isBlob;

  if (shouldSetJsonContentType) {
    headers.set("Content-Type", "application/json");
  }

  if (options.token) {
    headers.set("Authorization", `Bearer ${options.token}`);
  }

  if (options.householdId) {
    headers.set("X-Household-Id", options.householdId);
  }

  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...options,
    headers,
  });

  if (!response.ok) {
    let message = "Não foi possível concluir a operação.";
    try {
      const body = (await response.json()) as { detail?: string; title?: string };
      message = body.detail ?? body.title ?? message;
    } catch {
      message = response.statusText || message;
    }

    throw new ApiError(message, response.status);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

export async function apiFetchBlob(
  path: string,
  options: RequestInit & { token?: string; householdId?: string } = {},
): Promise<Blob> {
  const headers = new Headers(options.headers);

  if (options.token) {
    headers.set("Authorization", `Bearer ${options.token}`);
  }

  if (options.householdId) {
    headers.set("X-Household-Id", options.householdId);
  }

  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...options,
    headers,
    cache: "no-store",
  });

  if (!response.ok) {
    let message = "Não foi possível concluir a operação.";
    try {
      const body = (await response.json()) as { detail?: string; title?: string };
      message = body.detail ?? body.title ?? message;
    } catch {
      message = response.statusText || message;
    }

    throw new ApiError(message, response.status);
  }

  return await response.blob();
}

export function storeSession(auth: AuthResponse) {
  window.localStorage.setItem("homepit.session", JSON.stringify(auth));
}

export function readSession(): AuthResponse | null {
  const value = window.localStorage.getItem("homepit.session");
  if (!value) {
    return null;
  }

  try {
    return JSON.parse(value) as AuthResponse;
  } catch {
    window.localStorage.removeItem("homepit.session");
    return null;
  }
}

export function clearSession() {
  window.localStorage.removeItem("homepit.session");
}
