import type { Space } from "@/lib/api";

const ACTIVE_SPACE_STORAGE_KEY_PREFIX = "organizaclub.space-selection";

function buildStorageKey(userId: string) {
  return `${ACTIVE_SPACE_STORAGE_KEY_PREFIX}.${userId}`;
}

function canUseLocalStorage() {
  return typeof window !== "undefined";
}

export function readStoredActiveSpaceId(userId: string) {
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

export function storeActiveSpaceId(userId: string, spaceId: string) {
  if (!userId || !spaceId || !canUseLocalStorage()) {
    return;
  }

  try {
    window.localStorage.setItem(buildStorageKey(userId), spaceId);
  } catch {
    // Ignore storage failures so the app keeps working offline or in restricted browsers.
  }
}

export function clearStoredActiveSpaceId(userId: string) {
  if (!userId || !canUseLocalStorage()) {
    return;
  }

  try {
    window.localStorage.removeItem(buildStorageKey(userId));
  } catch {
    // Ignore storage failures so cleanup never blocks the UI.
  }
}

export function resolveActiveSpaceSelection(
  spaces: readonly Pick<Space, "id" | "createdAt">[],
  currentSpaceId: string,
  storedSpaceId: string | null,
  preferredSpaceId?: string,
) {
  const spaceIds = new Set(spaces.map((space) => space.id));
  const preferredSpaceIdIsValid = Boolean(preferredSpaceId && spaceIds.has(preferredSpaceId));
  const currentSpaceIdIsValid = Boolean(currentSpaceId && spaceIds.has(currentSpaceId));
  const storedSpaceIdIsValid = Boolean(storedSpaceId && spaceIds.has(storedSpaceId));
  const fallbackSpaceId = resolveFallbackSpaceId(spaces);

  return {
    spaceId:
      (preferredSpaceIdIsValid ? preferredSpaceId : "") ||
      (currentSpaceIdIsValid ? currentSpaceId : "") ||
      (storedSpaceIdIsValid ? storedSpaceId : "") ||
      fallbackSpaceId ||
      "",
    shouldClearStoredSpaceId: Boolean(storedSpaceId && !storedSpaceIdIsValid),
  };
}

function resolveFallbackSpaceId(spaces: readonly Pick<Space, "id" | "createdAt">[]) {
  const spacesWithCreatedAt = spaces
    .map((space, index) => ({
      space,
      index,
      createdAtMs: space.createdAt ? Date.parse(space.createdAt) : Number.NaN,
    }))
    .filter(({ createdAtMs }) => !Number.isNaN(createdAtMs))
    .sort((left, right) => right.createdAtMs - left.createdAtMs || right.index - left.index);

  if (spacesWithCreatedAt.length > 0) {
    return spacesWithCreatedAt[0]?.space.id ?? "";
  }

  return spaces[spaces.length - 1]?.id ?? "";
}
