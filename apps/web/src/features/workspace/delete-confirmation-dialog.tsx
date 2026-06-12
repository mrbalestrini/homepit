"use client";

import { useId, useState, type FormEvent } from "react";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";

export type DeleteConfirmationDialogProps = {
  open: boolean;
  title: string;
  description: string;
  impactItems: string[];
  onOpenChange: (open: boolean) => void;
  onConfirm: () => Promise<void>;
  confirmationTarget?: string;
  confirmationLabel?: string;
  confirmLabel: string;
};

function normalizeConfirmationValue(value: string) {
  return value.trim().toLocaleLowerCase();
}

export function DeleteConfirmationDialog({
  open,
  title,
  description,
  impactItems,
  onOpenChange,
  onConfirm,
  confirmationTarget,
  confirmationLabel,
  confirmLabel,
}: DeleteConfirmationDialogProps) {
  const [confirmationValue, setConfirmationValue] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const inputId = useId();
  const requiresTypedConfirmation = Boolean(confirmationTarget?.trim());
  const normalizedTarget = normalizeConfirmationValue(confirmationTarget ?? "");
  const matchesConfirmation =
    !requiresTypedConfirmation || normalizeConfirmationValue(confirmationValue) === normalizedTarget;

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (requiresTypedConfirmation && !matchesConfirmation) {
      setError("Digite exatamente o nome solicitado para confirmar.");
      return;
    }

    setError(null);
    setSaving(true);

    try {
      await onConfirm();
      onOpenChange(false);
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Não foi possível concluir a exclusão.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="w-[min(94vw,42rem)] max-h-[88vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>{description}</DialogDescription>
        </DialogHeader>

        <form className="space-y-4" onSubmit={handleSubmit}>
          {error ? (
            <div className="rounded-[18px] border border-danger/20 bg-status-danger-soft px-4 py-3 text-sm text-danger">
              {error}
            </div>
          ) : null}

          <div className="rounded-[18px] border border-danger/20 bg-status-danger-soft p-4">
            <p className="text-sm font-semibold text-danger">Impactos desta exclusão</p>
            <ul className="mt-3 list-disc space-y-2 pl-5 text-sm leading-6 text-foreground">
              {impactItems.map((item, index) => (
                <li key={`${index}-${item}`}>{item}</li>
              ))}
            </ul>
          </div>

          {requiresTypedConfirmation ? (
            <div className="space-y-2">
              <label htmlFor={inputId} className="text-sm font-medium text-foreground">
                {confirmationLabel ?? `Digite ${confirmationTarget} para confirmar`}
              </label>
              <Input
                id={inputId}
                value={confirmationValue}
                onChange={(event) => setConfirmationValue(event.target.value)}
                placeholder={confirmationTarget}
                autoComplete="off"
                autoCapitalize="none"
                autoCorrect="off"
                spellCheck={false}
                disabled={saving}
              />
              <p className="text-xs leading-5 text-muted-foreground">
                Essa etapa ajuda a evitar exclusões acidentais.
              </p>
            </div>
          ) : null}

          <DialogFooter>
            <Button variant="secondary" type="button" onClick={() => onOpenChange(false)}>
              Cancelar
            </Button>
            <Button type="submit" variant="danger" disabled={saving || (requiresTypedConfirmation && !matchesConfirmation)}>
              {confirmLabel}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
