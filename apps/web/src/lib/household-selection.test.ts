import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  clearStoredActiveHouseholdId,
  readStoredActiveHouseholdId,
  resolveActiveHouseholdSelection,
  storeActiveHouseholdId,
} from "./household-selection";

describe("household-selection helpers", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    window.localStorage.clear();
  });

  it("stores and reads a household selection per user", () => {
    storeActiveHouseholdId("user-1", "household-1");

    expect(readStoredActiveHouseholdId("user-1")).toBe("household-1");
    expect(readStoredActiveHouseholdId("user-2")).toBeNull();
  });

  it("cleans empty stored values instead of keeping junk", () => {
    storeActiveHouseholdId("user-1", "   ");

    expect(readStoredActiveHouseholdId("user-1")).toBeNull();
    expect(window.localStorage.getItem("homepit.household-selection.user-1")).toBeNull();
  });

  it("resolves the best household id and flags invalid stored values", () => {
    const households = [
      { id: "household-1" },
      { id: "household-2" },
      { id: "household-3" },
    ];

    const fromCurrent = resolveActiveHouseholdSelection(households, "household-2", "household-missing");
    expect(fromCurrent.householdId).toBe("household-2");
    expect(fromCurrent.shouldClearStoredHouseholdId).toBe(true);

    const fromStored = resolveActiveHouseholdSelection(households, "", "household-3");
    expect(fromStored.householdId).toBe("household-3");
    expect(fromStored.shouldClearStoredHouseholdId).toBe(false);

    const fallback = resolveActiveHouseholdSelection(households, "", "household-missing");
    expect(fallback.householdId).toBe("household-3");
    expect(fallback.shouldClearStoredHouseholdId).toBe(true);
  });

  it("prefers the most recently created household when creation timestamps exist", () => {
    const households = [
      { id: "household-1", createdAt: "2026-06-10T12:00:00.000Z" },
      { id: "household-2", createdAt: "2026-06-15T12:00:00.000Z" },
      { id: "household-3", createdAt: "2026-06-12T12:00:00.000Z" },
    ];

    const result = resolveActiveHouseholdSelection(households, "", null);

    expect(result.householdId).toBe("household-2");
    expect(result.shouldClearStoredHouseholdId).toBe(false);
  });

  it("clears a stored selection on demand", () => {
    storeActiveHouseholdId("user-1", "household-1");

    clearStoredActiveHouseholdId("user-1");

    expect(readStoredActiveHouseholdId("user-1")).toBeNull();
  });
});
