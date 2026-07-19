"use client";

import Image from "next/image";
import Link from "next/link";
import {
  ArrowDown,
  ArrowLeft,
  ArrowUp,
  ExternalLink,
  Home,
  ImageIcon,
  Loader2,
  LogOut,
  Plus,
  Save,
  Trash2,
} from "lucide-react";
import { FormEvent, useEffect, useState } from "react";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { OrganizaClubAuth } from "@/features/workspace/organiza-club-auth";
import {
  API_BASE_URL,
  type AuthResponse,
  type InstitutionalContentItem,
  type InstitutionalPageContent,
  apiFetch,
  clearSession,
  readSession,
  storeSession,
  subscribeToSessionChanges,
} from "@/lib/api";
import { COMMON_IMAGE_ACCEPT, COMMON_IMAGE_HELP_TEXT } from "@/lib/image-upload";
import { SeoImageCropDialog, type SeoImageCropDraft } from "./seo-image-crop-dialog";
import {
  SEO_IMAGE_HEIGHT,
  SEO_IMAGE_IDEAL_BYTES,
  SEO_IMAGE_MAX_BYTES,
  SEO_IMAGE_WIDTH,
  cropSeoImageFile,
  optimizeSeoImageFile,
  readImageDimensions,
  type PixelCrop,
} from "./seo-image-utils";

type ImageSlot = "hero" | "highlight" | "seo";
type ListField = "benefits" | "steps";

