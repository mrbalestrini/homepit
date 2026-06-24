"use client";

import { type FormEvent, type ReactNode, useEffect, useMemo, useState } from "react";
import { CalendarClock, Pencil, Plus, Radio, Trash2 } from "lucide-react";
import type { GsmNumber, GsmNumberPlan, GsmNumberStatus } from "@/lib/api";
import { Badge } from "@/components/ui/badge";
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
  HomePitWorkspaceShell,
  LoadingState,
} from "@/features/workspace/homepit-workspace-shell";
import type { GsmDashboardController, GsmFormInput } from "./use-gsm-dashboard";
import {
  formatDateOnlyPtBr,
  formatGsmMonthlyCost,
  formatGsmNumber,
  formatGsmPlanLabel,
  formatRechargeElapsed,
  isValidGsmNumber,
  maskGsmNumberInput,
  parseGsmMonthlyCostInput,
} from "./gsm-dashboard.utils";

function getStatusVariant(status: GsmNumberStatus) {
  if (status === "Ativo") {
    return "success" as const;
  }

  if (status === "Inativo") {
    return "warning" as const;
  }

  return "danger" as const;
}

function summarizeDescription(value: string | null | undefined, max = 140) {
  if (!value) {
    return null;
  }

  return value.length > max ? `${value.slice(0, max).trimEnd()}...` : value;
}

