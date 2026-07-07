export type Household = {
  id: string;
  name: string;
  role: "Owner" | "Admin" | "Member";
  createdAt?: string | null;
};

export type HouseholdMember = {
  id: string;
  userId: string;
  displayName: string;
  email: string;
  phoneNumber?: string | null;
  hasProfilePhoto: boolean;
  profilePhotoUpdatedAt?: string | null;
  role: "Owner" | "Admin" | "Member";
  isCurrentUser: boolean;
};

export type User = {
  id: string;
  email: string;
  displayName: string;
  phoneNumber?: string | null;
  systemRole: "User" | "Admin" | "SuperAdmin";
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
  hasImage: boolean;
  imageUpdatedAt?: string | null;
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
  universeHasImage: boolean;
  universeImageUpdatedAt?: string | null;
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
  universeHasImage: boolean;
  universeImageUpdatedAt?: string | null;
  createdByMemberId?: string | null;
  createdAt: string;
  title: string;
  description?: string | null;
  hasImage: boolean;
  imageUpdatedAt?: string | null;
  dueDate?: string | null;
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
  authorUserId: string;
  authorName: string;
  authorHasProfilePhoto: boolean;
  authorProfilePhotoUpdatedAt?: string | null;
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
  universeImageUrl?: string | null;
  universeHasImage: boolean;
  universeImageUpdatedAt?: string | null;
  title: string;
  description?: string | null;
  promptText: string;
  categories: PromptCategoryReference[];
  linkUrl?: string | null;
  linkTitle?: string | null;
  createdByMemberId?: string | null;
  isArchived: boolean;
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
  universeImageUrl?: string | null;
  universeHasImage: boolean;
  universeImageUpdatedAt?: string | null;
  title: string;
  description?: string | null;
  promptText: string;
  categories: PromptCategoryReference[];
  linkUrl?: string | null;
  linkTitle?: string | null;
  createdByMemberId?: string | null;
  isArchived: boolean;
  hasImage: boolean;
  imageUpdatedAt?: string | null;
  createdAt: string;
  updatedAt: string;
  canEdit: boolean;
  canDelete: boolean;
};

export type GsmNumberStatus = "Ativo" | "Inativo" | "Abandonado";
export type GsmNumberPlan = "PrePago" | "PosPago";

export type GsmNumber = {
  id: string;
  title: string;
  number: string;
  description?: string | null;
  plan: GsmNumberPlan;
  monthlyCost?: number | null;
  daysWithoutRecharge?: number | null;
  acquiredOn: string;
  lastRechargeOn?: string | null;
  status: GsmNumberStatus;
  createdByMemberId?: string | null;
  createdAt: string;
  updatedAt: string;
  canEdit: boolean;
  canDelete: boolean;
};

export type GsmRecharge = {
  id: string;
  gsmNumberId: string;
  rechargedOn: string;
  amount?: number | null;
  note?: string | null;
  createdByMemberId?: string | null;
  createdAt: string;
  updatedAt: string;
  canEdit: boolean;
  canDelete: boolean;
};

export type FinanceEntryType = "Entrada" | "Saida";
export type FinanceEntryOrigin = "Manual" | "RecurringTemplate" | "CreditCardStatement";
export type FinanceRecurrence = "Monthly" | "Annual";
export type AssetType = "Property" | "Vehicle" | "Other";

export type FinanceCategory = {
  id: string;
  name: string;
  isDefault: boolean;
  sortOrder: number;
  createdByMemberId?: string | null;
  usageCount: number;
  canEdit: boolean;
  canDelete: boolean;
};

export type FinancePeriodListItem = {
  id: string;
  year: number;
  month: number;
  totalIncome: number;
  totalExpense: number;
  cashBalance: number;
  entryCount: number;
};

export type FinancePeriodSummary = {
  totalIncome: number;
  totalExpense: number;
  cashBalance: number;
  analyticalExpenseTotal: number;
  verifiedEntries: number;
  pendingVerificationEntries: number;
  cardPurchaseCount: number;
};

export type FinanceEntry = {
  id: string;
  periodId: string;
  year: number;
  month: number;
  title: string;
  notes?: string | null;
  amount: number;
  type: FinanceEntryType;
  verified: boolean;
  referenceDate: string;
  origin: FinanceEntryOrigin;
  recurringTemplateId?: string | null;
  creditCardStatementId?: string | null;
  categoryId?: string | null;
  categoryName?: string | null;
  universeId?: string | null;
  universeName?: string | null;
  projectId?: string | null;
  projectName?: string | null;
  createdByMemberId?: string | null;
  createdAt: string;
  updatedAt: string;
  canEdit: boolean;
  canDelete: boolean;
};