export function InstitutionalAdmin() {
  const [session, setSession] = useState<AuthResponse | null>(null);
  const [sessionReady, setSessionReady] = useState(false);
  const [page, setPage] = useState<InstitutionalPageContent | null>(null);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [mediaLoading, setMediaLoading] = useState<ImageSlot | null>(null);
  const [seoCropDraft, setSeoCropDraft] = useState<SeoImageCropDraft | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    return () => {
      if (seoCropDraft) {
        URL.revokeObjectURL(seoCropDraft.previewUrl);
      }
    };
  }, [seoCropDraft]);

  useEffect(() => {
    let cancelled = false;
    void Promise.resolve().then(() => {
      if (!cancelled) {
        setSession(readSession());
        setSessionReady(true);
      }
    });

    const unsubscribe = subscribeToSessionChanges(setSession);
    return () => {
      cancelled = true;
      unsubscribe();
    };
  }, []);

  useEffect(() => {
    if (session?.user.systemRole !== "SuperAdmin") {
      return;
    }

    let cancelled = false;
    void Promise.resolve().then(async () => {
      if (cancelled) {
        return;
      }

      setLoading(true);
      setError(null);
      try {
        const content = await apiFetch<InstitutionalPageContent>("/api/admin/institutional-page", {
          token: session.accessToken,
        });
        if (!cancelled) {
          setPage(content);
        }
      } catch (exception) {
        if (!cancelled) {
          setError(exception instanceof Error ? exception.message : "Não foi possível carregar o CMS.");
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    });

    return () => {
      cancelled = true;
    };
  }, [session]);

  if (!sessionReady) {
    return <FullPageLoading label="Verificando sessão" />;
  }

  if (!session) {
    return (
      <OrganizaClubAuth
        onAuthenticated={(auth) => {
          storeSession(auth);
          setSession(auth);
        }}
      />
    );
  }

  if (session.user.systemRole !== "SuperAdmin") {
    return <AccessDenied onLogout={() => clearSession()} />;
  }

  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!page || !session) {
      return;
    }

    setSaving(true);
    setError(null);
    try {
      const updated = await apiFetch<InstitutionalPageContent>("/api/admin/institutional-page", {
        method: "PUT",
        token: session.accessToken,
        body: JSON.stringify({
          seoTitle: page.seoTitle,
          seoDescription: page.seoDescription,
          brandName: page.brandName,
          brandTagline: page.brandTagline,
          heroEyebrow: page.heroEyebrow,
          heroTitle: page.heroTitle,
          heroDescription: page.heroDescription,
          primaryCtaLabel: page.primaryCtaLabel,
          primaryCtaUrl: page.primaryCtaUrl,
          benefitsTitle: page.benefitsTitle,
          benefitsDescription: page.benefitsDescription,
          benefits: page.benefits.map(({ title, description }) => ({ title, description })),
          stepsTitle: page.stepsTitle,
          stepsDescription: page.stepsDescription,
          steps: page.steps.map(({ title, description }) => ({ title, description })),
          highlightEyebrow: page.highlightEyebrow,
          highlightTitle: page.highlightTitle,
          highlightDescription: page.highlightDescription,
          finalCtaTitle: page.finalCtaTitle,
          finalCtaDescription: page.finalCtaDescription,
          footerText: page.footerText,
          heroImageAlt: page.heroImageAlt,
          highlightImageAlt: page.highlightImageAlt,
        }),
      });
      setPage(updated);
      toast.success("Conteúdo salvo e publicada.");
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Não foi possível publicar o conteúdo.");
    } finally {
      setSaving(false);
    }
  }

  function updateField<K extends keyof InstitutionalPageContent>(field: K, value: InstitutionalPageContent[K]) {
    setPage((current) => (current ? { ...current, [field]: value } : current));
  }

  function updateListItem(field: ListField, index: number, key: "title" | "description", value: string) {
    setPage((current) => {
      if (!current) {
        return current;
      }

      const items = current[field].map((item, itemIndex) =>
        itemIndex === index ? { ...item, [key]: value } : item,
      );
      return { ...current, [field]: withPositions(items) };
    });
  }

  function addListItem(field: ListField) {
    setPage((current) => {
      if (!current || current[field].length >= 6) {
        return current;
      }

      return {
        ...current,
        [field]: withPositions([
          ...current[field],
          {
            position: current[field].length,
            title: "",
            description: "",
          },
        ]),
      };
    });
  }

  function removeListItem(field: ListField, index: number) {
    setPage((current) => {
      if (!current || current[field].length <= 1) {
        return current;
      }

      return {
        ...current,
        [field]: withPositions(current[field].filter((_, itemIndex) => itemIndex !== index)),
      };
    });
  }

  function moveListItem(field: ListField, index: number, direction: -1 | 1) {
    setPage((current) => {
      if (!current) {
        return current;
      }

      const targetIndex = index + direction;
      if (targetIndex < 0 || targetIndex >= current[field].length) {
        return current;
      }

      const items = [...current[field]];
      [items[index], items[targetIndex]] = [items[targetIndex], items[index]];
      return { ...current, [field]: withPositions(items) };
    });
  }

  async function uploadImage(slot: ImageSlot, file: File) {
    if (!session) {
      return false;
    }

    const form = new FormData();
    form.append("file", file);
    setMediaLoading(slot);
    setError(null);

    try {
      const updated = await apiFetch<InstitutionalPageContent>(`/api/admin/institutional-page/images/${slot}`, {
        method: "POST",
        token: session.accessToken,
        body: form,
      });
      setPage((current) => mergeMedia(current, updated));
      toast.success(
        slot === "hero"
          ? "Imagem principal publicada."
          : slot === "highlight"
            ? "Imagem de destaque publicada."
            : "Imagem SEO publicada.",
      );
      return true;
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Não foi possível enviar a imagem.");
      return false;
    } finally {
      setMediaLoading(null);
    }
  }

  async function deleteImage(slot: ImageSlot) {
    if (!session) {
      return;
    }

    setMediaLoading(slot);
    setError(null);
    try {
      const updated = await apiFetch<InstitutionalPageContent>(`/api/admin/institutional-page/images/${slot}`, {
        method: "DELETE",
        token: session.accessToken,
      });
      setPage((current) => mergeMedia(current, updated));
      toast.success("Imagem removida.");
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Não foi possível remover a imagem.");
    } finally {
      setMediaLoading(null);
    }
  }

  async function handleImageSelection(slot: ImageSlot, file: File) {
    if (slot !== "seo") {
      await uploadImage(slot, file);
      return;
    }

    await handleSeoImageSelection(file);
  }

  async function handleSeoImageSelection(file: File) {
    setError(null);

    if (file.type !== "image/webp") {
      setError("A imagem de SEO deve estar em WEBP.");
      return;
    }

    try {
      const dimensions = await readImageDimensions(file);
      if (dimensions.width === SEO_IMAGE_WIDTH && dimensions.height === SEO_IMAGE_HEIGHT) {
        const preparedFile = file.size <= SEO_IMAGE_IDEAL_BYTES ? file : await optimizeSeoImageFile(file);
        await uploadImage("seo", preparedFile);
        return;
      }

      setSeoCropDraft({
        file,
        previewUrl: URL.createObjectURL(file),
        width: dimensions.width,
        height: dimensions.height,
      });
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Não foi possível preparar a imagem SEO.");
    }
  }

  async function handleSeoCropConfirm(crop: PixelCrop) {
    if (!seoCropDraft) {
      return;
    }

    setError(null);
    try {
      const croppedFile = await cropSeoImageFile(seoCropDraft.file, crop);
      const uploaded = await uploadImage("seo", croppedFile);
      if (uploaded) {
        setSeoCropDraft(null);
      }
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Não foi possível gerar a imagem SEO.");
    }
  }

  function closeSeoCropDialog() {
    setSeoCropDraft(null);
  }

  return (
    <>
      <main className="min-h-screen bg-background">
        <header className="sticky top-0 z-20 border-b border-border/70 bg-surface-strong/95 backdrop-blur-xl">
        <div className="mx-auto flex max-w-[1500px] flex-wrap items-center gap-3 px-4 py-3 sm:px-6">
          <div className="flex min-w-0 flex-1 items-center gap-3">
            <span className="grid size-10 shrink-0 place-items-center rounded-[14px] bg-primary text-primary-foreground">
              <Home className="size-4" />
            </span>
            <div className="min-w-0">
              <p className="truncate font-display text-2xl leading-none">CMS institucional</p>
              <p className="mt-1 text-xs text-muted-foreground">Publicação imediata da landing page Organiza Club</p>
            </div>
          </div>

          <Badge variant="outline">SuperAdmin</Badge>
          <Button asChild variant="secondary">
            <Link href="/" target="_blank">
              <ExternalLink />
              Visualizar site
            </Link>
          </Button>
          <Button asChild variant="ghost">
            <Link href="/projects">
              <ArrowLeft />
              Voltar ao sistema
            </Link>
          </Button>
          <Button variant="ghost" size="icon" onClick={() => clearSession()} aria-label="Sair">
            <LogOut />
          </Button>
        </div>
      </header>

      <div className="mx-auto max-w-[1500px] px-4 py-6 sm:px-6">
        {error ? (
          <div className="mb-5 rounded-[18px] border border-danger/20 bg-status-danger-soft px-4 py-3 text-sm text-danger">
            {error}
          </div>
        ) : null}

        {loading || !page ? (
          <FullPageLoading label="Carregando conteúdo institucional" contained />
        ) : (
          <form className="grid gap-5 xl:grid-cols-[minmax(0,1fr)_420px]" onSubmit={save}>
            <div className="space-y-5">
              <EditorSection title="SEO e marca" description="Metadados de busca e identidade apresentada no cabeçalho.">
                <div className="grid gap-4 sm:grid-cols-2">
                  <Field label="Título SEO">
                    <Input value={page.seoTitle} maxLength={160} onChange={(event) => updateField("seoTitle", event.target.value)} required />
                  </Field>
                  <Field label="Nome da marca">
                    <Input value={page.brandName} maxLength={80} onChange={(event) => updateField("brandName", event.target.value)} required />
                  </Field>
                </div>
                <Field label="Descrição SEO">
                  <Textarea value={page.seoDescription} maxLength={320} onChange={(event) => updateField("seoDescription", event.target.value)} required />
                </Field>
                <Field label="Assinatura da marca">
                  <Input value={page.brandTagline} maxLength={200} onChange={(event) => updateField("brandTagline", event.target.value)} required />
                </Field>
                <ImageManager
                  slot="seo"
                  label="Imagem de SEO"
                  description={`Apenas WEBP em ${SEO_IMAGE_WIDTH} x ${SEO_IMAGE_HEIGHT} px. Máximo ${Math.round(SEO_IMAGE_MAX_BYTES / 1024)} KB e ideal abaixo de ${Math.round(SEO_IMAGE_IDEAL_BYTES / 1024)} KB. Se a resolução vier diferente, o sistema abre um crop para ajustar.`}
                  accept="image/webp"
                  previewClassName="aspect-[1200/630]"
                  hasImage={page.hasSeoImage}
                  updatedAt={page.seoImageUpdatedAt}
                  alt={page.seoTitle}
                  loading={mediaLoading === "seo"}
                  onUpload={(file) => void handleImageSelection("seo", file)}
                  onDelete={() => void deleteImage("seo")}
                />
              </EditorSection>

              <EditorSection title="Hero e conversão" description="Primeira mensagem da página e destino principal de contato.">
                <Field label="Destaque curto">
                  <Input value={page.heroEyebrow} maxLength={120} onChange={(event) => updateField("heroEyebrow", event.target.value)} required />
                </Field>
                <Field label="Título principal">
                  <Textarea className="min-h-28" value={page.heroTitle} maxLength={240} onChange={(event) => updateField("heroTitle", event.target.value)} required />
                </Field>
                <Field label="Descrição">
                  <Textarea value={page.heroDescription} maxLength={1200} onChange={(event) => updateField("heroDescription", event.target.value)} required />
                </Field>
                <div className="grid gap-4 sm:grid-cols-[0.7fr_1.3fr]">
                  <Field label="Texto do botão">
                    <Input value={page.primaryCtaLabel} maxLength={80} onChange={(event) => updateField("primaryCtaLabel", event.target.value)} required />
                  </Field>
                  <Field label="URL externa">
                    <Input
                      type="text"
                      value={page.primaryCtaUrl}
                      maxLength={2000}
                      placeholder="/projects ou https://..."
                      onChange={(event) => updateField("primaryCtaUrl", event.target.value)}
                      required
                    />
                  </Field>
                </div>
                <Field label="Texto alternativo da imagem principal">
                  <Input value={page.heroImageAlt} maxLength={300} onChange={(event) => updateField("heroImageAlt", event.target.value)} required />
                </Field>
                <ImageManager
                  slot="hero"
                  label="Imagem principal"
                  description={COMMON_IMAGE_HELP_TEXT}
                  accept={COMMON_IMAGE_ACCEPT}
                  hasImage={page.hasHeroImage}
                  updatedAt={page.heroImageUpdatedAt}
                  alt={page.heroImageAlt}
                  loading={mediaLoading === "hero"}
                  onUpload={(file) => void handleImageSelection("hero", file)}
                  onDelete={() => void deleteImage("hero")}
                />
              </EditorSection>

              <ListEditor
                title={page.benefitsTitle}
                description={page.benefitsDescription}
                titleLabel="Título da seção de benefícios"
                items={page.benefits}
                itemLabel="Benefício"
                onChangeTitle={(value) => updateField("benefitsTitle", value)}
                onChangeDescription={(value) => updateField("benefitsDescription", value)}
                onChangeItem={(index, key, value) => updateListItem("benefits", index, key, value)}
                onAdd={() => addListItem("benefits")}
                onRemove={(index) => removeListItem("benefits", index)}
                onMove={(index, direction) => moveListItem("benefits", index, direction)}
              />

              <ListEditor
                title={page.stepsTitle}
                description={page.stepsDescription}
                titleLabel="Título da seção de etapas"
                items={page.steps}
                itemLabel="Etapa"
                onChangeTitle={(value) => updateField("stepsTitle", value)}
                onChangeDescription={(value) => updateField("stepsDescription", value)}
                onChangeItem={(index, key, value) => updateListItem("steps", index, key, value)}
                onAdd={() => addListItem("steps")}
                onRemove={(index) => removeListItem("steps", index)}
                onMove={(index, direction) => moveListItem("steps", index, direction)}
              />

              <EditorSection title="Destaque do produto" description="Seção visual para reforçar o valor do Organiza Club.">
                <Field label="Destaque curto">
                  <Input value={page.highlightEyebrow} maxLength={120} onChange={(event) => updateField("highlightEyebrow", event.target.value)} required />
                </Field>
                <Field label="Título">
                  <Textarea value={page.highlightTitle} maxLength={240} onChange={(event) => updateField("highlightTitle", event.target.value)} required />
                </Field>
                <Field label="Descrição">
                  <Textarea value={page.highlightDescription} maxLength={1200} onChange={(event) => updateField("highlightDescription", event.target.value)} required />
                </Field>
                <Field label="Texto alternativo da imagem de destaque">
                  <Input value={page.highlightImageAlt} maxLength={300} onChange={(event) => updateField("highlightImageAlt", event.target.value)} required />
                </Field>
                <ImageManager
                  slot="highlight"
                  label="Imagem de destaque"
                  description={COMMON_IMAGE_HELP_TEXT}
                  accept={COMMON_IMAGE_ACCEPT}
                  hasImage={page.hasHighlightImage}
                  updatedAt={page.highlightImageUpdatedAt}
                  alt={page.highlightImageAlt}
                  loading={mediaLoading === "highlight"}
                  onUpload={(file) => void handleImageSelection("highlight", file)}
                  onDelete={() => void deleteImage("highlight")}
                />
              </EditorSection>

              <EditorSection title="Chamada final e rodapé" description="Fechamento comercial e texto institucional do rodapé.">
                <Field label="Título da chamada final">
                  <Textarea value={page.finalCtaTitle} maxLength={240} onChange={(event) => updateField("finalCtaTitle", event.target.value)} required />
                </Field>
                <Field label="Descrição da chamada final">
                  <Textarea value={page.finalCtaDescription} maxLength={1200} onChange={(event) => updateField("finalCtaDescription", event.target.value)} required />
                </Field>
                <Field label="Texto do rodapé">
                  <Textarea value={page.footerText} maxLength={600} onChange={(event) => updateField("footerText", event.target.value)} required />
                </Field>
              </EditorSection>
            </div>

            <aside className="space-y-4 xl:sticky xl:top-24 xl:self-start">
              <Card>
                <CardHeader>
                  <CardTitle>Prévia rápida</CardTitle>
                  <CardDescription>Resumo visual do conteúdo ainda não salvo.</CardDescription>
                </CardHeader>
                <CardContent>
                  <div className="overflow-hidden rounded-[24px] border border-border/70 bg-background">
                    {page.hasHeroImage ? (
                      <Image
                        className="aspect-[16/9] h-auto w-full object-cover"
                        src={institutionalImageUrl("hero", page.heroImageUpdatedAt)}
                        alt={page.heroImageAlt}
                        width={800}
                        height={450}
                      />
                    ) : (
                      <div className="grid aspect-[16/9] place-items-center bg-surface-muted">
                        <ImageIcon className="size-10 text-primary" />
                      </div>
                    )}
                    <div className="p-5">
                      <p className="text-[10px] font-bold uppercase tracking-[0.18em] text-primary">{page.heroEyebrow || "Destaque"}</p>
                      <h2 className="mt-3 font-display text-3xl leading-tight">{page.heroTitle || "Título principal"}</h2>
                      <p className="mt-3 line-clamp-4 text-sm leading-6 text-muted-foreground">{page.heroDescription || "Descrição da página."}</p>
                      <span className="mt-5 inline-flex rounded-xl bg-primary px-4 py-2 text-sm font-semibold text-primary-foreground">
                        {page.primaryCtaLabel || "Chamada principal"}
                      </span>
                    </div>
                  </div>
                </CardContent>
              </Card>

              <Card>
                <CardContent className="space-y-3 p-4">
                  <Button className="w-full" size="lg" type="submit" disabled={saving || Boolean(mediaLoading)}>
                    {saving ? <Loader2 className="animate-spin" /> : <Save />}
                    {saving ? "Publicando..." : "Salvar e publicar"}
                  </Button>
                  <p className="text-center text-xs leading-5 text-muted-foreground">
                    O salvamento atualiza imediatamente a página pública.
                  </p>
                  {page.updatedAt ? (
                    <p className="text-center text-xs text-muted-foreground">
                      Última publicação: {new Date(page.updatedAt).toLocaleString("pt-BR")}
                    </p>
                  ) : null}
                </CardContent>
              </Card>
            </aside>
          </form>
        )}
      </div>
      </main>

      <SeoImageCropDialog
        key={seoCropDraft?.previewUrl ?? "seo-crop-closed"}
        draft={seoCropDraft}
        onCancel={closeSeoCropDialog}
        onConfirm={handleSeoCropConfirm}
      />
    </>
  );
}

