"use client";

import {
  Check,
  ChevronLeft,
  ChevronRight,
  Copy,
  ExternalLink,
  Image as ImageIcon,
  Layers,
  Link2,
  MoreHorizontal,
  Pencil,
  Plus,
  Search,
  Sparkles,
  Tag,
  Trash2,
  Users,
} from "lucide-react";
import { FormEvent, useEffect, useMemo, useState } from "react";
import { toast } from "sonner";
import type { PromptCategory, PromptDetail, PromptListItem } from "@/lib/api";
import { ApiError, apiFetchBlob } from "@/lib/api";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader } from "@/components/ui/card";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import {
  EmptyState,
  Field,
  HomePitWorkspaceShell,
  Notice,
  StatCard,
} from "@/features/workspace/homepit-workspace-shell";
import { AvatarCircle } from "@/features/workspace/protected-user-avatar";
import { cn } from "@/lib/utils";
import type { PromptBankController, PromptFormInput } from "./use-prompt-bank";

const DESCRIPTION_PREVIEW_LIMIT = 120;
const PROMPT_PREVIEW_LIMIT = 150;

export function PromptBankWorkspace({ bank }: { bank: PromptBankController }) {
  return (
    <>
      <HomePitWorkspaceShell
        controller={{
          session: bank.session,
          activeHouseholdId: bank.activeHouseholdId,
          activeHousehold: bank.activeHousehold,
          members: bank.members,
          theme: bank.theme,
          sidebarCollapsed: bank.sidebarCollapsed,
          loading: bank.loading,
          error: bank.error,
          canShareHousehold: bank.canShareHousehold,
          canManageHousehold: bank.canManageHousehold,
          editingHousehold: bank.editingHousehold,
          isHouseholdDialogOpen: bank.activeModal === "household",
          isShareDialogOpen: bank.activeModal === "share",
          setError: bank.setError,
          setSidebarCollapsed: bank.setSidebarCollapsed,
          setTheme: bank.setTheme,
          handleHouseholdChange: bank.handleHouseholdChange,
          handleLogout: bank.handleLogout,
          refreshHouseholds: bank.refreshHouseholds,
          refreshWorkspace: bank.refreshWorkspace,
          openCreateHousehold: bank.openCreateHousehold,
          openEditHousehold: bank.openEditHousehold,
          openShareHousehold: bank.openShareHousehold,
          closeCommonModal: bank.closeCommonModal,
          createHousehold: bank.createHousehold,
          updateHousehold: bank.updateHousehold,
          deleteHousehold: bank.deleteHousehold,
          shareHousehold: bank.shareHousehold,
          updateProfile: bank.updateProfile,
        }}
        activeModule="prompts"
        subtitle={bank.subtitle}
        visibleCount={bank.promptPage.totalCount}
        visibleLabel="resultados"
      >
        <PromptQuickStats bank={bank} />

        <div className="grid gap-3 xl:grid-cols-[340px_minmax(0,1fr)]">
          <CategoryManager bank={bank} />
          <PromptBoard bank={bank} />
        </div>
      </HomePitWorkspaceShell>

      <PromptDialog
        key={`prompt-${bank.editingPrompt?.id ?? "new"}-${bank.activeModal === "prompt" ? "open" : "closed"}`}
        open={bank.activeModal === "prompt"}
        prompt={bank.editingPrompt}
        universes={bank.universes}
        categories={bank.categories}
        onOpenChange={(open) => !open && bank.closeModuleModal()}
        onSave={(input) =>
          bank.editingPrompt ? bank.updatePrompt(bank.editingPrompt.id, input) : bank.createPrompt(input)
        }
        token={bank.session?.accessToken ?? ""}
      />

      <CategoryDialog
        key={`category-${bank.editingCategory?.id ?? "new"}-${bank.activeModal === "category" ? "open" : "closed"}`}
        open={bank.activeModal === "category"}
        category={bank.editingCategory}
        onOpenChange={(open) => !open && bank.closeModuleModal()}
        onSave={(name) =>
          bank.editingCategory ? bank.updateCategory(bank.editingCategory.id, name) : bank.createCategory(name)
        }
      />

      <PromptDetailDialog
        open={Boolean(bank.selectedPromptDetail)}
        prompt={bank.selectedPromptDetail}
        loading={bank.detailLoading}
        token={bank.session?.accessToken ?? ""}
        onOpenChange={(open) => !open && bank.closePrompt()}
        onEdit={(promptId) => void bank.openEditPrompt(promptId)}
        onDelete={(prompt) => void bank.deletePrompt(prompt).catch(() => undefined)}
      />

      <CategoryDeleteDialog
        key={`category-delete-${bank.deletingCategory?.id ?? "none"}-${bank.deletingCategory ? "open" : "closed"}`}
        open={Boolean(bank.deletingCategory)}
        category={bank.deletingCategory}
        categories={bank.categories}
        onOpenChange={(open) => !open && bank.setDeletingCategory(null)}
        onDelete={(categoryId, replacementCategoryId) => bank.deleteCategory(categoryId, replacementCategoryId)}
      />
    </>
  );
}

