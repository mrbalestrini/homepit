"use client";

import Cropper, { type Area } from "react-easy-crop";
import { AlertTriangle, Loader2 } from "lucide-react";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { type PixelCrop } from "./profile-photo-utils";

export type ProfilePhotoCropDraft = {
  file: File;
  previewUrl: string;
};

type ProfilePhotoCropDialogProps = {
  draft: ProfilePhotoCropDraft | null;
  onCancel: () => void;
  onConfirm: (crop: PixelCrop) => Promise<void>;
};

export function ProfilePhotoCropDialog({ draft, onCancel, onConfirm }: ProfilePhotoCropDialogProps) {
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
      <DialogContent className="w-[min(96vw,56rem)] max-h-[90vh] overflow-y-auto p-4 sm:p-6">
        <DialogHeader>
          <DialogTitle>Ajustar foto de perfil</DialogTitle>
          <DialogDescription>
            Ajuste o enquadramento e o zoom até a foto ficar do jeito que você quer.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          <div className="rounded-[18px] border border-danger/20 bg-status-danger-soft px-4 py-3 text-sm leading-6 text-danger">
            <div className="flex items-start gap-3">
              <AlertTriangle className="mt-0.5 size-4 shrink-0" />
              <p>
                Ao confirmar, sua foto será substituída.
              </p>
            </div>
          </div>

          <div className="mx-auto w-full max-w-[34rem]">
            <div className="relative aspect-square overflow-hidden rounded-[24px] border border-border/70 bg-black">
              <Cropper
                image={draft.previewUrl}
                crop={crop}
                zoom={zoom}
                aspect={1}
                maxZoom={4}
                objectFit="cover"
                restrictPosition
                onCropChange={setCrop}
                onCropComplete={(_, areaPixels: Area) => setCroppedAreaPixels(areaPixels)}
                onZoomChange={setZoom}
              />
            </div>
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
            {processing ? "Aplicando..." : "Aplicar foto"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