function EditorSection({
  title,
  description,
  children,
}: {
  title: string;
  description: string;
  children: React.ReactNode;
}) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>{title}</CardTitle>
        <CardDescription>{description}</CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">{children}</CardContent>
    </Card>
  );
}

function ListEditor({
  title,
  description,
  titleLabel,
  items,
  itemLabel,
  onChangeTitle,
  onChangeDescription,
  onChangeItem,
  onAdd,
  onRemove,
  onMove,
}: {
  title: string;
  description: string;
  titleLabel: string;
  items: InstitutionalContentItem[];
  itemLabel: string;
  onChangeTitle: (value: string) => void;
  onChangeDescription: (value: string) => void;
  onChangeItem: (index: number, key: "title" | "description", value: string) => void;
  onAdd: () => void;
  onRemove: (index: number) => void;
  onMove: (index: number, direction: -1 | 1) => void;
}) {
  return (
    <EditorSection title={itemLabel === "Benefício" ? "Benefícios" : "Como funciona"} description="Entre 1 e 6 itens, exibidos na ordem abaixo.">
      <Field label={titleLabel}>
        <Input value={title} maxLength={200} onChange={(event) => onChangeTitle(event.target.value)} required />
      </Field>
      <Field label="Descrição da seção">
        <Textarea value={description} maxLength={600} onChange={(event) => onChangeDescription(event.target.value)} required />
      </Field>

      <div className="space-y-3">
        {items.map((item, index) => (
          <div className="rounded-[20px] border border-border/70 bg-surface-muted/55 p-4" key={`${item.position}-${index}`}>
            <div className="mb-3 flex items-center gap-2">
              <Badge variant="neutral">{itemLabel} {index + 1}</Badge>
              <div className="ml-auto flex gap-1">
                <Button type="button" variant="ghost" size="icon" onClick={() => onMove(index, -1)} disabled={index === 0} aria-label={`Mover ${itemLabel.toLowerCase()} para cima`}>
                  <ArrowUp />
                </Button>
                <Button type="button" variant="ghost" size="icon" onClick={() => onMove(index, 1)} disabled={index === items.length - 1} aria-label={`Mover ${itemLabel.toLowerCase()} para baixo`}>
                  <ArrowDown />
                </Button>
                <Button type="button" variant="ghost" size="icon" onClick={() => onRemove(index)} disabled={items.length <= 1} aria-label={`Remover ${itemLabel.toLowerCase()}`}>
                  <Trash2 />
                </Button>
              </div>
            </div>
            <div className="space-y-3">
              <Input value={item.title} maxLength={160} placeholder="Título" onChange={(event) => onChangeItem(index, "title", event.target.value)} required />
              <Textarea value={item.description} maxLength={600} placeholder="Descrição" onChange={(event) => onChangeItem(index, "description", event.target.value)} required />
            </div>
          </div>
        ))}
      </div>

      <Button type="button" variant="secondary" onClick={onAdd} disabled={items.length >= 6}>
        <Plus />
        Adicionar {itemLabel.toLowerCase()}
      </Button>
    </EditorSection>
  );
}

