"use client";

import Cropper, { type Area } from "react-easy-crop";
import { useState } from "react";
import { Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { SEO_IMAGE_HEIGHT, SEO_IMAGE_WIDTH, type PixelCrop } from "./seo-image-utils";

export type SeoImageCropDraft = {
  file: File;
  previewUrl: string;
  width: number;
  height: number;
};

type SeoImageCropDialogProps = {
  draft: SeoImageCropDraft | null;
  onCancel: () => void;
  onConfirm: (crop: PixelCrop) => Promise<void>;
};

export function SeoImageCropDialog({ draft, onCancel, onConfirm }: SeoImageCropDialogProps) {
  const [crop, setCrop] = useState({ x: 0, y: 0 });
  const [zoom, setZoom] = useState(1);
  const [croppedAreaPixels, setCroppedAreaPixels] = useState<PixelCrop | null>(null);
  const [processing, setProcessing] = useState(false);

  if (!draft) {
    return null;
  }

  async function confirmCrop() {
    if (!croppedAreaPixels) {
      return;
    }

    setProcessing(true);
    try {
      await onConfirm(croppedAreaPixels);
    } finally {
      setProcessing(false);
    }
  }

  return (
    <Dialog open={Boolean(draft)} onOpenChange={(open) => !open && !processing && onCancel()}>
      <DialogContent className="w-[min(96vw,72rem)] max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Ajustar imagem SEO</DialogTitle>
          <DialogDescription>
            A imagem final será publicada em WEBP com {SEO_IMAGE_WIDTH} x {SEO_IMAGE_HEIGHT} px.
            Reposicione e ajuste o zoom para alinhar o recorte do compartilhamento.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          <div className="rounded-[18px] border border-border/70 bg-surface-muted/55 px-4 py-3 text-sm text-muted-foreground">
            Arquivo original: {draft.width} x {draft.height} px. Máximo de 600 KB; o ideal é ficar abaixo de 100 KB.
          </div>

          <div className="relative aspect-[1200/630] overflow-hidden rounded-[24px] border border-border/70 bg-black">
            <Cropper
              image={draft.previewUrl}
              crop={crop}
              zoom={zoom}
              aspect={SEO_IMAGE_WIDTH / SEO_IMAGE_HEIGHT}
              maxZoom={4}
              objectFit="cover"
              restrictPosition
              onCropChange={setCrop}
              onCropComplete={(_, areaPixels: Area) => setCroppedAreaPixels(areaPixels)}
              onZoomChange={setZoom}
            />
          </div>

          <label className="grid gap-2">
            <span className="text-sm font-semibold text-foreground/80">Zoom</span>
            <input
              type="range"
              min={1}
              max={4}
              step={0.01}
              value={zoom}
              onChange={(event) => setZoom(Number(event.target.value))}
            />
          </label>
        </div>

        <DialogFooter>
          <Button type="button" variant="ghost" onClick={onCancel} disabled={processing}>
            Cancelar
          </Button>
          <Button type="button" onClick={() => void confirmCrop()} disabled={processing || !croppedAreaPixels}>
            {processing ? <Loader2 className="animate-spin" /> : null}
            {processing ? "Gerando imagem..." : "Usar este recorte"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
