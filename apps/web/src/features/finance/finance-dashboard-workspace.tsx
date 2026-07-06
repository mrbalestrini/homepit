"use client";

import { type FormEvent, type ReactNode, useEffect, useMemo, useState } from "react";
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
  { value: 3, label: "Marco" },
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

export function FinanceDashboardWorkspace({ dashboard }: { dashboard: FinanceDashboardController }) {
  const [filters, setFilters] = useState<FinanceEntryFilters>(defaultFilters);
  const [entryDialog, setEntryDialog] = useState<EntryDialogState | null>(null);
  const [templateDialog, setTemplateDialog] = useState<FinanceRecurringTemplate | null | "create">(null);
  const [assetDialog, setAssetDialog] = useState<Asset | null | "create">(null);
  const [valuationDialog, setValuationDialog] = useState<{ asset: Asset; valuation?: AssetValuation | null } | null>(null);
  const [cardDialog, setCardDialog] = useState<CreditCardAccount | null | "create">(null);
  const [transactionDialog, setTransactionDialog] = useState<CreditCardTransaction | null | "create">(null);
  const [statementDialog, setStatementDialog] = useState<CreditCardStatement | null | "create">(null);
  const [generateDialogOpen, setGenerateDialogOpen] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<
    | { kind: "entry"; id: string; name: string }
    | { kind: "template"; id: string; name: string }
    | { kind: "asset"; id: string; name: string }
    | { kind: "valuation"; assetId: string; id: string; name: string }
    | { kind: "card"; id: string; name: string }
    | { kind: "transaction"; id: string; name: string }
    | { kind: "statement"; id: string; name: string }
    | null
  >(null);

  const entries = dashboard.periodDetail?.entries ?? [];
  const filteredEntries = useMemo(() => filterFinanceEntries(entries, filters), [entries, filters]);
  const groupedEntries = useMemo(() => groupFinanceEntries(filteredEntries, filters.groupBy), [filteredEntries, filters.groupBy]);

  const periodSummary = dashboard.periodDetail?.summary;
  const headerStats = [
    { label: "Lancamentos", value: entries.length },
    { label: "Recorrencias", value: dashboard.recurringTemplates.length },
    { label: "Cartoes", value: dashboard.creditCardAccounts.length },
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

    if (deleteTarget.kind === "entry") {
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
        visibleLabel="lancamentos"
        headerStats={headerStats}
      >
        <Card>
          <CardContent className="flex flex-col gap-4 p-5 sm:p-6 lg:flex-row lg:items-end lg:justify-between">
            <div className="max-w-2xl">
              <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">Financeiro</p>
              <h1 className="mt-2 text-2xl font-semibold text-foreground">Operacao financeira da casa</h1>
              <p className="mt-2 text-sm leading-6 text-muted-foreground">
                Controle o caixa do mes, mantenha recorrencias, acompanhe cartoes e preserve o patrimonio em um unico lugar.
              </p>
            </div>

            <div className="grid gap-2 sm:grid-cols-4">
              <Select
                value={String(dashboard.activeYear)}
                onChange={(event) => dashboard.setActivePeriod(Number(event.target.value), dashboard.activeMonth)}
                aria-label="Ano do periodo"
              >
                {selectableYears.map((year) => (
                  <option key={year} value={year}>
                    {year}
                  </option>
                ))}
              </Select>
              <Select
                value={String(dashboard.activeMonth)}
                onChange={(event) => dashboard.setActivePeriod(dashboard.activeYear, Number(event.target.value))}
                aria-label="Mes do periodo"
              >
                {monthOptions.map((month) => (
                  <option key={month.value} value={month.value}>
                    {month.label}
                  </option>
                ))}
              </Select>
              <Button variant="secondary" onClick={() => void dashboard.refreshWorkspace()}>
                <RefreshCw />
                Atualizar
              </Button>
              <Button onClick={handleGenerateClick}>
                <CalendarRange />
                Gerar mes
              </Button>
            </div>
          </CardContent>
        </Card>

        {dashboard.loading && !dashboard.periodDetail ? (
          <LoadingState
            title="Carregando financeiro"
            description="Estamos reunindo o periodo mensal, recorrencias, cartoes e patrimonio da casa."
            icon={<Wallet className="size-5 animate-pulse" />}
          />
        ) : (
          <>
            <div className="grid gap-4 xl:grid-cols-4">
              <MetricCard label="Periodo" value={formatMonthLabel(dashboard.activeYear, dashboard.activeMonth)} helper={dashboard.periodDetail?.exists ? "Periodo existente" : "Periodo ainda nao gerado"} />
              <MetricCard label="Entradas" value={formatCurrency(periodSummary?.totalIncome ?? 0, "R$ 0,00")} helper="Fluxo de caixa do mes" />
              <MetricCard label="Saidas" value={formatCurrency(periodSummary?.totalExpense ?? 0, "R$ 0,00")} helper="Inclui a fatura consolidada" />
              <MetricCard label="Saldo" value={formatCurrency(periodSummary?.cashBalance ?? 0, "R$ 0,00")} helper={`${periodSummary?.pendingVerificationEntries ?? 0} pendentes de verificacao`} accent={(periodSummary?.cashBalance ?? 0) < 0 ? "danger" : "success"} />
            </div>

            <Card>
              <CardHeader className="border-b border-border/60 pb-4">
                <CardTitle className="text-lg">Resumo</CardTitle>
              </CardHeader>
              <CardContent className="grid gap-4 p-4 lg:grid-cols-3">
                <InfoBlock label="Gasto analitico do mes" value={formatCurrency(periodSummary?.analyticalExpenseTotal ?? 0, "R$ 0,00")} helper="Caixa sem fatura consolidada + compras de cartao do mes" />
                <InfoBlock label="Compras em cartao" value={String(periodSummary?.cardPurchaseCount ?? 0)} helper="Quantidade de compras no periodo analitico" />
                <InfoBlock label="Verificados" value={`${periodSummary?.verifiedEntries ?? 0}/${entries.length}`} helper="Lancamentos revisados no caixa mensal" />
              </CardContent>
            </Card>

            <Card>
              <CardHeader className="border-b border-border/60 pb-4">
                <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
                  <CardTitle className="text-lg">Caixa</CardTitle>
                  <div className="flex flex-wrap gap-2">
                    <Button variant="secondary" onClick={() => setEntryDialog({ mode: "create", entryType: "Entrada" })}>
                      <Plus />
                      Nova entrada
                    </Button>
                    <Button onClick={() => setEntryDialog({ mode: "create", entryType: "Saida" })}>
                      <Plus />
                      Nova saida
                    </Button>
                  </div>
                </div>
              </CardHeader>
              <CardContent className="space-y-4 p-4">
                <div className="grid gap-3 md:grid-cols-3 xl:grid-cols-6">
                  <Input
                    value={filters.search}
                    onChange={(event) => setFilters((current) => ({ ...current, search: event.target.value }))}
                    placeholder="Buscar lancamento"
                  />
                  <Select value={filters.type} onChange={(event) => setFilters((current) => ({ ...current, type: event.target.value as FinanceEntryFilters["type"] }))}>
                    <option value="all">Todos os tipos</option>
                    <option value="Entrada">Entradas</option>
                    <option value="Saida">Saidas</option>
                  </Select>
                  <Select value={filters.verified} onChange={(event) => setFilters((current) => ({ ...current, verified: event.target.value as FinanceEntryFilters["verified"] }))}>
                    <option value="all">Todos</option>
                    <option value="verified">Verificados</option>
                    <option value="pending">Pendentes</option>
                  </Select>
                  <Select value={filters.origin} onChange={(event) => setFilters((current) => ({ ...current, origin: event.target.value as FinanceEntryOrigin | "all" }))}>
                    <option value="all">Todas as origens</option>
                    <option value="Manual">Manual</option>
                    <option value="RecurringTemplate">Recorrencia</option>
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
                    title="Nenhum lancamento encontrado"
                    description="Ajuste os filtros ou gere o periodo para começar a operar o caixa."
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
                                  <TableHead>Data</TableHead>
                                  <TableHead>Projeto</TableHead>
                                  <TableHead>Valor</TableHead>
                                  <TableHead>Verificado</TableHead>
                                  <TableHead className="min-w-[220px] text-right">Acoes</TableHead>
                                </TableRow>
                              </TableHeader>
                              <TableBody>
                                {group.entries.map((entry) => (
                                  <TableRow key={entry.id}>
                                    <TableCell>
                                      <div className="space-y-1">
                                        <p className="font-medium text-foreground">{entry.title}</p>
                                        {entry.notes ? <p className="text-sm text-muted-foreground">{entry.notes}</p> : null}
                                      </div>
                                    </TableCell>
                                    <TableCell>{entry.type === "Entrada" ? "Entrada" : "Saida"}</TableCell>
                                    <TableCell>{formatOrigin(entry.origin)}</TableCell>
                                    <TableCell>{formatDateOnlyPtBr(entry.referenceDate)}</TableCell>
                                    <TableCell>{entry.projectName ?? entry.universeName ?? "Sem classificacao"}</TableCell>
                                    <TableCell className={`font-medium ${entry.type === "Entrada" ? "text-success" : "text-danger"}`}>
                                      {formatCurrency(entry.amount)}
                                    </TableCell>
                                    <TableCell>
                                      <label className="inline-flex items-center gap-2 text-sm">
                                        <input
                                          type="checkbox"
                                          checked={entry.verified}
                                          disabled={!entry.canEdit}
                                          onChange={() => void dashboard.toggleEntryVerified(entry)}
                                        />
                                        {entry.verified ? "Sim" : "Nao"}
                                      </label>
                                    </TableCell>
                                    <TableCell className="text-right">
                                      <div className="flex flex-wrap justify-end gap-2">
                                        <Button
                                          variant="secondary"
                                          size="sm"
                                          onClick={() => setEntryDialog({ mode: "edit", entryType: entry.type, entry })}
                                          disabled={!entry.canEdit}
                                        >
                                          <Pencil />
                                          Editar
                                        </Button>
                                        <Button
                                          variant="ghost"
                                          size="sm"
                                          onClick={() => setDeleteTarget({ kind: "entry", id: entry.id, name: entry.title })}
                                          disabled={!entry.canDelete}
                                        >
                                          <Trash2 />
                                          Excluir
                                        </Button>
                                      </div>
                                    </TableCell>
                                  </TableRow>
                                ))}
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
                  <CardTitle className="text-lg">Recorrencias</CardTitle>
                  <Button onClick={() => setTemplateDialog("create")}>
                    <Plus />
                    Nova recorrencia
                  </Button>
                </div>
              </CardHeader>
              <CardContent className="p-0">
                {dashboard.recurringTemplates.length === 0 ? (
                  <div className="p-4">
                    <EmptyState
                      icon={<Repeat2 className="size-5" />}
                      title="Nenhuma recorrencia configurada"
                      description="Cadastre recorrencias mensais ou anuais para acelerar a geracao do caixa."
                    />
                  </div>
                ) : (
                  <div className="overflow-x-auto">
                    <Table>
                      <TableHeader>
                        <TableRow className="border-b border-border/60 bg-surface-muted hover:bg-surface-muted">
                          <TableHead className="min-w-[180px]">Titulo</TableHead>
                          <TableHead>Tipo</TableHead>
                          <TableHead>Recorrencia</TableHead>
                          <TableHead>Valor padrao</TableHead>
                          <TableHead>Classificacao</TableHead>
                          <TableHead>Status</TableHead>
                          <TableHead className="min-w-[220px] text-right">Acoes</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {dashboard.recurringTemplates.map((template) => (
                          <TableRow key={template.id}>
                            <TableCell>
                              <div className="space-y-1">
                                <p className="font-medium text-foreground">{template.title}</p>
                                {template.notes ? <p className="text-sm text-muted-foreground">{template.notes}</p> : null}
                              </div>
                            </TableCell>
                            <TableCell>{template.type === "Entrada" ? "Entrada" : "Saida"}</TableCell>
                            <TableCell>{formatRecurrence(template.recurrence, template.dayOfMonth, template.monthOfYear)}</TableCell>
                            <TableCell>{formatCurrency(template.defaultAmount)}</TableCell>
                            <TableCell>{template.projectName ?? template.universeName ?? "Sem classificacao"}</TableCell>
                            <TableCell>{template.isActive ? "Ativa" : "Inativa"}</TableCell>
                            <TableCell className="text-right">
                              <div className="flex flex-wrap justify-end gap-2">
                                <Button variant="secondary" size="sm" onClick={() => setTemplateDialog(template)} disabled={!template.canEdit}>
                                  <Pencil />
                                  Editar
                                </Button>
                                <Button
                                  variant="ghost"
                                  size="sm"
                                  onClick={() => setDeleteTarget({ kind: "template", id: template.id, name: template.title })}
                                  disabled={!template.canDelete}
                                >
                                  <Trash2 />
                                  Excluir
                                </Button>
                              </div>
                            </TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  </div>
                )}
              </CardContent>
            </Card>

            <Card>
              <CardHeader className="border-b border-border/60 pb-4">
                <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
                  <CardTitle className="text-lg">Cartoes</CardTitle>
                  <div className="flex flex-wrap gap-2">
                    <Button variant="secondary" onClick={() => setCardDialog("create")}>
                      <Plus />
                      Novo cartao
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
                      title="Nenhum cartao cadastrado"
                      description="Crie o primeiro cartao para registrar compras e fechar faturas."
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
                              {card.brand ?? "Sem bandeira"}{card.lastFourDigits ? ` • ${card.lastFourDigits}` : ""}
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
                          <CardTitle className="text-base">Compras</CardTitle>
                        </CardHeader>
                        <CardContent className="p-0">
                          {dashboard.cardDetailsLoading ? (
                            <div className="p-4">
                              <LoadingState title="Carregando compras" description="Buscando as compras e as faturas do cartao selecionado." icon={<CreditCard className="size-5 animate-pulse" />} />
                            </div>
                          ) : (
                            <div className="overflow-x-auto">
                              <Table>
                                <TableHeader>
                                  <TableRow className="border-b border-border/60 bg-surface-muted hover:bg-surface-muted">
                                    <TableHead className="min-w-[180px]">Compra</TableHead>
                                    <TableHead>Data</TableHead>
                                    <TableHead>Classificacao</TableHead>
                                    <TableHead>Fatura</TableHead>
                                    <TableHead>Valor</TableHead>
                                    <TableHead className="min-w-[200px] text-right">Acoes</TableHead>
                                  </TableRow>
                                </TableHeader>
                                <TableBody>
                                  {dashboard.creditCardTransactions.length === 0 ? (
                                    <TableRow>
                                      <TableCell colSpan={6} className="py-8 text-center text-sm text-muted-foreground">
                                        Nenhuma compra registrada neste cartao.
                                      </TableCell>
                                    </TableRow>
                                  ) : (
                                    dashboard.creditCardTransactions.map((transaction) => (
                                      <TableRow key={transaction.id}>
                                        <TableCell>
                                          <div className="space-y-1">
                                            <p className="font-medium text-foreground">{transaction.title}</p>
                                            {transaction.merchant ? <p className="text-sm text-muted-foreground">{transaction.merchant}</p> : null}
                                          </div>
                                        </TableCell>
                                        <TableCell>{formatDateOnlyPtBr(transaction.purchasedOn)}</TableCell>
                                        <TableCell>{transaction.projectName ?? transaction.universeName ?? "Sem classificacao"}</TableCell>
                                        <TableCell>{transaction.creditCardStatementId ? "Fechada" : "Em aberto"}</TableCell>
                                        <TableCell className="font-medium text-foreground">{formatCurrency(transaction.amount)}</TableCell>
                                        <TableCell className="text-right">
                                          <div className="flex flex-wrap justify-end gap-2">
                                            <Button variant="secondary" size="sm" onClick={() => setTransactionDialog(transaction)} disabled={!transaction.canEdit}>
                                              <Pencil />
                                              Editar
                                            </Button>
                                            <Button
                                              variant="ghost"
                                              size="sm"
                                              onClick={() => setDeleteTarget({ kind: "transaction", id: transaction.id, name: transaction.title })}
                                              disabled={!transaction.canDelete}
                                            >
                                              <Trash2 />
                                              Excluir
                                            </Button>
                                          </div>
                                        </TableCell>
                                      </TableRow>
                                    ))
                                  )}
                                </TableBody>
                              </Table>
                            </div>
                          )}
                        </CardContent>
                      </Card>

                      <Card>
                        <CardHeader className="border-b border-border/60 pb-3">
                          <CardTitle className="text-base">Faturas</CardTitle>
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
                                  <TableHead className="min-w-[200px] text-right">Acoes</TableHead>
                                </TableRow>
                              </TableHeader>
                              <TableBody>
                                {dashboard.creditCardStatements.length === 0 ? (
                                  <TableRow>
                                    <TableCell colSpan={5} className="py-8 text-center text-sm text-muted-foreground">
                                      Nenhuma fatura fechada neste cartao.
                                    </TableCell>
                                  </TableRow>
                                ) : (
                                  dashboard.creditCardStatements.map((statement) => (
                                    <TableRow key={statement.id}>
                                      <TableCell>{formatDateOnlyPtBr(statement.closingDate)}</TableCell>
                                      <TableCell>{formatDateOnlyPtBr(statement.dueDate)}</TableCell>
                                      <TableCell>{statement.transactionCount}</TableCell>
                                      <TableCell className="font-medium text-foreground">{formatCurrency(statement.totalAmount)}</TableCell>
                                      <TableCell className="text-right">
                                        <div className="flex flex-wrap justify-end gap-2">
                                          <Button variant="secondary" size="sm" onClick={() => setStatementDialog(statement)} disabled={!statement.canEdit}>
                                            <Pencil />
                                            Editar
                                          </Button>
                                          <Button
                                            variant="ghost"
                                            size="sm"
                                            onClick={() => setDeleteTarget({ kind: "statement", id: statement.id, name: `fatura ${formatDateOnlyPtBr(statement.dueDate)}` })}
                                            disabled={!statement.canDelete}
                                          >
                                            <Trash2 />
                                            Excluir
                                          </Button>
                                        </div>
                                      </TableCell>
                                    </TableRow>
                                  ))
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
                      title="Selecione um cartao"
                      description="Escolha um cartao para ver compras abertas, faturas e a integracao com o caixa mensal."
                    />
                  )}
                </div>
              </CardContent>
            </Card>

            <Card>
              <CardHeader className="border-b border-border/60 pb-4">
                <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
                  <CardTitle className="text-lg">Patrimonio</CardTitle>
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
                      description="Registre casa, carro e outros bens de alto valor para manter contexto patrimonial da household."
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
                            label="Imovel"
                            value={asset.propertyDetails.propertyInscription ?? asset.propertyDetails.registryNumber ?? "Sem detalhes"}
                            helper={asset.propertyDetails.privateAreaSquareMeters ? `${asset.propertyDetails.privateAreaSquareMeters} m²` : undefined}
                          />
                        ) : null}
                        {asset.type === "Vehicle" && asset.vehicleDetails ? (
                          <InfoBlock
                            label="Veiculo"
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
                            Referencias anuais
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

      <DeleteConfirmationDialog
        open={Boolean(deleteTarget)}
        title="Excluir registro"
        description="Essa acao remove o registro selecionado."
        confirmationTarget={deleteTarget?.name}
        confirmationLabel={`Digite ${deleteTarget?.name ?? ""} para confirmar`}
        confirmLabel="Excluir"
        impactItems={["A exclusao e permanente e atualiza os totais e relacionamentos do modulo financeiro."]}
        onOpenChange={(open) => !open && setDeleteTarget(null)}
        onConfirm={handleDeleteConfirm}
      />
    </>
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
    return "Recorrencia";
  }

  return "Fatura";
}

function formatRecurrence(recurrence: FinanceRecurrence, dayOfMonth?: number | null, monthOfYear?: number | null) {
  if (recurrence === "Monthly") {
    return dayOfMonth ? `Mensal • dia ${dayOfMonth}` : "Mensal";
  }

  const monthLabel = monthOfYear ? monthOptions.find((item) => item.value === monthOfYear)?.label ?? monthOfYear : "mes indefinido";
  return dayOfMonth ? `Anual • ${monthLabel} • dia ${dayOfMonth}` : `Anual • ${monthLabel}`;
}

function formatAssetType(type: AssetType) {
  if (type === "Property") {
    return "Imovel";
  }

  if (type === "Vehicle") {
    return "Veiculo";
  }

  return "Outro bem";
}

function EntryDialog({
  open,
  entry,
  defaultEntryType,
  activeYear,
  activeMonth,
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
      setError("Informe o titulo do lancamento.");
      return;
    }

    if (parsedAmount == null || parsedAmount < 0) {
      setError("Informe um valor valido para o lancamento.");
      return;
    }

    if (!referenceDate) {
      setError("Informe a data de referencia.");
      return;
    }

    if (!referenceDate.startsWith(`${year}-${String(month).padStart(2, "0")}-`)) {
      setError("A data de referencia deve pertencer ao periodo selecionado.");
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
        universeId: universeId === "none" ? null : universeId,
        projectId: projectId === "none" ? null : projectId,
      });
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Nao foi possivel salvar o lancamento.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[88vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{entry ? "Editar lancamento" : "Novo lancamento"}</DialogTitle>
          <DialogDescription>Registre entradas e saidas do caixa mensal com classificacao opcional por universo e projeto.</DialogDescription>
        </DialogHeader>
        <form className="space-y-4" onSubmit={handleSubmit}>
          {error ? <Notice tone="danger">{error}</Notice> : null}
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Ano">
              <Input type="number" min={2000} max={9999} value={year} onChange={(event) => setYear(Number(event.target.value))} />
            </Field>
            <Field label="Mes">
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
            <Field label="Titulo">
              <Input value={title} onChange={(event) => setTitle(event.target.value)} />
            </Field>
            <Field label="Tipo">
              <Select value={type} onChange={(event) => setType(event.target.value as FinanceEntryType)}>
                <option value="Entrada">Entrada</option>
                <option value="Saida">Saida</option>
              </Select>
            </Field>
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Valor">
              <Input value={amount} onChange={(event) => setAmount(event.target.value)} placeholder="R$ 0,00" />
            </Field>
            <Field label="Data de referencia">
              <Input type="date" value={referenceDate} onChange={(event) => setReferenceDate(event.target.value)} />
            </Field>
          </div>
          <div className="grid gap-4 sm:grid-cols-3">
            <Field label="Recorrencia">
              <Select value={recurringTemplateId} onChange={(event) => setRecurringTemplateId(event.target.value)}>
                <option value="none">Sem recorrencia</option>
                {templates.map((template) => (
                  <option key={template.id} value={template.id}>
                    {template.title}
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
          <Field label="Observacoes">
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
              Salvar lancamento
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
  universes,
  projects,
  onOpenChange,
  onSave,
}: {
  open: boolean;
  template: FinanceRecurringTemplate | null;
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
      setError("Informe o titulo da recorrencia.");
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
        universeId: universeId === "none" ? null : universeId,
        projectId: projectId === "none" ? null : projectId,
      });
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Nao foi possivel salvar a recorrencia.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[88vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{template ? "Editar recorrencia" : "Nova recorrencia"}</DialogTitle>
          <DialogDescription>Configure itens mensais e anuais para acelerar a geracao do caixa mensal.</DialogDescription>
        </DialogHeader>
        <form className="space-y-4" onSubmit={handleSubmit}>
          {error ? <Notice tone="danger">{error}</Notice> : null}
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Titulo">
              <Input value={title} onChange={(event) => setTitle(event.target.value)} />
            </Field>
            <Field label="Tipo">
              <Select value={type} onChange={(event) => setType(event.target.value as FinanceEntryType)}>
                <option value="Entrada">Entrada</option>
                <option value="Saida">Saida</option>
              </Select>
            </Field>
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Valor padrao">
              <Input value={defaultAmount} onChange={(event) => setDefaultAmount(event.target.value)} />
            </Field>
            <Field label="Recorrencia">
              <Select value={recurrence} onChange={(event) => setRecurrence(event.target.value as FinanceRecurrence)}>
                <option value="Monthly">Mensal</option>
                <option value="Annual">Anual</option>
              </Select>
            </Field>
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Dia de referencia">
              <Input type="number" min={1} max={31} value={dayOfMonth} onChange={(event) => setDayOfMonth(event.target.value)} />
            </Field>
            <Field label="Mes anual">
              <Select value={monthOfYear || "none"} onChange={(event) => setMonthOfYear(event.target.value === "none" ? "" : event.target.value)} disabled={recurrence !== "Annual"}>
                <option value="none">Nao se aplica</option>
                {monthOptions.map((month) => (
                  <option key={month.value} value={month.value}>
                    {month.label}
                  </option>
                ))}
              </Select>
            </Field>
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
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
          <Field label="Observacoes">
            <Textarea value={notes} onChange={(event) => setNotes(event.target.value)} rows={3} />
          </Field>
          <label className="inline-flex items-center gap-2 text-sm font-medium text-foreground">
            <input type="checkbox" checked={isActive} onChange={(event) => setIsActive(event.target.checked)} />
            Recorrencia ativa
          </label>
          <DialogFooter>
            <Button variant="secondary" type="button" onClick={() => onOpenChange(false)} disabled={saving}>
              Cancelar
            </Button>
            <Button type="submit" disabled={saving}>
              Salvar recorrencia
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
      setError("Informe o titulo do bem.");
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
      setError(exception instanceof Error ? exception.message : "Nao foi possivel salvar o bem.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[88vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{asset ? "Editar bem" : "Novo bem"}</DialogTitle>
          <DialogDescription>Cadastre patrimonio da casa com detalhes tipados para imovel e veiculo.</DialogDescription>
        </DialogHeader>
        <form className="space-y-4" onSubmit={handleSubmit}>
          {error ? <Notice tone="danger">{error}</Notice> : null}
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Titulo">
              <Input value={title} onChange={(event) => setTitle(event.target.value)} />
            </Field>
            <Field label="Tipo">
              <Select value={type} onChange={(event) => setType(event.target.value as AssetType)}>
                <option value="Other">Outro bem</option>
                <option value="Property">Imovel</option>
                <option value="Vehicle">Veiculo</option>
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
          <Field label="Observacoes">
            <Textarea value={notes} onChange={(event) => setNotes(event.target.value)} rows={4} />
          </Field>
          <label className="inline-flex items-center gap-2 text-sm font-medium text-foreground">
            <input type="checkbox" checked={isPaidOff} onChange={(event) => setIsPaidOff(event.target.checked)} />
            Bem quitado
          </label>

          {type === "Property" ? (
            <div className="grid gap-4 rounded-[18px] border border-border/70 p-4 sm:grid-cols-2">
              <Field label="Matricula">
                <Input value={registryNumber} onChange={(event) => setRegistryNumber(event.target.value)} />
              </Field>
              <Field label="Inscricao">
                <Input value={propertyInscription} onChange={(event) => setPropertyInscription(event.target.value)} />
              </Field>
              <Field label="Area privativa (m²)">
                <Input value={privateAreaSquareMeters} onChange={(event) => setPrivateAreaSquareMeters(event.target.value)} />
              </Field>
              <Field label="Pesquisa debito">
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
  onOpenChange,
  onCreate,
  onEdit,
  onSave,
  onDelete,
}: {
  open: boolean;
  asset: Asset | null;
  valuation: AssetValuation | null;
  valuations: AssetValuation[];
  loading: boolean;
  onOpenChange: (open: boolean) => void;
  onCreate: (input: AssetValuationFormInput) => Promise<void>;
  onEdit: (valuation: AssetValuation) => void;
  onSave: (input: AssetValuationFormInput) => Promise<void>;
  onDelete: (valuation: AssetValuation) => void;
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
      setError("Informe o rotulo da referencia anual.");
      return;
    }

    if (parsedAmount == null || parsedAmount <= 0) {
      setError("Informe um valor positivo para a referencia anual.");
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
      setError(exception instanceof Error ? exception.message : "Nao foi possivel salvar a referencia anual.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[88vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Referencias anuais</DialogTitle>
          <DialogDescription>{asset ? `Registre FIPE, avaliacao ou outro valor anual para ${asset.title}.` : "Registre referencias anuais."}</DialogDescription>
        </DialogHeader>
        <div className="space-y-4">
          {loading ? <Notice tone="warning">Carregando referencias anuais...</Notice> : null}
          <div className="space-y-2">
            {valuations.map((item) => (
              <div key={item.id} className="flex flex-wrap items-center justify-between gap-3 rounded-[16px] border border-border/60 px-4 py-3">
                <div>
                  <p className="font-medium text-foreground">{item.label}</p>
                  <p className="text-sm text-muted-foreground">
                    {item.referenceYear} • {formatCurrency(item.amount)}
                  </p>
                </div>
                <div className="flex gap-2">
                  <Button variant="secondary" size="sm" onClick={() => onEdit(item)} disabled={!item.canEdit}>
                    <Pencil />
                    Editar
                  </Button>
                  <Button variant="ghost" size="sm" onClick={() => onDelete(item)} disabled={!item.canDelete}>
                    <Trash2 />
                    Excluir
                  </Button>
                </div>
              </div>
            ))}
          </div>
          <form className="space-y-4 rounded-[18px] border border-border/70 p-4" onSubmit={handleSubmit}>
            {error ? <Notice tone="danger">{error}</Notice> : null}
            <div className="grid gap-4 sm:grid-cols-2">
              <Field label="Ano">
                <Input type="number" min={2000} max={9999} value={referenceYear} onChange={(event) => setReferenceYear(Number(event.target.value))} />
              </Field>
              <Field label="Rotulo">
                <Input value={label} onChange={(event) => setLabel(event.target.value)} placeholder="FIPE, Avaliacao, etc." />
              </Field>
            </div>
            <Field label="Valor">
              <Input value={amount} onChange={(event) => setAmount(event.target.value)} placeholder="R$ 0,00" />
            </Field>
            <Field label="Observacoes">
              <Textarea value={notes} onChange={(event) => setNotes(event.target.value)} rows={3} />
            </Field>
            <DialogFooter>
              <Button variant="secondary" type="button" onClick={() => onOpenChange(false)} disabled={saving}>
                Fechar
              </Button>
              <Button type="submit" disabled={saving}>
                {valuation ? "Salvar referencia" : "Adicionar referencia"}
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
      setError("Informe o nome do cartao.");
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
      setError(exception instanceof Error ? exception.message : "Nao foi possivel salvar o cartao.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[88vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{card ? "Editar cartao" : "Novo cartao"}</DialogTitle>
          <DialogDescription>Cadastre cartoes da casa para registrar compras e fechar faturas no periodo correto.</DialogDescription>
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
            <Field label="Ultimos 4 digitos">
              <Input value={lastFourDigits} onChange={(event) => setLastFourDigits(event.target.value)} maxLength={4} />
            </Field>
            <Field label="Dia de fechamento">
              <Input type="number" min={1} max={31} value={closingDay} onChange={(event) => setClosingDay(Number(event.target.value))} />
            </Field>
            <Field label="Dia de vencimento">
              <Input type="number" min={1} max={31} value={dueDay} onChange={(event) => setDueDay(Number(event.target.value))} />
            </Field>
          </div>
          <Field label="Observacoes">
            <Textarea value={notes} onChange={(event) => setNotes(event.target.value)} rows={3} />
          </Field>
          <label className="inline-flex items-center gap-2 text-sm font-medium text-foreground">
            <input type="checkbox" checked={isActive} onChange={(event) => setIsActive(event.target.checked)} />
            Cartao ativo
          </label>
          <DialogFooter>
            <Button variant="secondary" type="button" onClick={() => onOpenChange(false)} disabled={saving}>
              Cancelar
            </Button>
            <Button type="submit" disabled={saving}>
              Salvar cartao
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
  universes,
  projects,
  onOpenChange,
  onSave,
}: {
  open: boolean;
  transaction: CreditCardTransaction | null;
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
      setError("Informe o titulo da compra.");
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
        universeId: universeId === "none" ? null : universeId,
        projectId: projectId === "none" ? null : projectId,
        externalSource: externalSource.trim(),
        externalReference: externalReference.trim(),
      });
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Nao foi possivel salvar a compra.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[88vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{transaction ? "Editar compra" : "Nova compra"}</DialogTitle>
          <DialogDescription>Registre compras do cartao antes de fechar a fatura do periodo correspondente.</DialogDescription>
        </DialogHeader>
        <form className="space-y-4" onSubmit={handleSubmit}>
          {error ? <Notice tone="danger">{error}</Notice> : null}
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Titulo">
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
          <div className="grid gap-4 sm:grid-cols-2">
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
          <Field label="Observacoes">
            <Textarea value={notes} onChange={(event) => setNotes(event.target.value)} rows={3} />
          </Field>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Origem externa">
              <Input value={externalSource} onChange={(event) => setExternalSource(event.target.value)} placeholder="SMS, XLS, etc." />
            </Field>
            <Field label="Referencia externa">
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
      setError("Informe fechamento e vencimento da fatura.");
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
      setError(exception instanceof Error ? exception.message : "Nao foi possivel salvar a fatura.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[88vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{statement ? "Editar fatura" : "Fechar fatura"}</DialogTitle>
          <DialogDescription>Selecione as compras que entram na fatura e gere a despesa consolidada no caixa do mes do vencimento.</DialogDescription>
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
          <Field label="Observacoes">
            <Textarea value={notes} onChange={(event) => setNotes(event.target.value)} rows={3} />
          </Field>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Origem externa">
              <Input value={externalSource} onChange={(event) => setExternalSource(event.target.value)} />
            </Field>
            <Field label="Referencia externa">
              <Input value={externalReference} onChange={(event) => setExternalReference(event.target.value)} />
            </Field>
          </div>
          <div className="space-y-2 rounded-[18px] border border-border/70 p-4">
            <p className="text-sm font-semibold text-foreground">Compras da fatura</p>
            {availableTransactions.length === 0 ? (
              <p className="text-sm text-muted-foreground">Nao ha compras disponiveis para esta fatura.</p>
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
          <DialogTitle>Gerar lancamentos de {periodLabel}</DialogTitle>
          <DialogDescription>Este periodo ja existe. Escolha se deseja adicionar apenas recorrencias faltantes ou duplicar novamente todas as recorrencias aplicaveis.</DialogDescription>
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
