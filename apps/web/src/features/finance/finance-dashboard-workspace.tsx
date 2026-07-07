"use client";

import { type FormEvent, type KeyboardEvent as ReactKeyboardEvent, type ReactNode, useEffect, useMemo, useRef, useState } from "react";
import {
  BadgeDollarSign,
  Building2,
  CalendarRange,
  CreditCard,
  Landmark,
  Pencil,
  Plus,
  RefreshCw,
  Repeat2,
  Wrench,
  Trash2,
  Wallet,
} from "lucide-react";
import type {
  Asset,
  AssetType,
  AssetValuation,
  CreditCardAccount,
  CreditCardStatement,
  CreditCardTransaction,
  FinanceCategory,
  FinanceEntry,
  FinanceEntryOrigin,
  FinanceEntryType,
  FinanceRecurringTemplate,
  FinanceRecurrence,
} from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Textarea } from "@/components/ui/textarea";
import { DeleteConfirmationDialog } from "@/features/workspace/delete-confirmation-dialog";
import {
  EmptyState,
  Field,
  HomePitWorkspaceShell,
  LoadingState,
  Notice,
} from "@/features/workspace/homepit-workspace-shell";
import type {
  AssetFormInput,
  AssetValuationFormInput,
  CreditCardAccountFormInput,
  CreditCardStatementFormInput,
  CreditCardTransactionFormInput,
  FinanceCategoryFormInput,
  FinanceDashboardController,
  FinanceEntryFormInput,
  FinanceRecurringTemplateFormInput,
} from "./use-finance-dashboard";
import {
  filterFinanceEntries,
  formatCurrency,
  formatDateOnlyInputValue,
  formatDateOnlyPtBr,
  formatMonthLabel,
  groupFinanceEntries,
  parseCurrencyInput,
  type FinanceEntryFilters,
} from "./finance-dashboard.utils";

const monthOptions = [
  { value: 1, label: "Janeiro" },
  { value: 2, label: "Fevereiro" },
  { value: 3, label: "Março" },
  { value: 4, label: "Abril" },
  { value: 5, label: "Maio" },
  { value: 6, label: "Junho" },
  { value: 7, label: "Julho" },
  { value: 8, label: "Agosto" },
  { value: 9, label: "Setembro" },
  { value: 10, label: "Outubro" },
  { value: 11, label: "Novembro" },
  { value: 12, label: "Dezembro" },
];

const defaultFilters: FinanceEntryFilters = {
  search: "",
  type: "all",
  verified: "all",
  origin: "all",
  universeId: "all",
  projectId: "all",
  groupBy: "type",
};

type EntryDialogState = { mode: "create" | "edit"; entryType: FinanceEntryType; entry?: FinanceEntry | null };
type InlineCellMode = "idle" | "editing" | "saving" | "syncing";
type InlineSelectOption = { value: string; label: string; disabled?: boolean };

function InlineSyncLabel({ syncing, label = "Sincronizando..." }: { syncing: boolean; label?: string }) {
  if (!syncing) {
    return null;
  }

  return (
    <span className="inline-flex items-center gap-2 text-xs font-medium text-muted-foreground">
      <RefreshCw className="size-3 animate-spin" />
      {label}
    </span>
  );
}

function InlineCellStatus({ mode }: { mode: InlineCellMode }) {
  if (mode === "saving") {
    return <RefreshCw className="size-3 animate-spin text-muted-foreground" aria-hidden />;
  }

  if (mode === "syncing") {
    return <RefreshCw className="size-3 animate-spin text-primary" aria-hidden />;
  }

  return null;
}

function InlineInputCell<T>({
  value,
  displayValue,
  ariaLabel,
  canEdit,
  rowBusy,
  isSyncing,
  inputType = "text",
  placeholder,
  displayClassName,
  toDraft,
  fromDraft,
  onSave,
}: {
  value: T;
  displayValue: string;
  ariaLabel: string;
  canEdit: boolean;
  rowBusy: boolean;
  isSyncing: boolean;
  inputType?: "date" | "number" | "text";
  placeholder?: string;
  displayClassName?: string;
  toDraft: (value: T) => string;
  fromDraft: (draft: string) => { value: T; error?: string };
  onSave: (value: T) => Promise<void>;
}) {
  const [mode, setMode] = useState<InlineCellMode>("idle");
  const [draft, setDraft] = useState(() => toDraft(value));
  const [error, setError] = useState<string | null>(null);
  const buttonRef = useRef<HTMLButtonElement | null>(null);
  const inputRef = useRef<HTMLInputElement | null>(null);
  const committingRef = useRef(false);

  useEffect(() => {
    if (mode === "idle" || mode === "syncing") {
      setDraft(toDraft(value));
    }
  }, [mode, toDraft, value]);

  useEffect(() => {
    if (mode === "editing") {
      window.requestAnimationFrame(() => {
        inputRef.current?.focus();
        if (inputType !== "date") {
          inputRef.current?.select();
        }
      });
    }
  }, [inputType, mode]);

  useEffect(() => {
    if (mode === "syncing" && !isSyncing) {
      setMode("idle");
      setError(null);
      buttonRef.current?.focus();
    }
  }, [isSyncing, mode]);

  function resetToIdle() {
    setDraft(toDraft(value));
    setError(null);
    setMode("idle");
    buttonRef.current?.focus();
  }

  async function commit(nextDraft = draft) {
    if (!canEdit || rowBusy || committingRef.current) {
      return;
    }

    const parsed = fromDraft(nextDraft);
    if (parsed.error) {
      setError(parsed.error);
      setMode("editing");
      window.requestAnimationFrame(() => inputRef.current?.focus());
      return;
    }

    if (Object.is(parsed.value, value)) {
      resetToIdle();
      return;
    }

    committingRef.current = true;
    setMode("saving");
    setError(null);

    try {
      await onSave(parsed.value);
      setMode("syncing");
    } catch (exception) {
      setMode("editing");
      setError(exception instanceof Error ? exception.message : "Não foi possível salvar.");
      window.requestAnimationFrame(() => inputRef.current?.focus());
    } finally {
      committingRef.current = false;
    }
  }

  function handleKeyDown(event: ReactKeyboardEvent<HTMLInputElement>) {
    if (event.key === "Enter") {
      event.preventDefault();
      void commit();
      return;
    }

    if (event.key === "Escape") {
      event.preventDefault();
      resetToIdle();
    }
  }

  if (mode === "editing" || mode === "saving") {
    return (
      <div className="space-y-1">
        <div className="flex items-center gap-2 rounded-md border border-border/70 bg-surface px-2 py-1">
          <Input
            ref={inputRef}
            type={inputType}
            value={draft}
            onChange={(event) => setDraft(event.target.value)}
            onBlur={() => {
              if (!committingRef.current) {
                void commit();
              }
            }}
            onKeyDown={handleKeyDown}
            disabled={mode === "saving"}
            placeholder={placeholder}
            aria-label={ariaLabel}
            className="h-8 border-0 px-0 shadow-none focus-visible:ring-0"
          />
          <InlineCellStatus mode={mode} />
        </div>
        {error ? <p className="text-xs text-danger">{error}</p> : null}
      </div>
    );
  }

  return canEdit ? (
    <button
      ref={buttonRef}
      type="button"
      onClick={() => {
        if (!rowBusy) {
          setError(null);
          setMode("editing");
        }
      }}
      disabled={rowBusy}
      className="inline-flex w-full items-center gap-2 rounded-md border border-transparent px-2 py-1 text-left text-sm transition hover:border-border/70 hover:bg-surface-muted/60 disabled:cursor-not-allowed disabled:opacity-60"
      aria-label={ariaLabel}
    >
      <span className={displayValue ? `truncate ${displayClassName ?? "text-foreground"}` : "truncate text-muted-foreground"}>
        {displayValue || placeholder || "Não informado"}
      </span>
      <InlineCellStatus mode={mode} />
    </button>
  ) : (
    <span className={displayValue ? displayClassName ?? "text-foreground" : "text-muted-foreground"}>
      {displayValue || placeholder || "Não informado"}
    </span>
  );
}

function InlineSelectCell({
  value,
  displayValue,
  ariaLabel,
  canEdit,
  rowBusy,
  isSyncing,
  options,
  onSave,
}: {
  value: string;
  displayValue: string;
  ariaLabel: string;
  canEdit: boolean;
  rowBusy: boolean;
  isSyncing: boolean;
  options: InlineSelectOption[];
  onSave: (value: string) => Promise<void>;
}) {
  const [mode, setMode] = useState<InlineCellMode>("idle");
  const [draft, setDraft] = useState(value);
  const [error, setError] = useState<string | null>(null);
  const buttonRef = useRef<HTMLButtonElement | null>(null);
  const selectRef = useRef<HTMLSelectElement | null>(null);
  const committingRef = useRef(false);

  useEffect(() => {
    if (mode === "idle" || mode === "syncing") {
      setDraft(value);
    }
  }, [mode, value]);

  useEffect(() => {
    if (mode === "editing") {
      window.requestAnimationFrame(() => selectRef.current?.focus());
    }
  }, [mode]);

  useEffect(() => {
    if (mode === "syncing" && !isSyncing) {
      setMode("idle");
      setError(null);
      buttonRef.current?.focus();
    }
  }, [isSyncing, mode]);

  function resetToIdle() {
    setDraft(value);
    setError(null);
    setMode("idle");
    buttonRef.current?.focus();
  }

  async function commit(nextValue: string) {
    if (!canEdit || rowBusy || committingRef.current) {
      return;
    }

    if (nextValue === value) {
      resetToIdle();
      return;
    }

    committingRef.current = true;
    setMode("saving");
    setError(null);

    try {
      await onSave(nextValue);
      setMode("syncing");
    } catch (exception) {
      setMode("editing");
      setError(exception instanceof Error ? exception.message : "Não foi possível salvar.");
      window.requestAnimationFrame(() => selectRef.current?.focus());
    } finally {
      committingRef.current = false;
    }
  }

  if (mode === "editing" || mode === "saving") {
    return (
      <div className="space-y-1">
        <div className="flex items-center gap-2 rounded-md border border-border/70 bg-surface px-2 py-1">
          <Select
            ref={selectRef}
            value={draft}
            onChange={(event) => {
              setDraft(event.target.value);
              void commit(event.target.value);
            }}
            onBlur={() => {
              if (!committingRef.current) {
                resetToIdle();
              }
            }}
            onKeyDown={(event) => {
              if (event.key === "Escape") {
                event.preventDefault();
                resetToIdle();
              }
            }}
            disabled={mode === "saving"}
            aria-label={ariaLabel}
            className="h-8 border-0 px-0 shadow-none focus-visible:ring-0"
          >
            {options.map((option) => (
              <option key={option.value} value={option.value} disabled={option.disabled}>
                {option.label}
              </option>
            ))}
          </Select>
          <InlineCellStatus mode={mode} />
        </div>
        {error ? <p className="text-xs text-danger">{error}</p> : null}
      </div>
    );
  }

  return canEdit ? (
    <button
      ref={buttonRef}
      type="button"
      onClick={() => {
        if (!rowBusy) {
          setError(null);
          setMode("editing");
        }
      }}
      disabled={rowBusy}
      className="inline-flex w-full items-center gap-2 rounded-md border border-transparent px-2 py-1 text-left text-sm transition hover:border-border/70 hover:bg-surface-muted/60 disabled:cursor-not-allowed disabled:opacity-60"
      aria-label={ariaLabel}
    >
      <span className={displayValue ? "truncate text-foreground" : "truncate text-muted-foreground"}>{displayValue || "Não informado"}</span>
      <InlineCellStatus mode={mode} />
    </button>
  ) : (
    <span className={displayValue ? "text-foreground" : "text-muted-foreground"}>{displayValue || "Não informado"}</span>
  );
}

function InlineCheckboxCell({
  checked,
  checkedLabel,
  uncheckedLabel,
  ariaLabel,
  canEdit,
  rowBusy,
  isSyncing,
  onSave,
}: {
  checked: boolean;
  checkedLabel: string;
  uncheckedLabel: string;
  ariaLabel: string;
  canEdit: boolean;
  rowBusy: boolean;
  isSyncing: boolean;
  onSave: (checked: boolean) => Promise<void>;
}) {
  const [mode, setMode] = useState<InlineCellMode>("idle");
  const buttonRef = useRef<HTMLButtonElement | null>(null);

  useEffect(() => {
    if (mode === "syncing" && !isSyncing) {
      setMode("idle");
      buttonRef.current?.focus();
    }
  }, [isSyncing, mode]);

  async function handleToggle() {
    if (!canEdit || rowBusy || mode === "saving") {
      return;
    }

    setMode("saving");
    try {
      await onSave(!checked);
      setMode("syncing");
    } catch {
      setMode("idle");
    }
  }

  return canEdit ? (
    <button
      ref={buttonRef}
      type="button"
      onClick={() => void handleToggle()}
      disabled={rowBusy || mode === "saving"}
      className="inline-flex items-center gap-2 rounded-md border border-transparent px-2 py-1 text-sm transition hover:border-border/70 hover:bg-surface-muted/60 disabled:cursor-not-allowed disabled:opacity-60"
      aria-label={ariaLabel}
    >
      <input type="checkbox" checked={checked} readOnly tabIndex={-1} className="pointer-events-none" />
      <span>{checked ? checkedLabel : uncheckedLabel}</span>
      <InlineCellStatus mode={mode} />
    </button>
  ) : (
    <label className="inline-flex items-center gap-2 text-sm">
      <input type="checkbox" checked={checked} readOnly tabIndex={-1} />
      {checked ? checkedLabel : uncheckedLabel}
    </label>
  );
}