function PromptQuickStats({ bank }: { bank: PromptBankController }) {
  return (
    <div className="grid gap-2.5 sm:grid-cols-2 xl:grid-cols-5">
      <StatCard label="Prompts" value={bank.promptPage.totalCount} icon={<Sparkles className="size-4" />} tone="default" />
      <StatCard label="Categorias" value={bank.categories.length} icon={<Tag className="size-4" />} tone="default" />
      <StatCard label="Universos" value={bank.universes.length} icon={<Layers className="size-4" />} tone="default" />
      <StatCard label="Com imagem" value={bank.imageCount} icon={<ImageIcon className="size-4" />} tone="success" />
      <StatCard label="Pessoas" value={bank.members.length} icon={<Users className="size-4" />} tone="warning" />
    </div>
  );
}

function CategoryManager({ bank }: { bank: PromptBankController }) {
  return (
    <Card>
      <CardHeader className="border-b border-border/60 pb-4">
        <div className="flex items-start justify-between gap-3">
          <div>
            <h2 className="text-lg font-semibold text-foreground">Categorias</h2>
            <p className="mt-1 text-sm text-muted-foreground">Gerencie a taxonomia compartilhada da casa.</p>
          </div>
          <Button variant="secondary" size="icon" onClick={bank.openCreateCategory} aria-label="Nova categoria">
            <Plus />
          </Button>
        </div>
      </CardHeader>

      <CardContent className="space-y-3 p-4">
        {bank.categories.length === 0 ? (
          <EmptyState
            icon={<Tag className="size-5" />}
            title="Nenhuma categoria criada"
            description="Crie a primeira categoria antes de cadastrar prompts."
            action={
              <Button onClick={bank.openCreateCategory}>
                <Plus />
                Criar categoria
              </Button>
            }
          />
        ) : (
          <div className="space-y-2">
            {bank.categories.map((category) => (
              <div
                key={category.id}
                className="rounded-[18px] border border-border/60 bg-surface px-3 py-3"
              >
                <div className="flex items-start justify-between gap-3">
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <p className="truncate text-sm font-semibold text-foreground">{category.name}</p>
                      <Badge variant="neutral">{category.usageCount}</Badge>
                      {category.replacementRequiredCount > 0 ? (
                        <Badge variant="warning">{category.replacementRequiredCount} exigem substituição</Badge>
                      ) : null}
                    </div>
                    <p className="mt-1 text-xs text-muted-foreground">
                      {category.usageCount === 1 ? "1 prompt vinculado" : `${category.usageCount} prompts vinculados`}
                    </p>
                  </div>

                  {category.canEdit || category.canDelete ? (
                    <DropdownMenu>
                      <DropdownMenuTrigger asChild>
                        <Button variant="ghost" size="icon" aria-label={`Ações da categoria ${category.name}`}>
                          <MoreHorizontal />
                        </Button>
                      </DropdownMenuTrigger>
                      <DropdownMenuContent align="end">
                        <DropdownMenuLabel>{category.name}</DropdownMenuLabel>
                        {category.canEdit ? (
                          <DropdownMenuItem onClick={() => bank.openEditCategory(category)}>
                            <Pencil className="size-4" />
                            Editar
                          </DropdownMenuItem>
                        ) : null}
                        {category.canDelete ? (
                          <>
                            <DropdownMenuSeparator />
                            <DropdownMenuItem className="text-danger focus:text-danger" onClick={() => bank.openDeleteCategory(category)}>
                              <Trash2 className="size-4" />
                              Excluir
                            </DropdownMenuItem>
                          </>
                        ) : null}
                      </DropdownMenuContent>
                    </DropdownMenu>
                  ) : null}
                </div>
              </div>
            ))}
          </div>
        )}
      </CardContent>
    </Card>
  );
}

