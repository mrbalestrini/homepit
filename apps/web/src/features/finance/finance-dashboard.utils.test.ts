import { describe, expect, it } from "vitest";
import type { CreditCardTransaction, FinanceEntry } from "@/lib/api";
import {
  filterFinanceEntries,
  getCurrentPeriodParts,
  groupFinanceEntries,
  summarizeAnalyticalExpenses,
} from "./finance-dashboard.utils";

function buildEntry(overrides: Partial<FinanceEntry> & Pick<FinanceEntry, "id" | "title">): FinanceEntry {
  return {
    id: overrides.id,
    periodId: "period-1",
    year: 2026,
    month: 7,
    title: overrides.title,
    notes: null,
    amount: 0,
    type: "Saida",
    verified: false,
    referenceDate: "2026-07-06",
    origin: "Manual",
    recurringTemplateId: null,
    creditCardStatementId: null,
    universeId: null,
    universeName: null,
    projectId: null,
    projectName: null,
    createdByMemberId: null,
    createdAt: "2026-07-06T12:00:00.000Z",
    updatedAt: "2026-07-06T12:00:00.000Z",
    canEdit: true,
    canDelete: true,
    ...overrides,
  };
}

function buildTransaction(
  overrides: Partial<CreditCardTransaction> & Pick<CreditCardTransaction, "id" | "title">,
): CreditCardTransaction {
  return {
    id: overrides.id,
    creditCardAccountId: "card-1",
    creditCardAccountName: "Nubank",
    creditCardStatementId: null,
    title: overrides.title,
    merchant: null,
    amount: 0,
    purchasedOn: "2026-07-06",
    notes: null,
    universeId: null,
    universeName: null,
    projectId: null,
    projectName: null,
    externalSource: null,
    externalReference: null,
    importedAt: null,
    createdByMemberId: null,
    createdAt: "2026-07-06T12:00:00.000Z",
    updatedAt: "2026-07-06T12:00:00.000Z",
    canEdit: true,
    canDelete: true,
    ...overrides,
  };
}

describe("finance dashboard utils", () => {
  it("uses UTC when resolving the current finance period", () => {
    const period = getCurrentPeriodParts(new Date("2026-07-31T23:30:00-03:00"));

    expect(period).toEqual({ year: 2026, month: 8 });
  });

  it("filters finance entries by search, verification, origin and project", () => {
    const entries = [
      buildEntry({
        id: "entry-1",
        title: "Condominio",
        notes: "Apartamento",
        projectId: "project-1",
        projectName: "Moradia",
        verified: true,
      }),
      buildEntry({
        id: "entry-2",
        title: "Fatura Nubank",
        origin: "CreditCardStatement",
        projectId: "project-2",
        projectName: "Viagem",
      }),
    ];

    const filtered = filterFinanceEntries(entries, {
      search: "fatúra",
      type: "all",
      verified: "pending",
      origin: "CreditCardStatement",
      universeId: "all",
      projectId: "project-2",
      groupBy: "project",
    });

    expect(filtered).toHaveLength(1);
    expect(filtered[0]?.id).toBe("entry-2");
  });

  it("groups entries by project and keeps a fallback label when there is no project", () => {
    const entries = [
      buildEntry({
        id: "entry-1",
        title: "Condominio",
        projectId: "project-1",
        projectName: "Moradia",
      }),
      buildEntry({
        id: "entry-2",
        title: "Mercado",
      }),
    ];

    const groups = groupFinanceEntries(entries, "project");

    expect(groups).toEqual([
      expect.objectContaining({ key: "project-1", label: "Moradia" }),
      expect.objectContaining({ key: "without-project", label: "Sem projeto" }),
    ]);
  });

  it("excludes the consolidated credit-card statement from analytical expenses while adding card purchases", () => {
    const entries = [
      buildEntry({ id: "entry-1", title: "Condominio", amount: 700, type: "Saida" }),
      buildEntry({
        id: "entry-2",
        title: "Fatura Nubank",
        amount: 220.9,
        type: "Saida",
        origin: "CreditCardStatement",
      }),
      buildEntry({ id: "entry-3", title: "Salario", amount: 5000, type: "Entrada" }),
    ];
    const transactions = [
      buildTransaction({ id: "tx-1", title: "Supermercado", amount: 120.9 }),
      buildTransaction({ id: "tx-2", title: "Farmacia", amount: 100 }),
    ];

    expect(summarizeAnalyticalExpenses(entries, transactions)).toBe(920.9);
  });
});