export type CreditCardTransaction = {
  id: string;
  creditCardAccountId: string;
  creditCardAccountName: string;
  creditCardStatementId?: string | null;
  title: string;
  merchant?: string | null;
  amount: number;
  purchasedOn: string;
  notes?: string | null;
  categoryId?: string | null;
  categoryName?: string | null;
  universeId?: string | null;
  universeName?: string | null;
  projectId?: string | null;
  projectName?: string | null;
  externalSource?: string | null;
  externalReference?: string | null;
  importedAt?: string | null;
  createdByMemberId?: string | null;
  createdAt: string;
  updatedAt: string;
  canEdit: boolean;
  canDelete: boolean;
};

export type CreditCardStatement = {
  id: string;
  creditCardAccountId: string;
  creditCardAccountName: string;
  closingDate: string;
  dueDate: string;
  totalAmount: number;
  notes?: string | null;
  transactionCount: number;
  financeEntryId?: string | null;
  externalSource?: string | null;
  externalReference?: string | null;
  importedAt?: string | null;
  createdByMemberId?: string | null;
  createdAt: string;
  updatedAt: string;
  canEdit: boolean;
  canDelete: boolean;
};

export type FinancePeriodDetail = {
  id?: string | null;
  year: number;
  month: number;
  exists: boolean;
  summary: FinancePeriodSummary;
  entries: FinanceEntry[];
  cardTransactions: CreditCardTransaction[];
  statements: CreditCardStatement[];
};

export type FinanceRecurringTemplate = {
  id: string;
  title: string;
  notes?: string | null;
  type: FinanceEntryType;
  defaultAmount: number;
  recurrence: FinanceRecurrence;
  dayOfMonth?: number | null;
  monthOfYear?: number | null;
  isActive: boolean;
  categoryId?: string | null;
  categoryName?: string | null;
  universeId?: string | null;
  universeName?: string | null;
  projectId?: string | null;
  projectName?: string | null;
  createdByMemberId?: string | null;
  createdAt: string;
  updatedAt: string;
  canEdit: boolean;
  canDelete: boolean;
};

export type AssetPropertyDetails = {
  registryNumber?: string | null;
  propertyInscription?: string | null;
  privateAreaSquareMeters?: number | null;
  debtCheckOn?: string | null;
};

export type AssetVehicleDetails = {
  brand?: string | null;
  model?: string | null;
  yearModel?: string | null;
  renavam?: string | null;
};

export type Asset = {
  id: string;
  title: string;
  type: AssetType;
  currentValue?: number | null;
  remainingDebt?: number | null;
  isPaidOff: boolean;
  notes?: string | null;
  propertyDetails?: AssetPropertyDetails | null;
  vehicleDetails?: AssetVehicleDetails | null;
  createdByMemberId?: string | null;
  createdAt: string;
  updatedAt: string;
  canEdit: boolean;
  canDelete: boolean;
};

export type AssetValuation = {
  id: string;
  assetId: string;
  referenceYear: number;
  label: string;
  amount: number;
  notes?: string | null;
  createdAt: string;
  updatedAt: string;
  canEdit: boolean;
  canDelete: boolean;
};

export type CreditCardAccount = {
  id: string;
  name: string;
  brand?: string | null;
  lastFourDigits?: string | null;
  closingDay: number;
  dueDay: number;
  notes?: string | null;
  isActive: boolean;
  openTransactionCount: number;
  openTransactionTotal: number;
  createdByMemberId?: string | null;
  createdAt: string;
  updatedAt: string;
  canEdit: boolean;
  canDelete: boolean;
};

export type InstitutionalContentItem = {
  position: number;
  title: string;
  description: string;
};

