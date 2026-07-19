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
  ImportCreditCardTransactionItem,
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
  OrganizaClubWorkspaceShell,
  LoadingState,
  Notice,
} from "@/features/workspace/organiza-club-workspace-shell";
import { cn } from "@/lib/utils";
import type {
  AssetFormInput,
  AssetValuationFormInput,
  CreditCardAccountFormInput,
  CreditCardStatementFormInput,
  CreditCardTransactionFormInput,
  CreditCardTransactionImportSummary,
  FinanceCategoryFormInput,
  FinanceDashboardController,
  FinanceEntryFormInput,
  FinanceRecurringTemplateFormInput,
  ImportedCreditCardTransactionDraft,
} from "./use-finance-dashboard";
import {
  filterCreditCardTransactions,
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
  coreId: "all",
  projectId: "all",
  groupBy: "type",
};

type EntryDialogState = { mode: "create" | "edit"; entryType: FinanceEntryType; entry?: FinanceEntry | null };
type InlineCellMode = "idle" | "editing" | "saving" | "syncing";
type InlineSelectOption = { value: string; label: string; disabled?: boolean };
type FinanceWorkspaceSection = "cash" | "cards";
type EditableImportedTransactionField = Exclude<keyof ImportedCreditCardTransactionDraft, "localId" | "errors">;

const financeWorkspaceSections: Array<{ value: FinanceWorkspaceSection; label: string }> = [
  { value: "cash", label: "Caixa" },
  { value: "cards", label: "Cartões" },
];

let importDraftSequence = 0;

function areStringArraysEqual(left: string[], right: string[]) {
  if (left.length !== right.length) {
    return false;
  }

  return left.every((value, index) => value === right[index]);
}

function createImportDraftLocalId() {
  importDraftSequence += 1;
  return `import-draft-${importDraftSequence}`;
}

function normalizeImportLookup(value: string) {
  return value
    .trim()
    .normalize("NFD")
    .replace(/\p{Diacritic}/gu, "")
    .toLowerCase();
}

function coerceOptionalImportText(value: unknown, field: string) {
  if (value == null) {
    return "";
  }

  if (typeof value === "string") {
    return value.trim();
  }

  if (typeof value === "number" || typeof value === "boolean") {
    return String(value);
  }

  throw new Error(`O campo "${field}" deve ser texto.`);
}

function coerceImportAmount(value: unknown) {
  if (value == null) {
    return "";
  }

  if (typeof value === "number") {
    return String(value);
  }

  if (typeof value === "string") {
    return value.trim();
  }

  throw new Error('O campo "amount" deve ser número ou texto.');
}

function createEmptyImportedTransactionDraft(overrides: Partial<ImportedCreditCardTransactionDraft> = {}): ImportedCreditCardTransactionDraft {
  return {
    localId: createImportDraftLocalId(),
    title: "",
    merchant: "",
    amount: "",
    purchasedOn: formatDateOnlyInputValue(),
    notes: "",
    categoryName: "",
    coreName: "",
    projectName: "",
    externalSource: "",
    externalReference: "",
    importedAt: "",
    errors: [],
    ...overrides,
  };
}

function parseImportedTransactionDraftsFromJson(content: string) {
  let parsed: unknown;

  try {
    parsed = JSON.parse(content);
  } catch {
    throw new Error("O arquivo não contém um JSON válido.");
  }

  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed)) {
    throw new Error('O arquivo deve seguir o formato {"transactions":[...]}');
  }

  const transactions = (parsed as { transactions?: unknown }).transactions;
  if (!Array.isArray(transactions)) {
    throw new Error('O JSON deve conter uma lista "transactions".');
  }

  if (transactions.length === 0) {
    throw new Error("Adicione ao menos uma compra em transactions.");
  }

  return transactions.map((item, index) => {
    if (!item || typeof item !== "object" || Array.isArray(item)) {
      throw new Error(`O item ${index + 1} de "transactions" deve ser um objeto.`);
    }

    const value = item as Record<string, unknown>;
    return createEmptyImportedTransactionDraft({
      title: coerceOptionalImportText(value.title, "title"),
      merchant: coerceOptionalImportText(value.merchant, "merchant"),
      amount: coerceImportAmount(value.amount),
      purchasedOn: coerceOptionalImportText(value.purchasedOn, "purchasedOn"),
      notes: coerceOptionalImportText(value.notes, "notes"),
      categoryName: coerceOptionalImportText(value.categoryName, "categoryName"),
      coreName: coerceOptionalImportText(value.coreName, "coreName"),
      projectName: coerceOptionalImportText(value.projectName, "projectName"),
      externalSource: coerceOptionalImportText(value.externalSource, "externalSource"),
      externalReference: coerceOptionalImportText(value.externalReference, "externalReference"),
      importedAt: coerceOptionalImportText(value.importedAt, "importedAt"),
    });
  });
}

function buildCreditCardImportExampleJson() {
  return JSON.stringify(
    {
      transactions: [
        {
          title: "Supermercado",
          merchant: "Mercado da esquina",
          amount: 220.9,
          purchasedOn: "2026-07-06",
          notes: "Compra mensal",
          categoryName: "Mercado",
          coreName: "Espaço",
          projectName: "Moradia",
          externalSource: "JSON",
          externalReference: "json-001",
          importedAt: "2026-07-08T12:00:00Z",
        },
      ],
    },
    null,
    2,
  );
}

async function readImportFileContent(file: File) {
  if (typeof file.text === "function") {
    return await file.text();
  }

  return await new Promise<string>((resolve, reject) => {
    const reader = new FileReader();
    reader.onerror = () => reject(new Error("Não foi possível ler o arquivo JSON."));
    reader.onload = () => resolve(typeof reader.result === "string" ? reader.result : "");
    reader.readAsText(file);
  });
}

