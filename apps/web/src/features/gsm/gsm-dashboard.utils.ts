import type { GsmNumber } from "@/lib/api";

function getDaysInMonth(year: number, monthIndex: number) {
  return new Date(Date.UTC(year, monthIndex + 1, 0)).getUTCDate();
}

function parseDateOnly(value: string | null | undefined) {
  if (!value) {
    return null;
  }

  const date = new Date(`${value}T00:00:00Z`);
  return Number.isNaN(date.getTime()) ? null : date;
}

function formatUnit(value: number, singular: string, plural: string) {
  return value === 1 ? `${value} ${singular}` : `${value} ${plural}`;
}

export function extractGsmDigits(value: string) {
  return value.replace(/\D/g, "").slice(0, 13);
}

export function normalizeGsmNumber(value: string) {
  const digits = extractGsmDigits(value);
  if (digits.length === 11) {
    return `55${digits}`;
  }

  if (digits.length === 13) {
    return digits;
  }

  return null;
}

export function isValidGsmNumber(value: string) {
  const digits = extractGsmDigits(value);
  return digits.length === 11 || digits.length === 13;
}

export function formatGsmNumber(value: string) {
  const digits = extractGsmDigits(value);

  if (digits.length === 0) {
    return "";
  }

  if (digits.length <= 11) {
    const ddd = digits.slice(0, 2);
    const first = digits.slice(2, 7);
    const last = digits.slice(7, 11);

    if (digits.length <= 2) {
      return ddd ? `(${ddd}` : "";
    }

    if (digits.length <= 7) {
      return `(${ddd}) ${first}`;
    }

    return `(${ddd}) ${first}${last ? `-${last}` : ""}`;
  }

  const ddi = digits.slice(0, 2);
  const ddd = digits.slice(2, 4);
  const first = digits.slice(4, 9);
  const last = digits.slice(9, 13);

  if (digits.length <= 4) {
    return `+${ddi}${ddd ? ` (${ddd}` : ""}`;
  }

  if (digits.length <= 9) {
    return `+${ddi} (${ddd}) ${first}`;
  }

  return `+${ddi} (${ddd}) ${first}${last ? `-${last}` : ""}`;
}

export function maskGsmNumberInput(value: string) {
  return formatGsmNumber(extractGsmDigits(value));
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

export function formatRechargeElapsed(value: string | null | undefined, referenceDate = new Date()) {
  const start = parseDateOnly(value);
  if (!start) {
    return "Sem recarga registrada";
  }

  const end = new Date(Date.UTC(
    referenceDate.getUTCFullYear(),
    referenceDate.getUTCMonth(),
    referenceDate.getUTCDate(),
  ));

  if (start.getTime() >= end.getTime()) {
    return "0 dias";
  }

  let years = end.getUTCFullYear() - start.getUTCFullYear();
  let months = end.getUTCMonth() - start.getUTCMonth();
  let days = end.getUTCDate() - start.getUTCDate();

  if (days < 0) {
    months -= 1;
    const previousMonthIndex = (end.getUTCMonth() + 11) % 12;
    const previousMonthYear = previousMonthIndex === 11 ? end.getUTCFullYear() - 1 : end.getUTCFullYear();
    days += getDaysInMonth(previousMonthYear, previousMonthIndex);
  }

  if (months < 0) {
    years -= 1;
    months += 12;
  }

  if (years > 0) {
    return months > 0
      ? `${formatUnit(years, "ano", "anos")} e ${formatUnit(months, "mês", "meses")}`
      : formatUnit(years, "ano", "anos");
  }

  if (months > 0) {
    return days > 0
      ? `${formatUnit(months, "mês", "meses")} e ${formatUnit(days, "dia", "dias")}`
      : formatUnit(months, "mês", "meses");
  }

  return formatUnit(days, "dia", "dias");
}

export function sortGsmNumbersByUrgency(items: GsmNumber[]) {
  return [...items].sort((left, right) => {
    if (!left.lastRechargeOn && !right.lastRechargeOn) {
      return left.title.localeCompare(right.title);
    }

    if (!left.lastRechargeOn) {
      return -1;
    }

    if (!right.lastRechargeOn) {
      return 1;
    }

    const leftTime = parseDateOnly(left.lastRechargeOn)?.getTime() ?? Number.MAX_SAFE_INTEGER;
    const rightTime = parseDateOnly(right.lastRechargeOn)?.getTime() ?? Number.MAX_SAFE_INTEGER;

    if (leftTime !== rightTime) {
      return leftTime - rightTime;
    }

    return left.title.localeCompare(right.title);
  });
}
