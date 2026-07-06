import type { CreditCardTransaction, FinanceEntry, FinanceEntryOrigin, FinanceEntryType } from "@/lib/api";

export type FinanceEntryGroupBy = "none" | "type" | "universe" | "project";

export type FinanceEntryFilters = {
  search: string;
  type: "all" | FinanceEntryType;
  verified: "all" | "verified" | "pending";
  origin: "all" | FinanceEntryOrigin;
  universeId: "all" | string;
  projectId: "all" | string;
  groupBy: FinanceEntryGroupBy;
};

function parseDateOnly(value: string | null | undefined) {
  if (!value) {
    return null;
  }

  const date = new Date(`${value}T00:00:00Z`);
  return Number.isNaN(date.getTime()) ? null : date;
}

function normalizeText(value: string) {
  return value
    .normalize("NFD")
    .replace(/\p{Diacritic}/gu, "")
    .toLowerCase();
}

export function getCurrentPeriodParts(referenceDate = new Date()) {
  return {
    year: referenceDate.getUTCFullYear(),
    month: referenceDate.getUTCMonth() + 1,
  };
}

export function formatCurrency(value: number | null | undefined, fallback = "Nao informado") {
  if (value == null) {
    return fallback;
  }

  return new Intl.NumberFormat("pt-BR", {
    style: "currency",
    currency: "BRL",
  }).format(value);
}

export function parseCurrencyInput(value: string) {
  const normalized = value.trim();
  if (!normalized) {
    return null;
  }

  const cleaned = normalized.replace(/^r\$\s*/i, "").replace(/\s+/g, "");
  const lastComma = cleaned.lastIndexOf(",");
  const lastDot = cleaned.lastIndexOf(".");
  const decimalSeparatorIndex = Math.max(lastComma, lastDot);
  const hasDecimalSeparator = decimalSeparatorIndex > -1;

  let digits = cleaned.replace(/[^0-9.,-]/g, "");
  if (!digits) {
    return null;
  }

  if (hasDecimalSeparator) {
    const integerPart = digits.slice(0, decimalSeparatorIndex).replace(/[.,]/g, "");
    const fractionPart = digits.slice(decimalSeparatorIndex + 1).replace(/[.,]/g, "");
    digits = fractionPart ? `${integerPart}.${fractionPart}` : integerPart;
  } else {
    digits = digits.replace(/[.,]/g, "");
  }

  const parsed = Number(digits);
  if (!Number.isFinite(parsed)) {
    return null;
  }

  return Math.round(parsed * 100) / 100;
}

export function formatDateOnlyPtBr(value: string | null | undefined, fallback = "Sem data") {
  const date = parseDateOnly(value);
  if (!date) {
    return fallback;
  }

  return new Intl.DateTimeFormat("pt-BR", {
    timeZone: "UTC",
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  }).format(date);
}

export function formatDateOnlyInputValue(value = new Date()) {
  const year = value.getUTCFullYear();
  const month = String(value.getUTCMonth() + 1).padStart(2, "0");
  const day = String(value.getUTCDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

export function formatMonthLabel(year: number, month: number) {
  const date = new Date(Date.UTC(year, month - 1, 1));
  return new Intl.DateTimeFormat("pt-BR", {
    timeZone: "UTC",
    month: "long",
    year: "numeric",
  }).format(date);
}

export function filterFinanceEntries(entries: FinanceEntry[], filters: FinanceEntryFilters) {
  const normalizedSearch = normalizeText(filters.search.trim());

  return entries.filter((entry) => {
    if (filters.type !== "all" && entry.type !== filters.type) {
      return false;
    }

    if (filters.verified === "verified" && !entry.verified) {
      return false;
    }

    if (filters.verified === "pending" && entry.verified) {
      return false;
    }

    if (filters.origin !== "all" && entry.origin !== filters.origin) {
      return false;
    }

    if (filters.universeId !== "all" && entry.universeId !== filters.universeId) {
      return false;
    }

    if (filters.projectId !== "all" && entry.projectId !== filters.projectId) {
      return false;
    }

    if (!normalizedSearch) {
      return true;
    }

    const haystack = normalizeText(
      [
        entry.title,
        entry.notes ?? "",
        entry.universeName ?? "",
        entry.projectName ?? "",
      ].join(" "),
    );

    return haystack.includes(normalizedSearch);
  });
}

export function groupFinanceEntries(entries: FinanceEntry[], groupBy: FinanceEntryGroupBy) {
  if (groupBy === "none") {
    return [{ key: "all", label: "Todos os lancamentos", entries }];
  }

  const groups = new Map<string, FinanceEntry[]>();
  for (const entry of entries) {
    const key =
      groupBy === "type"
        ? entry.type
        : groupBy === "universe"
          ? entry.universeId ?? "without-universe"
          : entry.projectId ?? "without-project";

    const current = groups.get(key) ?? [];
    current.push(entry);
    groups.set(key, current);
  }

  return Array.from(groups.entries()).map(([key, groupEntries]) => ({
    key,
    label:
      groupBy === "type"
        ? key === "Entrada"
          ? "Entradas"
          : "Saidas"
        : groupBy === "universe"
          ? groupEntries[0]?.universeName ?? "Sem universo"
          : groupEntries[0]?.projectName ?? "Sem projeto",
    entries: groupEntries,
  }));
}

export function summarizeAnalyticalExpenses(entries: FinanceEntry[], cardTransactions: CreditCardTransaction[]) {
  const cashExpenses = entries
    .filter((entry) => entry.type === "Saida" && entry.origin !== "CreditCardStatement")
    .reduce((total, entry) => total + entry.amount, 0);

  const cardExpenses = cardTransactions.reduce((total, transaction) => total + transaction.amount, 0);
  return cashExpenses + cardExpenses;
}