function PromptBoard({ bank }: { bank: PromptBankController }) {
  return (
    <Card>
      <CardHeader className="border-b border-border/60 pb-4">
        <div className="flex flex-col gap-3">
          <div className="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
            <div>
              <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">Banco de Prompts</p>
              <h2 className="mt-1 text-xl font-semibold text-foreground">{bank.subtitle}</h2>
            </div>

            <div className="flex flex-wrap items-center gap-2">
              <Button onClick={bank.openCreatePrompt} disabled={bank.categories.length === 0}>
                <Plus />
                Novo prompt
              </Button>
            </div>
          </div>

          <div className="flex flex-wrap items-center gap-2 xl:flex-nowrap">
            <div className="relative min-w-[18rem] flex-[1.7]">
              <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                className="pl-9"
                value={bank.search}
                onChange={(event) => bank.setSearchValue(event.target.value)}
                placeholder="Buscar por título, descrição, prompt ou categoria"
                aria-label="Buscar prompts"
              />
            </div>

            <Select className="min-w-[12rem] xl:w-[12rem]" value={bank.universeFilter} onChange={(event) => bank.setUniverseFilterValue(event.target.value)}>
              <option value="all">Todos os universos</option>
              <option value="none">Sem universo</option>
              {bank.universes.map((universe) => (
                <option key={universe.id} value={universe.id}>
                  {universe.name}
                </option>
              ))}
            </Select>

            <CategoryFilterDropdown bank={bank} />
          </div>
        </div>
      </CardHeader>

      <CardContent className="space-y-4 p-4">
        {bank.categories.length === 0 ? (
          <EmptyState
            icon={<Tag className="size-5" />}
            title="Crie categorias antes de cadastrar prompts"
            description="Cada prompt precisa ter pelo menos uma categoria, então o primeiro passo é preparar a taxonomia."
            action={
              <Button onClick={bank.openCreateCategory}>
                <Plus />
                Criar categoria
              </Button>
            }
          />
        ) : bank.promptPage.items.length === 0 ? (
          <EmptyState
            icon={<Sparkles className="size-5" />}
            title="Nenhum prompt encontrado"
            description="Ajuste os filtros atuais ou crie um novo prompt para começar seu banco."
            action={
              <Button onClick={bank.openCreatePrompt}>
                <Plus />
                Novo prompt
              </Button>
            }
          />
        ) : (
          <>
            <div className="flex flex-wrap gap-4">
              {bank.promptPage.items.map((prompt) => (
                <PromptCard
                  key={prompt.id}
                  prompt={prompt}
                  token={bank.session?.accessToken ?? ""}
                  onOpen={() => void bank.openPrompt(prompt.id)}
                  onEdit={() => void bank.openEditPrompt(prompt.id)}
                  onDelete={() => void bank.deletePrompt(prompt).catch(() => undefined)}
                />
              ))}
            </div>

            <PaginationControls
              page={bank.page}
              totalPages={bank.totalPages}
              totalCount={bank.promptPage.totalCount}
              pageSize={bank.promptPage.pageSize}
              onPrevious={() => bank.setPage(Math.max(1, bank.page - 1))}
              onNext={() => bank.setPage(Math.min(bank.totalPages, bank.page + 1))}
            />
          </>
        )}
      </CardContent>
    </Card>
  );
}

