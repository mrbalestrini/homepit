import type { Household } from "@/lib/api";

const ACTIVE_HOUSEHOLD_STORAGE_KEY_PREFIX = "homepit.household-selection";

function buildStorageKey(userId: string) {
  return `${ACTIVE_HOUSEHOLD_STORAGE_KEY_PREFIX}.${userId}`;
}

function canUseLocalStorage() {
  return typeof window !== "undefined";
}

export function readStoredActiveHouseholdId(userId: string) {
  if (!userId || !canUseLocalStorage()) {
    return null;
  }

  try {
    const value = window.localStorage.getItem(buildStorageKey(userId));
    if (!value) {
      return null;
    }

    const trimmedValue = value.trim();
    if (!trimmedValue) {
      window.localStorage.removeItem(buildStorageKey(userId));
      return null;
    }

    return trimmedValue;
  } catch {
    return null;
  }
}

export function storeActiveHouseholdId(userId: string, householdId: string) {
  if (!userId || !householdId || !canUseLocalStorage()) {
    return;
  }

  try {
    window.localStorage.setItem(buildStorageKey(userId), householdId);
  } catch {
    // Ignore storage failures so the app keeps working offline or in restricted browsers.
  }
}

export function clearStoredActiveHouseholdId(userId: string) {
  if (!userId || !canUseLocalStorage()) {
    return;
  }

  try {
    window.localStorage.removeItem(buildStorageKey(userId));
  } catch {
    // Ignore storage failures so cleanup never blocks the UI.
  }
}

export function resolveActiveHouseholdSelection(
  households: readonly Pick<Household, "id" | "createdAt">[],
  currentHouseholdId: string,
  storedHouseholdId: string | null,
  preferredHouseholdId?: string,
) {
  const householdIds = new Set(households.map((household) => household.id));
  const preferredHouseholdIdIsValid = Boolean(preferredHouseholdId && householdIds.has(preferredHouseholdId));
  const currentHouseholdIdIsValid = Boolean(currentHouseholdId && householdIds.has(currentHouseholdId));
  const storedHouseholdIdIsValid = Boolean(storedHouseholdId && householdIds.has(storedHouseholdId));
  const fallbackHouseholdId = resolveFallbackHouseholdId(households);

  return {
    householdId:
      (preferredHouseholdIdIsValid ? preferredHouseholdId : "") ||
      (currentHouseholdIdIsValid ? currentHouseholdId : "") ||
      (storedHouseholdIdIsValid ? storedHouseholdId : "") ||
      fallbackHouseholdId ||
      "",
    shouldClearStoredHouseholdId: Boolean(storedHouseholdId && !storedHouseholdIdIsValid),
  };
}

function resolveFallbackHouseholdId(households: readonly Pick<Household, "id" | "createdAt">[]) {
  const householdsWithCreatedAt = households
    .map((household, index) => ({
      household,
      index,
      createdAtMs: household.createdAt ? Date.parse(household.createdAt) : Number.NaN,
    }))
    .filter(({ createdAtMs }) => !Number.isNaN(createdAtMs))
    .sort((left, right) => right.createdAtMs - left.createdAtMs || right.index - left.index);

  if (householdsWithCreatedAt.length > 0) {
    return householdsWithCreatedAt[0]?.household.id ?? "";
  }

  return households[households.length - 1]?.id ?? "";
}