function ImageManager({
  slot,
  label,
  description,
  accept,
  previewClassName,
  hasImage,
  updatedAt,
  alt,
  loading,
  onUpload,
  onDelete,
}: {
  slot: ImageSlot;
  label: string;
  description: string;
  accept: string;
  previewClassName?: string;
  hasImage: boolean;
  updatedAt?: string | null;
  alt: string;
  loading: boolean;
  onUpload: (file: File) => void;
  onDelete: () => void;
}) {
  return (
    <div className="rounded-[20px] border border-border/70 bg-surface-muted/55 p-4">
      <div className="grid gap-4 sm:grid-cols-[180px_1fr]">
        <div className="overflow-hidden rounded-[16px] border border-border/70 bg-surface-strong">
          {hasImage ? (
            <Image
              className={`${previewClassName ?? "aspect-[4/3]"} h-auto w-full object-cover`}
              src={institutionalImageUrl(slot, updatedAt)}
              alt={alt}
              width={480}
              height={360}
            />
          ) : (
            <div className={`grid ${previewClassName ?? "aspect-[4/3]"} place-items-center`}>
              <ImageIcon className="size-8 text-muted-foreground" />
            </div>
          )}
        </div>
        <div>
          <p className="font-semibold">{label}</p>
          <p className="mt-1 text-sm leading-6 text-muted-foreground">{description}</p>
          <div className="mt-3 flex flex-wrap gap-2">
            <label className="inline-flex h-10 cursor-pointer items-center justify-center gap-2 rounded-xl border border-border/70 bg-surface px-3.5 text-sm font-semibold shadow-xs hover:bg-surface-strong">
              {loading ? <Loader2 className="size-4 animate-spin" /> : <ImageIcon className="size-4" />}
              {hasImage ? "Substituir imagem" : "Enviar imagem"}
              <input
                className="sr-only"
                type="file"
                accept={accept}
                disabled={loading}
                onChange={(event) => {
                  const file = event.target.files?.[0];
                  if (file) {
                    onUpload(file);
                  }
                  event.currentTarget.value = "";
                }}
              />
            </label>
            {hasImage ? (
              <Button type="button" variant="ghost" onClick={onDelete} disabled={loading}>
                <Trash2 />
                Remover
              </Button>
            ) : null}
          </div>
        </div>
      </div>
    </div>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="grid gap-2">
      <span className="text-sm font-semibold text-foreground/80">{label}</span>
      {children}
    </label>
  );
}