function CategoryFilterDropdown({ bank }: { bank: PromptBankController }) {
  const label =
    bank.selectedCategoryIds.length === 0
      ? "Todas as categorias"
      : bank.selectedCategoryIds.length === 1
        ? bank.categories.find((category) => category.id === bank.selectedCategoryIds[0])?.name ?? "1 categoria"
        : `${bank.selectedCategoryIds.length} categorias`;

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="secondary" className="min-w-[12rem] justify-between">
          <span className="truncate">{label}</span>
          <Tag className="size-4" />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-[19rem]">
        <DropdownMenuLabel>Filtrar por categoria</DropdownMenuLabel>
        {bank.categories.length === 0 ? (
          <div className="px-3 py-3 text-sm text-muted-foreground">Nenhuma categoria cadastrada.</div>
        ) : (
          <>
            {bank.categories.map((category) => {
              const selected = bank.selectedCategoryIds.includes(category.id);
              return (
                <DropdownMenuItem
                  key={category.id}
                  onSelect={(event) => {
                    event.preventDefault();
                    bank.toggleCategoryFilter(category.id);
                  }}
                >
                  <Check className={cn("size-4", selected ? "opacity-100" : "opacity-0")} />
                  <span className="flex-1">{category.name}</span>
                  <Badge variant="neutral">{category.usageCount}</Badge>
                </DropdownMenuItem>
              );
            })}
            {bank.selectedCategoryIds.length > 0 ? (
              <>
                <DropdownMenuSeparator />
                <DropdownMenuItem
                  onSelect={(event) => {
                    event.preventDefault();
                    bank.selectedCategoryIds.forEach((categoryId) => bank.toggleCategoryFilter(categoryId));
                  }}
                >
                  Limpar seleção
                </DropdownMenuItem>
              </>
            ) : null}
          </>
        )}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

function PromptUniverseBadge({
  name,
  imageUrl,
}: {
  name: string;
  imageUrl?: string | null;
}) {
  return (
    <Badge variant="outline" className="gap-1.5 pl-1">
      <AvatarCircle name={name} imageUrl={imageUrl} className="size-4 text-[8px]" />
      <span className="truncate">{name}</span>
    </Badge>
  );
}

export function PromptCard({
  prompt,
  token,
  onOpen,
  onEdit,
  onDelete,
}: {
  prompt: PromptListItem;
  token: string;
  onOpen: () => void;
  onEdit: () => void;
  onDelete: () => void;
}) {
  return (
    <div
      className="group w-full cursor-pointer rounded-[24px] border border-border/70 bg-surface text-left shadow-xs transition hover:-translate-y-0.5 hover:shadow-sm focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-ring/70 sm:w-[21rem]"
      role="button"
      tabIndex={0}
      onClick={onOpen}
      onKeyDown={(event) => {
        if (event.key === "Enter" || event.key === " ") {
          event.preventDefault();
          onOpen();
        }
      }}
    >
      <div className="relative">
        <PromptImageFrame
          promptId={prompt.id}
          title={prompt.title}
          hasImage={prompt.hasImage}
          imageUpdatedAt={prompt.imageUpdatedAt}
          token={token}
          className="rounded-t-[24px]"
        />
        <div className="absolute right-3 top-3" onClick={(event) => event.stopPropagation()}>
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="secondary" size="icon" aria-label={`Ações do prompt ${prompt.title}`}>
                <MoreHorizontal />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              <DropdownMenuLabel>{prompt.title}</DropdownMenuLabel>
              <DropdownMenuItem onClick={onEdit} disabled={!prompt.canEdit}>
                <Pencil className="size-4" />
                Editar
              </DropdownMenuItem>
              <DropdownMenuSeparator />
              <DropdownMenuItem className="text-danger focus:text-danger" onClick={onDelete} disabled={!prompt.canDelete}>
                <Trash2 className="size-4" />
                Excluir
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </div>
      </div>

      <div className="space-y-3 p-4">
        <div className="flex flex-wrap items-center gap-2">
          {prompt.universeName ? (
            <PromptUniverseBadge name={prompt.universeName} imageUrl={prompt.universeImageUrl} />
          ) : (
            <Badge variant="neutral">Sem universo</Badge>
          )}
          {prompt.categories.slice(0, 2).map((category) => (
            <Badge key={category.id} variant="neutral">
              {category.name}
            </Badge>
          ))}
          {prompt.categories.length > 2 ? <Badge variant="neutral">+{prompt.categories.length - 2}</Badge> : null}
        </div>

        <div>
          <h3 className="line-clamp-2 text-lg font-semibold text-foreground">{prompt.title}</h3>
          {prompt.description ? (
            <p className="mt-2 line-clamp-3 text-sm leading-6 text-muted-foreground">
              {truncateText(prompt.description, DESCRIPTION_PREVIEW_LIMIT)}
            </p>
          ) : (
            <p className="mt-2 text-sm leading-6 text-muted-foreground">Sem descrição adicional.</p>
          )}
        </div>

        <div className="rounded-[16px] border border-border/60 bg-surface-elevated p-3">
          <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">Prompt</p>
          <p className="mt-2 line-clamp-4 whitespace-pre-wrap text-sm leading-6 text-foreground/85">
            {truncateText(prompt.promptText, PROMPT_PREVIEW_LIMIT)}
          </p>
        </div>

        {prompt.linkUrl && prompt.linkTitle ? (
          <div className="inline-flex items-center gap-2 text-sm font-medium text-primary">
            <Link2 className="size-4" />
            <span className="truncate">{prompt.linkTitle}</span>
          </div>
        ) : null}
      </div>
    </div>
  );
}

function PaginationControls({
  page,
  totalPages,
  totalCount,
  pageSize,
  onPrevious,
  onNext,
}: {
  page: number;
  totalPages: number;
  totalCount: number;
  pageSize: number;
  onPrevious: () => void;
  onNext: () => void;
}) {
  const start = totalCount === 0 ? 0 : (page - 1) * pageSize + 1;
  const end = Math.min(totalCount, page * pageSize);

  return (
    <div className="flex flex-col gap-3 rounded-[20px] border border-border/60 bg-surface-elevated px-4 py-3 sm:flex-row sm:items-center sm:justify-between">
      <p className="text-sm text-muted-foreground">
        Mostrando {start}-{end} de {totalCount}
      </p>
      <div className="flex items-center gap-2">
        <Button variant="secondary" size="icon" onClick={onPrevious} disabled={page <= 1} aria-label="Página anterior">
          <ChevronLeft />
        </Button>
        <Badge variant="neutral">
          Página {page} de {totalPages}
        </Badge>
        <Button variant="secondary" size="icon" onClick={onNext} disabled={page >= totalPages} aria-label="Próxima página">
          <ChevronRight />
        </Button>
      </div>
    </div>
  );
}

function PromptDialog({
  open,
  prompt,
  universes,
  categories,
  onOpenChange,
  onSave,
  token,
}: {
  open: boolean;
  prompt: PromptDetail | null;
  universes: Array<{ id: string; name: string }>;
  categories: PromptCategory[];
  onOpenChange: (open: boolean) => void;
  onSave: (input: PromptFormInput) => Promise<void>;
  token: string;
}) {
  const [universeId, setUniverseId] = useState(prompt?.universeId ?? "");
  const [title, setTitle] = useState(prompt?.title ?? "");
  const [description, setDescription] = useState(prompt?.description ?? "");
  const [promptText, setPromptText] = useState(prompt?.promptText ?? "");
  const [categoryIds, setCategoryIds] = useState<string[]>(prompt?.categories.map((category) => category.id) ?? []);
  const [linkUrl, setLinkUrl] = useState(prompt?.linkUrl ?? "");
  const [linkTitle, setLinkTitle] = useState(prompt?.linkTitle ?? "");
  const [imageFile, setImageFile] = useState<File | null>(null);
  const [removeImage, setRemoveImage] = useState(false);
  const previewUrl = useObjectUrl(imageFile);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const isEditing = Boolean(prompt);

  function toggleCategory(categoryId: string) {
    setCategoryIds((current) =>
      current.includes(categoryId) ? current.filter((item) => item !== categoryId) : [...current, categoryId],
    );
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setSaving(true);

    try {
      await onSave({
        universeId,
        title,
        description,
        promptText,
        categoryIds,
        linkUrl,
        linkTitle,
        imageFile,
        removeImage,
      });
      onOpenChange(false);
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Não foi possível salvar o prompt.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[88vh] w-[min(94vw,58rem)] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{isEditing ? "Editar prompt" : "Novo prompt"}</DialogTitle>
          <DialogDescription>Cadastre prompts com contexto suficiente para serem reutilizados depois.</DialogDescription>
        </DialogHeader>

        <form className="space-y-4" onSubmit={submit}>
          {error ? <Notice tone="danger">{error}</Notice> : null}

          <div className="grid gap-4 lg:grid-cols-[220px_minmax(0,1fr)]">
            <div className="space-y-3">
              <PromptImageFrame
                promptId={prompt?.id ?? "preview"}
                title={title || "Prompt"}
                hasImage={Boolean(prompt?.hasImage) && !removeImage && !imageFile}
                imageUpdatedAt={prompt?.imageUpdatedAt}
                token={token}
                className="rounded-[22px]"
                previewUrl={previewUrl}
              />

              <Input
                type="file"
                accept="image/jpeg,image/png,image/webp"
                onChange={(event) => {
                  setImageFile(event.target.files?.[0] ?? null);
                  if (event.target.files?.[0]) {
                    setRemoveImage(false);
                  }
                }}
              />

              {prompt?.hasImage ? (
                <button
                  className={cn(
                    "w-full rounded-[16px] border px-3 py-2 text-sm font-medium transition",
                    removeImage
                      ? "border-danger/30 bg-status-danger-soft text-danger"
                      : "border-border/70 bg-surface-strong text-muted-foreground hover:text-foreground",
                  )}
                  type="button"
                  onClick={() => {
                    setRemoveImage((current) => !current);
                    setImageFile(null);
                  }}
                >
                  {removeImage ? "Imagem marcada para remoção" : "Remover imagem atual ao salvar"}
                </button>
              ) : null}
            </div>

            <div className="space-y-4">
              <div className="grid gap-4 sm:grid-cols-2">
                <Field label="Título">
                  <Input value={title} onChange={(event) => setTitle(event.target.value)} autoFocus required />
                </Field>
                <Field label="Universo">
                  <Select value={universeId} onChange={(event) => setUniverseId(event.target.value)}>
                    <option value="">Sem universo</option>
                    {universes.map((universe) => (
                      <option key={universe.id} value={universe.id}>
                        {universe.name}
                      </option>
                    ))}
                  </Select>
                </Field>
              </div>

              <Field label="Descrição">
                <Textarea value={description} onChange={(event) => setDescription(event.target.value)} rows={4} />
              </Field>

              <div className="grid gap-2">
                <div className="flex items-center justify-between gap-3">
                  <span className="text-sm font-semibold text-foreground/85">Prompt</span>
                  <CopyPromptButton value={promptText} />
                </div>
                <Textarea value={promptText} onChange={(event) => setPromptText(event.target.value)} rows={10} required />
              </div>

              <div className="grid gap-4 sm:grid-cols-2">
                <Field label="Link">
                  <Input
                    value={linkUrl}
                    onChange={(event) => setLinkUrl(event.target.value)}
                    placeholder="https://..."
                  />
                </Field>
                <Field label="Título do link">
                  <Input
                    value={linkTitle}
                    onChange={(event) => setLinkTitle(event.target.value)}
                    placeholder="Ex.: Referência externa"
                  />
                </Field>
              </div>

              <Field label="Categorias">
                <div className="grid gap-2 sm:grid-cols-2">
                  {categories.map((category) => {
                    const selected = categoryIds.includes(category.id);
                    return (
                      <button
                        key={category.id}
                        className={cn(
                          "flex items-center justify-between rounded-[16px] border px-3 py-2.5 text-left text-sm transition",
                          selected
                            ? "border-primary/30 bg-highlight text-accent-foreground"
                            : "border-border/70 bg-surface-strong hover:bg-surface-muted",
                        )}
                        type="button"
                        onClick={() => toggleCategory(category.id)}
                      >
                        <span className="truncate">{category.name}</span>
                        <Check className={cn("size-4", selected ? "opacity-100" : "opacity-0")} />
                      </button>
                    );
                  })}
                </div>
              </Field>
            </div>
          </div>

          <DialogFooter>
            <Button variant="secondary" type="button" onClick={() => onOpenChange(false)}>
              Cancelar
            </Button>
            <Button type="submit" disabled={saving || categories.length === 0}>
              {isEditing ? "Salvar prompt" : "Criar prompt"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function CategoryDialog({
  open,
  category,
  onOpenChange,
  onSave,
}: {
  open: boolean;
  category: PromptCategory | null;
  onOpenChange: (open: boolean) => void;
  onSave: (name: string) => Promise<void>;
}) {
  const [name, setName] = useState(category?.name ?? "");
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const isEditing = Boolean(category);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setSaving(true);

    try {
      await onSave(name);
      onOpenChange(false);
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
          <DialogTitle>{isEditing ? "Editar categoria" : "Nova categoria"}</DialogTitle>
          <DialogDescription>Categorias ajudam a manter o banco filtrável e consistente.</DialogDescription>
        </DialogHeader>

        <form className="space-y-4" onSubmit={submit}>
          {error ? <Notice tone="danger">{error}</Notice> : null}
          <Field label="Nome da categoria">
            <Input value={name} onChange={(event) => setName(event.target.value)} autoFocus required />
          </Field>
          <DialogFooter>
            <Button variant="secondary" type="button" onClick={() => onOpenChange(false)}>
              Cancelar
            </Button>
            <Button type="submit" disabled={saving}>
              {isEditing ? "Salvar categoria" : "Criar categoria"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

export function CategoryDeleteDialog({
  open,
  category,
  categories,
  onOpenChange,
  onDelete,
}: {
  open: boolean;
  category: PromptCategory | null;
  categories: PromptCategory[];
  onOpenChange: (open: boolean) => void;
  onDelete: (categoryId: string, replacementCategoryId?: string) => Promise<void>;
}) {
  const [replacementCategoryId, setReplacementCategoryId] = useState(
    categories.find((item) => item.id !== category?.id)?.id ?? "",
  );
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!category) {
      return;
    }

    setError(null);
    setSaving(true);

    try {
      await onDelete(
        category.id,
        category.replacementRequiredCount > 0 ? replacementCategoryId || undefined : undefined,
      );
      onOpenChange(false);
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Não foi possível excluir a categoria.");
    } finally {
      setSaving(false);
    }
  }

  const replacementOptions = categories.filter((item) => item.id !== category?.id);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Excluir categoria</DialogTitle>
          <DialogDescription>
            Revise o impacto antes de remover a categoria do banco de prompts.
          </DialogDescription>
        </DialogHeader>

        {category ? (
          <form className="space-y-4" onSubmit={submit}>
            {error ? <Notice tone="danger">{error}</Notice> : null}

            <div className="rounded-[18px] border border-border/60 bg-surface-muted p-4">
              <p className="text-sm font-semibold text-foreground">{category.name}</p>
              <p className="mt-2 text-sm text-muted-foreground">
                {category.usageCount === 0
                  ? "Nenhum prompt usa esta categoria."
                  : `${category.usageCount} prompts usam esta categoria.`}
              </p>
              <p className="mt-1 text-sm text-muted-foreground">
                {category.replacementRequiredCount === 0
                  ? "Nenhum prompt ficará inválido após a exclusão."
                  : `${category.replacementRequiredCount} prompts perderiam a última categoria e exigem substituição.`}
              </p>
            </div>

            {category.replacementRequiredCount > 0 ? (
              <Field label="Categoria de substituição">
                <Select
                  value={replacementCategoryId}
                  onChange={(event) => setReplacementCategoryId(event.target.value)}
                  required
                >
                  <option value="">Selecione</option>
                  {replacementOptions.map((option) => (
                    <option key={option.id} value={option.id}>
                      {option.name}
                    </option>
                  ))}
                </Select>
              </Field>
            ) : null}

            <DialogFooter>
              <Button variant="secondary" type="button" onClick={() => onOpenChange(false)}>
                Cancelar
              </Button>
              <Button type="submit" variant="danger" disabled={saving || (category.replacementRequiredCount > 0 && !replacementCategoryId)}>
                Excluir categoria
              </Button>
            </DialogFooter>
          </form>
        ) : null}
      </DialogContent>
    </Dialog>
  );
}

export function PromptDetailDialog({
  open,
  prompt,
  loading,
  token,
  onOpenChange,
  onEdit,
  onDelete,
}: {
  open: boolean;
  prompt: PromptDetail | null;
  loading: boolean;
  token: string;
  onOpenChange: (open: boolean) => void;
  onEdit: (promptId: string) => void;
  onDelete: (prompt: PromptDetail) => void;
}) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[88vh] w-[min(94vw,62rem)] overflow-y-auto">
        {loading || !prompt ? (
          <div className="space-y-3 p-2">
            <div className="h-8 w-48 rounded bg-surface-muted" />
            <div className="h-64 rounded-[24px] bg-surface-muted" />
            <div className="h-24 rounded-[24px] bg-surface-muted" />
          </div>
        ) : (
          <>
            <DialogHeader>
              <div className="flex flex-wrap items-start justify-between gap-3 pr-10">
                <div className="space-y-3">
                  <div className="flex flex-wrap items-center gap-2">
                    {prompt.universeName ? (
                      <PromptUniverseBadge name={prompt.universeName} imageUrl={prompt.universeImageUrl} />
                    ) : (
                      <Badge variant="neutral">Sem universo</Badge>
                    )}
                    {prompt.categories.map((category) => (
                      <Badge key={category.id} variant="neutral">
                        {category.name}
                      </Badge>
                    ))}
                  </div>
                  <div>
                    <DialogTitle>{prompt.title}</DialogTitle>
                    <DialogDescription className="mt-2">
                      {prompt.description || "Sem descrição adicional para este prompt."}
                    </DialogDescription>
                  </div>
                </div>

                <div className="flex flex-wrap gap-2">
                  <Button variant="secondary" onClick={() => onEdit(prompt.id)} disabled={!prompt.canEdit}>
                    <Pencil />
                    Editar
                  </Button>
                  <Button variant="danger" onClick={() => onDelete(prompt)} disabled={!prompt.canDelete}>
                    <Trash2 />
                    Excluir
                  </Button>
                </div>
              </div>
            </DialogHeader>

            <div className="grid gap-4 lg:grid-cols-[260px_minmax(0,1fr)]">
              <PromptImageFrame
                promptId={prompt.id}
                title={prompt.title}
                hasImage={prompt.hasImage}
                imageUpdatedAt={prompt.imageUpdatedAt}
                token={token}
                className="rounded-[24px]"
              />

              <div className="space-y-4">
                <div className="rounded-[20px] border border-border/60 bg-surface-elevated p-4">
                  <div className="flex items-start justify-between gap-3">
                    <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">Prompt completo</p>
                    <CopyPromptButton className="-mr-1 -mt-1" value={prompt.promptText} />
                  </div>
                  <p className="mt-3 whitespace-pre-wrap text-sm leading-7 text-foreground">{prompt.promptText}</p>
                </div>

                {prompt.linkUrl && prompt.linkTitle ? (
                  <div className="rounded-[20px] border border-border/60 bg-surface-elevated p-4">
                    <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">Link</p>
                    <a
                      className="mt-3 inline-flex items-center gap-2 text-sm font-semibold text-primary hover:underline"
                      href={prompt.linkUrl}
                      target="_blank"
                      rel="noreferrer"
                    >
                      <ExternalLink className="size-4" />
                      {prompt.linkTitle}
                    </a>
                  </div>
                ) : null}
              </div>
            </div>
          </>
        )}
      </DialogContent>
    </Dialog>
  );
}

function CopyPromptButton({ value, className }: { value: string; className?: string }) {
  const disabled = value.trim().length === 0;

  async function handleCopy() {
    const normalized = value.trim();

    if (!normalized) {
      toast.error("Não há prompt para copiar.");
      return;
    }

    try {
      const copied = await copyText(normalized);
      if (!copied) {
        toast.error("A cópia não está disponível neste navegador.");
        return;
      }
      toast.success("Prompt copiado.");
    } catch {
      toast.error("Não foi possível copiar o prompt.");
    }
  }

  return (
    <Button
      type="button"
      variant="ghost"
      size="icon"
      className={cn("size-8 rounded-lg", className)}
      aria-label="Copiar prompt"
      title="Copiar prompt"
      disabled={disabled}
      onClick={() => void handleCopy()}
    >
      <Copy className="size-4" />
    </Button>
  );
}

async function copyText(value: string) {
  if (typeof navigator !== "undefined" && navigator.clipboard?.writeText) {
    try {
      await navigator.clipboard.writeText(value);
      return true;
    } catch {
      // Fall back to a manual copy path for browsers/contexts that block the async clipboard API.
    }
  }

  if (typeof document === "undefined") {
    return false;
  }

  const textarea = document.createElement("textarea");
  textarea.value = value;
  textarea.setAttribute("readonly", "");
  textarea.style.position = "fixed";
  textarea.style.opacity = "0";
  textarea.style.pointerEvents = "none";

  document.body.appendChild(textarea);
  textarea.focus();
  textarea.select();
  textarea.setSelectionRange(0, textarea.value.length);

  try {
    return document.execCommand("copy");
  } finally {
    document.body.removeChild(textarea);
  }
}

function PromptImageFrame({
  promptId,
  title,
  hasImage,
  imageUpdatedAt,
  token,
  className,
  previewUrl,
}: {
  promptId: string;
  title: string;
  hasImage: boolean;
  imageUpdatedAt?: string | null;
  token: string;
  className?: string;
  previewUrl?: string | null;
}) {
  const protectedImageUrl = useProtectedPromptImage(promptId, hasImage, imageUpdatedAt, token);
  const imageUrl = previewUrl ?? protectedImageUrl;

  return (
    <div
      className={cn(
        "aspect-[4/5] overflow-hidden border border-border/60 bg-[radial-gradient(circle_at_top_left,rgba(138,106,84,0.16),transparent_52%),linear-gradient(180deg,rgba(255,255,255,0.6),rgba(237,227,213,0.78))]",
        className,
      )}
    >
      {imageUrl ? (
        // eslint-disable-next-line @next/next/no-img-element
        <img alt={title} className="h-full w-full object-cover" src={imageUrl} />
      ) : (
        <div className="flex h-full flex-col items-center justify-center gap-3 text-center">
          <div className="grid size-14 place-items-center rounded-[20px] bg-surface-strong text-accent-foreground shadow-xs">
            <Sparkles className="size-6" />
          </div>
          <div className="max-w-[14rem]">
            <p className="text-sm font-semibold text-foreground">{title}</p>
            <p className="mt-1 text-xs leading-5 text-muted-foreground">Imagem opcional em proporção 4:5.</p>
          </div>
        </div>
      )}
    </div>
  );
}

function useProtectedPromptImage(promptId: string, hasImage: boolean, imageUpdatedAt: string | null | undefined, token: string) {
  const [imageUrl, setImageUrl] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    if (!hasImage || !token) {
      return () => {
        cancelled = true;
      };
    }

    void apiFetchBlob(`/api/prompts/${promptId}/image`, { token })
      .then((blob) => {
        if (cancelled) {
          return;
        }

        setImageUrl(URL.createObjectURL(blob));
      })
      .catch((exception) => {
        if (cancelled) {
          return;
        }

        setImageUrl(null);
        if (!(exception instanceof ApiError && exception.status === 404)) {
          console.error(exception);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [hasImage, imageUpdatedAt, promptId, token]);

  useEffect(() => {
    return () => {
      if (imageUrl) {
        URL.revokeObjectURL(imageUrl);
      }
    };
  }, [imageUrl]);

  return hasImage && token ? imageUrl : null;
}

function useObjectUrl(file: File | null) {
  const objectUrl = useMemo(() => (file ? URL.createObjectURL(file) : null), [file]);

  useEffect(() => {
    return () => {
      if (objectUrl) {
        URL.revokeObjectURL(objectUrl);
      }
    };
  }, [objectUrl]);

  return objectUrl;
}

function truncateText(value: string, limit: number) {
  const normalized = value.trim();
  if (normalized.length <= limit) {
    return normalized;
  }

  return `${normalized.slice(0, limit).trimEnd()}...`;
}