export type InstitutionalPageContent = {
  slug: string;
  seoTitle: string;
  seoDescription: string;
  brandName: string;
  brandTagline: string;
  heroEyebrow: string;
  heroTitle: string;
  heroDescription: string;
  primaryCtaLabel: string;
  primaryCtaUrl: string;
  benefitsTitle: string;
  benefitsDescription: string;
  benefits: InstitutionalContentItem[];
  stepsTitle: string;
  stepsDescription: string;
  steps: InstitutionalContentItem[];
  highlightEyebrow: string;
  highlightTitle: string;
  highlightDescription: string;
  finalCtaTitle: string;
  finalCtaDescription: string;
  footerText: string;
  heroImageAlt: string;
  hasHeroImage: boolean;
  heroImageUpdatedAt?: string | null;
  highlightImageAlt: string;
  hasHighlightImage: boolean;
  highlightImageUpdatedAt?: string | null;
  hasSeoImage: boolean;
  seoImageUpdatedAt?: string | null;
  updatedAt?: string | null;
};

export const API_BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080";
const SESSION_STORAGE_KEY = "homepit.session";
const SESSION_EVENT_NAME = "homepit:session-changed";
const ACCESS_TOKEN_REFRESH_LEEWAY_MS = 2 * 60 * 1000;

type ApiRequestOptions = RequestInit & { token?: string; householdId?: string };
type SessionListener = (session: AuthResponse | null) => void;

let refreshSessionPromise: Promise<AuthResponse | null> | null = null;

export class ApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
  ) {
    super(message);
  }
}

export async function apiFetch<T>(path: string, options: ApiRequestOptions = {}): Promise<T> {
  const response = await requestApi(path, options);

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

export async function apiFetchBlob(path: string, options: ApiRequestOptions = {}): Promise<Blob> {
  const response = await requestApi(path, {
    ...options,
    cache: "no-store",
  });

  return await response.blob();
}

export function storeSession(auth: AuthResponse) {
  if (typeof window === "undefined") {
    return;
  }

  window.localStorage.setItem(SESSION_STORAGE_KEY, JSON.stringify(auth));
  dispatchSessionChange(auth);
}

export function updateStoredSession(
  updater: (session: AuthResponse) => AuthResponse | null,
): AuthResponse | null {
  const currentSession = readSession();
  if (!currentSession) {
    return null;
  }

  const nextSession = updater(currentSession);
  if (nextSession) {
    storeSession(nextSession);
    return nextSession;
  }

  clearSession();
  return null;
}

export function readSession(): AuthResponse | null {
  if (typeof window === "undefined") {
    return null;
  }

  return parseSessionValue(window.localStorage.getItem(SESSION_STORAGE_KEY), true);
}

export function clearSession() {
  if (typeof window === "undefined") {
    return;
  }

  window.localStorage.removeItem(SESSION_STORAGE_KEY);
  dispatchSessionChange(null);
}

export function subscribeToSessionChanges(listener: SessionListener) {
  if (typeof window === "undefined") {
    return () => undefined;
  }

  const handleSessionChange = (event: Event) => {
    listener((event as CustomEvent<AuthResponse | null>).detail ?? null);
  };

  const handleStorage = (event: StorageEvent) => {
    if (event.key !== SESSION_STORAGE_KEY) {
      return;
    }

    listener(parseSessionValue(event.newValue, false));
  };

  window.addEventListener(SESSION_EVENT_NAME, handleSessionChange as EventListener);
  window.addEventListener("storage", handleStorage);

  return () => {
    window.removeEventListener(SESSION_EVENT_NAME, handleSessionChange as EventListener);
    window.removeEventListener("storage", handleStorage);
  };
}

async function requestApi(path: string, options: ApiRequestOptions): Promise<Response> {
  const token = await prepareAccessToken(options);
  let response = await executeRequest(path, options, token);

  if (response.ok || response.status !== 401 || !options.token) {
    if (!response.ok) {
      throw await buildApiError(response);
    }

    return response;
  }

  const latestSession = readSession();
  if (latestSession?.accessToken && latestSession.accessToken !== token) {
    response = await executeRequest(path, options, latestSession.accessToken);
    if (response.ok) {
      return response;
    }

    if (response.status !== 401) {
      throw await buildApiError(response);
    }
  }

  const refreshedSession = await refreshStoredSession(true);
  if (!refreshedSession) {
    throw createExpiredSessionError();
  }

  response = await executeRequest(path, options, refreshedSession.accessToken);
  if (!response.ok) {
    if (response.status === 401) {
      clearSessionIfCurrent(refreshedSession.refreshToken);
      throw createExpiredSessionError();
    }

    throw await buildApiError(response);
  }

  return response;
}

async function prepareAccessToken(options: ApiRequestOptions) {
  if (!options.token) {
    return undefined;
  }

  const currentToken = getCurrentSessionToken(options.token);
  const currentSession = readSession();
  if (!currentSession || currentToken !== currentSession.accessToken) {
    return currentToken;
  }

  if (!isSessionExpiringSoon(currentSession)) {
    return currentToken;
  }

  try {
    const refreshedSession = await refreshStoredSession(false);
    return refreshedSession?.accessToken ?? currentToken;
  } catch {
    return currentToken;
  }
}

async function executeRequest(path: string, options: ApiRequestOptions, token?: string) {
  const headers = new Headers(options.headers);
  const hasBody = options.body !== undefined && options.body !== null;
  const isFormData = typeof FormData !== "undefined" && options.body instanceof FormData;
  const isBlob = typeof Blob !== "undefined" && options.body instanceof Blob;
  const shouldSetJsonContentType = hasBody && !headers.has("Content-Type") && !isFormData && !isBlob;

  if (shouldSetJsonContentType) {
    headers.set("Content-Type", "application/json");
  }

  if (token) {
    headers.set("Authorization", `Bearer ${token}`);
  }

  if (options.householdId) {
    headers.set("X-Household-Id", options.householdId);
  }

  return await fetch(`${API_BASE_URL}${path}`, {
    ...options,
    headers,
  });
}

async function refreshStoredSession(force: boolean): Promise<AuthResponse | null> {
  const currentSession = readSession();
  if (!currentSession?.refreshToken) {
    if (force) {
      clearSession();
    }

    return null;
  }

  if (!force && !isSessionExpiringSoon(currentSession)) {
    return currentSession;
  }

  if (refreshSessionPromise) {
    return await refreshSessionPromise;
  }

  const sessionSnapshot = currentSession;
  refreshSessionPromise = (async () => {
    const response = await fetch(`${API_BASE_URL}/api/auth/refresh`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ refreshToken: sessionSnapshot.refreshToken }),
    });

    if (!response.ok) {
      if (response.status === 401 || response.status === 403) {
        clearSessionIfCurrent(sessionSnapshot.refreshToken);
        return null;
      }

      throw await buildApiError(response);
    }

    const refreshedSession = (await response.json()) as AuthResponse;
    const latestSession = readSession();

    if (latestSession?.refreshToken !== sessionSnapshot.refreshToken) {
      return latestSession;
    }

    storeSession(refreshedSession);
    return refreshedSession;
  })();

  try {
    return await refreshSessionPromise;
  } finally {
    refreshSessionPromise = null;
  }
}

