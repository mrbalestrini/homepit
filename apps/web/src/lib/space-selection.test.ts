import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  clearStoredActiveSpaceId,
  readStoredActiveSpaceId,
  resolveActiveSpaceSelection,
  storeActiveSpaceId,
} from "./space-selection";

describe("space-selection helpers", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    window.localStorage.clear();
  });

  it("stores and reads a space selection per user", () => {
    storeActiveSpaceId("user-1", "space-1");

    expect(readStoredActiveSpaceId("user-1")).toBe("space-1");
    expect(readStoredActiveSpaceId("user-2")).toBeNull();
  });

  it("cleans empty stored values instead of keeping junk", () => {
    storeActiveSpaceId("user-1", "   ");

    expect(readStoredActiveSpaceId("user-1")).toBeNull();
    expect(window.localStorage.getItem("organizaclub.space-selection.user-1")).toBeNull();
  });

  it("resolves the best space id and flags invalid stored values", () => {
    const spaces = [
      { id: "space-1" },
      { id: "space-2" },
      { id: "space-3" },
    ];

    const fromCurrent = resolveActiveSpaceSelection(spaces, "space-2", "space-missing");
    expect(fromCurrent.spaceId).toBe("space-2");
    expect(fromCurrent.shouldClearStoredSpaceId).toBe(true);

    const fromStored = resolveActiveSpaceSelection(spaces, "", "space-3");
    expect(fromStored.spaceId).toBe("space-3");
    expect(fromStored.shouldClearStoredSpaceId).toBe(false);

    const fallback = resolveActiveSpaceSelection(spaces, "", "space-missing");
    expect(fallback.spaceId).toBe("space-3");
    expect(fallback.shouldClearStoredSpaceId).toBe(true);
  });

  it("prefers the most recently created space when creation timestamps exist", () => {
    const spaces = [
      { id: "space-1", createdAt: "2026-06-10T12:00:00.000Z" },
      { id: "space-2", createdAt: "2026-06-15T12:00:00.000Z" },
      { id: "space-3", createdAt: "2026-06-12T12:00:00.000Z" },
    ];

    const result = resolveActiveSpaceSelection(spaces, "", null);

    expect(result.spaceId).toBe("space-2");
    expect(result.shouldClearStoredSpaceId).toBe(false);
  });

  it("clears a stored selection on demand", () => {
    storeActiveSpaceId("user-1", "space-1");

    clearStoredActiveSpaceId("user-1");

    expect(readStoredActiveSpaceId("user-1")).toBeNull();
  });
});