export function FinanceDashboardWorkspace({ dashboard }: { dashboard: FinanceDashboardController }) {
  const [filters, setFilters] = useState<FinanceEntryFilters>(defaultFilters);
  const [categoriesDialogOpen, setCategoriesDialogOpen] = useState(false);
  const [categoryDialog, setCategoryDialog] = useState<FinanceCategory | null | "create">(null);
  const [entryDialog, setEntryDialog] = useState<EntryDialogState | null>(null);
  const [templateDialog, setTemplateDialog] = useState<FinanceRecurringTemplate | null | "create">(null);
  const [recurringTemplatesDialogOpen, setRecurringTemplatesDialogOpen] = useState(false);
  const [assetDialog, setAssetDialog] = useState<Asset | null | "create">(null);
  const [valuationDialog, setValuationDialog] = useState<{ asset: Asset; valuation?: AssetValuation | null } | null>(null);
  const [cardDialog, setCardDialog] = useState<CreditCardAccount | null | "create">(null);
  const [transactionDialog, setTransactionDialog] = useState<CreditCardTransaction | null | "create">(null);
  const [statementDialog, setStatementDialog] = useState<CreditCardStatement | null | "create">(null);
  const [generateDialogOpen, setGenerateDialogOpen] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<
    | { kind: "category"; id: string; name: string }
    | { kind: "entry"; id: string; name: string }
    | { kind: "template"; id: string; name: string }
    | { kind: "asset"; id: string; name: string }
    | { kind: "valuation"; assetId: string; id: string; name: string }
    | { kind: "card"; id: string; name: string }
    | { kind: "transaction"; id: string; name: string }
    | { kind: "statement"; id: string; name: string }
    | null
  >(null);
  const [savingRows, setSavingRows] = useState<Record<string, boolean>>({});

  const entries = dashboard.periodDetail?.entries ?? [];
  const filteredEntries = useMemo(() => filterFinanceEntries(entries, filters), [entries, filters]);
  const groupedEntries = useMemo(() => groupFinanceEntries(filteredEntries, filters.groupBy), [filteredEntries, filters.groupBy]);

  const periodSummary = dashboard.periodDetail?.summary;
  const headerStats = [
    { label: "Lançamentos", value: entries.length },
    { label: "Recorrências", value: dashboard.recurringTemplates.length },
    { label: "Cartões", value: dashboard.creditCardAccounts.length },
    { label: "Bens", value: dashboard.assets.length },
  ];

  const selectableYears = useMemo(() => {
    const years = new Set<number>([dashboard.activeYear - 1, dashboard.activeYear, dashboard.activeYear + 1]);
    for (const period of dashboard.financePeriods) {
      years.add(period.year);
    }

    return Array.from(years).sort((left, right) => right - left);
  }, [dashboard.activeYear, dashboard.financePeriods]);

  const selectedCard = dashboard.creditCardAccounts.find((card) => card.id === dashboard.selectedCreditCardId) ?? null;
  const openTransactions = dashboard.creditCardTransactions.filter(
    (transaction) =>
      !transaction.creditCardStatementId ||
      transaction.creditCardStatementId === (typeof statementDialog === "object" ? statementDialog?.id : null),
  );

  function setRowSaving(rowKey: string, saving: boolean) {
    setSavingRows((current) => {
      if (saving) {
        return { ...current, [rowKey]: true };
      }

      if (!current[rowKey]) {
        return current;
      }

      const next = { ...current };
      delete next[rowKey];
      return next;
    });
  }

  async function runRowSave(rowKey: string, action: () => Promise<void>) {
    setRowSaving(rowKey, true);
    try {
      await action();
    } finally {
      setRowSaving(rowKey, false);
    }
  }

  function isRowSaving(rowKey: string) {
    return Boolean(savingRows[rowKey]);
  }

  function buildEntryInput(entry: FinanceEntry, overrides: Partial<FinanceEntryFormInput> = {}): FinanceEntryFormInput {
    return {
      year: entry.year,
      month: entry.month,
      title: entry.title,
      notes: entry.notes ?? "",
      amount: entry.amount,
      type: entry.type,
      verified: entry.verified,
      referenceDate: entry.referenceDate,
      recurringTemplateId: entry.recurringTemplateId ?? null,
      categoryId: entry.categoryId ?? null,
      universeId: entry.universeId ?? null,
      projectId: entry.projectId ?? null,
      ...overrides,
    };
  }

  function buildRecurringTemplateInput(
    template: FinanceRecurringTemplate,
    overrides: Partial<FinanceRecurringTemplateFormInput> = {},
  ): FinanceRecurringTemplateFormInput {
    return {
      title: template.title,
      notes: template.notes ?? "",
      type: template.type,
      defaultAmount: template.defaultAmount,
      recurrence: template.recurrence,
      dayOfMonth: template.dayOfMonth ?? null,
      monthOfYear: template.monthOfYear ?? null,
      isActive: template.isActive,
      categoryId: template.categoryId ?? null,
      universeId: template.universeId ?? null,
      projectId: template.projectId ?? null,
      ...overrides,
    };
  }

  function buildTransactionInput(
    transaction: CreditCardTransaction,
    overrides: Partial<CreditCardTransactionFormInput> = {},
  ): CreditCardTransactionFormInput {
    return {
      title: transaction.title,
      merchant: transaction.merchant ?? "",
      amount: transaction.amount,
      purchasedOn: transaction.purchasedOn,
      notes: transaction.notes ?? "",
      categoryId: transaction.categoryId ?? null,
      universeId: transaction.universeId ?? null,
      projectId: transaction.projectId ?? null,
      externalSource: transaction.externalSource ?? "",
      externalReference: transaction.externalReference ?? "",
      ...overrides,
    };
  }

  function buildStatementInput(
    statement: CreditCardStatement,
    overrides: Partial<CreditCardStatementFormInput> = {},
  ): CreditCardStatementFormInput {
    return {
      closingDate: statement.closingDate,
      dueDate: statement.dueDate,
      notes: statement.notes ?? "",
      transactionIds: dashboard.creditCardTransactions
        .filter((transaction) => transaction.creditCardStatementId === statement.id)
        .map((transaction) => transaction.id),
      externalSource: statement.externalSource ?? "",
      externalReference: statement.externalReference ?? "",
      ...overrides,
    };
  }

  function buildAssetValuationInput(
    valuation: AssetValuation,
    overrides: Partial<AssetValuationFormInput> = {},
  ): AssetValuationFormInput {
    return {
      referenceYear: valuation.referenceYear,
      label: valuation.label,
      amount: valuation.amount,
      notes: valuation.notes ?? "",
      ...overrides,
    };
  }

  function handleGenerateClick() {
    if (dashboard.periodDetail?.exists) {
      setGenerateDialogOpen(true);
      return;
    }

    void dashboard.generatePeriod("missingOnly");
  }

  async function handleDeleteConfirm() {
    if (!deleteTarget) {
      return;
    }

    if (deleteTarget.kind === "category") {
      await dashboard.deleteCategory(deleteTarget.id);
    } else if (deleteTarget.kind === "entry") {
      await dashboard.deleteEntry(deleteTarget.id);
    } else if (deleteTarget.kind === "template") {
      await dashboard.deleteRecurringTemplate(deleteTarget.id);
    } else if (deleteTarget.kind === "asset") {
      await dashboard.deleteAsset(deleteTarget.id);
    } else if (deleteTarget.kind === "valuation") {
      await dashboard.deleteAssetValuation(deleteTarget.assetId, deleteTarget.id);
    } else if (deleteTarget.kind === "card") {
      await dashboard.deleteCreditCardAccount(deleteTarget.id);
    } else if (deleteTarget.kind === "transaction") {
      await dashboard.deleteCreditCardTransaction(deleteTarget.id);
    } else if (deleteTarget.kind === "statement") {
      await dashboard.deleteCreditCardStatement(deleteTarget.id);
    }

    setDeleteTarget(null);
  }

  return (
    <>
      <HomePitWorkspaceShell
        controller={{
          session: dashboard.session,
          activeHouseholdId: dashboard.activeHouseholdId,
          activeHousehold: dashboard.activeHousehold,
          members: dashboard.members,
          theme: dashboard.theme,
          sidebarCollapsed: dashboard.sidebarCollapsed,
          loading: dashboard.loading,
          error: dashboard.error,
          canShareHousehold: dashboard.canShareHousehold,
          canManageHousehold: dashboard.canManageHousehold,
          editingHousehold: dashboard.editingHousehold,
          isHouseholdDialogOpen: dashboard.isHouseholdDialogOpen,
          isShareDialogOpen: dashboard.isShareDialogOpen,
          setError: dashboard.setError,
          setSidebarCollapsed: dashboard.setSidebarCollapsed,
          setTheme: dashboard.setTheme,
          handleHouseholdChange: dashboard.handleHouseholdChange,
          handleLogout: dashboard.handleLogout,
          refreshHouseholds: dashboard.refreshHouseholds,
          refreshWorkspace: dashboard.refreshWorkspace,
          openCreateHousehold: dashboard.openCreateHousehold,
          openEditHousehold: dashboard.openEditHousehold,
          openShareHousehold: dashboard.openShareHousehold,
          closeCommonModal: dashboard.closeCommonModal,
          createHousehold: dashboard.createHousehold,
          updateHousehold: dashboard.updateHousehold,
          deleteHousehold: dashboard.deleteHousehold,
          shareHousehold: dashboard.shareHousehold,
          updateProfile: dashboard.updateProfile,
        }}
        activeModule="finance"
        subtitle={dashboard.subtitle}
        visibleCount={entries.length}
        visibleLabel="lançamentos"
        headerStats={headerStats}
      >
        <Card>
          <CardContent className="flex flex-col gap-4 p-5 sm:p-6 lg:flex-row lg:items-end lg:justify-between">
            <div className="max-w-2xl">
              <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">Financeiro</p>
              <h1 className="mt-2 text-2xl font-semibold text-foreground">Operação financeira da casa</h1>
              <p className="mt-2 text-sm leading-6 text-muted-foreground">
                Controle o caixa do mês, mantenha recorrências, acompanhe cartões e preserve o patrimônio em um único lugar.
              </p>
            </div>

            <div className="flex w-full flex-wrap items-center gap-2 lg:w-auto lg:justify-end">
              <Select
                className="w-full sm:w-[8rem] lg:shrink-0"
                value={String(dashboard.activeYear)}
                onChange={(event) => dashboard.setActivePeriod(Number(event.target.value), dashboard.activeMonth)}
                aria-label="Ano do período"
              >
                {selectableYears.map((year) => (
                  <option key={year} value={year}>
                    {year}
                  </option>
                ))}
              </Select>
              <Select
                className="w-full sm:w-[8.5rem] lg:shrink-0"
                value={String(dashboard.activeMonth)}
                onChange={(event) => dashboard.setActivePeriod(dashboard.activeYear, Number(event.target.value))}
                aria-label="Mês do período"
              >
                {monthOptions.map((month) => (
                  <option key={month.value} value={month.value}>
                    {month.label}
                  </option>
                ))}
              </Select>
              <Button variant="secondary" className="lg:shrink-0" onClick={() => void dashboard.refreshWorkspace()}>
                <RefreshCw />
                Atualizar
              </Button>
              <Button className="lg:shrink-0" onClick={handleGenerateClick}>
                <CalendarRange />
                Inserir Recorrências
              </Button>
              <Button variant="secondary" className="lg:shrink-0" onClick={() => setRecurringTemplatesDialogOpen(true)}>
                <Wrench />
                Recorrências
              </Button>
              <Button variant="secondary" className="lg:shrink-0" onClick={() => setCategoriesDialogOpen(true)}>
                <Wrench />
                Categorias
              </Button>
            </div>
          </CardContent>
        </Card>

        {dashboard.loading && !dashboard.periodDetail ? (
          <LoadingState
            title="Carregando financeiro"
            description="Estamos reunindo o período mensal, recorrências, cartões e patrimônio da casa."
            icon={<Wallet className="size-5 animate-pulse" />}
          />
        ) : (
          <>
            <div className="grid gap-4 xl:grid-cols-4">
              <MetricCard
                label="Período"
                value={formatMonthLabel(dashboard.activeYear, dashboard.activeMonth)}
                helper={dashboard.periodDetail?.exists ? "Período existente" : "Período ainda não gerado"}
              />
              <MetricCard label="Entradas" value={formatCurrency(periodSummary?.totalIncome ?? 0, "R$ 0,00")} helper="Fluxo de caixa do mês" />
              <MetricCard label="Saídas" value={formatCurrency(periodSummary?.totalExpense ?? 0, "R$ 0,00")} helper="Inclui a fatura consolidada" />
              <MetricCard
                label="Saldo"
                value={formatCurrency(periodSummary?.cashBalance ?? 0, "R$ 0,00")}
                helper={`${periodSummary?.pendingVerificationEntries ?? 0} pendentes de verificação`}
                accent={(periodSummary?.cashBalance ?? 0) < 0 ? "danger" : "success"}
              />
            </div>

            <Card>
              <CardHeader className="border-b border-border/60 pb-4">
                <CardTitle className="text-lg">Resumo</CardTitle>
              </CardHeader>
              <CardContent className="grid gap-4 p-4 lg:grid-cols-3">
                <InfoBlock
                  label="Gasto analítico do mês"
                  value={formatCurrency(periodSummary?.analyticalExpenseTotal ?? 0, "R$ 0,00")}
                  helper="Caixa sem fatura consolidada + compras de cartão do mês"
                />
                <InfoBlock
                  label="Compras em cartão"
                  value={String(periodSummary?.cardPurchaseCount ?? 0)}
                  helper="Quantidade de compras no período analítico"
                />
                <InfoBlock label="Verificados" value={`${periodSummary?.verifiedEntries ?? 0}/${entries.length}`} helper="Lançamentos revisados no caixa mensal" />
              </CardContent>
            </Card>

            <Card>
              <CardHeader className="border-b border-border/60 pb-4">
                <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
                  <div className="space-y-1">
                    <CardTitle className="text-lg">Caixa</CardTitle>
                    <InlineSyncLabel syncing={dashboard.syncingSections.cash} label="Sincronizando caixa..." />
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <Button variant="secondary" onClick={() => setEntryDialog({ mode: "create", entryType: "Entrada" })}>
                      <Plus />
                      Nova entrada
                    </Button>
                    <Button onClick={() => setEntryDialog({ mode: "create", entryType: "Saida" })}>
                      <Plus />
                      Nova saída
                    </Button>
                  </div>
                </div>
              </CardHeader>
              <CardContent className="space-y-4 p-4">
                <div className="grid gap-3 md:grid-cols-3 xl:grid-cols-6">
                  <Input
                    value={filters.search}
                    onChange={(event) => setFilters((current) => ({ ...current, search: event.target.value }))}
                    placeholder="Buscar lançamento"
                  />
                  <Select value={filters.type} onChange={(event) => setFilters((current) => ({ ...current, type: event.target.value as FinanceEntryFilters["type"] }))}>
                    <option value="all">Todos os tipos</option>
                    <option value="Entrada">Entradas</option>
                    <option value="Saida">Saídas</option>
                  </Select>
                  <Select value={filters.verified} onChange={(event) => setFilters((current) => ({ ...current, verified: event.target.value as FinanceEntryFilters["verified"] }))}>
                    <option value="all">Todos</option>
                    <option value="verified">Verificados</option>
                    <option value="pending">Pendentes</option>
                  </Select>
                  <Select value={filters.origin} onChange={(event) => setFilters((current) => ({ ...current, origin: event.target.value as FinanceEntryOrigin | "all" }))}>
                    <option value="all">Todas as origens</option>
                    <option value="Manual">Manual</option>
                    <option value="RecurringTemplate">Recorrência</option>
                    <option value="CreditCardStatement">Fatura</option>
                  </Select>
                  <Select value={filters.universeId} onChange={(event) => setFilters((current) => ({ ...current, universeId: event.target.value }))}>
                    <option value="all">Todos os universos</option>
                    {dashboard.universes.map((universe) => (
                      <option key={universe.id} value={universe.id}>
                        {universe.name}
                      </option>
                    ))}
                  </Select>
                  <Select value={filters.projectId} onChange={(event) => setFilters((current) => ({ ...current, projectId: event.target.value }))}>
                    <option value="all">Todos os projetos</option>
                    {dashboard.projects.map((project) => (
                      <option key={project.id} value={project.id}>
                        {project.name}
                      </option>
                    ))}
                  </Select>
                </div>

                <div className="flex flex-wrap items-center gap-2">
                  <span className="text-sm font-medium text-muted-foreground">Agrupar por</span>
                  <Select value={filters.groupBy} onChange={(event) => setFilters((current) => ({ ...current, groupBy: event.target.value as FinanceEntryFilters["groupBy"] }))}>
                    <option value="none">Sem agrupamento</option>
                    <option value="type">Tipo</option>
                    <option value="universe">Universo</option>
                    <option value="project">Projeto</option>
                  </Select>
                </div>

                {groupedEntries.length === 0 ? (
                  <EmptyState
                    icon={<BadgeDollarSign className="size-5" />}
                    title="Nenhum lançamento encontrado"
                    description="Ajuste os filtros ou insira o período para começar a operar o caixa."
                  />
                ) : (
                  <div className="space-y-4">
                    {groupedEntries.map((group) => (
                      <Card key={group.key}>
                        <CardHeader className="border-b border-border/60 pb-3">
                          <CardTitle className="text-base">{group.label}</CardTitle>
                        </CardHeader>
                        <CardContent className="p-0">
                          <div className="overflow-x-auto">
                            <Table>
                              <TableHeader>
                                <TableRow className="border-b border-border/60 bg-surface-muted hover:bg-surface-muted">
                                  <TableHead className="min-w-[180px]">Item</TableHead>
                                  <TableHead>Tipo</TableHead>
                                  <TableHead>Origem</TableHead>
                                  <TableHead>Categoria</TableHead>
                                  <TableHead>Data</TableHead>
                                  <TableHead>Projeto</TableHead>
                                  <TableHead>Valor</TableHead>
                                  <TableHead>Verificado</TableHead>
                                  <TableHead className="min-w-[220px] text-right">Ações</TableHead>
                                </TableRow>
                              </TableHeader>
                              <TableBody>
                                {group.entries.map((entry) => {
                                  const rowKey = `entry:${entry.id}`;
                                  const rowBusy = isRowSaving(rowKey);
                                  const canInlineEdit = entry.canEdit && entry.origin !== "CreditCardStatement";

                                  return (
                                    <TableRow key={entry.id}>
                                      <TableCell>
                                        <div className="space-y-1">
                                          <InlineInputCell
                                            value={entry.title}
                                            displayValue={entry.title}
                                            ariaLabel={`Editar título do lançamento ${entry.title}`}
                                            canEdit={canInlineEdit}
                                            rowBusy={rowBusy}
                                            isSyncing={dashboard.syncingSections.cash}
                                            placeholder="Sem título"
                                            toDraft={(current) => current}
                                            fromDraft={(draft) => {
                                              const trimmed = draft.trim();
                                              return trimmed ? { value: trimmed } : { value: draft, error: "Informe o título do lançamento." };
                                            }}
                                            onSave={(title) =>
                                              runRowSave(rowKey, async () => {
                                                await dashboard.updateEntry(entry.id, buildEntryInput(entry, { title }), { silentSuccess: true });
                                              })
                                            }
                                          />
                                          {entry.notes ? <p className="px-2 text-sm text-muted-foreground">{entry.notes}</p> : null}
                                        </div>
                                      </TableCell>
                                      <TableCell>{entry.type === "Entrada" ? "Entrada" : "Saída"}</TableCell>
                                      <TableCell>{formatOrigin(entry.origin)}</TableCell>
                                      <TableCell>
                                        <InlineSelectCell
                                          value={entry.categoryId ?? "none"}
                                          displayValue={entry.categoryName ?? "Sem categoria"}
                                          ariaLabel={`Editar categoria do lançamento ${entry.title}`}
                                          canEdit={canInlineEdit}
                                          rowBusy={rowBusy}
                                          isSyncing={dashboard.syncingSections.cash}
                                          options={[
                                            { value: "none", label: "Sem categoria" },
                                            ...dashboard.categories.map((category) => ({ value: category.id, label: category.name })),
                                          ]}
                                          onSave={(categoryId) =>
                                            runRowSave(rowKey, async () => {
                                              await dashboard.updateEntry(
                                                entry.id,
                                                buildEntryInput(entry, { categoryId: categoryId === "none" ? null : categoryId }),
                                                { silentSuccess: true },
                                              );
                                            })
                                          }
                                        />
                                      </TableCell>
                                      <TableCell>
                                        <InlineInputCell
                                          value={entry.referenceDate}
                                          displayValue={formatDateOnlyPtBr(entry.referenceDate)}
                                          ariaLabel={`Editar data do lançamento ${entry.title}`}
                                          canEdit={canInlineEdit}
                                          rowBusy={rowBusy}
                                          isSyncing={dashboard.syncingSections.cash}
                                          inputType="date"
                                          toDraft={(current) => current}
                                          fromDraft={(draft) =>
                                            draft.startsWith(`${entry.year}-${String(entry.month).padStart(2, "0")}-`)
                                              ? { value: draft }
                                              : { value: draft, error: "A data deve permanecer dentro do período atual." }
                                          }
                                          onSave={(referenceDate) =>
                                            runRowSave(rowKey, async () => {
                                              await dashboard.updateEntry(entry.id, buildEntryInput(entry, { referenceDate }), { silentSuccess: true });
                                            })
                                          }
                                        />
                                      </TableCell>
                                      <TableCell>{entry.projectName ?? entry.universeName ?? "Sem classificação"}</TableCell>
                                      <TableCell className={`font-medium ${entry.type === "Entrada" ? "text-success" : "text-danger"}`}>
                                        <InlineInputCell
                                          value={entry.amount}
                                          displayValue={formatCurrency(entry.amount)}
                                          ariaLabel={`Editar valor do lançamento ${entry.title}`}
                                          canEdit={canInlineEdit}
                                          rowBusy={rowBusy}
                                          isSyncing={dashboard.syncingSections.cash}
                                          placeholder="R$ 0,00"
                                          displayClassName={entry.type === "Entrada" ? "text-success" : "text-danger"}
                                          toDraft={(current) => formatCurrency(current)}
                                          fromDraft={(draft) => {
                                            const parsed = parseCurrencyInput(draft);
                                            if (parsed == null || parsed < 0) {
                                              return { value: entry.amount, error: "Informe um valor válido para o lançamento." };
                                            }

                                            return { value: parsed };
                                          }}
                                          onSave={(amount) =>
                                            runRowSave(rowKey, async () => {
                                              await dashboard.updateEntry(entry.id, buildEntryInput(entry, { amount }), { silentSuccess: true });
                                            })
                                          }
                                        />
                                      </TableCell>
                                      <TableCell>
                                        <InlineCheckboxCell
                                          checked={entry.verified}
                                          checkedLabel="Sim"
                                          uncheckedLabel="Não"
                                          ariaLabel={`Alternar verificação do lançamento ${entry.title}`}
                                          canEdit={canInlineEdit}
                                          rowBusy={rowBusy}
                                          isSyncing={dashboard.syncingSections.cash}
                                          onSave={(verified) =>
                                            runRowSave(rowKey, async () => {
                                              await dashboard.updateEntry(entry.id, buildEntryInput(entry, { verified }), { silentSuccess: true });
                                            })
                                          }
                                        />
                                      </TableCell>
                                      <TableCell className="text-right">
                                        <div className="flex flex-wrap justify-end gap-2">
                                          <Button
                                            variant="secondary"
                                            size="sm"
                                            onClick={() => setEntryDialog({ mode: "edit", entryType: entry.type, entry })}
                                            disabled={!entry.canEdit || rowBusy}
                                          >
                                            <Pencil />
                                            Editar
                                          </Button>
                                          <Button
                                            variant="ghost"
                                            size="sm"
                                            onClick={() => setDeleteTarget({ kind: "entry", id: entry.id, name: entry.title })}
                                            disabled={!entry.canDelete || rowBusy}
                                          >
                                            <Trash2 />
                                            Excluir
                                          </Button>
                                        </div>
                                      </TableCell>
                                    </TableRow>
                                  );
                                })}
                              </TableBody>
                            </Table>
                          </div>
                        </CardContent>
                      </Card>
                    ))}
                  </div>
                )}
              </CardContent>
            </Card>

            <Card>
              <CardHeader className="border-b border-border/60 pb-4">
                <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
                  <div className="space-y-1">
                    <CardTitle className="text-lg">Cartões</CardTitle>
                    <InlineSyncLabel
                      syncing={dashboard.syncingSections.cardTransactions || dashboard.syncingSections.cardStatements}
                      label="Sincronizando cartões..."
                    />
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <Button variant="secondary" onClick={() => setCardDialog("create")}>
                      <Plus />
                      Novo cartão
                    </Button>
                    <Button onClick={() => setTransactionDialog("create")} disabled={!selectedCard}>
                      <Plus />
                      Nova compra
                    </Button>
                    <Button onClick={() => setStatementDialog("create")} disabled={!selectedCard}>
                      <Plus />
                      Fechar fatura
                    </Button>
                  </div>
                </div>
              </CardHeader>
              <CardContent className="grid gap-4 p-4 xl:grid-cols-[320px_minmax(0,1fr)]">
                <div className="space-y-3">
                  {dashboard.creditCardAccounts.length === 0 ? (
                    <EmptyState
                      icon={<CreditCard className="size-5" />}
                      title="Nenhum cartão cadastrado"
                      description="Crie o primeiro cartão para registrar compras e fechar faturas."
                    />
                  ) : (
                    dashboard.creditCardAccounts.map((card) => (
                      <button
                        key={card.id}
                        type="button"
                        className={`w-full rounded-[18px] border px-4 py-3 text-left transition ${dashboard.selectedCreditCardId === card.id ? "border-primary bg-highlight text-accent-foreground" : "border-border/70 bg-surface-muted hover:bg-surface"}`}
                        onClick={() => dashboard.setSelectedCreditCardId(card.id)}
                      >
                        <div className="flex items-start justify-between gap-3">
                          <div>
                            <p className="font-semibold">{card.name}</p>
                            <p className="text-sm opacity-80">
                              {card.brand ?? "Sem bandeira"}
                              {card.lastFourDigits ? ` • ${card.lastFourDigits}` : ""}
                            </p>
                          </div>
                          <div className="text-right text-sm">
                            <p>{card.openTransactionCount} abertas</p>
                            <p>{formatCurrency(card.openTransactionTotal, "R$ 0,00")}</p>
                          </div>
                        </div>
                      </button>
                    ))
                  )}
                </div>

                <div className="space-y-4">
                  {selectedCard ? (
                    <>
                      <Card>
                        <CardHeader className="border-b border-border/60 pb-3">
                          <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
                            <CardTitle className="text-base">
                              {selectedCard.name}
                              {selectedCard.lastFourDigits ? ` • ${selectedCard.lastFourDigits}` : ""}
                            </CardTitle>
                            <div className="flex flex-wrap gap-2">
                              <Button variant="secondary" size="sm" onClick={() => setCardDialog(selectedCard)} disabled={!selectedCard.canEdit}>
                                <Pencil />
                                Editar
                              </Button>
                              <Button
                                variant="ghost"
                                size="sm"
                                onClick={() => setDeleteTarget({ kind: "card", id: selectedCard.id, name: selectedCard.name })}
                                disabled={!selectedCard.canDelete}
                              >
                                <Trash2 />
                                Excluir
                              </Button>
                            </div>
                          </div>
                        </CardHeader>
                        <CardContent className="grid gap-3 p-4 md:grid-cols-3">
                          <InfoBlock label="Fechamento" value={`Dia ${selectedCard.closingDay}`} />
                          <InfoBlock label="Vencimento" value={`Dia ${selectedCard.dueDay}`} />
                          <InfoBlock label="Aberto" value={formatCurrency(selectedCard.openTransactionTotal, "R$ 0,00")} helper={`${selectedCard.openTransactionCount} compras sem fatura`} />
                        </CardContent>
                      </Card>

                      <Card>
                        <CardHeader className="border-b border-border/60 pb-3">
                          <div className="space-y-1">
                            <CardTitle className="text-base">Compras</CardTitle>
                            <InlineSyncLabel syncing={dashboard.syncingSections.cardTransactions} label="Sincronizando compras..." />
                          </div>
                        </CardHeader>
                        <CardContent className="p-0">
                          {dashboard.cardDetailsLoading ? (
                            <div className="p-4">
                              <LoadingState title="Carregando compras" description="Buscando as compras e as faturas do cartão selecionado." icon={<CreditCard className="size-5 animate-pulse" />} />
                            </div>
                          ) : (
                            <div className="overflow-x-auto">
                              <Table>
                                <TableHeader>
                                  <TableRow className="border-b border-border/60 bg-surface-muted hover:bg-surface-muted">
                                    <TableHead className="min-w-[180px]">Compra</TableHead>
                                    <TableHead>Data</TableHead>
                                    <TableHead>Categoria</TableHead>
                                    <TableHead>Classificação</TableHead>
                                    <TableHead>Fatura</TableHead>
                                    <TableHead>Valor</TableHead>
                                    <TableHead className="min-w-[200px] text-right">Ações</TableHead>
                                  </TableRow>
                                </TableHeader>
                                <TableBody>
                                {dashboard.creditCardTransactions.length === 0 ? (
                                  <TableRow>
                                    <TableCell colSpan={7} className="py-8 text-center text-sm text-muted-foreground">
                                      Nenhuma compra registrada neste cartão.
                                    </TableCell>
                                  </TableRow>
                                ) : (
                                    dashboard.creditCardTransactions.map((transaction) => {
                                      const rowKey = `transaction:${transaction.id}`;
                                      const rowBusy = isRowSaving(rowKey);

                                      return (
                                        <TableRow key={transaction.id}>
                                          <TableCell>
                                            <div className="space-y-1">
                                              <InlineInputCell
                                                value={transaction.title}
                                                displayValue={transaction.title}
                                                ariaLabel={`Editar título da compra ${transaction.title}`}
                                                canEdit={transaction.canEdit}
                                                rowBusy={rowBusy}
                                                isSyncing={dashboard.syncingSections.cardTransactions || dashboard.syncingSections.cardStatements}
                                                toDraft={(current) => current}
                                                fromDraft={(draft) => {
                                                  const trimmed = draft.trim();
                                                  return trimmed ? { value: trimmed } : { value: draft, error: "Informe o título da compra." };
                                                }}
                                                onSave={(title) =>
                                                  runRowSave(rowKey, async () => {
                                                    await dashboard.updateCreditCardTransaction(
                                                      transaction.id,
                                                      buildTransactionInput(transaction, { title }),
                                                      { silentSuccess: true },
                                                    );
                                                  })
                                                }
                                              />
                                              {transaction.merchant ? <p className="px-2 text-sm text-muted-foreground">{transaction.merchant}</p> : null}
                                            </div>
                                          </TableCell>
                                          <TableCell>
                                            <InlineInputCell
                                              value={transaction.purchasedOn}
                                              displayValue={formatDateOnlyPtBr(transaction.purchasedOn)}
                                              ariaLabel={`Editar data da compra ${transaction.title}`}
                                              canEdit={transaction.canEdit}
                                              rowBusy={rowBusy}
                                              isSyncing={dashboard.syncingSections.cardTransactions || dashboard.syncingSections.cardStatements}
                                              inputType="date"
                                              toDraft={(current) => current}
                                              fromDraft={(draft) => (draft ? { value: draft } : { value: draft, error: "Informe a data da compra." })}
                                              onSave={(purchasedOn) =>
                                                runRowSave(rowKey, async () => {
                                                  await dashboard.updateCreditCardTransaction(
                                                    transaction.id,
                                                    buildTransactionInput(transaction, { purchasedOn }),
                                                    { silentSuccess: true },
                                                  );
                                                })
                                              }
                                            />
                                          </TableCell>
                                          <TableCell>
                                            <InlineSelectCell
                                              value={transaction.categoryId ?? "none"}
                                              displayValue={transaction.categoryName ?? "Sem categoria"}
                                              ariaLabel={`Editar categoria da compra ${transaction.title}`}
                                              canEdit={transaction.canEdit}
                                              rowBusy={rowBusy}
                                              isSyncing={dashboard.syncingSections.cardTransactions || dashboard.syncingSections.cardStatements}
                                              options={[
                                                { value: "none", label: "Sem categoria" },
                                                ...dashboard.categories.map((category) => ({ value: category.id, label: category.name })),
                                              ]}
                                              onSave={(categoryId) =>
                                                runRowSave(rowKey, async () => {
                                                  await dashboard.updateCreditCardTransaction(
                                                    transaction.id,
                                                    buildTransactionInput(transaction, { categoryId: categoryId === "none" ? null : categoryId }),
                                                    { silentSuccess: true },
                                                  );
                                                })
                                              }
                                            />
                                          </TableCell>
                                          <TableCell>{transaction.projectName ?? transaction.universeName ?? "Sem classificação"}</TableCell>
                                          <TableCell>{transaction.creditCardStatementId ? "Fechada" : "Em aberto"}</TableCell>
                                          <TableCell className="font-medium text-foreground">
                                            <InlineInputCell
                                              value={transaction.amount}
                                              displayValue={formatCurrency(transaction.amount)}
                                              ariaLabel={`Editar valor da compra ${transaction.title}`}
                                              canEdit={transaction.canEdit}
                                              rowBusy={rowBusy}
                                              isSyncing={dashboard.syncingSections.cardTransactions || dashboard.syncingSections.cardStatements}
                                              placeholder="R$ 0,00"
                                              toDraft={(current) => formatCurrency(current)}
                                              fromDraft={(draft) => {
                                                const parsed = parseCurrencyInput(draft);
                                                if (parsed == null || parsed <= 0) {
                                                  return { value: transaction.amount, error: "Informe um valor positivo para a compra." };
                                                }

                                                return { value: parsed };
                                              }}
                                              onSave={(amount) =>
                                                runRowSave(rowKey, async () => {
                                                  await dashboard.updateCreditCardTransaction(
                                                    transaction.id,
                                                    buildTransactionInput(transaction, { amount }),
                                                    { silentSuccess: true },
                                                  );
                                                })
                                              }
                                            />
                                          </TableCell>
                                          <TableCell className="text-right">
                                            <div className="flex flex-wrap justify-end gap-2">
                                              <Button variant="secondary" size="sm" onClick={() => setTransactionDialog(transaction)} disabled={!transaction.canEdit || rowBusy}>
                                                <Pencil />
                                                Editar
                                              </Button>
                                              <Button
                                                variant="ghost"
                                                size="sm"
                                                onClick={() => setDeleteTarget({ kind: "transaction", id: transaction.id, name: transaction.title })}
                                                disabled={!transaction.canDelete || rowBusy}
                                              >
                                                <Trash2 />
                                                Excluir
                                              </Button>
                                            </div>
                                          </TableCell>
                                        </TableRow>
                                      );
                                    })
                                  )}
                                </TableBody>
                              </Table>
                            </div>
                          )}
                        </CardContent>
                      </Card>

                      <Card>
                        <CardHeader className="border-b border-border/60 pb-3">
                          <div className="space-y-1">
                            <CardTitle className="text-base">Faturas</CardTitle>
                            <InlineSyncLabel syncing={dashboard.syncingSections.cardStatements} label="Sincronizando faturas..." />
                          </div>
                        </CardHeader>
                        <CardContent className="p-0">
                          <div className="overflow-x-auto">
                            <Table>
                              <TableHeader>
                                <TableRow className="border-b border-border/60 bg-surface-muted hover:bg-surface-muted">
                                  <TableHead>Fechamento</TableHead>
                                  <TableHead>Vencimento</TableHead>
                                  <TableHead>Compras</TableHead>
                                  <TableHead>Valor</TableHead>
                                  <TableHead className="min-w-[200px] text-right">Ações</TableHead>
                                </TableRow>
                              </TableHeader>
                              <TableBody>
                                {dashboard.creditCardStatements.length === 0 ? (
                                  <TableRow>
                                    <TableCell colSpan={5} className="py-8 text-center text-sm text-muted-foreground">
                                      Nenhuma fatura fechada neste cartão.
                                    </TableCell>
                                  </TableRow>
                                ) : (
                                  dashboard.creditCardStatements.map((statement) => {
                                    const rowKey = `statement:${statement.id}`;
                                    const rowBusy = isRowSaving(rowKey);

                                    return (
                                      <TableRow key={statement.id}>
                                        <TableCell>
                                          <InlineInputCell
                                            value={statement.closingDate}
                                            displayValue={formatDateOnlyPtBr(statement.closingDate)}
                                            ariaLabel={`Editar fechamento da fatura ${formatDateOnlyPtBr(statement.dueDate)}`}
                                            canEdit={statement.canEdit}
                                            rowBusy={rowBusy}
                                            isSyncing={dashboard.syncingSections.cardStatements}
                                            inputType="date"
                                            toDraft={(current) => current}
                                            fromDraft={(draft) => (draft ? { value: draft } : { value: draft, error: "Informe a data de fechamento." })}
                                            onSave={(closingDate) =>
                                              runRowSave(rowKey, async () => {
                                                await dashboard.updateCreditCardStatement(
                                                  statement.id,
                                                  buildStatementInput(statement, { closingDate }),
                                                  { silentSuccess: true },
                                                );
                                              })
                                            }
                                          />
                                        </TableCell>
                                        <TableCell>
                                          <InlineInputCell
                                            value={statement.dueDate}
                                            displayValue={formatDateOnlyPtBr(statement.dueDate)}
                                            ariaLabel={`Editar vencimento da fatura ${formatDateOnlyPtBr(statement.dueDate)}`}
                                            canEdit={statement.canEdit}
                                            rowBusy={rowBusy}
                                            isSyncing={dashboard.syncingSections.cardStatements}
                                            inputType="date"
                                            toDraft={(current) => current}
                                            fromDraft={(draft) => (draft ? { value: draft } : { value: draft, error: "Informe a data de vencimento." })}
                                            onSave={(dueDate) =>
                                              runRowSave(rowKey, async () => {
                                                await dashboard.updateCreditCardStatement(
                                                  statement.id,
                                                  buildStatementInput(statement, { dueDate }),
                                                  { silentSuccess: true },
                                                );
                                              })
                                            }
                                          />
                                        </TableCell>
                                        <TableCell>{statement.transactionCount}</TableCell>
                                        <TableCell className="font-medium text-foreground">{formatCurrency(statement.totalAmount)}</TableCell>
                                        <TableCell className="text-right">
                                          <div className="flex flex-wrap justify-end gap-2">
                                            <Button variant="secondary" size="sm" onClick={() => setStatementDialog(statement)} disabled={!statement.canEdit || rowBusy}>
                                              <Pencil />
                                              Editar
                                            </Button>
                                            <Button
                                              variant="ghost"
                                              size="sm"
                                              onClick={() => setDeleteTarget({ kind: "statement", id: statement.id, name: `fatura ${formatDateOnlyPtBr(statement.dueDate)}` })}
                                              disabled={!statement.canDelete || rowBusy}
                                            >
                                              <Trash2 />
                                              Excluir
                                            </Button>
                                          </div>
                                        </TableCell>
                                      </TableRow>
                                    );
                                  })
                                )}
                              </TableBody>
                            </Table>
                          </div>
                        </CardContent>
                      </Card>
                    </>
                  ) : (
                    <EmptyState
                      icon={<CreditCard className="size-5" />}
                      title="Selecione um cartão"
                      description="Escolha um cartão para ver compras abertas, faturas e a integração com o caixa mensal."
                    />
                  )}
                </div>
              </CardContent>
            </Card>

            <Card>
              <CardHeader className="border-b border-border/60 pb-4">
                <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
                  <CardTitle className="text-lg">Patrimônio</CardTitle>
                  <Button onClick={() => setAssetDialog("create")}>
                    <Plus />
                    Novo bem
                  </Button>
                </div>
              </CardHeader>
              <CardContent className="grid gap-4 p-4 md:grid-cols-2 xl:grid-cols-3">
                {dashboard.assets.length === 0 ? (
                  <div className="md:col-span-2 xl:col-span-3">
                    <EmptyState
                      icon={<Landmark className="size-5" />}
                      title="Nenhum bem cadastrado"
                      description="Registre casa, carro e outros bens de alto valor para manter o contexto patrimonial da household."
                    />
                  </div>
                ) : (
                  dashboard.assets.map((asset) => (
                    <Card key={asset.id}>
                      <CardContent className="space-y-4 p-4">
                        <div className="flex items-start justify-between gap-3">
                          <div>
                            <p className="font-semibold text-foreground">{asset.title}</p>
                            <p className="text-sm text-muted-foreground">{formatAssetType(asset.type)}</p>
                          </div>
                          <span className={`rounded-full px-2.5 py-1 text-xs font-semibold ${asset.isPaidOff ? "bg-status-success-soft text-success" : "bg-status-warning-soft text-warning"}`}>
                            {asset.isPaidOff ? "Quitado" : "Em aberto"}
                          </span>
                        </div>
                        <InfoBlock label="Valor atual" value={formatCurrency(asset.currentValue)} />
                        <InfoBlock label="Divida restante" value={formatCurrency(asset.remainingDebt)} />
                        {asset.type === "Property" && asset.propertyDetails ? (
                          <InfoBlock
                            label="Imóvel"
                            value={asset.propertyDetails.propertyInscription ?? asset.propertyDetails.registryNumber ?? "Sem detalhes"}
                            helper={asset.propertyDetails.privateAreaSquareMeters ? `${asset.propertyDetails.privateAreaSquareMeters} m²` : undefined}
                          />
                        ) : null}
                        {asset.type === "Vehicle" && asset.vehicleDetails ? (
                          <InfoBlock
                            label="Veículo"
                            value={[asset.vehicleDetails.brand, asset.vehicleDetails.model].filter(Boolean).join(" ") || "Sem detalhes"}
                            helper={asset.vehicleDetails.yearModel ?? asset.vehicleDetails.renavam ?? undefined}
                          />
                        ) : null}
                        {asset.notes ? <p className="text-sm text-muted-foreground">{asset.notes}</p> : null}
                        <div className="flex flex-wrap gap-2">
                          <Button variant="secondary" size="sm" onClick={() => setAssetDialog(asset)} disabled={!asset.canEdit}>
                            <Pencil />
                            Editar
                          </Button>
                          <Button
                            variant="secondary"
                            size="sm"
                            onClick={() => {
                              void dashboard.loadAssetValuations(asset.id);
                              setValuationDialog({ asset, valuation: null });
                            }}
                          >
                            <Building2 />
                            Referências anuais
                          </Button>
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => setDeleteTarget({ kind: "asset", id: asset.id, name: asset.title })}
                            disabled={!asset.canDelete}
                          >
                            <Trash2 />
                            Excluir
                          </Button>
                        </div>
                      </CardContent>
                    </Card>
                  ))
                )}
              </CardContent>
            </Card>
          </>
        )}
      </HomePitWorkspaceShell>

      <EntryDialog
        open={Boolean(entryDialog)}
        entry={entryDialog?.entry ?? null}
        defaultEntryType={entryDialog?.entryType ?? "Saida"}
        activeYear={dashboard.activeYear}
        activeMonth={dashboard.activeMonth}
        categories={dashboard.categories}
        templates={dashboard.recurringTemplates}
        universes={dashboard.universes}
        projects={dashboard.projects}
        onOpenChange={(open) => !open && setEntryDialog(null)}
        onSave={async (input) => {
          if (entryDialog?.mode === "edit" && entryDialog.entry) {
            await dashboard.updateEntry(entryDialog.entry.id, input);
          } else {
            await dashboard.createEntry(input);
          }
          setEntryDialog(null);
        }}
      />

      <RecurringTemplateDialog
        open={templateDialog !== null}
        template={templateDialog && templateDialog !== "create" ? templateDialog : null}
        categories={dashboard.categories}
        universes={dashboard.universes}
        projects={dashboard.projects}
        onOpenChange={(open) => !open && setTemplateDialog(null)}
        onSave={async (input) => {
          if (templateDialog && templateDialog !== "create") {
            await dashboard.updateRecurringTemplate(templateDialog.id, input);
          } else {
            await dashboard.createRecurringTemplate(input);
          }
          setTemplateDialog(null);
        }}
      />

      <AssetDialog
        open={assetDialog !== null}
        asset={assetDialog && assetDialog !== "create" ? assetDialog : null}
        onOpenChange={(open) => !open && setAssetDialog(null)}
        onSave={async (input) => {
          if (assetDialog && assetDialog !== "create") {
            await dashboard.updateAsset(assetDialog.id, input);
          } else {
            await dashboard.createAsset(input);
          }
          setAssetDialog(null);
        }}
      />

      <AssetValuationDialog
        open={valuationDialog !== null}
        asset={valuationDialog?.asset ?? null}
        valuation={valuationDialog?.valuation ?? null}
        valuations={valuationDialog?.asset ? dashboard.assetValuations[valuationDialog.asset.id] ?? [] : []}
        loading={valuationDialog?.asset ? dashboard.assetValuationsLoadingFor === valuationDialog.asset.id : false}
        syncing={dashboard.syncingSections.assetValuations}
        isRowSaving={isRowSaving}
        onOpenChange={(open) => !open && setValuationDialog(null)}
        onCreate={async (input) => {
          if (!valuationDialog?.asset) {
            return;
          }

          await dashboard.createAssetValuation(valuationDialog.asset.id, input);
        }}
        onEdit={(valuation) => {
          if (!valuationDialog?.asset) {
            return;
          }

          setValuationDialog({ asset: valuationDialog.asset, valuation });
        }}
        onSave={async (input) => {
          if (!valuationDialog?.asset) {
            return;
          }

          if (valuationDialog.valuation) {
            await dashboard.updateAssetValuation(valuationDialog.asset.id, valuationDialog.valuation.id, input);
          } else {
            await dashboard.createAssetValuation(valuationDialog.asset.id, input);
          }

          if (valuationDialog.asset) {
            await dashboard.loadAssetValuations(valuationDialog.asset.id);
          }

          setValuationDialog(null);
        }}
        onInlineSave={async (valuation, overrides) => {
          if (!valuationDialog?.asset) {
            return;
          }

          await runRowSave(`valuation:${valuation.id}`, async () => {
            await dashboard.updateAssetValuation(
              valuationDialog.asset.id,
              valuation.id,
              buildAssetValuationInput(valuation, overrides),
              { silentSuccess: true },
            );
          });
        }}
        onDelete={(valuation) => {
          if (!valuationDialog?.asset) {
            return;
          }

          setDeleteTarget({
            kind: "valuation",
            assetId: valuationDialog.asset.id,
            id: valuation.id,
            name: `${valuation.label} ${valuation.referenceYear}`,
          });
        }}
      />

      <CreditCardAccountDialog
        open={cardDialog !== null}
        card={cardDialog && cardDialog !== "create" ? cardDialog : null}
        onOpenChange={(open) => !open && setCardDialog(null)}
        onSave={async (input) => {
          if (cardDialog && cardDialog !== "create") {
            await dashboard.updateCreditCardAccount(cardDialog.id, input);
          } else {
            await dashboard.createCreditCardAccount(input);
          }
          setCardDialog(null);
        }}
      />

      <CreditCardTransactionDialog
        open={transactionDialog !== null}
        transaction={transactionDialog && transactionDialog !== "create" ? transactionDialog : null}
        categories={dashboard.categories}
        universes={dashboard.universes}
        projects={dashboard.projects}
        onOpenChange={(open) => !open && setTransactionDialog(null)}
        onSave={async (input) => {
          if (transactionDialog && transactionDialog !== "create") {
            await dashboard.updateCreditCardTransaction(transactionDialog.id, input);
          } else {
            await dashboard.createCreditCardTransaction(input);
          }
          setTransactionDialog(null);
        }}
      />

      <CreditCardStatementDialog
        open={statementDialog !== null}
        statement={statementDialog && statementDialog !== "create" ? statementDialog : null}
        availableTransactions={openTransactions}
        onOpenChange={(open) => !open && setStatementDialog(null)}
        onSave={async (input) => {
          if (statementDialog && statementDialog !== "create") {
            await dashboard.updateCreditCardStatement(statementDialog.id, input);
          } else {
            await dashboard.createCreditCardStatement(input);
          }
          setStatementDialog(null);
        }}
      />

      <RecurringTemplatesDialog
        open={recurringTemplatesDialogOpen}
        templates={dashboard.recurringTemplates}
        categories={dashboard.categories}
        syncing={dashboard.syncingSections.recurringTemplates}
        isRowSaving={isRowSaving}
        onOpenChange={(open) => {
          setRecurringTemplatesDialogOpen(open);
          if (!open) {
            setTemplateDialog(null);
          }
        }}
        onCreateNew={() => {
          setTemplateDialog("create");
        }}
        onEditTemplate={(template) => {
          setTemplateDialog(template);
        }}
        onDeleteTemplate={(template) => setDeleteTarget({ kind: "template", id: template.id, name: template.title })}
        onInlineSaveTemplate={async (template, overrides) => {
          await runRowSave(`template:${template.id}`, async () => {
            await dashboard.updateRecurringTemplate(template.id, buildRecurringTemplateInput(template, overrides), { silentSuccess: true });
          });
        }}
      />

      <CategoriesDialog
        open={categoriesDialogOpen}
        categories={dashboard.categories}
        syncing={dashboard.syncingSections.categories}
        isRowSaving={isRowSaving}
        onOpenChange={(open) => {
          setCategoriesDialogOpen(open);
          if (!open) {
            setCategoryDialog(null);
          }
        }}
        onCreateNew={() => {
          setCategoryDialog("create");
        }}
        onEditCategory={(category) => {
          setCategoryDialog(category);
        }}
        onDeleteCategory={(category) => setDeleteTarget({ kind: "category", id: category.id, name: category.name })}
        onInlineSaveCategory={async (category, name) => {
          await runRowSave(`category:${category.id}`, async () => {
            await dashboard.updateCategory(category.id, { name }, { silentSuccess: true });
          });
        }}
      />

      <GeneratePeriodDialog
        open={generateDialogOpen}
        periodLabel={formatMonthLabel(dashboard.activeYear, dashboard.activeMonth)}
        onOpenChange={setGenerateDialogOpen}
        onMissingOnly={async () => {
          await dashboard.generatePeriod("missingOnly");
          setGenerateDialogOpen(false);
        }}
        onDuplicateAll={async () => {
          await dashboard.generatePeriod("duplicateAll");
          setGenerateDialogOpen(false);
        }}
      />

      <CategoryDialog
        open={categoryDialog !== null}
        category={categoryDialog && categoryDialog !== "create" ? categoryDialog : null}
        onOpenChange={(open) => !open && setCategoryDialog(null)}
        onSave={async (input) => {
          if (categoryDialog && categoryDialog !== "create") {
            await dashboard.updateCategory(categoryDialog.id, input);
          } else {
            await dashboard.createCategory(input);
          }

          setCategoryDialog(null);
        }}
      />

      <DeleteConfirmationDialog
        open={Boolean(deleteTarget)}
        title={deleteTarget?.kind === "category" ? "Excluir categoria" : "Excluir registro"}
        description={
          deleteTarget?.kind === "category"
            ? "Essa ação remove a categoria personalizada e desvincula os registros que a utilizavam."
            : "Essa acao remove o registro selecionado."
        }
        confirmationTarget={deleteTarget?.name}
        confirmationLabel={`Digite ${deleteTarget?.name ?? ""} para confirmar`}
        confirmLabel="Excluir"
        impactItems={[
          deleteTarget?.kind === "category"
            ? "A exclusão é permanente e os lançamentos, recorrências e compras vinculados passam a ficar sem categoria."
            : "A exclusao e permanente e atualiza os totais e relacionamentos do modulo financeiro.",
        ]}
        onOpenChange={(open) => !open && setDeleteTarget(null)}
        onConfirm={handleDeleteConfirm}
      />
    </>
  );
}