function validateImportedTransactionDrafts(
  drafts: ImportedCreditCardTransactionDraft[],
  categories: FinanceCategory[],
  cores: { id: string; name: string }[],
  projects: { id: string; name: string; coreId: string }[],
) {
  const normalizedCategories = new Set(categories.map((category) => normalizeImportLookup(category.name)));

  return drafts.map((draft) => {
    const errors: ImportedCreditCardTransactionDraft["errors"] = [];
    const title = draft.title.trim();
    const amount = parseCurrencyInput(draft.amount);
    const purchasedOn = draft.purchasedOn.trim();
    const categoryName = draft.categoryName.trim();
    const coreName = draft.coreName.trim();
    const projectName = draft.projectName.trim();
    const importedAt = draft.importedAt.trim();

    if (!title) {
      errors.push({ field: "title", message: "Informe o título da compra." });
    }

    if (amount == null || amount <= 0) {
      errors.push({ field: "amount", message: "Informe um valor positivo." });
    }

    const purchasedOnDate = purchasedOn ? new Date(`${purchasedOn}T00:00:00Z`) : null;
    if (!purchasedOn || !purchasedOnDate || Number.isNaN(purchasedOnDate.getTime())) {
      errors.push({ field: "purchasedOn", message: "Informe uma data de compra válida." });
    }

    if (importedAt) {
      const importedAtDate = new Date(importedAt);
      if (Number.isNaN(importedAtDate.getTime())) {
        errors.push({ field: "importedAt", message: "Use uma data/hora válida em importedAt." });
      }
    }

    const coreMatches = coreName
      ? cores.filter((core) => normalizeImportLookup(core.name) === normalizeImportLookup(coreName))
      : [];

    if (coreName && coreMatches.length === 0) {
      errors.push({ field: "coreName", message: "Núcleo não encontrado." });
    }

    if (coreMatches.length > 1) {
      errors.push({ field: "coreName", message: "Há mais de um núcleo com esse nome." });
    }

    if (projectName) {
      const matchingProjects = projects.filter((project) => {
        if (normalizeImportLookup(project.name) !== normalizeImportLookup(projectName)) {
          return false;
        }

        if (coreMatches.length === 1) {
          return project.coreId === coreMatches[0]?.id;
        }

        return true;
      });

      if (matchingProjects.length === 0) {
        errors.push({
          field: "projectName",
          message: coreMatches.length === 1 ? "Projeto não encontrado no núcleo informado." : "Projeto não encontrado.",
        });
      } else if (matchingProjects.length > 1) {
        errors.push({
          field: "projectName",
          message: coreMatches.length === 1 ? "Há mais de um projeto com esse nome neste núcleo." : "Informe também o núcleo para este projeto.",
        });
      }
    }

    const nextDraft: ImportedCreditCardTransactionDraft = {
      ...draft,
      title,
      merchant: draft.merchant.trim(),
      notes: draft.notes.trim(),
      categoryName,
      coreName,
      projectName,
      externalSource: draft.externalSource.trim(),
      externalReference: draft.externalReference.trim(),
      importedAt,
      errors,
    };

    if (categoryName && !normalizedCategories.has(normalizeImportLookup(categoryName))) {
      return nextDraft;
    }

    return nextDraft;
  });
}

function summarizeImportedTransactionDrafts(
  drafts: ImportedCreditCardTransactionDraft[],
  categories: FinanceCategory[],
): CreditCardTransactionImportSummary {
  const existingCategories = new Set(categories.map((category) => normalizeImportLookup(category.name)));
  const totalAmount = drafts.reduce((sum, draft) => {
    const parsed = parseCurrencyInput(draft.amount);
    return parsed != null && parsed > 0 ? sum + parsed : sum;
  }, 0);
  const invalidCount = drafts.filter((draft) => draft.errors.length > 0).length;
  const newCategoryCount = new Set(
    drafts
      .map((draft) => draft.categoryName.trim())
      .filter(Boolean)
      .filter((name) => !existingCategories.has(normalizeImportLookup(name))),
  ).size;

  return {
    totalCount: drafts.length,
    validCount: drafts.length - invalidCount,
    invalidCount,
    totalAmount,
    newCategoryCount,
  };
}

function buildImportRequestItems(drafts: ImportedCreditCardTransactionDraft[]): ImportCreditCardTransactionItem[] {
  return drafts.map((draft) => ({
    title: draft.title.trim(),
    merchant: draft.merchant.trim() || null,
    amount: parseCurrencyInput(draft.amount) ?? 0,
    purchasedOn: draft.purchasedOn.trim(),
    notes: draft.notes.trim() || null,
    categoryName: draft.categoryName.trim() || null,
    coreName: draft.coreName.trim() || null,
    projectName: draft.projectName.trim() || null,
    externalSource: draft.externalSource.trim() || null,
    externalReference: draft.externalReference.trim() || null,
    importedAt: draft.importedAt.trim() || null,
  }));
}

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