async function buildApiError(response: Response) {
  let message = defaultErrorMessage(response.status);

  try {
    const body = (await response.json()) as { detail?: string; title?: string };
    message = body.detail ?? body.title ?? message;
  } catch {
    message = response.statusText || message;
  }

  return new ApiError(message, response.status);
}

function defaultErrorMessage(status: number) {
  if (status === 401) {
    return "Sessão expirada. Faça login novamente.";
  }

  if (status === 403) {
    return "Acesso negado.";
  }

  return "Não foi possível concluir a operação.";
}

function createExpiredSessionError() {
  return new ApiError("Sessão expirada. Faça login novamente.", 401);
}

function getCurrentSessionToken(fallbackToken: string) {
  return readSession()?.accessToken ?? fallbackToken;
}

function clearSessionIfCurrent(refreshToken: string) {
  const currentSession = readSession();
  if (currentSession?.refreshToken === refreshToken) {
    clearSession();
  }
}

function isSessionExpiringSoon(session: AuthResponse) {
  const expiresAt = Date.parse(session.expiresAt);
  return Number.isNaN(expiresAt) || expiresAt - Date.now() <= ACCESS_TOKEN_REFRESH_LEEWAY_MS;
}

function parseSessionValue(value: string | null, clearInvalidValue: boolean): AuthResponse | null {
  if (!value) {
    return null;
  }

  try {
    return JSON.parse(value) as AuthResponse;
  } catch {
    if (clearInvalidValue && typeof window !== "undefined") {
      window.localStorage.removeItem(SESSION_STORAGE_KEY);
      dispatchSessionChange(null);
    }

    return null;
  }
}

function dispatchSessionChange(session: AuthResponse | null) {
  if (typeof window === "undefined") {
    return;
  }

  window.dispatchEvent(new CustomEvent<AuthResponse | null>(SESSION_EVENT_NAME, { detail: session }));
}
