import { describe, expect, it } from "vitest";
import type { GsmNumber } from "@/lib/api";
import {
  formatGsmNumber,
  formatGsmMonthlyCost,
  formatGsmPlanLabel,
  formatRechargeElapsed,
  getGsmRechargeProjection,
  maskGsmNumberInput,
  normalizeGsmNumber,
  parseGsmMonthlyCostInput,
  sortGsmNumbersByUrgency,
} from "./gsm-dashboard.utils";

describe("gsm dashboard utils", () => {
  it("formats gsm numbers with or without explicit ddi", () => {
    expect(formatGsmNumber("11912345678")).toBe("(11) 91234-5678");
    expect(formatGsmNumber("5511912345678")).toBe("+55 (11) 91234-5678");
  });

  it("masks input and normalizes gsm numbers", () => {
    expect(maskGsmNumberInput("5511912345678")).toBe("+55 (11) 91234-5678");
    expect(normalizeGsmNumber("(11) 91234-5678")).toBe("5511912345678");
    expect(normalizeGsmNumber("+44 (11) 91234-5678")).toBe("4411912345678");
  });

  it("formats plan labels and monthly costs", () => {
    expect(formatGsmPlanLabel("PrePago")).toBe("Pré-pago");
    expect(formatGsmPlanLabel("PosPago")).toBe("Pós-pago");
    expect(formatGsmMonthlyCost(59.9)).toBe("R$\u00a059,90");
    expect(formatGsmMonthlyCost(null)).toBe("Não informado");
  });

  it("parses monthly cost input", () => {
    expect(parseGsmMonthlyCostInput("")).toBeNull();
    expect(parseGsmMonthlyCostInput("R$ 59,90")).toBe(59.9);
    expect(parseGsmMonthlyCostInput("1.234,56")).toBe(1234.56);
  });

  it("formats elapsed time since recharge", () => {
    const referenceDate = new Date("2026-06-23T12:00:00Z");

    expect(formatRechargeElapsed("2026-06-13", referenceDate)).toBe("10 dias");
    expect(formatRechargeElapsed("2026-04-16", referenceDate)).toBe("2 meses e 7 dias");
    expect(formatRechargeElapsed("2025-03-23", referenceDate)).toBe("1 ano e 3 meses");
    expect(formatRechargeElapsed(null, referenceDate)).toBe("Sem recarga registrada");
  });

  it("calculates the next recharge projection and overdue days", () => {
    const projection = getGsmRechargeProjection(
      {
        id: "1",
        title: "Linha A",
        number: "5511888888888",
        description: null,
        plan: "PrePago",
        monthlyCost: null,
        daysWithoutRecharge: 30,
        acquiredOn: "2026-01-01",
        lastRechargeOn: "2026-06-10",
        status: "Ativo",
        createdByMemberId: null,
        createdAt: "2026-01-01T00:00:00Z",
        updatedAt: "2026-01-01T00:00:00Z",
        canEdit: true,
        canDelete: true,
      },
      new Date("2026-07-20T12:00:00Z"),
    );

    expect(projection).toEqual({
      nextRechargeOn: "2026-07-10",
      isOverdue: true,
      overdueDays: 10,
    });
  });

  it("sorts gsm numbers by recharge urgency", () => {
    const items: GsmNumber[] = [
      {
        id: "3",
        title: "Linha B",
        number: "5511999999999",
        description: null,
        acquiredOn: "2026-01-01",
        lastRechargeOn: "2026-06-20",
        daysWithoutRecharge: 30,
        status: "Ativo",
        createdByMemberId: null,
        createdAt: "2026-01-01T00:00:00Z",
        updatedAt: "2026-01-01T00:00:00Z",
        canEdit: true,
        canDelete: true,
      },
      {
        id: "1",
        title: "Linha A",
        number: "5511888888888",
        description: null,
        acquiredOn: "2026-01-01",
        lastRechargeOn: null,
        daysWithoutRecharge: 30,
        status: "Ativo",
        createdByMemberId: null,
        createdAt: "2026-01-01T00:00:00Z",
        updatedAt: "2026-01-01T00:00:00Z",
        canEdit: true,
        canDelete: true,
      },
      {
        id: "2",
        title: "Linha C",
        number: "5511777777777",
        description: null,
        acquiredOn: "2026-01-01",
        lastRechargeOn: "2026-06-10",
        daysWithoutRecharge: null,
        status: "Ativo",
        createdByMemberId: null,
        createdAt: "2026-01-01T00:00:00Z",
        updatedAt: "2026-01-01T00:00:00Z",
        canEdit: true,
        canDelete: true,
      },
    ];

    expect(sortGsmNumbersByUrgency(items).map((item) => item.id)).toEqual(["1", "3", "2"]);
  });
});