function FinanceWorkspaceTabs({
  value,
  onChange,
}: {
  value: FinanceWorkspaceSection;
  onChange: (section: FinanceWorkspaceSection) => void;
}) {
  const tabRefs = useRef<Record<FinanceWorkspaceSection, HTMLButtonElement | null>>({
    cash: null,
    cards: null,
  });

  function focusSection(section: FinanceWorkspaceSection) {
    onChange(section);
    window.requestAnimationFrame(() => tabRefs.current[section]?.focus());
  }

  function handleKeyDown(event: ReactKeyboardEvent<HTMLButtonElement>) {
    const currentIndex = financeWorkspaceSections.findIndex((section) => section.value === value);
    if (currentIndex < 0) {
      return;
    }

    let nextIndex = -1;
    if (event.key === "ArrowRight") {
      nextIndex = (currentIndex + 1) % financeWorkspaceSections.length;
    } else if (event.key === "ArrowLeft") {
      nextIndex = (currentIndex - 1 + financeWorkspaceSections.length) % financeWorkspaceSections.length;
    } else if (event.key === "Home") {
      nextIndex = 0;
    } else if (event.key === "End") {
      nextIndex = financeWorkspaceSections.length - 1;
    }

    if (nextIndex === -1) {
      return;
    }

    event.preventDefault();
    const nextSection = financeWorkspaceSections[nextIndex];
    if (nextSection) {
      focusSection(nextSection.value);
    }
  }

  return (
    <div
      className="inline-flex flex-wrap gap-2 rounded-[18px] border border-border/60 bg-surface-muted p-1"
      role="tablist"
      aria-label="Seções do financeiro"
    >
      {financeWorkspaceSections.map((section) => {
        const isActive = section.value === value;

        return (
          <button
            key={section.value}
            ref={(element) => {
              tabRefs.current[section.value] = element;
            }}
            type="button"
            role="tab"
            id={`finance-tab-${section.value}`}
            aria-controls={`finance-panel-${section.value}`}
            aria-selected={isActive}
            tabIndex={isActive ? 0 : -1}
            onClick={() => focusSection(section.value)}
            onKeyDown={handleKeyDown}
            className={cn(
              "rounded-[14px] px-4 py-2 text-sm font-semibold transition",
              isActive
                ? "bg-primary text-primary-foreground shadow-xs"
                : "text-muted-foreground hover:bg-surface hover:text-foreground",
            )}
          >
            {section.label}
          </button>
        );
      })}
    </div>
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
  const [activeSection, setActiveSection] = useState<FinanceWorkspaceSection>("cash");
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
  const [importDialogOpen, setImportDialogOpen] = useState(false);
  const [importReviewOpen, setImportReviewOpen] = useState(false);
  const [importDrafts, setImportDrafts] = useState<ImportedCreditCardTransactionDraft[]>([]);
  const [importError, setImportError] = useState<string | null>(null);
  const [importFileName, setImportFileName] = useState<string | null>(null);
  const [importParsing, setImportParsing] = useState(false);
  const [importSubmitting, setImportSubmitting] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<
    | { kind: "category"; id: string; name: string }
    | { kind: "entry"; id: string; name: string }
    | { kind: "entry-bulk"; ids: string[]; count: number }
    | { kind: "template"; id: string; name: string }
    | { kind: "asset"; id: string; name: string }
    | { kind: "valuation"; assetId: string; id: string; name: string }
    | { kind: "card"; id: string; name: string }
    | { kind: "transaction"; id: string; name: string }
    | { kind: "transaction-bulk"; ids: string[]; count: number }
    | { kind: "statement"; id: string; name: string }
    | null
  >(null);
  const [selectedEntryIds, setSelectedEntryIds] = useState<string[]>([]);
  const [selectedTransactionIds, setSelectedTransactionIds] = useState<string[]>([]);
  const [creditCardTransactionSearch, setCreditCardTransactionSearch] = useState("");
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
  const filteredCreditCardTransactions = useMemo(
    () => filterCreditCardTransactions(dashboard.creditCardTransactions, creditCardTransactionSearch),
    [creditCardTransactionSearch, dashboard.creditCardTransactions],
  );
  const visibleCount = activeSection === "cash" ? filteredEntries.length : filteredCreditCardTransactions.length;
  const visibleLabel = activeSection === "cash" ? "lançamentos" : "compras";
  const importReviewDrafts = useMemo(
    () => validateImportedTransactionDrafts(importDrafts, dashboard.categories, dashboard.cores, dashboard.projects),
    [dashboard.categories, dashboard.projects, dashboard.cores, importDrafts],
  );
  const importSummary = useMemo(
    () => summarizeImportedTransactionDrafts(importReviewDrafts, dashboard.categories),
    [dashboard.categories, importReviewDrafts],
  );
  const openTransactions = dashboard.creditCardTransactions.filter(
    (transaction) =>
      !transaction.creditCardStatementId ||
      transaction.creditCardStatementId === (typeof statementDialog === "object" ? statementDialog?.id : null),
  );
  const selectedVisibleEntryIds = filteredEntries.filter((entry) => selectedEntryIds.includes(entry.id)).map((entry) => entry.id);
  const selectedVisibleTransactionIds = dashboard.creditCardTransactions
    .filter((transaction) => selectedTransactionIds.includes(transaction.id))
    .map((transaction) => transaction.id);
  const visibleSelectableTransactionIds = filteredCreditCardTransactions
    .filter((transaction) => transaction.canDelete)
    .map((transaction) => transaction.id);

  useEffect(() => {
    setSelectedEntryIds((current) => {
      const next = current.filter((entryId) => entries.some((entry) => entry.id === entryId));
      return areStringArraysEqual(current, next) ? current : next;
    });
  }, [entries]);

  useEffect(() => {
    setSelectedTransactionIds((current) => {
      const next = current.filter((transactionId) => dashboard.creditCardTransactions.some((transaction) => transaction.id === transactionId));
      return areStringArraysEqual(current, next) ? current : next;
    });
  }, [dashboard.creditCardTransactions]);

  useEffect(() => {
    setSelectedTransactionIds((current) => (current.length === 0 ? current : []));
  }, [dashboard.selectedCreditCardId]);

  useEffect(() => {
    setCreditCardTransactionSearch("");
  }, [dashboard.selectedCreditCardId]);

  useEffect(() => {
    setImportDialogOpen(false);
    setImportReviewOpen(false);
    setImportDrafts([]);
    setImportError(null);
    setImportFileName(null);
    setImportParsing(false);
    setImportSubmitting(false);
  }, [dashboard.selectedCreditCardId]);

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

  function toggleEntrySelection(entryId: string) {
    setSelectedEntryIds((current) => (current.includes(entryId) ? current.filter((id) => id !== entryId) : [...current, entryId]));
  }

  function toggleGroupEntries(entryIds: string[]) {
    if (entryIds.length === 0) {
      return;
    }

    setSelectedEntryIds((current) => {
      const allSelected = entryIds.every((entryId) => current.includes(entryId));
      return allSelected ? current.filter((id) => !entryIds.includes(id)) : Array.from(new Set([...current, ...entryIds]));
    });
  }

  function toggleTransactionSelection(transactionId: string) {
    setSelectedTransactionIds((current) =>
      current.includes(transactionId) ? current.filter((id) => id !== transactionId) : [...current, transactionId],
    );
  }

  function toggleAllTransactions(transactionIds: string[]) {
    if (transactionIds.length === 0) {
      return;
    }

    setSelectedTransactionIds((current) => {
      const allSelected = transactionIds.every((transactionId) => current.includes(transactionId));
      return allSelected
        ? current.filter((id) => !transactionIds.includes(id))
        : Array.from(new Set([...current, ...transactionIds]));
    });
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
      coreId: entry.coreId ?? null,
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
      coreId: template.coreId ?? null,
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
      coreId: transaction.coreId ?? null,
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

  function resetImportFlow() {
    setImportDialogOpen(false);
    setImportReviewOpen(false);
    setImportDrafts([]);
    setImportError(null);
    setImportFileName(null);
    setImportParsing(false);
    setImportSubmitting(false);
  }

  function updateImportDraft(localId: string, field: EditableImportedTransactionField, value: string) {
    setImportDrafts((current) =>
      current.map((draft) => (draft.localId === localId ? { ...draft, [field]: value } : draft)),
    );
  }

  function addImportDraft() {
    setImportDrafts((current) => [...current, createEmptyImportedTransactionDraft()]);
  }

  function removeImportDraft(localId: string) {
    setImportDrafts((current) => current.filter((draft) => draft.localId !== localId));
  }

  function downloadImportExample() {
    const blob = new Blob([buildCreditCardImportExampleJson()], { type: "application/json" });
    const objectUrl = window.URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = objectUrl;
    anchor.download = "organiza-club-cartao-import-exemplo.json";
    anchor.click();
    window.URL.revokeObjectURL(objectUrl);
  }

  async function handleImportFile(file: File) {
    setImportParsing(true);
    setImportError(null);

    try {
      const parsedDrafts = parseImportedTransactionDraftsFromJson(await readImportFileContent(file));
      setImportDrafts(parsedDrafts);
      setImportFileName(file.name);
      setImportReviewOpen(true);
    } catch (exception) {
      setImportError(exception instanceof Error ? exception.message : "Não foi possível ler o arquivo JSON.");
    } finally {
      setImportParsing(false);
    }
  }

  async function handleImportConfirm() {
    if (importReviewDrafts.length === 0 || importSummary.invalidCount > 0) {
      return;
    }

    setImportSubmitting(true);
    setImportError(null);
    try {
      await dashboard.importCreditCardTransactions(buildImportRequestItems(importReviewDrafts));
      resetImportFlow();
    } catch (exception) {
      setImportError(exception instanceof Error ? exception.message : "Não foi possível importar as compras do cartão.");
    } finally {
      setImportSubmitting(false);
    }
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
    } else if (deleteTarget.kind === "entry-bulk") {
      await dashboard.deleteEntries(deleteTarget.ids);
      setSelectedEntryIds((current) => current.filter((id) => !deleteTarget.ids.includes(id)));
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
    } else if (deleteTarget.kind === "transaction-bulk") {
      await dashboard.deleteCreditCardTransactions(deleteTarget.ids);
      setSelectedTransactionIds((current) => current.filter((id) => !deleteTarget.ids.includes(id)));
    } else if (deleteTarget.kind === "statement") {
      await dashboard.deleteCreditCardStatement(deleteTarget.id);
    }

    setDeleteTarget(null);
  }

  const deleteDialogTitle =
    deleteTarget?.kind === "category"
      ? "Excluir categoria"
      : deleteTarget?.kind === "entry-bulk" || deleteTarget?.kind === "transaction-bulk"
        ? "Excluir registros"
        : "Excluir registro";
  const deleteDialogDescription =
    deleteTarget?.kind === "category"
      ? "Essa ação remove a categoria personalizada e desvincula os registros que a utilizavam."
      : deleteTarget?.kind === "entry-bulk"
        ? `Essa ação remove ${deleteTarget.count} lançamentos selecionados do caixa.`
        : deleteTarget?.kind === "transaction-bulk"
          ? `Essa ação remove ${deleteTarget.count} compras selecionadas do cartão.`
          : "Essa ação remove o registro selecionado.";
  const deleteDialogRequiresTyping = deleteTarget?.kind === "category" || deleteTarget?.kind === "card";
  const deleteDialogImpact =
    deleteTarget?.kind === "category"
      ? "A exclusão é permanente e os lançamentos, recorrências e compras vinculados passam a ficar sem categoria."
      : deleteTarget?.kind === "entry-bulk"
        ? "A exclusão em lote é permanente e recalcula os totais do caixa do período atual."
        : deleteTarget?.kind === "transaction-bulk"
          ? "A exclusão em lote é permanente e atualiza as compras abertas, faturas e totais relacionados."
          : "A exclusão é permanente e atualiza os totais e relacionamentos do módulo financeiro.";

  return (
    <>
      <OrganizaClubWorkspaceShell
        controller={{
          session: dashboard.session,
          activeSpaceId: dashboard.activeSpaceId,
          activeSpace: dashboard.activeSpace,
          members: dashboard.members,
          theme: dashboard.theme,
          sidebarCollapsed: dashboard.sidebarCollapsed,
          loading: dashboard.loading,
          error: dashboard.error,
          canShareSpace: dashboard.canShareSpace,
          canManageSpace: dashboard.canManageSpace,
          editingSpace: dashboard.editingSpace,
          isSpaceDialogOpen: dashboard.isSpaceDialogOpen,
          isShareDialogOpen: dashboard.isShareDialogOpen,
          setError: dashboard.setError,
          setSidebarCollapsed: dashboard.setSidebarCollapsed,
          setTheme: dashboard.setTheme,
          handleSpaceChange: dashboard.handleSpaceChange,
          handleLogout: dashboard.handleLogout,
          refreshSpaces: dashboard.refreshSpaces,
          refreshWorkspace: dashboard.refreshWorkspace,
          openCreateSpace: dashboard.openCreateSpace,
          openEditSpace: dashboard.openEditSpace,
          openShareSpace: dashboard.openShareSpace,
          closeCommonModal: dashboard.closeCommonModal,
          createSpace: dashboard.createSpace,
          updateSpace: dashboard.updateSpace,
          deleteSpace: dashboard.deleteSpace,
          shareSpace: dashboard.shareSpace,
        }}
        activeModule="finance"
        subtitle={dashboard.subtitle}
        visibleCount={visibleCount}
        visibleLabel={visibleLabel}
        headerStats={headerStats}
      >
        <Card>
          <CardContent className="flex flex-col gap-4 p-5 sm:p-6 lg:flex-row lg:items-end lg:justify-between">
            <div className="max-w-2xl">
              <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">Financeiro</p>
              <h1 className="mt-2 text-2xl font-semibold text-foreground">Operação financeira do espaço</h1>
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
            description="Estamos reunindo o período mensal, recorrências, cartões e patrimônio do espaço."
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
                <InfoBlock label="Verificadas" value={`${periodSummary?.verifiedEntries ?? 0}/${entries.length}`} helper="Lançamentos revisados no caixa mensal" />
              </CardContent>
            </Card>

            <div className="space-y-4">
              <FinanceWorkspaceTabs value={activeSection} onChange={setActiveSection} />

              <section
                role="tabpanel"
                id="finance-panel-cash"
                aria-labelledby="finance-tab-cash"
                hidden={activeSection !== "cash"}
                className="space-y-4"
              >
                <Card>
              <CardHeader className="border-b border-border/60 pb-4">
                <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
                  <div className="space-y-1">
                    <CardTitle className="text-lg">Caixa</CardTitle>
                    <InlineSyncLabel syncing={dashboard.syncingSections.cash} label="Sincronizando caixa..." />
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <Button
                      variant="ghost"
                      onClick={() =>
                        setDeleteTarget({
                          kind: "entry-bulk",
                          ids: selectedVisibleEntryIds,
                          count: selectedVisibleEntryIds.length,
                        })
                      }
                      disabled={selectedVisibleEntryIds.length === 0}
                    >
                      <Trash2 />
                      Excluir selecionados{selectedVisibleEntryIds.length > 0 ? ` (${selectedVisibleEntryIds.length})` : ""}
                    </Button>
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
                    <option value="verified">Verificadas</option>
                    <option value="pending">Pendentes</option>
                  </Select>
                  <Select value={filters.origin} onChange={(event) => setFilters((current) => ({ ...current, origin: event.target.value as FinanceEntryOrigin | "all" }))}>
                    <option value="all">Todas as origens</option>
                    <option value="Manual">Manual</option>
                    <option value="RecurringTemplate">Recorrência</option>
                    <option value="CreditCardStatement">Fatura</option>
                  </Select>
                  <Select value={filters.coreId} onChange={(event) => setFilters((current) => ({ ...current, coreId: event.target.value }))}>
                    <option value="all">Todos os núcleos</option>
                    {dashboard.cores.map((core) => (
                      <option key={core.id} value={core.id}>
                        {core.name}
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
                    <option value="core">Núcleo</option>
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
                                  <TableHead className="w-12">
                                    <span className="sr-only">Selecionar lançamentos</span>
                                  </TableHead>
                                  <TableHead className="min-w-[180px]">Item</TableHead>
                                  <TableHead>Tipo</TableHead>
                                  <TableHead>Origem</TableHead>
                                  <TableHead>Categoria</TableHead>
                                  <TableHead>Data</TableHead>
                                  <TableHead>Projeto</TableHead>
                                  <TableHead>Valor</TableHead>
                                  <TableHead>Verificada</TableHead>
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
                                        <input
                                          type="checkbox"
                                          checked={selectedEntryIds.includes(entry.id)}
                                          onChange={() => toggleEntrySelection(entry.id)}
                                          disabled={!entry.canDelete || rowBusy}
                                          aria-label={`Selecionar lançamento ${entry.title}`}
                                        />
                                      </TableCell>
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
                                      <TableCell>{entry.projectName ?? entry.coreName ?? "Sem classificação"}</TableCell>
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
                        <div className="border-t border-border/60 px-4 py-3">
                          <label className="inline-flex items-center gap-2 text-sm text-muted-foreground">
                            <input
                              type="checkbox"
                              checked={
                                group.entries.filter((entry) => entry.canDelete).length > 0 &&
                                group.entries.filter((entry) => entry.canDelete).every((entry) => selectedEntryIds.includes(entry.id))
                              }
                              onChange={() => toggleGroupEntries(group.entries.filter((entry) => entry.canDelete).map((entry) => entry.id))}
                            />
                            Selecionar todos deste grupo
                          </label>
                        </div>
                      </Card>
                    ))}
                  </div>
                )}
              </CardContent>
            </Card>

              </section>

              <section
                role="tabpanel"
                id="finance-panel-cards"
                aria-labelledby="finance-tab-cards"
                hidden={activeSection !== "cards"}
                className="space-y-4"
              >
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
                    <Button variant="secondary" onClick={() => setImportDialogOpen(true)} disabled={!selectedCard}>
                      <Plus />
                      Importar JSON
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
                          <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
                            <div className="space-y-1">
                              <CardTitle className="text-base">Compras</CardTitle>
                              <InlineSyncLabel syncing={dashboard.syncingSections.cardTransactions} label="Sincronizando compras..." />
                            </div>
                            <Button
                              variant="ghost"
                              size="sm"
                              onClick={() =>
                                setDeleteTarget({
                                  kind: "transaction-bulk",
                                  ids: selectedVisibleTransactionIds,
                                  count: selectedVisibleTransactionIds.length,
                                })
                              }
                              disabled={selectedVisibleTransactionIds.length === 0}
                            >
                              <Trash2 />
                              Excluir selecionadas{selectedVisibleTransactionIds.length > 0 ? ` (${selectedVisibleTransactionIds.length})` : ""}
                            </Button>
                          </div>
                        </CardHeader>
                        <CardContent className="space-y-4 p-4">
                          {dashboard.cardDetailsLoading ? (
                            <LoadingState
                              title="Carregando compras"
                              description="Buscando as compras e as faturas do cartão selecionado."
                              icon={<CreditCard className="size-5 animate-pulse" />}
                            />
                          ) : (
                            <>
                              <Input
                                value={creditCardTransactionSearch}
                                onChange={(event) => setCreditCardTransactionSearch(event.target.value)}
                                placeholder="Filtrar por título, comerciante, categoria, classificação, fatura ou valor"
                                aria-label="Filtrar compras do cartão"
                              />
                              <div className="overflow-x-auto">
                                <Table>
                                  <TableHeader>
                                    <TableRow className="border-b border-border/60 bg-surface-muted hover:bg-surface-muted">
                                      <TableHead className="w-12">
                                        <input
                                          type="checkbox"
                                          checked={
                                            visibleSelectableTransactionIds.length > 0 &&
                                            visibleSelectableTransactionIds.every((transactionId) => selectedTransactionIds.includes(transactionId))
                                          }
                                          onChange={() => toggleAllTransactions(visibleSelectableTransactionIds)}
                                          disabled={visibleSelectableTransactionIds.length === 0}
                                          aria-label="Selecionar compras visíveis do cartão"
                                        />
                                      </TableHead>
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
                                    {filteredCreditCardTransactions.length === 0 ? (
                                      <TableRow>
                                        <TableCell colSpan={8} className="py-8 text-center text-sm text-muted-foreground">
                                          {creditCardTransactionSearch.trim()
                                            ? "Nenhuma compra corresponde ao filtro."
                                            : "Nenhuma compra registrada neste cartão."}
                                        </TableCell>
                                      </TableRow>
                                    ) : (
                                      filteredCreditCardTransactions.map((transaction) => {
                                        const rowKey = `transaction:${transaction.id}`;
                                        const rowBusy = isRowSaving(rowKey);

                                        return (
                                          <TableRow key={transaction.id}>
                                            <TableCell>
                                              <input
                                                type="checkbox"
                                                checked={selectedTransactionIds.includes(transaction.id)}
                                                onChange={() => toggleTransactionSelection(transaction.id)}
                                                disabled={!transaction.canDelete || rowBusy}
                                                aria-label={`Selecionar compra ${transaction.title}`}
                                              />
                                            </TableCell>
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
                                            <TableCell>{transaction.projectName ?? transaction.coreName ?? "Sem classificação"}</TableCell>
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
                            </>
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

              </section>
            </div>

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
                      description="Registre espaço, carro e outros bens de alto valor para manter o contexto patrimonial da space."
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
      </OrganizaClubWorkspaceShell>

      <EntryDialog
        open={Boolean(entryDialog)}
        entry={entryDialog?.entry ?? null}
        defaultEntryType={entryDialog?.entryType ?? "Saida"}
        activeYear={dashboard.activeYear}
        activeMonth={dashboard.activeMonth}
        categories={dashboard.categories}
        templates={dashboard.recurringTemplates}
        cores={dashboard.cores}
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
        cores={dashboard.cores}
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
        cores={dashboard.cores}
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

      <CreditCardTransactionImportDialog
        open={importDialogOpen}
        card={selectedCard}
        error={importError}
        loading={importParsing}
        importFileName={importFileName}
        onOpenChange={(open) => {
          setImportDialogOpen(open);
          if (!open) {
            setImportError(null);
          }
        }}
        onDownloadExample={downloadImportExample}
        onImportFile={handleImportFile}
      >
        <CreditCardTransactionImportReviewDialog
          open={importReviewOpen}
          card={selectedCard}
          drafts={importReviewDrafts}
          summary={importSummary}
          error={importError}
          loading={importSubmitting}
          categories={dashboard.categories}
          cores={dashboard.cores}
          projects={dashboard.projects}
          onOpenChange={(open) => {
            if (!open) {
              resetImportFlow();
            }
          }}
          onDraftChange={updateImportDraft}
          onAddDraft={addImportDraft}
          onRemoveDraft={removeImportDraft}
          onConfirm={handleImportConfirm}
        />
      </CreditCardTransactionImportDialog>

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
        title={deleteDialogTitle}
        description={deleteDialogDescription}
        confirmationTarget={deleteDialogRequiresTyping ? deleteTarget?.name : undefined}
        confirmationLabel={deleteDialogRequiresTyping ? `Digite ${deleteTarget?.name ?? ""} para confirmar` : undefined}
        confirmLabel="Excluir"
        impactItems={[deleteDialogImpact]}
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
  cores,
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
  cores: { id: string; name: string }[];
  projects: { id: string; name: string; coreId: string }[];
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
  const [coreId, setCoreId] = useState(entry?.coreId ?? "none");
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
    setCoreId(entry?.coreId ?? "none");
    setProjectId(entry?.projectId ?? "none");
    setError(null);
    setSaving(false);
  }, [activeMonth, activeYear, defaultEntryType, entry, open]);

  const scopedProjects = useMemo(
    () => (coreId === "none" ? projects : projects.filter((project) => project.coreId === coreId)),
    [projects, coreId],
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
        coreId: coreId === "none" ? null : coreId,
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
          <DialogDescription>Registre entradas e saídas do caixa mensal com classificação opcional por núcleo e projeto.</DialogDescription>
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
            <Field label="Núcleo">
              <Select value={coreId} onChange={(event) => setCoreId(event.target.value)}>
                <option value="none">Sem núcleo</option>
                {cores.map((core) => (
                  <option key={core.id} value={core.id}>
                    {core.name}
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
            Marcar como verificada
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
  cores,
  projects,
  onOpenChange,
  onSave,
}: {
  open: boolean;
  template: FinanceRecurringTemplate | null;
  categories: FinanceCategory[];
  cores: { id: string; name: string }[];
  projects: { id: string; name: string; coreId: string }[];
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
  const [coreId, setCoreId] = useState(template?.coreId ?? "none");
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
    setCoreId(template?.coreId ?? "none");
    setProjectId(template?.projectId ?? "none");
    setError(null);
    setSaving(false);
  }, [open, template]);

  const scopedProjects = useMemo(
    () => (coreId === "none" ? projects : projects.filter((project) => project.coreId === coreId)),
    [projects, coreId],
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
        coreId: coreId === "none" ? null : coreId,
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
            <Field label="Núcleo">
              <Select value={coreId} onChange={(event) => setCoreId(event.target.value)}>
                <option value="none">Sem núcleo</option>
                {cores.map((core) => (
                  <option key={core.id} value={core.id}>
                    {core.name}
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
          <DialogDescription>Cadastre o patrimônio do espaço com detalhes tipados para imóvel e veículo.</DialogDescription>
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
          <DialogDescription>Cadastre cartões do espaço para registrar compras e fechar faturas no período correto.</DialogDescription>
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
  cores,
  projects,
  onOpenChange,
  onSave,
}: {
  open: boolean;
  transaction: CreditCardTransaction | null;
  categories: FinanceCategory[];
  cores: { id: string; name: string }[];
  projects: { id: string; name: string; coreId: string }[];
  onOpenChange: (open: boolean) => void;
  onSave: (input: CreditCardTransactionFormInput) => Promise<void>;
}) {
  const [title, setTitle] = useState(transaction?.title ?? "");
  const [merchant, setMerchant] = useState(transaction?.merchant ?? "");
  const [amount, setAmount] = useState(transaction ? formatCurrency(transaction.amount) : "");
  const [purchasedOn, setPurchasedOn] = useState(transaction?.purchasedOn ?? formatDateOnlyInputValue());
  const [notes, setNotes] = useState(transaction?.notes ?? "");
  const [categoryId, setCategoryId] = useState(transaction?.categoryId ?? "none");
  const [coreId, setCoreId] = useState(transaction?.coreId ?? "none");
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
    setCoreId(transaction?.coreId ?? "none");
    setProjectId(transaction?.projectId ?? "none");
    setExternalSource(transaction?.externalSource ?? "");
    setExternalReference(transaction?.externalReference ?? "");
    setError(null);
    setSaving(false);
  }, [open, transaction]);

  const scopedProjects = useMemo(
    () => (coreId === "none" ? projects : projects.filter((project) => project.coreId === coreId)),
    [projects, coreId],
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
        coreId: coreId === "none" ? null : coreId,
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
            <Field label="Núcleo">
              <Select value={coreId} onChange={(event) => setCoreId(event.target.value)}>
                <option value="none">Sem núcleo</option>
                {cores.map((core) => (
                  <option key={core.id} value={core.id}>
                    {core.name}
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

function CreditCardTransactionImportDialog({
  open,
  card,
  error,
  loading,
  importFileName,
  onOpenChange,
  onDownloadExample,
  onImportFile,
  children,
}: {
  open: boolean;
  card: CreditCardAccount | null;
  error: string | null;
  loading: boolean;
  importFileName: string | null;
  onOpenChange: (open: boolean) => void;
  onDownloadExample: () => void;
  onImportFile: (file: File) => Promise<void>;
  children?: ReactNode;
}) {
  const [selectedFile, setSelectedFile] = useState<File | null>(null);

  useEffect(() => {
    if (!open) {
      setSelectedFile(null);
    }
  }, [open]);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selectedFile) {
      return;
    }

    await onImportFile(selectedFile);
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Importar compras por JSON</DialogTitle>
          <DialogDescription>
            {card
              ? `Envie um arquivo JSON para importar múltiplas compras no cartão ${card.name}.`
              : "Selecione um cartão para importar compras por JSON."}
          </DialogDescription>
        </DialogHeader>
        <form className="space-y-4" onSubmit={handleSubmit}>
          {error ? <Notice tone="danger">{error}</Notice> : null}
          {card ? (
            <div className="grid gap-3 rounded-xl border border-border/60 bg-surface-muted/60 p-4 sm:grid-cols-3">
              <InfoBlock label="Cartão" value={card.name} helper={card.brand ?? "Sem bandeira"} />
              <InfoBlock label="Fechamento" value={`Dia ${card.closingDay}`} />
              <InfoBlock label="Vencimento" value={`Dia ${card.dueDay}`} />
            </div>
          ) : null}
          <Notice tone="warning">
            Formato oficial: <code>{'{"transactions":[...]}'}</code> com os campos{" "}
            <code>title</code>, <code>merchant</code>, <code>amount</code>, <code>purchasedOn</code>, <code>notes</code>,{" "}
            <code>categoryName</code>, <code>coreName</code>, <code>projectName</code>, <code>externalSource</code>,{" "}
            <code>externalReference</code> e <code>importedAt</code>.
          </Notice>
          <div className="flex flex-wrap gap-2">
            <Button type="button" variant="secondary" onClick={onDownloadExample}>
              Baixar exemplo
            </Button>
          </div>
          <Field label="Arquivo JSON">
            <Input
              type="file"
              accept=".json,application/json"
              onChange={(event) => setSelectedFile(event.target.files?.[0] ?? null)}
            />
          </Field>
          {selectedFile || importFileName ? (
            <p className="text-sm text-muted-foreground">Arquivo selecionado: {selectedFile?.name ?? importFileName}</p>
          ) : null}
          <DialogFooter>
            <Button variant="secondary" type="button" onClick={() => onOpenChange(false)} disabled={loading}>
              Cancelar
            </Button>
            <Button type="submit" disabled={!card || !selectedFile || loading}>
              {loading ? "Lendo JSON..." : "Revisar importação"}
            </Button>
          </DialogFooter>
        </form>
        {children}
      </DialogContent>
    </Dialog>
  );
}

function CreditCardTransactionImportReviewDialog({
  open,
  card,
  drafts,
  summary,
  error,
  loading,
  categories,
  cores,
  projects,
  onOpenChange,
  onDraftChange,
  onAddDraft,
  onRemoveDraft,
  onConfirm,
}: {
  open: boolean;
  card: CreditCardAccount | null;
  drafts: ImportedCreditCardTransactionDraft[];
  summary: CreditCardTransactionImportSummary;
  error: string | null;
  loading: boolean;
  categories: FinanceCategory[];
  cores: { id: string; name: string }[];
  projects: { id: string; name: string; coreId: string }[];
  onOpenChange: (open: boolean) => void;
  onDraftChange: (localId: string, field: EditableImportedTransactionField, value: string) => void;
  onAddDraft: () => void;
  onRemoveDraft: (localId: string) => void;
  onConfirm: () => Promise<void>;
}) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="flex h-[92vh] w-[min(98vw,98rem)] max-w-none flex-col overflow-hidden p-0">
        <div className="border-b border-border/60 px-6 py-5">
          <DialogHeader className="space-y-2">
            <DialogTitle>Revisar importação</DialogTitle>
            <DialogDescription>
              {card
                ? `Confira, ajuste e confirme as compras que serão inseridas no cartão ${card.name}.`
                : "Confira, ajuste e confirme as compras importadas."}
            </DialogDescription>
          </DialogHeader>
        </div>

        <div className="flex-1 space-y-4 overflow-y-auto px-6 py-5">
          {error ? <Notice tone="danger">{error}</Notice> : null}
          {summary.invalidCount > 0 ? (
            <Notice tone="warning">
              Corrija as linhas inválidas antes de concluir a importação. O lote é salvo por completo ou rejeitado por completo.
            </Notice>
          ) : null}

          <div className="grid gap-3 md:grid-cols-4">
            <Card>
              <CardContent className="p-4">
                <InfoBlock label="Registros" value={String(summary.totalCount)} helper={`${summary.validCount} válidos`} />
              </CardContent>
            </Card>
            <Card>
              <CardContent className="p-4">
                <InfoBlock label="Valor total" value={formatCurrency(summary.totalAmount, "R$ 0,00")} />
              </CardContent>
            </Card>
            <Card>
              <CardContent className="p-4">
                <InfoBlock label="Linhas inválidas" value={String(summary.invalidCount)} />
              </CardContent>
            </Card>
            <Card>
              <CardContent className="p-4">
                <InfoBlock label="Novas categorias" value={String(summary.newCategoryCount)} />
              </CardContent>
            </Card>
          </div>

          <div className="flex flex-wrap justify-between gap-2">
            <Button type="button" variant="secondary" onClick={onAddDraft}>
              <Plus />
              Adicionar linha
            </Button>
            <p className="text-sm text-muted-foreground">Todos os campos da compra podem ser ajustados antes do envio.</p>
          </div>

          {drafts.length === 0 ? (
            <EmptyState
              icon={<CreditCard className="size-5" />}
              title="Nenhuma compra para revisar"
              description="Adicione ao menos uma linha para continuar com a importação."
            />
          ) : (
            <div className="overflow-x-auto rounded-xl border border-border/60">
              <Table>
                <TableHeader>
                  <TableRow className="border-b border-border/60 bg-surface-muted hover:bg-surface-muted">
                    <TableHead className="min-w-[180px]">Título</TableHead>
                    <TableHead className="min-w-[150px]">Merchant</TableHead>
                    <TableHead className="min-w-[130px]">Valor</TableHead>
                    <TableHead className="min-w-[140px]">Data</TableHead>
                    <TableHead className="min-w-[160px]">Categoria</TableHead>
                    <TableHead className="min-w-[160px]">Núcleo</TableHead>
                    <TableHead className="min-w-[160px]">Projeto</TableHead>
                    <TableHead className="min-w-[180px]">Origem externa</TableHead>
                    <TableHead className="min-w-[180px]">Referência externa</TableHead>
                    <TableHead className="min-w-[220px]">ImportedAt</TableHead>
                    <TableHead className="min-w-[220px]">Observações</TableHead>
                    <TableHead className="min-w-[220px]">Status</TableHead>
                    <TableHead className="w-24 text-right">Ações</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {drafts.map((draft, index) => (
                    <TableRow key={draft.localId}>
                      <TableCell>
                        <Input value={draft.title} onChange={(event) => onDraftChange(draft.localId, "title", event.target.value)} aria-label={`Título da linha ${index + 1}`} />
                      </TableCell>
                      <TableCell>
                        <Input value={draft.merchant} onChange={(event) => onDraftChange(draft.localId, "merchant", event.target.value)} aria-label={`Merchant da linha ${index + 1}`} />
                      </TableCell>
                      <TableCell>
                        <Input value={draft.amount} onChange={(event) => onDraftChange(draft.localId, "amount", event.target.value)} aria-label={`Valor da linha ${index + 1}`} />
                      </TableCell>
                      <TableCell>
                        <Input type="date" value={draft.purchasedOn} onChange={(event) => onDraftChange(draft.localId, "purchasedOn", event.target.value)} aria-label={`Data da compra da linha ${index + 1}`} />
                      </TableCell>
                      <TableCell>
                        <Input
                          list="finance-import-categories"
                          value={draft.categoryName}
                          onChange={(event) => onDraftChange(draft.localId, "categoryName", event.target.value)}
                          aria-label={`Categoria da linha ${index + 1}`}
                        />
                      </TableCell>
                      <TableCell>
                        <Input
                          list="finance-import-cores"
                          value={draft.coreName}
                          onChange={(event) => onDraftChange(draft.localId, "coreName", event.target.value)}
                          aria-label={`Núcleo da linha ${index + 1}`}
                        />
                      </TableCell>
                      <TableCell>
                        <Input
                          list="finance-import-projects"
                          value={draft.projectName}
                          onChange={(event) => onDraftChange(draft.localId, "projectName", event.target.value)}
                          aria-label={`Projeto da linha ${index + 1}`}
                        />
                      </TableCell>
                      <TableCell>
                        <Input value={draft.externalSource} onChange={(event) => onDraftChange(draft.localId, "externalSource", event.target.value)} aria-label={`Origem externa da linha ${index + 1}`} />
                      </TableCell>
                      <TableCell>
                        <Input value={draft.externalReference} onChange={(event) => onDraftChange(draft.localId, "externalReference", event.target.value)} aria-label={`Referência externa da linha ${index + 1}`} />
                      </TableCell>
                      <TableCell>
                        <Input value={draft.importedAt} onChange={(event) => onDraftChange(draft.localId, "importedAt", event.target.value)} placeholder="2026-07-08T12:00:00Z" aria-label={`ImportedAt da linha ${index + 1}`} />
                      </TableCell>
                      <TableCell>
                        <Input value={draft.notes} onChange={(event) => onDraftChange(draft.localId, "notes", event.target.value)} aria-label={`Observações da linha ${index + 1}`} />
                      </TableCell>
                      <TableCell className="align-top">
                        {draft.errors.length === 0 ? (
                          <span className="text-sm font-medium text-success">Pronta para importar</span>
                        ) : (
                          <div className="space-y-1 text-sm text-danger">
                            {draft.errors.map((draftError) => (
                              <p key={`${draft.localId}:${draftError.field}:${draftError.message}`}>{draftError.message}</p>
                            ))}
                          </div>
                        )}
                      </TableCell>
                      <TableCell className="text-right">
                        <Button type="button" variant="ghost" size="sm" onClick={() => onRemoveDraft(draft.localId)}>
                          <Trash2 />
                          Remover
                        </Button>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}
        </div>

        <datalist id="finance-import-categories">
          {categories.map((category) => (
            <option key={category.id} value={category.name} />
          ))}
        </datalist>
        <datalist id="finance-import-cores">
          {cores.map((core) => (
            <option key={core.id} value={core.name} />
          ))}
        </datalist>
        <datalist id="finance-import-projects">
          {projects.map((project) => (
            <option key={project.id} value={project.name} />
          ))}
        </datalist>

        <DialogFooter className="border-t border-border/60 px-6 py-4">
          <Button variant="secondary" type="button" onClick={() => onOpenChange(false)} disabled={loading}>
            Cancelar
          </Button>
          <Button type="button" onClick={() => void onConfirm()} disabled={loading || drafts.length === 0 || summary.invalidCount > 0}>
            {loading ? "Importando..." : "Confirmar importação"}
          </Button>
        </DialogFooter>
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

  const allAvailableTransactionIds = useMemo(() => availableTransactions.map((transaction) => transaction.id), [availableTransactions]);
  const allAvailableTransactionsSelected =
    allAvailableTransactionIds.length > 0 && allAvailableTransactionIds.every((transactionId) => transactionIds.includes(transactionId));

  function toggleTransaction(transactionId: string) {
    setTransactionIds((current) =>
      current.includes(transactionId) ? current.filter((item) => item !== transactionId) : [...current, transactionId],
    );
  }

  function toggleAllTransactions() {
    if (allAvailableTransactionIds.length === 0) {
      return;
    }

    setTransactionIds((current) =>
      allAvailableTransactionsSelected
        ? current.filter((transactionId) => !allAvailableTransactionIds.includes(transactionId))
        : Array.from(new Set([...current, ...allAvailableTransactionIds])),
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
            <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
              <p className="text-sm font-semibold text-foreground">Compras da fatura</p>
              <Button type="button" variant="secondary" size="sm" onClick={toggleAllTransactions} disabled={allAvailableTransactionIds.length === 0}>
                {allAvailableTransactionsSelected ? "Desmarcar todas" : "Selecionar todas"}
              </Button>
            </div>
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
                        <TableCell>{template.projectName ?? template.coreName ?? "Sem classificação"}</TableCell>
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