function FullPageLoading({ label, contained = false }: { label: string; contained?: boolean }) {
  return (
    <div className={contained ? "grid min-h-[420px] place-items-center" : "grid min-h-screen place-items-center bg-background"}>
      <div className="text-center">
        <Loader2 className="mx-auto size-8 animate-spin text-primary" />
        <p className="mt-3 text-sm text-muted-foreground">{label}</p>
      </div>
    </div>
  );
}

function AccessDenied({ onLogout }: { onLogout: () => void }) {
  return (
    <main className="grid min-h-screen place-items-center bg-background px-4">
      <Card className="w-full max-w-lg">
        <CardHeader>
          <CardTitle>Acesso restrito</CardTitle>
          <CardDescription>Somente o perfil SuperAdmin pode acessar o CMS da página institucional.</CardDescription>
        </CardHeader>
        <CardContent className="flex flex-wrap gap-2">
          <Button asChild>
            <Link href="/projects">Voltar ao sistema</Link>
          </Button>
          <Button variant="ghost" onClick={onLogout}>
            <LogOut />
            Sair
          </Button>
        </CardContent>
      </Card>
    </main>
  );
}

function withPositions(items: InstitutionalContentItem[]) {
  return items.map((item, position) => ({ ...item, position }));
}

function mergeMedia(
  current: InstitutionalPageContent | null,
  updated: InstitutionalPageContent,
): InstitutionalPageContent {
  if (!current) {
    return updated;
  }

  return {
    ...current,
    hasHeroImage: updated.hasHeroImage,
    heroImageUpdatedAt: updated.heroImageUpdatedAt,
    hasHighlightImage: updated.hasHighlightImage,
    highlightImageUpdatedAt: updated.highlightImageUpdatedAt,
    hasSeoImage: updated.hasSeoImage,
    seoImageUpdatedAt: updated.seoImageUpdatedAt,
    updatedAt: updated.updatedAt,
  };
}

function institutionalImageUrl(slot: ImageSlot, updatedAt?: string | null) {
  return `${API_BASE_URL}/api/institutional-page/images/${slot}?v=${encodeURIComponent(updatedAt ?? "")}`;
}