export function GsmDashboardWorkspace({ dashboard }: { dashboard: GsmDashboardController }) {
  const [deletingNumber, setDeletingNumber] = useState<GsmNumber | null>(null);
  const statusCounts = useMemo(
    () => ({
      ativo: dashboard.gsmNumbers.filter((item) => item.status === "Ativo").length,
      inativo: dashboard.gsmNumbers.filter((item) => item.status === "Inativo").length,
      abandonado: dashboard.gsmNumbers.filter((item) => item.status === "Abandonado").length,
    }),
    [dashboard.gsmNumbers],
  );

  const headerStats = [
    { label: "Total", value: dashboard.gsmNumbers.length },
    { label: "Ativos", value: statusCounts.ativo },
    { label: "Inativos", value: statusCounts.inativo },
    { label: "Abandonados", value: statusCounts.abandonado },
  ];

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
          isHouseholdDialogOpen: dashboard.activeModal === "household",
          isShareDialogOpen: dashboard.activeModal === "share",
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
        activeModule="gsm"
        subtitle={dashboard.subtitle}
        visibleCount={dashboard.gsmNumbers.length}
        visibleLabel="linhas"
        headerStats={headerStats}
      >
        <Card>
          <CardContent className="flex flex-col gap-4 p-5 sm:p-6 lg:flex-row lg:items-end lg:justify-between">
            <div className="max-w-2xl">
              <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">Gestão GSM</p>
              <h1 className="mt-2 text-2xl font-semibold text-foreground">Gerenciamento de números de telefone</h1>
              <p className="mt-2 text-sm leading-6 text-muted-foreground">
                Cadastre os números que são gerenciados pela casa para não se perder.
              </p>
            </div>

            <div className="flex flex-col gap-2 sm:flex-row">
              <Button variant="secondary" onClick={() => void dashboard.refreshWorkspace()}>
                <CalendarClock />
                Atualizar
              </Button>
              <Button onClick={dashboard.openCreateGsmNumber}>
                <Plus />
                Cadastrar número
              </Button>
            </div>
          </CardContent>
        </Card>

        {dashboard.loading && dashboard.gsmNumbers.length === 0 ? (
          <LoadingState
            title="Carregando números GSM"
            description="Estamos reunindo as linhas da casa e o estado das últimas recargas."
            icon={<Radio className="size-5 animate-pulse" />}
          />
        ) : dashboard.gsmNumbers.length === 0 ? (
          <EmptyState
            icon={<Radio className="size-5" />}
            title="Nenhum número GSM cadastrado"
            description="Cadastre a primeira linha da casa para começar a acompanhar plano, custo, aquisição e recargas."
            action={
              <Button onClick={dashboard.openCreateGsmNumber}>
                <Plus />
                Cadastrar primeiro número
              </Button>
            }
          />
        ) : (
          <Card>
            <CardHeader className="border-b border-border/60 pb-4">
              <CardTitle className="text-lg">Números cadastrados</CardTitle>
            </CardHeader>
            <CardContent className="p-0">
              <div className="overflow-x-auto">
                <Table>
                  <TableHeader>
                    <TableRow className="border-b border-border/60 bg-surface-muted hover:bg-surface-muted">
                      <TableHead className="min-w-[220px]">Linha</TableHead>
                      <TableHead className="min-w-[180px]">Número</TableHead>
                      <TableHead>Plano</TableHead>
                      <TableHead>Custo mensal</TableHead>
                      <TableHead>Aquisição</TableHead>
                      <TableHead>Última recarga</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead className="w-[160px] text-right">Ações</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {dashboard.gsmNumbers.map((gsmNumber) => (
                      <TableRow key={gsmNumber.id}>
                        <TableCell className="min-w-[220px]">
                          <div className="space-y-1">
                            <p className="font-semibold text-foreground">{gsmNumber.title}</p>
                            {summarizeDescription(gsmNumber.description) ? (
                              <p className="text-sm leading-6 text-muted-foreground">
                                {summarizeDescription(gsmNumber.description)}
                              </p>
                            ) : (
                              <p className="text-sm text-muted-foreground">Sem descrição registrada.</p>
                            )}
                          </div>
                        </TableCell>
                        <TableCell className="min-w-[180px] font-medium text-foreground">
                          {formatGsmNumber(gsmNumber.number)}
                        </TableCell>
                        <TableCell>
                          <Badge variant="outline">{formatGsmPlanLabel(gsmNumber.plan)}</Badge>
                        </TableCell>
                        <TableCell className="whitespace-nowrap text-sm font-medium text-foreground">
                          {formatGsmMonthlyCost(gsmNumber.monthlyCost)}
                        </TableCell>
                        <TableCell className="whitespace-nowrap text-sm text-foreground">
                          {formatDateOnlyPtBr(gsmNumber.acquiredOn)}
                        </TableCell>
                        <TableCell className="whitespace-nowrap">
                          <div className="space-y-1">
                            <p className="text-sm text-foreground">
                              {formatDateOnlyPtBr(gsmNumber.lastRechargeOn, "Sem recarga registrada")}
                            </p>
                            <p className="text-xs text-muted-foreground">
                              {formatRechargeElapsed(gsmNumber.lastRechargeOn)}
                            </p>
                          </div>
                        </TableCell>
                        <TableCell>
                          <Badge variant={getStatusVariant(gsmNumber.status)}>{gsmNumber.status}</Badge>
                        </TableCell>
                        <TableCell className="text-right">
                          <div className="flex justify-end gap-2">
                            <Button
                              variant="secondary"
                              size="sm"
                              onClick={() => dashboard.openEditGsmNumber(gsmNumber)}
                              disabled={!gsmNumber.canEdit}
                            >
                              <Pencil />
                              Editar
                            </Button>
                            <Button
                              variant="ghost"
                              size="sm"
                              onClick={() => setDeletingNumber(gsmNumber)}
                              disabled={!gsmNumber.canDelete}
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
        )}
      </HomePitWorkspaceShell>

      <GsmNumberDialog
        key={`gsm-${dashboard.editingGsmNumber?.id ?? "new"}-${dashboard.activeModal === "gsm" ? "open" : "closed"}`}
        open={dashboard.activeModal === "gsm"}
        gsmNumber={dashboard.editingGsmNumber}
        onOpenChange={(open) => !open && dashboard.closeModuleModal()}
        onSave={(input) =>
          dashboard.editingGsmNumber
            ? dashboard.updateGsmNumber(dashboard.editingGsmNumber.id, input)
            : dashboard.createGsmNumber(input)
        }
      />

      <DeleteConfirmationDialog
        key={`gsm-delete-${deletingNumber?.id ?? "none"}`}
        open={Boolean(deletingNumber)}
        title="Excluir número GSM"
        description="Essa ação remove o cadastro da linha da casa e não pode ser desfeita."
        confirmationTarget={deletingNumber?.title}
        confirmationLabel={`Digite o título ${deletingNumber?.title ?? ""} para confirmar`}
        confirmLabel="Excluir número"
        impactItems={[
          "O número deixa de aparecer na gestão GSM desta casa.",
          "O contexto de título, descrição, aquisição e recarga é removido.",
          "Será necessário cadastrar novamente a linha caso queira recuperá-la depois.",
        ]}
        onOpenChange={(open) => {
          if (!open) {
            setDeletingNumber(null);
          }
        }}
        onConfirm={async () => {
          if (!deletingNumber) {
            return;
          }

          await dashboard.deleteGsmNumber(deletingNumber);
          setDeletingNumber(null);
        }}
      />
    </>
  );
}

export function GsmNumberDialog({
  open,
  gsmNumber,
  onOpenChange,
  onSave,
}: {
  open: boolean;
  gsmNumber: GsmNumber | null;
  onOpenChange: (open: boolean) => void;
  onSave: (input: GsmFormInput) => Promise<void>;
}) {
  const [title, setTitle] = useState(gsmNumber?.title ?? "");
  const [number, setNumber] = useState(gsmNumber ? formatGsmNumber(gsmNumber.number) : "");
  const [description, setDescription] = useState(gsmNumber?.description ?? "");
  const [plan, setPlan] = useState<GsmNumberPlan>(gsmNumber?.plan ?? "PrePago");
  const [monthlyCost, setMonthlyCost] = useState(
    gsmNumber?.monthlyCost != null ? formatGsmMonthlyCost(gsmNumber.monthlyCost) : "",
  );
  const [acquiredOn, setAcquiredOn] = useState(gsmNumber?.acquiredOn ?? "");
  const [lastRechargeOn, setLastRechargeOn] = useState(gsmNumber?.lastRechargeOn ?? "");
  const [status, setStatus] = useState<GsmNumberStatus>(gsmNumber?.status ?? "Ativo");
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    setTitle(gsmNumber?.title ?? "");
    setNumber(gsmNumber ? formatGsmNumber(gsmNumber.number) : "");
    setDescription(gsmNumber?.description ?? "");
    setPlan(gsmNumber?.plan ?? "PrePago");
    setMonthlyCost(gsmNumber?.monthlyCost != null ? formatGsmMonthlyCost(gsmNumber.monthlyCost) : "");
    setAcquiredOn(gsmNumber?.acquiredOn ?? "");
    setLastRechargeOn(gsmNumber?.lastRechargeOn ?? "");
    setStatus(gsmNumber?.status ?? "Ativo");
    setError(null);
    setSaving(false);
  }, [gsmNumber, open]);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!title.trim()) {
      setError("Informe o título do número GSM.");
      return;
    }

    if (!isValidGsmNumber(number)) {
      setError("Informe um número GSM válido com DDI opcional e DDD obrigatório.");
      return;
    }

    const parsedMonthlyCost = parseGsmMonthlyCostInput(monthlyCost);
    if (monthlyCost.trim() && parsedMonthlyCost === null) {
      setError("Informe um custo mensal válido ou deixe o campo em branco.");
      return;
    }

    if (!acquiredOn) {
      setError("Informe a data de aquisição.");
      return;
    }

    setError(null);
    setSaving(true);

    try {
      await onSave({
        title: title.trim(),
        number,
        description: description.trim(),
        plan,
        monthlyCost: parsedMonthlyCost,
        acquiredOn,
        lastRechargeOn,
        status,
      });
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Não foi possível salvar o número GSM.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="w-[min(94vw,42rem)] max-h-[88vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{gsmNumber ? "Editar número GSM" : "Cadastrar número GSM"}</DialogTitle>
          <DialogDescription>
            Registre título, número, plano, custo mensal opcional, datas importantes e o status atual da linha.
          </DialogDescription>
        </DialogHeader>

        <form className="space-y-4" onSubmit={handleSubmit}>
          {error ? (
            <div className="rounded-[18px] border border-danger/20 bg-status-danger-soft px-4 py-3 text-sm text-danger">
              {error}
            </div>
          ) : null}

          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Título">
              <Input value={title} onChange={(event) => setTitle(event.target.value)} placeholder="Ex.: Chip do portão" />
            </Field>

            <Field label="Status">
              <Select value={status} onChange={(event) => setStatus(event.target.value as GsmNumberStatus)}>
                <option value="Ativo">Ativo</option>
                <option value="Inativo">Inativo</option>
                <option value="Abandonado">Abandonado</option>
              </Select>
            </Field>
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Número GSM">
              <Input
                value={number}
                onChange={(event) => setNumber(maskGsmNumberInput(event.target.value))}
                placeholder="(11) 91234-5678 ou +55 (11) 91234-5678"
                inputMode="numeric"
              />
              <p className="mt-2 text-xs leading-5 text-muted-foreground">
                Aceita DDD + número com 11 dígitos ou DDI + DDD + número com 13 dígitos.
              </p>
            </Field>

            <Field label="Plano">
              <Select value={plan} onChange={(event) => setPlan(event.target.value as GsmNumberPlan)}>
                <option value="PrePago">Pré-pago</option>
                <option value="PosPago">Pós-pago</option>
              </Select>
            </Field>
          </div>

          <Field label="Custo mensal">
            <Input
              value={monthlyCost}
              onChange={(event) => setMonthlyCost(event.target.value)}
              placeholder="Ex.: R$ 59,90"
              inputMode="decimal"
            />
            <p className="mt-2 text-xs leading-5 text-muted-foreground">
              Campo opcional. Use para linhas com mensalidade recorrente.
            </p>
          </Field>

          <Field label="Descrição">
            <Textarea
              value={description}
              onChange={(event) => setDescription(event.target.value)}
              placeholder="Contexto da linha, uso, operadora ou observações importantes"
              rows={4}
            />
          </Field>

          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Data de aquisição">
              <Input type="date" value={acquiredOn} onChange={(event) => setAcquiredOn(event.target.value)} />
            </Field>

            <Field label="Última recarga">
              <Input type="date" value={lastRechargeOn} onChange={(event) => setLastRechargeOn(event.target.value)} />
            </Field>
          </div>

          <DialogFooter>
            <Button variant="secondary" type="button" onClick={() => onOpenChange(false)} disabled={saving}>
              Cancelar
            </Button>
            <Button type="submit" disabled={saving}>
              {gsmNumber ? "Salvar número" : "Cadastrar número"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function Field({
  label,
  children,
}: {
  label: string;
  children: ReactNode;
}) {
  return (
    <label className="block space-y-2">
      <span className="text-sm font-medium text-foreground">{label}</span>
      {children}
    </label>
  );
}
