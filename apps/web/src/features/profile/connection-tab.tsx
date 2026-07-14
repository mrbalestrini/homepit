"use client";

import { Clipboard, KeyRound, Loader2, Plus, XCircle } from "lucide-react";
import { type FormEvent, useEffect, useState } from "react";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
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
import {
  type CreateIntegrationConnectionRequest,
  type CreateIntegrationConnectionResult,
  type Household,
  type IntegrationConnection,
  apiFetch,
} from "@/lib/api";

const CONNECTIONS_PATH = "/api/users/me/integration-connections";
const DEFAULT_EXPIRATION_DAYS = 90;
const MAX_EXPIRATION_DAYS = 365;

type RevealedConnection = Pick<CreateIntegrationConnectionResult, "token" | "restApiUrl" | "mcpUrl"> & {
  name: string;
};

export function ConnectionTab({ token, households }: { token: string; households: Household[] }) {
  const [connections, setConnections] = useState<IntegrationConnection[]>([]);
  const [loading, setLoading] = useState(true);
  const [createDialogOpen, setCreateDialogOpen] = useState(false);
  const [revealedConnection, setRevealedConnection] = useState<RevealedConnection | null>(null);
  const [connectionToRevoke, setConnectionToRevoke] = useState<IntegrationConnection | null>(null);

  async function loadConnections() {
    setLoading(true);

    try {
      const nextConnections = await apiFetch<IntegrationConnection[]>(CONNECTIONS_PATH, { token });
      setConnections(nextConnections);
    } catch (exception) {
      toast.error(exception instanceof Error ? exception.message : "Não foi possível carregar as conexões.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    const timer = window.setTimeout(() => void loadConnections(), 0);

    return () => window.clearTimeout(timer);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [token]);

  async function revokeConnection() {
    if (!connectionToRevoke) {
      return;
    }

    try {
      await apiFetch<void>(`${CONNECTIONS_PATH}/${connectionToRevoke.id}/revoke`, {
        method: "POST",
        token,
      });
      setConnections((current) =>
        current.map((connection) =>
          connection.id === connectionToRevoke.id
            ? { ...connection, isActive: false, revokedAt: new Date().toISOString() }
            : connection,
        ),
      );
      setConnectionToRevoke(null);
      toast.success("Conexão revogada.");
    } catch (exception) {
      toast.error(exception instanceof Error ? exception.message : "Não foi possível revogar a conexão.");
    }
  }

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader>
          <CardTitle>Guia rápido</CardTitle>
          <CardDescription>Use a chave somente fora do navegador, em uma variável de ambiente segura.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-3 text-sm leading-6 text-muted-foreground">
          <p>A Casa já está vinculada à conexão. Não envie o cabeçalho <code>X-Household-Id</code>.</p>
          <pre className="overflow-x-auto rounded-[14px] border border-border/70 bg-surface-muted p-3 text-xs text-foreground">
            Authorization: Bearer $HOMEPIT_INTEGRATION_TOKEN
          </pre>
          <p>
            Consulte <code>GET /api/integrations/v1/space</code> antes de automatizar. Escritas exigem uma conexão com
            permissão de leitura e escrita e uma chave de idempotência.
          </p>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
            <div>
              <CardTitle>Conexões</CardTitle>
              <CardDescription>Crie chaves para conectar suas ferramentas e automações a uma casa.</CardDescription>
            </div>
            <Button type="button" onClick={() => setCreateDialogOpen(true)}>
              <Plus />
              Nova conexão
            </Button>
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="rounded-[18px] border border-border/70 bg-surface-muted p-4 text-sm leading-6 text-muted-foreground">
            A chave é exibida apenas uma vez, logo após a criação. Guarde-a em um local seguro e revogue a conexão quando ela não for mais necessária.
          </div>

          {loading ? (
            <div className="flex items-center gap-2 py-6 text-sm text-muted-foreground">
              <Loader2 className="size-4 animate-spin" />
              Carregando conexões...
            </div>
          ) : connections.length === 0 ? (
            <div className="rounded-[18px] border border-dashed border-border/80 px-5 py-10 text-center">
              <KeyRound className="mx-auto size-7 text-muted-foreground" />
              <p className="mt-3 text-sm font-semibold text-foreground">Nenhuma conexão criada</p>
              <p className="mx-auto mt-1 max-w-md text-sm leading-6 text-muted-foreground">
                Crie uma conexão para permitir que outra ferramenta trabalhe somente na casa escolhida.
              </p>
            </div>
          ) : (
            <div className="space-y-3">
              {connections.map((connection) => (
                <ConnectionCard key={connection.id} connection={connection} onRevoke={() => setConnectionToRevoke(connection)} />
              ))}
            </div>
          )}
        </CardContent>
      </Card>

      <CreateConnectionDialog
        key={createDialogOpen ? "connection-create-open" : "connection-create-closed"}
        open={createDialogOpen}
        households={households}
        token={token}
        onOpenChange={setCreateDialogOpen}
        onCreated={(result) => {
          setConnections((current) => [result.connection, ...current]);
          setRevealedConnection({
            name: result.connection.name,
            token: result.token,
            restApiUrl: result.restApiUrl,
            mcpUrl: result.mcpUrl,
          });
        }}
      />

      <RevealConnectionDialog
        connection={revealedConnection}
        onOpenChange={(open) => {
          if (!open) {
            setRevealedConnection(null);
          }
        }}
      />

      <Dialog open={connectionToRevoke !== null} onOpenChange={(open) => !open && setConnectionToRevoke(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Revogar conexão</DialogTitle>
            <DialogDescription>
              Revogar “{connectionToRevoke?.name ?? ""}” interrompe imediatamente o acesso dessa chave à casa vinculada.
            </DialogDescription>
          </DialogHeader>
          <div className="rounded-[18px] border border-danger/20 bg-status-danger-soft p-4 text-sm leading-6 text-foreground">
            Esta ação não pode ser desfeita. Você poderá criar uma nova conexão quando precisar.
          </div>
          <DialogFooter>
            <Button type="button" variant="secondary" onClick={() => setConnectionToRevoke(null)}>
              Voltar
            </Button>
            <Button type="button" variant="danger" onClick={() => void revokeConnection()}>
              <XCircle />
              Revogar conexão
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function ConnectionCard({ connection, onRevoke }: { connection: IntegrationConnection; onRevoke: () => void }) {
  const status = connection.revokedAt ? "Revogada" : connection.isActive ? "Ativa" : "Encerrada";
  const statusVariant = status === "Ativa" ? "success" : "danger";

  return (
    <div className="rounded-[18px] border border-border/70 bg-surface-muted p-4">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
        <div className="min-w-0 space-y-2">
          <div className="flex flex-wrap items-center gap-2">
            <p className="truncate text-sm font-semibold text-foreground">{connection.name}</p>
            <Badge variant={statusVariant}>{status}</Badge>
            <Badge variant="outline">{connection.accessMode === "ReadOnly" ? "Somente leitura" : "Leitura e escrita"}</Badge>
          </div>
          <p className="text-sm text-muted-foreground">Casa: {connection.householdName}</p>
          <div className="flex flex-wrap gap-x-4 gap-y-1 text-xs leading-5 text-muted-foreground">
            {connection.tokenPrefix ? <span>Chave: {connection.tokenPrefix}••••</span> : null}
            <span>Expira em {formatDate(connection.expiresAt)}</span>
            <span>{connection.lastUsedAt ? `Usada em ${formatDateTime(connection.lastUsedAt)}` : "Ainda não foi usada"}</span>
          </div>
        </div>
        {status === "Ativa" ? (
          <Button type="button" variant="secondary" size="sm" onClick={onRevoke}>
            <XCircle />
            Revogar
          </Button>
        ) : null}
      </div>
    </div>
  );
}

function CreateConnectionDialog({
  open,
  households,
  token,
  onOpenChange,
  onCreated,
}: {
  open: boolean;
  households: Household[];
  token: string;
  onOpenChange: (open: boolean) => void;
  onCreated: (result: CreateIntegrationConnectionResult) => void;
}) {
  const [defaultExpiresAt] = useState(() => toDateInputValue(addDays(new Date(), DEFAULT_EXPIRATION_DAYS)));
  const [maxExpiresAt] = useState(() => toDateInputValue(addDays(new Date(), MAX_EXPIRATION_DAYS)));
  const [name, setName] = useState("");
  const [householdId, setHouseholdId] = useState(households[0]?.id ?? "");
  const [accessMode, setAccessMode] = useState<CreateIntegrationConnectionRequest["accessMode"]>("ReadOnly");
  const [expiresAt, setExpiresAt] = useState(defaultExpiresAt);
  const [creating, setCreating] = useState(false);

  async function createConnection(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!householdId) {
      toast.error("Escolha a casa que esta conexão poderá acessar.");
      return;
    }

    if (!isExpirationValid(expiresAt, maxExpiresAt)) {
      toast.error("Escolha uma validade futura de até um ano.");
      return;
    }

    setCreating(true);

    try {
      const result = await apiFetch<CreateIntegrationConnectionResult>(CONNECTIONS_PATH, {
        method: "POST",
        token,
        body: JSON.stringify({
          name: name.trim(),
          householdId,
          accessMode,
          expiresAt: toEndOfDayIso(expiresAt),
        } satisfies CreateIntegrationConnectionRequest),
      });
      onCreated(result);
      onOpenChange(false);
      toast.success("Conexão criada. Copie a chave antes de fechar o aviso.");
    } catch (exception) {
      toast.error(exception instanceof Error ? exception.message : "Não foi possível criar a conexão.");
    } finally {
      setCreating(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Nova conexão</DialogTitle>
          <DialogDescription>Defina a casa, o nível de acesso e a validade da nova chave.</DialogDescription>
        </DialogHeader>
        <form className="grid gap-5" onSubmit={createConnection}>
          <label className="grid gap-2">
            <span className="text-sm font-semibold text-foreground">Nome</span>
            <Input value={name} onChange={(event) => setName(event.target.value)} placeholder="Ex.: Automação financeira" maxLength={120} required autoFocus />
            <span className="text-xs leading-5 text-muted-foreground">Use um nome que ajude a reconhecer a ferramenta depois.</span>
          </label>
          <label className="grid gap-2">
            <span className="text-sm font-semibold text-foreground">Casa</span>
            <Select value={householdId} onChange={(event) => setHouseholdId(event.target.value)} required>
              {households.map((household) => (
                <option key={household.id} value={household.id}>
                  {household.name}
                </option>
              ))}
            </Select>
          </label>
          <label className="grid gap-2">
            <span className="text-sm font-semibold text-foreground">Permissão</span>
            <Select value={accessMode} onChange={(event) => setAccessMode(event.target.value as CreateIntegrationConnectionRequest["accessMode"])}>
              <option value="ReadOnly">Somente leitura</option>
              <option value="ReadWrite">Leitura e escrita</option>
            </Select>
            <span className="text-xs leading-5 text-muted-foreground">Permita escrita somente quando a ferramenta precisar criar ou atualizar informações.</span>
          </label>
          <label className="grid gap-2">
            <span className="text-sm font-semibold text-foreground">Válida até</span>
            <Input type="date" value={expiresAt} min={toDateInputValue(addDays(new Date(), 1))} max={maxExpiresAt} onChange={(event) => setExpiresAt(event.target.value)} required />
            <span className="text-xs leading-5 text-muted-foreground">A validade padrão é de 90 dias e o máximo é de um ano.</span>
          </label>
          <DialogFooter>
            <Button type="button" variant="secondary" onClick={() => onOpenChange(false)} disabled={creating}>
              Cancelar
            </Button>
            <Button type="submit" disabled={creating || households.length === 0}>
              {creating ? <Loader2 className="animate-spin" /> : <KeyRound />}
              Criar conexão
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function RevealConnectionDialog({
  connection,
  onOpenChange,
}: {
  connection: RevealedConnection | null;
  onOpenChange: (open: boolean) => void;
}) {
  async function copyToken() {
    if (!connection) {
      return;
    }

    try {
      await navigator.clipboard.writeText(connection.token);
      toast.success("Chave copiada.");
    } catch {
      toast.error("Não foi possível copiar a chave. Selecione e copie o valor manualmente.");
    }
  }

  return (
    <Dialog open={connection !== null} onOpenChange={onOpenChange}>
      <DialogContent className="w-[min(94vw,42rem)]">
        <DialogHeader>
          <DialogTitle>Guarde sua chave</DialogTitle>
          <DialogDescription>
            Esta é a única vez que a chave de “{connection?.name ?? ""}” será exibida. Copie-a agora e guarde-a em um local seguro.
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-4">
          <div className="rounded-[18px] border border-warning/20 bg-status-warning-soft p-4 text-sm leading-6 text-foreground">
            Não envie esta chave em conversas, capturas de tela ou repositórios de código.
          </div>
          <label className="grid gap-2">
            <span className="text-sm font-semibold text-foreground">Chave de integração</span>
            <Input value={connection?.token ?? ""} readOnly aria-label="Chave de integração" className="font-mono text-xs" />
          </label>
          <div className="grid gap-3 text-sm text-muted-foreground sm:grid-cols-2">
            <EndpointCard label="API REST" value={connection?.restApiUrl ?? ""} />
            <EndpointCard label="Servidor MCP" value={connection?.mcpUrl ?? ""} />
          </div>
        </div>
        <DialogFooter>
          <Button type="button" variant="secondary" onClick={() => onOpenChange(false)}>
            Fechar
          </Button>
          <Button type="button" onClick={() => void copyToken()}>
            <Clipboard />
            Copiar chave
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function EndpointCard({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-[16px] border border-border/70 bg-surface-muted p-3">
      <p className="text-xs font-semibold text-foreground">{label}</p>
      <p className="mt-1 break-all font-mono text-xs leading-5">{value}</p>
    </div>
  );
}

function addDays(date: Date, days: number) {
  const nextDate = new Date(date);
  nextDate.setDate(nextDate.getDate() + days);
  return nextDate;
}

function toDateInputValue(date: Date) {
  const timezoneOffset = date.getTimezoneOffset() * 60_000;
  return new Date(date.getTime() - timezoneOffset).toISOString().slice(0, 10);
}

function toEndOfDayIso(value: string) {
  return new Date(`${value}T23:59:59.999`).toISOString();
}

function isExpirationValid(value: string, maxValue: string) {
  const selected = new Date(`${value}T00:00:00`).getTime();
  const tomorrow = new Date(`${toDateInputValue(addDays(new Date(), 1))}T00:00:00`).getTime();
  const maximum = new Date(`${maxValue}T23:59:59.999`).getTime();
  return Number.isFinite(selected) && selected >= tomorrow && selected <= maximum;
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat("pt-BR", { dateStyle: "medium" }).format(new Date(value));
}

function formatDateTime(value: string) {
  return new Intl.DateTimeFormat("pt-BR", { dateStyle: "short", timeStyle: "short" }).format(new Date(value));
}