function CategoryDialog({
  open,
  category,
  onOpenChange,
  onSave,
}: {
  open: boolean;
  category: FinanceCategory | null;
  onOpenChange: (open: boolean) => void;
  onSave: (input: FinanceCategoryFormInput) => Promise<void>;
}) {
  const [name, setName] = useState(category?.name ?? "");
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    setName(category?.name ?? "");
    setError(null);
    setSaving(false);
  }, [category, open]);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!name.trim()) {
      setError("Informe o nome da categoria.");
      return;
    }

    setSaving(true);
    setError(null);

    try {
      await onSave({ name: name.trim() });
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Não foi possível salvar a categoria.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{category ? "Editar categoria" : "Nova categoria"}</DialogTitle>
          <DialogDescription>Use categorias para organizar caixa, recorrências e compras de cartão dentro do módulo financeiro.</DialogDescription>
        </DialogHeader>
        <form className="space-y-4" onSubmit={handleSubmit}>
          {error ? <Notice tone="danger">{error}</Notice> : null}
          <Field label="Nome">
            <Input value={name} onChange={(event) => setName(event.target.value)} autoFocus />
          </Field>
          <DialogFooter>
            <Button variant="secondary" type="button" onClick={() => onOpenChange(false)} disabled={saving}>
              Cancelar
            </Button>
            <Button type="submit" disabled={saving}>
              Salvar categoria
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function CategoriesDialog({
  open,
  categories,
  syncing,
  isRowSaving,
  onOpenChange,
  onCreateNew,
  onEditCategory,
  onDeleteCategory,
  onInlineSaveCategory,
}: {
  open: boolean;
  categories: FinanceCategory[];
  syncing: boolean;
  isRowSaving: (rowKey: string) => boolean;
  onOpenChange: (open: boolean) => void;
  onCreateNew: () => void;
  onEditCategory: (category: FinanceCategory) => void;
  onDeleteCategory: (category: FinanceCategory) => void;
  onInlineSaveCategory: (category: FinanceCategory, name: string) => Promise<void>;
}) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="flex h-[92vh] w-[min(96vw,72rem)] max-w-none flex-col overflow-hidden p-0">
        <div className="flex flex-col gap-4 border-b border-border/60 p-5 sm:p-6 lg:flex-row lg:items-start lg:justify-between">
          <DialogHeader className="space-y-2">
            <DialogTitle>Categorias</DialogTitle>
            <DialogDescription>Gerencie as categorias padrão e personalizadas usadas no caixa, nas recorrências e nas compras de cartão.</DialogDescription>
            <InlineSyncLabel syncing={syncing} label="Sincronizando categorias..." />
          </DialogHeader>
          <Button onClick={onCreateNew}>
            <Plus />
            Nova categoria
          </Button>
        </div>
        <div className="flex-1 overflow-y-auto p-5 sm:p-6">
          {categories.length === 0 ? (
            <EmptyState
              icon={<Wrench className="size-5" />}
              title="Nenhuma categoria disponível"
              description="Crie categorias personalizadas para classificar caixa, recorrências e compras de cartão."
            />
          ) : (
            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow className="border-b border-border/60 bg-surface-muted hover:bg-surface-muted">
                    <TableHead className="min-w-[220px]">Nome</TableHead>
                    <TableHead>Tipo</TableHead>
                    <TableHead>Uso</TableHead>
                    <TableHead className="min-w-[220px] text-right">Ações</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {categories.map((category) => {
                    const rowKey = `category:${category.id}`;
                    const rowBusy = isRowSaving(rowKey);

                    return (
                      <TableRow key={category.id}>
                        <TableCell>
                          <InlineInputCell
                            value={category.name}
                            displayValue={category.name}
                            ariaLabel={`Editar nome da categoria ${category.name}`}
                            canEdit={category.canEdit}
                            rowBusy={rowBusy}
                            isSyncing={syncing}
                            toDraft={(current) => current}
                            fromDraft={(draft) => {
                              const trimmed = draft.trim();
                              return trimmed ? { value: trimmed } : { value: draft, error: "Informe o nome da categoria." };
                            }}
                            onSave={(name) => onInlineSaveCategory(category, name)}
                          />
                        </TableCell>
                        <TableCell>{category.isDefault ? "Padrão" : "Personalizada"}</TableCell>
                        <TableCell>{category.usageCount === 1 ? "1 uso no financeiro" : `${category.usageCount} usos no financeiro`}</TableCell>
                        <TableCell className="text-right">
                          <div className="flex flex-wrap justify-end gap-2">
                            <Button variant="secondary" size="sm" onClick={() => onEditCategory(category)} disabled={!category.canEdit || rowBusy}>
                              <Pencil />
                              Editar
                            </Button>
                            <Button variant="ghost" size="sm" onClick={() => onDeleteCategory(category)} disabled={!category.canDelete || rowBusy}>
                              <Trash2 />
                              Excluir
                            </Button>
                          </div>
                        </TableCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            </div>
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
}

function MetricCard({
  label,
  value,
  helper,
  accent = "default",
}: {
  label: string;
  value: string;
  helper?: string;
  accent?: "default" | "success" | "danger";
}) {
  return (
    <Card>
      <CardContent className="space-y-2 p-4">
        <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">{label}</p>
        <p className={`text-xl font-semibold ${accent === "success" ? "text-success" : accent === "danger" ? "text-danger" : "text-foreground"}`}>{value}</p>
        {helper ? <p className="text-sm text-muted-foreground">{helper}</p> : null}
      </CardContent>
    </Card>
  );
}

function InfoBlock({ label, value, helper }: { label: string; value: string; helper?: string }) {
  return (
    <div className="rounded-[18px] border border-border/70 bg-surface-muted/60 p-3">
      <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">{label}</p>
      <p className="mt-1 text-base font-semibold text-foreground">{value}</p>
      {helper ? <p className="mt-1 text-sm text-muted-foreground">{helper}</p> : null}
    </div>
  );
}

function formatOrigin(origin: FinanceEntryOrigin) {
  if (origin === "Manual") {
    return "Manual";
  }

  if (origin === "RecurringTemplate") {
    return "Recorrência";
  }

  return "Fatura";
}

function formatRecurrence(recurrence: FinanceRecurrence, dayOfMonth?: number | null, monthOfYear?: number | null) {
  if (recurrence === "Monthly") {
    return dayOfMonth ? `Mensal • dia ${dayOfMonth}` : "Mensal";
  }

  const monthLabel = monthOfYear ? monthOptions.find((item) => item.value === monthOfYear)?.label ?? monthOfYear : "mês indefinido";
  return dayOfMonth ? `Anual • ${monthLabel} • dia ${dayOfMonth}` : `Anual • ${monthLabel}`;
}

function formatAssetType(type: AssetType) {
  if (type === "Property") {
    return "Imóvel";
  }

  if (type === "Vehicle") {
    return "Veículo";
  }

  return "Outro bem";
}

function EntryDialog({
  open,
  entry,
  defaultEntryType,
  activeYear,
  activeMonth,
  categories,
  templates,
  universes,
  projects,
  onOpenChange,
  onSave,
}: {
  open: boolean;
  entry: FinanceEntry | null;
  defaultEntryType: FinanceEntryType;
  activeYear: number;
  activeMonth: number;
  categories: FinanceCategory[];
  templates: FinanceRecurringTemplate[];
  universes: { id: string; name: string }[];
  projects: { id: string; name: string; universeId: string }[];
  onOpenChange: (open: boolean) => void;
  onSave: (input: FinanceEntryFormInput) => Promise<void>;
}) {
  const [year, setYear] = useState(entry?.year ?? activeYear);
  const [month, setMonth] = useState(entry?.month ?? activeMonth);
  const [title, setTitle] = useState(entry?.title ?? "");
  const [notes, setNotes] = useState(entry?.notes ?? "");
  const [amount, setAmount] = useState(entry ? formatCurrency(entry.amount) : "");
  const [type, setType] = useState<FinanceEntryType>(entry?.type ?? defaultEntryType);
  const [verified, setVerified] = useState(entry?.verified ?? false);
  const [referenceDate, setReferenceDate] = useState(entry?.referenceDate ?? `${activeYear}-${String(activeMonth).padStart(2, "0")}-01`);
  const [recurringTemplateId, setRecurringTemplateId] = useState(entry?.recurringTemplateId ?? "none");
  const [categoryId, setCategoryId] = useState(entry?.categoryId ?? "none");
  const [universeId, setUniverseId] = useState(entry?.universeId ?? "none");
  const [projectId, setProjectId] = useState(entry?.projectId ?? "none");
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    setYear(entry?.year ?? activeYear);
    setMonth(entry?.month ?? activeMonth);
    setTitle(entry?.title ?? "");
    setNotes(entry?.notes ?? "");
    setAmount(entry ? formatCurrency(entry.amount) : "");
    setType(entry?.type ?? defaultEntryType);
    setVerified(entry?.verified ?? false);
    setReferenceDate(entry?.referenceDate ?? `${activeYear}-${String(activeMonth).padStart(2, "0")}-01`);
    setRecurringTemplateId(entry?.recurringTemplateId ?? "none");
    setCategoryId(entry?.categoryId ?? "none");
    setUniverseId(entry?.universeId ?? "none");
    setProjectId(entry?.projectId ?? "none");
    setError(null);
    setSaving(false);
  }, [activeMonth, activeYear, defaultEntryType, entry, open]);

  const scopedProjects = useMemo(
    () => (universeId === "none" ? projects : projects.filter((project) => project.universeId === universeId)),
    [projects, universeId],
  );

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const parsedAmount = parseCurrencyInput(amount);
    if (!title.trim()) {
      setError("Informe o título do lançamento.");
      return;
    }

    if (parsedAmount == null || parsedAmount < 0) {
      setError("Informe um valor válido para o lançamento.");
      return;
    }

    if (!referenceDate) {
      setError("Informe a data de referência.");
      return;
    }

    if (!referenceDate.startsWith(`${year}-${String(month).padStart(2, "0")}-`)) {
      setError("A data de referência deve pertencer ao período selecionado.");
      return;
    }

    setSaving(true);
    setError(null);

    try {
      await onSave({
        year,
        month,
        title: title.trim(),
        notes: notes.trim(),
        amount: parsedAmount,
        type,
        verified,
        referenceDate,
        recurringTemplateId: recurringTemplateId === "none" ? null : recurringTemplateId,
        categoryId: categoryId === "none" ? null : categoryId,
        universeId: universeId === "none" ? null : universeId,
        projectId: projectId === "none" ? null : projectId,
      });
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Não foi possível salvar o lançamento.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[88vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{entry ? "Editar lançamento" : "Novo lançamento"}</DialogTitle>
          <DialogDescription>Registre entradas e saídas do caixa mensal com classificação opcional por universo e projeto.</DialogDescription>
        </DialogHeader>
        <form className="space-y-4" onSubmit={handleSubmit}>
          {error ? <Notice tone="danger">{error}</Notice> : null}
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Ano">
              <Input type="number" min={2000} max={9999} value={year} onChange={(event) => setYear(Number(event.target.value))} />
            </Field>
            <Field label="Mês">
              <Select value={String(month)} onChange={(event) => setMonth(Number(event.target.value))}>
                {monthOptions.map((monthOption) => (
                  <option key={monthOption.value} value={monthOption.value}>
                    {monthOption.label}
                  </option>
                ))}
              </Select>
            </Field>
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Título">
              <Input value={title} onChange={(event) => setTitle(event.target.value)} />
            </Field>
            <Field label="Tipo">
              <Select value={type} onChange={(event) => setType(event.target.value as FinanceEntryType)}>
                <option value="Entrada">Entrada</option>
                <option value="Saida">Saída</option>
              </Select>
            </Field>
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Valor">
              <Input value={amount} onChange={(event) => setAmount(event.target.value)} placeholder="R$ 0,00" />
            </Field>
            <Field label="Data de referência">
              <Input type="date" value={referenceDate} onChange={(event) => setReferenceDate(event.target.value)} />
            </Field>
          </div>
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
            <Field label="Recorrência">
              <Select value={recurringTemplateId} onChange={(event) => setRecurringTemplateId(event.target.value)}>
                <option value="none">Sem recorrência</option>
                {templates.map((template) => (
                  <option key={template.id} value={template.id}>
                    {template.title}
                  </option>
                ))}
              </Select>
            </Field>
            <Field label="Categoria">
              <Select value={categoryId} onChange={(event) => setCategoryId(event.target.value)}>
                <option value="none">Sem categoria</option>
                {categories.map((category) => (
                  <option key={category.id} value={category.id}>
                    {category.name}
                  </option>
                ))}
              </Select>
            </Field>
            <Field label="Universo">
              <Select value={universeId} onChange={(event) => setUniverseId(event.target.value)}>
                <option value="none">Sem universo</option>
                {universes.map((universe) => (
                  <option key={universe.id} value={universe.id}>
                    {universe.name}
                  </option>
                ))}
              </Select>
            </Field>
            <Field label="Projeto">
              <Select value={projectId} onChange={(event) => setProjectId(event.target.value)}>
                <option value="none">Sem projeto</option>
                {scopedProjects.map((project) => (
                  <option key={project.id} value={project.id}>
                    {project.name}
                  </option>
                ))}
              </Select>
            </Field>
          </div>
          <Field label="Observações">
            <Textarea value={notes} onChange={(event) => setNotes(event.target.value)} rows={4} />
          </Field>
          <label className="inline-flex items-center gap-2 text-sm font-medium text-foreground">
            <input type="checkbox" checked={verified} onChange={(event) => setVerified(event.target.checked)} />
            Marcar como verificado
          </label>
          <DialogFooter>
            <Button variant="secondary" type="button" onClick={() => onOpenChange(false)} disabled={saving}>
              Cancelar
            </Button>
            <Button type="submit" disabled={saving}>
              Salvar lançamento
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function RecurringTemplateDialog({
  open,
  template,
  categories,
  universes,
  projects,
  onOpenChange,
  onSave,
}: {
  open: boolean;
  template: FinanceRecurringTemplate | null;
  categories: FinanceCategory[];
  universes: { id: string; name: string }[];
  projects: { id: string; name: string; universeId: string }[];
  onOpenChange: (open: boolean) => void;
  onSave: (input: FinanceRecurringTemplateFormInput) => Promise<void>;
}) {
  const [title, setTitle] = useState(template?.title ?? "");
  const [notes, setNotes] = useState(template?.notes ?? "");
  const [type, setType] = useState<FinanceEntryType>(template?.type ?? "Saida");
  const [defaultAmount, setDefaultAmount] = useState(template ? formatCurrency(template.defaultAmount) : "");
  const [recurrence, setRecurrence] = useState<FinanceRecurrence>(template?.recurrence ?? "Monthly");
  const [dayOfMonth, setDayOfMonth] = useState(template?.dayOfMonth?.toString() ?? "");
  const [monthOfYear, setMonthOfYear] = useState(template?.monthOfYear?.toString() ?? "");
  const [isActive, setIsActive] = useState(template?.isActive ?? true);
  const [categoryId, setCategoryId] = useState(template?.categoryId ?? "none");
  const [universeId, setUniverseId] = useState(template?.universeId ?? "none");
  const [projectId, setProjectId] = useState(template?.projectId ?? "none");
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    setTitle(template?.title ?? "");
    setNotes(template?.notes ?? "");
    setType(template?.type ?? "Saida");
    setDefaultAmount(template ? formatCurrency(template.defaultAmount) : "");
    setRecurrence(template?.recurrence ?? "Monthly");
    setDayOfMonth(template?.dayOfMonth?.toString() ?? "");
    setMonthOfYear(template?.monthOfYear?.toString() ?? "");
    setIsActive(template?.isActive ?? true);
    setCategoryId(template?.categoryId ?? "none");
    setUniverseId(template?.universeId ?? "none");
    setProjectId(template?.projectId ?? "none");
    setError(null);
    setSaving(false);
  }, [open, template]);

  const scopedProjects = useMemo(
    () => (universeId === "none" ? projects : projects.filter((project) => project.universeId === universeId)),
    [projects, universeId],
  );

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const parsedAmount = parseCurrencyInput(defaultAmount);
    const parsedDay = dayOfMonth.trim() ? Number(dayOfMonth) : null;
    const parsedMonth = recurrence === "Annual" && monthOfYear.trim() ? Number(monthOfYear) : null;

    if (!title.trim()) {
      setError("Informe o título da recorrência.");
      return;
    }

    if (parsedAmount == null || parsedAmount < 0) {
      setError("Informe um valor padrao valido.");
      return;
    }

    setSaving(true);
    setError(null);
    try {
      await onSave({
        title: title.trim(),
        notes: notes.trim(),
        type,
        defaultAmount: parsedAmount,
        recurrence,
        dayOfMonth: parsedDay,
        monthOfYear: recurrence === "Annual" ? parsedMonth : null,
        isActive,
        categoryId: categoryId === "none" ? null : categoryId,
        universeId: universeId === "none" ? null : universeId,
        projectId: projectId === "none" ? null : projectId,
      });
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Não foi possível salvar a recorrência.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[88vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{template ? "Editar recorrência" : "Nova recorrência"}</DialogTitle>
          <DialogDescription>Configure itens mensais e anuais para acelerar a geração do caixa mensal.</DialogDescription>
        </DialogHeader>
        <form className="space-y-4" onSubmit={handleSubmit}>
          {error ? <Notice tone="danger">{error}</Notice> : null}
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Título">
              <Input value={title} onChange={(event) => setTitle(event.target.value)} />
            </Field>
            <Field label="Tipo">
              <Select value={type} onChange={(event) => setType(event.target.value as FinanceEntryType)}>
                <option value="Entrada">Entrada</option>
                <option value="Saida">Saída</option>
              </Select>
            </Field>
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Valor padrão">
              <Input value={defaultAmount} onChange={(event) => setDefaultAmount(event.target.value)} />
            </Field>
            <Field label="Recorrência">
              <Select value={recurrence} onChange={(event) => setRecurrence(event.target.value as FinanceRecurrence)}>
                <option value="Monthly">Mensal</option>
                <option value="Annual">Anual</option>
              </Select>
            </Field>
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Dia de referência">
              <Input type="number" min={1} max={31} value={dayOfMonth} onChange={(event) => setDayOfMonth(event.target.value)} />
            </Field>
            <Field label="Mês anual">
              <Select value={monthOfYear || "none"} onChange={(event) => setMonthOfYear(event.target.value === "none" ? "" : event.target.value)} disabled={recurrence !== "Annual"}>
                <option value="none">Não se aplica</option>
                {monthOptions.map((month) => (
                  <option key={month.value} value={month.value}>
                    {month.label}
                  </option>
                ))}
              </Select>
            </Field>
          </div>
          <div className="grid gap-4 sm:grid-cols-3">
            <Field label="Categoria">
              <Select value={categoryId} onChange={(event) => setCategoryId(event.target.value)}>
                <option value="none">Sem categoria</option>
                {categories.map((category) => (
                  <option key={category.id} value={category.id}>
                    {category.name}
                  </option>
                ))}
              </Select>
            </Field>
            <Field label="Universo">
              <Select value={universeId} onChange={(event) => setUniverseId(event.target.value)}>
                <option value="none">Sem universo</option>
                {universes.map((universe) => (
                  <option key={universe.id} value={universe.id}>
                    {universe.name}
                  </option>
                ))}
              </Select>
            </Field>
            <Field label="Projeto">
              <Select value={projectId} onChange={(event) => setProjectId(event.target.value)}>
                <option value="none">Sem projeto</option>
                {scopedProjects.map((project) => (
                  <option key={project.id} value={project.id}>
                    {project.name}
                  </option>
                ))}
              </Select>
            </Field>
          </div>
          <Field label="Observações">
            <Textarea value={notes} onChange={(event) => setNotes(event.target.value)} rows={3} />
          </Field>
          <label className="inline-flex items-center gap-2 text-sm font-medium text-foreground">
            <input type="checkbox" checked={isActive} onChange={(event) => setIsActive(event.target.checked)} />
            Recorrência ativa
          </label>
          <DialogFooter>
            <Button variant="secondary" type="button" onClick={() => onOpenChange(false)} disabled={saving}>
              Cancelar
            </Button>
            <Button type="submit" disabled={saving}>
              Salvar recorrência
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function AssetDialog({
  open,
  asset,
  onOpenChange,
  onSave,
}: {
  open: boolean;
  asset: Asset | null;
  onOpenChange: (open: boolean) => void;
  onSave: (input: AssetFormInput) => Promise<void>;
}) {
  const [title, setTitle] = useState(asset?.title ?? "");
  const [type, setType] = useState<AssetType>(asset?.type ?? "Other");
  const [currentValue, setCurrentValue] = useState(asset?.currentValue != null ? formatCurrency(asset.currentValue) : "");
  const [remainingDebt, setRemainingDebt] = useState(asset?.remainingDebt != null ? formatCurrency(asset.remainingDebt) : "");
  const [isPaidOff, setIsPaidOff] = useState(asset?.isPaidOff ?? false);
  const [notes, setNotes] = useState(asset?.notes ?? "");
  const [registryNumber, setRegistryNumber] = useState(asset?.propertyDetails?.registryNumber ?? "");
  const [propertyInscription, setPropertyInscription] = useState(asset?.propertyDetails?.propertyInscription ?? "");
  const [privateAreaSquareMeters, setPrivateAreaSquareMeters] = useState(asset?.propertyDetails?.privateAreaSquareMeters?.toString() ?? "");
  const [debtCheckOn, setDebtCheckOn] = useState(asset?.propertyDetails?.debtCheckOn ?? "");
  const [brand, setBrand] = useState(asset?.vehicleDetails?.brand ?? "");
  const [model, setModel] = useState(asset?.vehicleDetails?.model ?? "");
  const [yearModel, setYearModel] = useState(asset?.vehicleDetails?.yearModel ?? "");
  const [renavam, setRenavam] = useState(asset?.vehicleDetails?.renavam ?? "");
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    setTitle(asset?.title ?? "");
    setType(asset?.type ?? "Other");
    setCurrentValue(asset?.currentValue != null ? formatCurrency(asset.currentValue) : "");
    setRemainingDebt(asset?.remainingDebt != null ? formatCurrency(asset.remainingDebt) : "");
    setIsPaidOff(asset?.isPaidOff ?? false);
    setNotes(asset?.notes ?? "");
    setRegistryNumber(asset?.propertyDetails?.registryNumber ?? "");
    setPropertyInscription(asset?.propertyDetails?.propertyInscription ?? "");
    setPrivateAreaSquareMeters(asset?.propertyDetails?.privateAreaSquareMeters?.toString() ?? "");
    setDebtCheckOn(asset?.propertyDetails?.debtCheckOn ?? "");
    setBrand(asset?.vehicleDetails?.brand ?? "");
    setModel(asset?.vehicleDetails?.model ?? "");
    setYearModel(asset?.vehicleDetails?.yearModel ?? "");
    setRenavam(asset?.vehicleDetails?.renavam ?? "");
    setError(null);
    setSaving(false);
  }, [asset, open]);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const parsedCurrentValue = parseCurrencyInput(currentValue);
    const parsedRemainingDebt = parseCurrencyInput(remainingDebt);

    if (!title.trim()) {
      setError("Informe o título do bem.");
      return;
    }

    setSaving(true);
    setError(null);
    try {
      await onSave({
        title: title.trim(),
        type,
        currentValue: parsedCurrentValue,
        remainingDebt: parsedRemainingDebt,
        isPaidOff,
        notes: notes.trim(),
        propertyDetails:
          type === "Property"
            ? {
                registryNumber: registryNumber.trim(),
                propertyInscription: propertyInscription.trim(),
                privateAreaSquareMeters: privateAreaSquareMeters ? Number(privateAreaSquareMeters) : null,
                debtCheckOn: debtCheckOn || null,
              }
            : null,
        vehicleDetails:
          type === "Vehicle"
            ? {
                brand: brand.trim(),
                model: model.trim(),
                yearModel: yearModel.trim(),
                renavam: renavam.trim(),
              }
            : null,
      });
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Não foi possível salvar o bem.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[88vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{asset ? "Editar bem" : "Novo bem"}</DialogTitle>
          <DialogDescription>Cadastre o patrimônio da casa com detalhes tipados para imóvel e veículo.</DialogDescription>
        </DialogHeader>
        <form className="space-y-4" onSubmit={handleSubmit}>
          {error ? <Notice tone="danger">{error}</Notice> : null}
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Título">
              <Input value={title} onChange={(event) => setTitle(event.target.value)} />
            </Field>
            <Field label="Tipo">
              <Select value={type} onChange={(event) => setType(event.target.value as AssetType)}>
                <option value="Other">Outro bem</option>
                <option value="Property">Imóvel</option>
                <option value="Vehicle">Veículo</option>
              </Select>
            </Field>
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Valor atual">
              <Input value={currentValue} onChange={(event) => setCurrentValue(event.target.value)} placeholder="R$ 0,00" />
            </Field>
            <Field label="Divida restante">
              <Input value={remainingDebt} onChange={(event) => setRemainingDebt(event.target.value)} placeholder="R$ 0,00" />
            </Field>
          </div>
          <Field label="Observações">
            <Textarea value={notes} onChange={(event) => setNotes(event.target.value)} rows={4} />
          </Field>
          <label className="inline-flex items-center gap-2 text-sm font-medium text-foreground">
            <input type="checkbox" checked={isPaidOff} onChange={(event) => setIsPaidOff(event.target.checked)} />
            Bem quitado
          </label>

          {type === "Property" ? (
            <div className="grid gap-4 rounded-[18px] border border-border/70 p-4 sm:grid-cols-2">
              <Field label="Matrícula">
                <Input value={registryNumber} onChange={(event) => setRegistryNumber(event.target.value)} />
              </Field>
              <Field label="Inscrição">
                <Input value={propertyInscription} onChange={(event) => setPropertyInscription(event.target.value)} />
              </Field>
              <Field label="Área privativa (m²)">
                <Input value={privateAreaSquareMeters} onChange={(event) => setPrivateAreaSquareMeters(event.target.value)} />
              </Field>
              <Field label="Pesquisa débito">
                <Input type="date" value={debtCheckOn} onChange={(event) => setDebtCheckOn(event.target.value)} />
              </Field>
            </div>
          ) : null}

          {type === "Vehicle" ? (
            <div className="grid gap-4 rounded-[18px] border border-border/70 p-4 sm:grid-cols-2">
              <Field label="Marca">
                <Input value={brand} onChange={(event) => setBrand(event.target.value)} />
              </Field>
              <Field label="Modelo">
                <Input value={model} onChange={(event) => setModel(event.target.value)} />
              </Field>
              <Field label="Ano/modelo">
                <Input value={yearModel} onChange={(event) => setYearModel(event.target.value)} />
              </Field>
              <Field label="Renavam">
                <Input value={renavam} onChange={(event) => setRenavam(event.target.value)} />
              </Field>
            </div>
          ) : null}

          <DialogFooter>
            <Button variant="secondary" type="button" onClick={() => onOpenChange(false)} disabled={saving}>
              Cancelar
            </Button>
            <Button type="submit" disabled={saving}>
              Salvar bem
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function AssetValuationDialog({
  open,
  asset,
  valuation,
  valuations,
  loading,
  syncing,
  isRowSaving,
  onOpenChange,
  onCreate,
  onEdit,
  onSave,
  onDelete,
  onInlineSave,
}: {
  open: boolean;
  asset: Asset | null;
  valuation: AssetValuation | null;
  valuations: AssetValuation[];
  loading: boolean;
  syncing: boolean;
  isRowSaving: (rowKey: string) => boolean;
  onOpenChange: (open: boolean) => void;
  onCreate: (input: AssetValuationFormInput) => Promise<void>;
  onEdit: (valuation: AssetValuation) => void;
  onSave: (input: AssetValuationFormInput) => Promise<void>;
  onDelete: (valuation: AssetValuation) => void;
  onInlineSave: (valuation: AssetValuation, overrides: Partial<AssetValuationFormInput>) => Promise<void>;
}) {
  const [referenceYear, setReferenceYear] = useState(valuation?.referenceYear ?? new Date().getUTCFullYear());
  const [label, setLabel] = useState(valuation?.label ?? "");
  const [amount, setAmount] = useState(valuation ? formatCurrency(valuation.amount) : "");
  const [notes, setNotes] = useState(valuation?.notes ?? "");
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    setReferenceYear(valuation?.referenceYear ?? new Date().getUTCFullYear());
    setLabel(valuation?.label ?? "");
    setAmount(valuation ? formatCurrency(valuation.amount) : "");
    setNotes(valuation?.notes ?? "");
    setError(null);
    setSaving(false);
  }, [open, valuation]);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const parsedAmount = parseCurrencyInput(amount);
    if (!label.trim()) {
      setError("Informe o rótulo da referência anual.");
      return;
    }

    if (parsedAmount == null || parsedAmount <= 0) {
      setError("Informe um valor positivo para a referência anual.");
      return;
    }

    setSaving(true);
    setError(null);
    try {
      const input = {
        referenceYear,
        label: label.trim(),
        amount: parsedAmount,
        notes: notes.trim(),
      };
      if (valuation) {
        await onSave(input);
      } else {
        await onCreate(input);
      }
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Não foi possível salvar a referência anual.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[88vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Referências anuais</DialogTitle>
          <DialogDescription>{asset ? `Registre FIPE, avaliação ou outro valor anual para ${asset.title}.` : "Registre referências anuais."}</DialogDescription>
          <InlineSyncLabel syncing={syncing} label="Sincronizando referências anuais..." />
        </DialogHeader>
        <div className="space-y-4">
          {loading ? <Notice tone="warning">Carregando referências anuais...</Notice> : null}
          <div className="overflow-x-auto">
            <Table>
              <TableHeader>
                <TableRow className="border-b border-border/60 bg-surface-muted hover:bg-surface-muted">
                  <TableHead>Ano</TableHead>
                  <TableHead>Rótulo</TableHead>
                  <TableHead>Valor</TableHead>
                  <TableHead className="min-w-[220px] text-right">Ações</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {valuations.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={4} className="py-8 text-center text-sm text-muted-foreground">
                      Nenhuma referência anual registrada para este bem.
                    </TableCell>
                  </TableRow>
                ) : (
                  valuations.map((item) => {
                    const rowKey = `valuation:${item.id}`;
                    const rowBusy = isRowSaving(rowKey);

                    return (
                      <TableRow key={item.id}>
                        <TableCell>
                          <InlineInputCell
                            value={item.referenceYear}
                            displayValue={String(item.referenceYear)}
                            ariaLabel={`Editar ano da referência ${item.label}`}
                            canEdit={item.canEdit}
                            rowBusy={rowBusy}
                            isSyncing={syncing}
                            inputType="number"
                            toDraft={(current) => String(current)}
                            fromDraft={(draft) => {
                              const parsed = Number(draft);
                              if (!Number.isInteger(parsed) || parsed < 2000 || parsed > 9999) {
                                return { value: item.referenceYear, error: "Informe um ano válido." };
                              }

                              return { value: parsed };
                            }}
                            onSave={(referenceYear) => onInlineSave(item, { referenceYear })}
                          />
                        </TableCell>
                        <TableCell>
                          <InlineInputCell
                            value={item.label}
                            displayValue={item.label}
                            ariaLabel={`Editar rótulo da referência ${item.label}`}
                            canEdit={item.canEdit}
                            rowBusy={rowBusy}
                            isSyncing={syncing}
                            toDraft={(current) => current}
                            fromDraft={(draft) => {
                              const trimmed = draft.trim();
                              return trimmed ? { value: trimmed } : { value: draft, error: "Informe o rótulo da referência." };
                            }}
                            onSave={(label) => onInlineSave(item, { label })}
                          />
                        </TableCell>
                        <TableCell>
                          <InlineInputCell
                            value={item.amount}
                            displayValue={formatCurrency(item.amount)}
                            ariaLabel={`Editar valor da referência ${item.label}`}
                            canEdit={item.canEdit}
                            rowBusy={rowBusy}
                            isSyncing={syncing}
                            placeholder="R$ 0,00"
                            toDraft={(current) => formatCurrency(current)}
                            fromDraft={(draft) => {
                              const parsed = parseCurrencyInput(draft);
                              if (parsed == null || parsed <= 0) {
                                return { value: item.amount, error: "Informe um valor positivo para a referência anual." };
                              }

                              return { value: parsed };
                            }}
                            onSave={(amount) => onInlineSave(item, { amount })}
                          />
                        </TableCell>
                        <TableCell className="text-right">
                          <div className="flex flex-wrap justify-end gap-2">
                            <Button variant="secondary" size="sm" onClick={() => onEdit(item)} disabled={!item.canEdit || rowBusy}>
                              <Pencil />
                              Editar
                            </Button>
                            <Button variant="ghost" size="sm" onClick={() => onDelete(item)} disabled={!item.canDelete || rowBusy}>
                              <Trash2 />
                              Excluir
                            </Button>
                          </div>
                        </TableCell>
                      </TableRow>
                    );
                  })
                )}
              </TableBody>
            </Table>
          </div>
          <form className="space-y-4 rounded-[18px] border border-border/70 p-4" onSubmit={handleSubmit}>
            {error ? <Notice tone="danger">{error}</Notice> : null}
            <div className="grid gap-4 sm:grid-cols-2">
              <Field label="Ano">
                <Input type="number" min={2000} max={9999} value={referenceYear} onChange={(event) => setReferenceYear(Number(event.target.value))} />
              </Field>
              <Field label="Rótulo">
                <Input value={label} onChange={(event) => setLabel(event.target.value)} placeholder="FIPE, avaliação, etc." />
              </Field>
            </div>
            <Field label="Valor">
              <Input value={amount} onChange={(event) => setAmount(event.target.value)} placeholder="R$ 0,00" />
            </Field>
            <Field label="Observações">
              <Textarea value={notes} onChange={(event) => setNotes(event.target.value)} rows={3} />
            </Field>
            <DialogFooter>
              <Button variant="secondary" type="button" onClick={() => onOpenChange(false)} disabled={saving}>
                Fechar
              </Button>
              <Button type="submit" disabled={saving}>
                {valuation ? "Salvar referência" : "Adicionar referência"}
              </Button>
            </DialogFooter>
          </form>
        </div>
      </DialogContent>
    </Dialog>
  );
}

function CreditCardAccountDialog({
  open,
  card,
  onOpenChange,
  onSave,
}: {
  open: boolean;
  card: CreditCardAccount | null;
  onOpenChange: (open: boolean) => void;
  onSave: (input: CreditCardAccountFormInput) => Promise<void>;
}) {
  const [name, setName] = useState(card?.name ?? "");
  const [brand, setBrand] = useState(card?.brand ?? "");
  const [lastFourDigits, setLastFourDigits] = useState(card?.lastFourDigits ?? "");
  const [closingDay, setClosingDay] = useState(card?.closingDay ?? 1);
  const [dueDay, setDueDay] = useState(card?.dueDay ?? 1);
  const [notes, setNotes] = useState(card?.notes ?? "");
  const [isActive, setIsActive] = useState(card?.isActive ?? true);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    setName(card?.name ?? "");
    setBrand(card?.brand ?? "");
    setLastFourDigits(card?.lastFourDigits ?? "");
    setClosingDay(card?.closingDay ?? 1);
    setDueDay(card?.dueDay ?? 1);
    setNotes(card?.notes ?? "");
    setIsActive(card?.isActive ?? true);
    setError(null);
    setSaving(false);
  }, [card, open]);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!name.trim()) {
      setError("Informe o nome do cartão.");
      return;
    }

    setSaving(true);
    setError(null);
    try {
      await onSave({
        name: name.trim(),
        brand: brand.trim(),
        lastFourDigits: lastFourDigits.trim(),
        closingDay,
        dueDay,
        notes: notes.trim(),
        isActive,
      });
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Não foi possível salvar o cartão.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[88vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{card ? "Editar cartão" : "Novo cartão"}</DialogTitle>
          <DialogDescription>Cadastre cartões da casa para registrar compras e fechar faturas no período correto.</DialogDescription>
        </DialogHeader>
        <form className="space-y-4" onSubmit={handleSubmit}>
          {error ? <Notice tone="danger">{error}</Notice> : null}
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Nome">
              <Input value={name} onChange={(event) => setName(event.target.value)} />
            </Field>
            <Field label="Bandeira">
              <Input value={brand} onChange={(event) => setBrand(event.target.value)} />
            </Field>
          </div>
          <div className="grid gap-4 sm:grid-cols-3">
            <Field label="Últimos 4 dígitos">
              <Input value={lastFourDigits} onChange={(event) => setLastFourDigits(event.target.value)} maxLength={4} />
            </Field>
            <Field label="Dia de fechamento">
              <Input type="number" min={1} max={31} value={closingDay} onChange={(event) => setClosingDay(Number(event.target.value))} />
            </Field>
            <Field label="Dia de vencimento">
              <Input type="number" min={1} max={31} value={dueDay} onChange={(event) => setDueDay(Number(event.target.value))} />
            </Field>
          </div>
          <Field label="Observações">
            <Textarea value={notes} onChange={(event) => setNotes(event.target.value)} rows={3} />
          </Field>
          <label className="inline-flex items-center gap-2 text-sm font-medium text-foreground">
            <input type="checkbox" checked={isActive} onChange={(event) => setIsActive(event.target.checked)} />
            Cartão ativo
          </label>
          <DialogFooter>
            <Button variant="secondary" type="button" onClick={() => onOpenChange(false)} disabled={saving}>
              Cancelar
            </Button>
            <Button type="submit" disabled={saving}>
              Salvar cartão
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function CreditCardTransactionDialog({
  open,
  transaction,
  categories,
  universes,
  projects,
  onOpenChange,
  onSave,
}: {
  open: boolean;
  transaction: CreditCardTransaction | null;
  categories: FinanceCategory[];
  universes: { id: string; name: string }[];
  projects: { id: string; name: string; universeId: string }[];
  onOpenChange: (open: boolean) => void;
  onSave: (input: CreditCardTransactionFormInput) => Promise<void>;
}) {
  const [title, setTitle] = useState(transaction?.title ?? "");
  const [merchant, setMerchant] = useState(transaction?.merchant ?? "");
  const [amount, setAmount] = useState(transaction ? formatCurrency(transaction.amount) : "");
  const [purchasedOn, setPurchasedOn] = useState(transaction?.purchasedOn ?? formatDateOnlyInputValue());
  const [notes, setNotes] = useState(transaction?.notes ?? "");
  const [categoryId, setCategoryId] = useState(transaction?.categoryId ?? "none");
  const [universeId, setUniverseId] = useState(transaction?.universeId ?? "none");
  const [projectId, setProjectId] = useState(transaction?.projectId ?? "none");
  const [externalSource, setExternalSource] = useState(transaction?.externalSource ?? "");
  const [externalReference, setExternalReference] = useState(transaction?.externalReference ?? "");
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    setTitle(transaction?.title ?? "");
    setMerchant(transaction?.merchant ?? "");
    setAmount(transaction ? formatCurrency(transaction.amount) : "");
    setPurchasedOn(transaction?.purchasedOn ?? formatDateOnlyInputValue());
    setNotes(transaction?.notes ?? "");
    setCategoryId(transaction?.categoryId ?? "none");
    setUniverseId(transaction?.universeId ?? "none");
    setProjectId(transaction?.projectId ?? "none");
    setExternalSource(transaction?.externalSource ?? "");
    setExternalReference(transaction?.externalReference ?? "");
    setError(null);
    setSaving(false);
  }, [open, transaction]);

  const scopedProjects = useMemo(
    () => (universeId === "none" ? projects : projects.filter((project) => project.universeId === universeId)),
    [projects, universeId],
  );

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const parsedAmount = parseCurrencyInput(amount);
    if (!title.trim()) {
      setError("Informe o título da compra.");
      return;
    }

    if (parsedAmount == null || parsedAmount <= 0) {
      setError("Informe um valor positivo para a compra.");
      return;
    }

    setSaving(true);
    setError(null);
    try {
      await onSave({
        title: title.trim(),
        merchant: merchant.trim(),
        amount: parsedAmount,
        purchasedOn,
        notes: notes.trim(),
        categoryId: categoryId === "none" ? null : categoryId,
        universeId: universeId === "none" ? null : universeId,
        projectId: projectId === "none" ? null : projectId,
        externalSource: externalSource.trim(),
        externalReference: externalReference.trim(),
      });
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Não foi possível salvar a compra.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[88vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{transaction ? "Editar compra" : "Nova compra"}</DialogTitle>
          <DialogDescription>Registre compras do cartão antes de fechar a fatura do período correspondente.</DialogDescription>
        </DialogHeader>
        <form className="space-y-4" onSubmit={handleSubmit}>
          {error ? <Notice tone="danger">{error}</Notice> : null}
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Título">
              <Input value={title} onChange={(event) => setTitle(event.target.value)} />
            </Field>
            <Field label="Loja/merchant">
              <Input value={merchant} onChange={(event) => setMerchant(event.target.value)} />
            </Field>
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Valor">
              <Input value={amount} onChange={(event) => setAmount(event.target.value)} placeholder="R$ 0,00" />
            </Field>
            <Field label="Data da compra">
              <Input type="date" value={purchasedOn} onChange={(event) => setPurchasedOn(event.target.value)} />
            </Field>
          </div>
          <div className="grid gap-4 sm:grid-cols-3">
            <Field label="Categoria">
              <Select value={categoryId} onChange={(event) => setCategoryId(event.target.value)}>
                <option value="none">Sem categoria</option>
                {categories.map((category) => (
                  <option key={category.id} value={category.id}>
                    {category.name}
                  </option>
                ))}
              </Select>
            </Field>
            <Field label="Universo">
              <Select value={universeId} onChange={(event) => setUniverseId(event.target.value)}>
                <option value="none">Sem universo</option>
                {universes.map((universe) => (
                  <option key={universe.id} value={universe.id}>
                    {universe.name}
                  </option>
                ))}
              </Select>
            </Field>
            <Field label="Projeto">
              <Select value={projectId} onChange={(event) => setProjectId(event.target.value)}>
                <option value="none">Sem projeto</option>
                {scopedProjects.map((project) => (
                  <option key={project.id} value={project.id}>
                    {project.name}
                  </option>
                ))}
              </Select>
            </Field>
          </div>
          <Field label="Observações">
            <Textarea value={notes} onChange={(event) => setNotes(event.target.value)} rows={3} />
          </Field>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Origem externa">
              <Input value={externalSource} onChange={(event) => setExternalSource(event.target.value)} placeholder="SMS, XLS, etc." />
            </Field>
            <Field label="Referência externa">
              <Input value={externalReference} onChange={(event) => setExternalReference(event.target.value)} />
            </Field>
          </div>
          <DialogFooter>
            <Button variant="secondary" type="button" onClick={() => onOpenChange(false)} disabled={saving}>
              Cancelar
            </Button>
            <Button type="submit" disabled={saving}>
              Salvar compra
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function CreditCardStatementDialog({
  open,
  statement,
  availableTransactions,
  onOpenChange,
  onSave,
}: {
  open: boolean;
  statement: CreditCardStatement | null;
  availableTransactions: CreditCardTransaction[];
  onOpenChange: (open: boolean) => void;
  onSave: (input: CreditCardStatementFormInput) => Promise<void>;
}) {
  const [closingDate, setClosingDate] = useState(statement?.closingDate ?? formatDateOnlyInputValue());
  const [dueDate, setDueDate] = useState(statement?.dueDate ?? formatDateOnlyInputValue(new Date(Date.now() + 7 * 24 * 60 * 60 * 1000)));
  const [notes, setNotes] = useState(statement?.notes ?? "");
  const [externalSource, setExternalSource] = useState(statement?.externalSource ?? "");
  const [externalReference, setExternalReference] = useState(statement?.externalReference ?? "");
  const [transactionIds, setTransactionIds] = useState<string[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    setClosingDate(statement?.closingDate ?? formatDateOnlyInputValue());
    setDueDate(statement?.dueDate ?? formatDateOnlyInputValue(new Date(Date.now() + 7 * 24 * 60 * 60 * 1000)));
    setNotes(statement?.notes ?? "");
    setExternalSource(statement?.externalSource ?? "");
    setExternalReference(statement?.externalReference ?? "");
    setTransactionIds(
      availableTransactions
        .filter((transaction) => transaction.creditCardStatementId === statement?.id)
        .map((transaction) => transaction.id),
    );
    setError(null);
    setSaving(false);
  }, [availableTransactions, open, statement]);

  function toggleTransaction(transactionId: string) {
    setTransactionIds((current) =>
      current.includes(transactionId) ? current.filter((item) => item !== transactionId) : [...current, transactionId],
    );
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!closingDate || !dueDate) {
      setError("Informe o fechamento e o vencimento da fatura.");
      return;
    }

    setSaving(true);
    setError(null);
    try {
      await onSave({
        closingDate,
        dueDate,
        notes: notes.trim(),
        transactionIds,
        externalSource: externalSource.trim(),
        externalReference: externalReference.trim(),
      });
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Não foi possível salvar a fatura.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[88vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{statement ? "Editar fatura" : "Fechar fatura"}</DialogTitle>
          <DialogDescription>Selecione as compras que entram na fatura e gere a despesa consolidada no caixa do mês do vencimento.</DialogDescription>
        </DialogHeader>
        <form className="space-y-4" onSubmit={handleSubmit}>
          {error ? <Notice tone="danger">{error}</Notice> : null}
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Fechamento">
              <Input type="date" value={closingDate} onChange={(event) => setClosingDate(event.target.value)} />
            </Field>
            <Field label="Vencimento">
              <Input type="date" value={dueDate} onChange={(event) => setDueDate(event.target.value)} />
            </Field>
          </div>
          <Field label="Observações">
            <Textarea value={notes} onChange={(event) => setNotes(event.target.value)} rows={3} />
          </Field>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Origem externa">
              <Input value={externalSource} onChange={(event) => setExternalSource(event.target.value)} />
            </Field>
            <Field label="Referência externa">
              <Input value={externalReference} onChange={(event) => setExternalReference(event.target.value)} />
            </Field>
          </div>
          <div className="space-y-2 rounded-[18px] border border-border/70 p-4">
            <p className="text-sm font-semibold text-foreground">Compras da fatura</p>
            {availableTransactions.length === 0 ? (
              <p className="text-sm text-muted-foreground">Não há compras disponíveis para esta fatura.</p>
            ) : (
              availableTransactions.map((transaction) => (
                <label key={transaction.id} className="flex items-center justify-between gap-3 rounded-[14px] border border-border/60 px-3 py-2 text-sm">
                  <span className="inline-flex items-center gap-2">
                    <input type="checkbox" checked={transactionIds.includes(transaction.id)} onChange={() => toggleTransaction(transaction.id)} />
                    <span>
                      {transaction.title} • {formatDateOnlyPtBr(transaction.purchasedOn)}
                    </span>
                  </span>
                  <span className="font-medium text-foreground">{formatCurrency(transaction.amount)}</span>
                </label>
              ))
            )}
          </div>
          <DialogFooter>
            <Button variant="secondary" type="button" onClick={() => onOpenChange(false)} disabled={saving}>
              Cancelar
            </Button>
            <Button type="submit" disabled={saving}>
              Salvar fatura
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function RecurringTemplatesDialog({
  open,
  templates,
  categories,
  syncing,
  isRowSaving,
  onOpenChange,
  onCreateNew,
  onEditTemplate,
  onDeleteTemplate,
  onInlineSaveTemplate,
}: {
  open: boolean;
  templates: FinanceRecurringTemplate[];
  categories: FinanceCategory[];
  syncing: boolean;
  isRowSaving: (rowKey: string) => boolean;
  onOpenChange: (open: boolean) => void;
  onCreateNew: () => void;
  onEditTemplate: (template: FinanceRecurringTemplate) => void;
  onDeleteTemplate: (template: FinanceRecurringTemplate) => void;
  onInlineSaveTemplate: (
    template: FinanceRecurringTemplate,
    overrides: Partial<FinanceRecurringTemplateFormInput>,
  ) => Promise<void>;
}) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="flex h-[92vh] w-[min(96vw,80rem)] max-w-none flex-col overflow-hidden p-0">
        <div className="flex flex-col gap-4 border-b border-border/60 p-5 sm:p-6 lg:flex-row lg:items-start lg:justify-between">
          <DialogHeader className="space-y-2">
            <DialogTitle>Recorrências</DialogTitle>
            <DialogDescription>Gerencie as recorrências mensais e anuais em uma janela dedicada quase em tela cheia.</DialogDescription>
            <InlineSyncLabel syncing={syncing} label="Sincronizando recorrências..." />
          </DialogHeader>
          <Button onClick={onCreateNew}>
            <Plus />
            Nova recorrência
          </Button>
        </div>
        <div className="flex-1 overflow-y-auto p-5 sm:p-6">
          {templates.length === 0 ? (
            <EmptyState
              icon={<Repeat2 className="size-5" />}
              title="Nenhuma recorrência configurada"
              description="Cadastre recorrências mensais ou anuais para acelerar a geração do caixa."
            />
          ) : (
            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow className="border-b border-border/60 bg-surface-muted hover:bg-surface-muted">
                    <TableHead className="min-w-[180px]">Título</TableHead>
                    <TableHead>Tipo</TableHead>
                    <TableHead>Recorrência</TableHead>
                    <TableHead>Valor padrão</TableHead>
                    <TableHead>Categoria</TableHead>
                    <TableHead>Classificação</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead className="min-w-[220px] text-right">Ações</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {templates.map((template) => {
                    const rowKey = `template:${template.id}`;
                    const rowBusy = isRowSaving(rowKey);

                    return (
                      <TableRow key={template.id}>
                        <TableCell>
                          <div className="space-y-1">
                            <InlineInputCell
                              value={template.title}
                              displayValue={template.title}
                              ariaLabel={`Editar título da recorrência ${template.title}`}
                              canEdit={template.canEdit}
                              rowBusy={rowBusy}
                              isSyncing={syncing}
                              toDraft={(current) => current}
                              fromDraft={(draft) => {
                                const trimmed = draft.trim();
                                return trimmed ? { value: trimmed } : { value: draft, error: "Informe o título da recorrência." };
                              }}
                              onSave={(title) => onInlineSaveTemplate(template, { title })}
                            />
                            {template.notes ? <p className="px-2 text-sm text-muted-foreground">{template.notes}</p> : null}
                          </div>
                        </TableCell>
                        <TableCell>{template.type === "Entrada" ? "Entrada" : "Saída"}</TableCell>
                        <TableCell>{formatRecurrence(template.recurrence, template.dayOfMonth, template.monthOfYear)}</TableCell>
                        <TableCell>
                          <InlineInputCell
                            value={template.defaultAmount}
                            displayValue={formatCurrency(template.defaultAmount)}
                            ariaLabel={`Editar valor padrão da recorrência ${template.title}`}
                            canEdit={template.canEdit}
                            rowBusy={rowBusy}
                            isSyncing={syncing}
                            placeholder="R$ 0,00"
                            toDraft={(current) => formatCurrency(current)}
                            fromDraft={(draft) => {
                              const parsed = parseCurrencyInput(draft);
                              if (parsed == null || parsed < 0) {
                                return { value: template.defaultAmount, error: "Informe um valor válido para a recorrência." };
                              }

                              return { value: parsed };
                            }}
                            onSave={(defaultAmount) => onInlineSaveTemplate(template, { defaultAmount })}
                          />
                        </TableCell>
                        <TableCell>
                          <InlineSelectCell
                            value={template.categoryId ?? "none"}
                            displayValue={template.categoryName ?? "Sem categoria"}
                            ariaLabel={`Editar categoria da recorrência ${template.title}`}
                            canEdit={template.canEdit}
                            rowBusy={rowBusy}
                            isSyncing={syncing}
                            options={[
                              { value: "none", label: "Sem categoria" },
                              ...categories.map((category) => ({ value: category.id, label: category.name })),
                            ]}
                            onSave={(categoryId) =>
                              onInlineSaveTemplate(template, { categoryId: categoryId === "none" ? null : categoryId })
                            }
                          />
                        </TableCell>
                        <TableCell>{template.projectName ?? template.universeName ?? "Sem classificação"}</TableCell>
                        <TableCell>
                          <InlineCheckboxCell
                            checked={template.isActive}
                            checkedLabel="Ativa"
                            uncheckedLabel="Inativa"
                            ariaLabel={`Alternar status da recorrência ${template.title}`}
                            canEdit={template.canEdit}
                            rowBusy={rowBusy}
                            isSyncing={syncing}
                            onSave={(isActive) => onInlineSaveTemplate(template, { isActive })}
                          />
                        </TableCell>
                        <TableCell className="text-right">
                          <div className="flex flex-wrap justify-end gap-2">
                            <Button variant="secondary" size="sm" onClick={() => onEditTemplate(template)} disabled={!template.canEdit || rowBusy}>
                              <Pencil />
                              Editar
                            </Button>
                            <Button variant="ghost" size="sm" onClick={() => onDeleteTemplate(template)} disabled={!template.canDelete || rowBusy}>
                              <Trash2 />
                              Excluir
                            </Button>
                          </div>
                        </TableCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            </div>
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
}

function GeneratePeriodDialog({
  open,
  periodLabel,
  onOpenChange,
  onMissingOnly,
  onDuplicateAll,
}: {
  open: boolean;
  periodLabel: string;
  onOpenChange: (open: boolean) => void;
  onMissingOnly: () => Promise<void>;
  onDuplicateAll: () => Promise<void>;
}) {
  const [saving, setSaving] = useState<"missing" | "duplicate" | null>(null);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Inserir recorrências de {periodLabel}</DialogTitle>
          <DialogDescription>
            Este período já existe. Escolha se deseja adicionar apenas recorrências faltantes ou duplicar novamente todas as recorrências aplicáveis.
          </DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <Button variant="secondary" type="button" onClick={() => onOpenChange(false)} disabled={saving !== null}>
            Cancelar
          </Button>
          <Button
            variant="secondary"
            type="button"
            disabled={saving !== null}
            onClick={async () => {
              setSaving("missing");
              await onMissingOnly();
              setSaving(null);
            }}
          >
            Adicionar faltantes
          </Button>
          <Button
            type="button"
            disabled={saving !== null}
            onClick={async () => {
              setSaving("duplicate");
              await onDuplicateAll();
              setSaving(null);
            }}
          >
            Duplicar todos
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
