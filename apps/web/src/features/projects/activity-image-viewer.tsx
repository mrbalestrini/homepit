"use client";

import { Minus, Move, Plus, RotateCcw } from "lucide-react";
import { type PointerEvent as ReactPointerEvent, type WheelEvent as ReactWheelEvent, useEffect, useRef, useState } from "react";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { cn } from "@/lib/utils";

export const ACTIVITY_IMAGE_MIN_ZOOM = 1;
export const ACTIVITY_IMAGE_MAX_ZOOM = 4;
export const ACTIVITY_IMAGE_ZOOM_STEP = 0.2;

type ActivityImageViewState = {
  scale: number;
  x: number;
  y: number;
};

type ActivityImageDragState = {
  pointerId: number;
  startX: number;
  startY: number;
  originX: number;
  originY: number;
};

export function clampActivityImageZoom(value: number) {
  if (!Number.isFinite(value)) {
    return ACTIVITY_IMAGE_MIN_ZOOM;
  }

  return Math.min(ACTIVITY_IMAGE_MAX_ZOOM, Math.max(ACTIVITY_IMAGE_MIN_ZOOM, value));
}

export function stepActivityImageZoom(currentScale: number, direction: -1 | 1) {
  const nextScale = clampActivityImageZoom(currentScale + direction * ACTIVITY_IMAGE_ZOOM_STEP);
  return nextScale === ACTIVITY_IMAGE_MIN_ZOOM ? ACTIVITY_IMAGE_MIN_ZOOM : nextScale;
}

export function ActivityImageViewerDialog({
  open,
  title,
  imageUrl,
  onOpenChange,
}: {
  open: boolean;
  title: string;
  imageUrl: string | null;
  onOpenChange: (open: boolean) => void;
}) {
  const [viewState, setViewState] = useState<ActivityImageViewState>({ scale: 1, x: 0, y: 0 });
  const dragStateRef = useRef<ActivityImageDragState | null>(null);
  const [isDragging, setIsDragging] = useState(false);

  useEffect(() => {
    if (!open || !imageUrl) {
      setViewState({ scale: 1, x: 0, y: 0 });
      setIsDragging(false);
      dragStateRef.current = null;
      return;
    }

    setViewState({ scale: 1, x: 0, y: 0 });
    setIsDragging(false);
    dragStateRef.current = null;
  }, [imageUrl, open]);

  if (!open || !imageUrl) {
    return null;
  }

  function handleZoom(direction: -1 | 1) {
    setViewState((current) => {
      const scale = stepActivityImageZoom(current.scale, direction);
      if (scale === ACTIVITY_IMAGE_MIN_ZOOM) {
        return { scale, x: 0, y: 0 };
      }

      return { ...current, scale };
    });
  }

  function resetView() {
    setViewState({ scale: 1, x: 0, y: 0 });
    setIsDragging(false);
    dragStateRef.current = null;
  }

  function handleWheel(event: ReactWheelEvent<HTMLDivElement>) {
    event.preventDefault();
    const direction: -1 | 1 = event.deltaY > 0 ? -1 : 1;
    setViewState((current) => {
      const scale = stepActivityImageZoom(current.scale, direction);
      if (scale === ACTIVITY_IMAGE_MIN_ZOOM) {
        return { scale, x: 0, y: 0 };
      }

      return { ...current, scale };
    });
  }

  function handlePointerDown(event: ReactPointerEvent<HTMLDivElement>) {
    if (viewState.scale <= ACTIVITY_IMAGE_MIN_ZOOM) {
      return;
    }

    dragStateRef.current = {
      pointerId: event.pointerId,
      startX: event.clientX,
      startY: event.clientY,
      originX: viewState.x,
      originY: viewState.y,
    };
    setIsDragging(true);
    event.currentTarget.setPointerCapture(event.pointerId);
  }

  function handlePointerMove(event: ReactPointerEvent<HTMLDivElement>) {
    const dragState = dragStateRef.current;
    if (!dragState || dragState.pointerId !== event.pointerId) {
      return;
    }

    setViewState((current) => ({
      scale: current.scale,
      x: dragState.originX + event.clientX - dragState.startX,
      y: dragState.originY + event.clientY - dragState.startY,
    }));
  }

  function endDrag(event: ReactPointerEvent<HTMLDivElement>) {
    const dragState = dragStateRef.current;
    if (!dragState || dragState.pointerId !== event.pointerId) {
      return;
    }

    dragStateRef.current = null;
    setIsDragging(false);
  }

  const cursor = viewState.scale > ACTIVITY_IMAGE_MIN_ZOOM ? (isDragging ? "grabbing" : "grab") : "zoom-in";

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="w-[min(96vw,72rem)] max-h-[92vh] overflow-hidden p-5" style={{ zIndex: 60 }}>
        <DialogHeader className="pr-10">
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>
            Arraste a imagem para mover. Use a roda do mouse ou os controles para ajustar o zoom.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          <div className="flex flex-wrap items-center gap-2">
            <div className="inline-flex items-center gap-2 rounded-full border border-border/70 bg-surface-muted px-3 py-1 text-xs font-semibold text-muted-foreground">
              <Move className="size-3.5" />
              Zoom {Math.round(viewState.scale * 100)}%
            </div>

            <div className="ml-auto flex flex-wrap gap-2">
              <Button variant="secondary" size="icon" type="button" onClick={() => handleZoom(-1)} aria-label="Reduzir zoom">
                <Minus />
              </Button>
              <Button variant="secondary" type="button" onClick={resetView}>
                <RotateCcw />
                Redefinir
              </Button>
              <Button variant="secondary" size="icon" type="button" onClick={() => handleZoom(1)} aria-label="Aumentar zoom">
                <Plus />
              </Button>
            </div>
          </div>

          <div
            aria-label="Área de visualização da imagem"
            className={cn(
              "relative h-[min(72vh,48rem)] overflow-hidden rounded-[24px] border border-border/70 bg-[radial-gradient(circle_at_top,rgba(255,255,255,0.18),transparent_34%),linear-gradient(180deg,rgba(15,15,15,0.98),rgba(28,22,18,0.94))]",
            )}
            onWheel={handleWheel}
            onPointerDown={handlePointerDown}
            onPointerMove={handlePointerMove}
            onPointerUp={endDrag}
            onPointerCancel={endDrag}
            style={{ touchAction: "none" }}
          >
            <div className="absolute inset-0 bg-[linear-gradient(rgba(255,255,255,0.02)_1px,transparent_1px),linear-gradient(90deg,rgba(255,255,255,0.02)_1px,transparent_1px)] bg-[size:24px_24px] opacity-40" />

            <div className="relative grid h-full w-full place-items-center p-4">
              {/* eslint-disable-next-line @next/next/no-img-element */}
              <img
                alt={title}
                className="max-h-full max-w-full select-none object-contain"
                draggable={false}
                src={imageUrl}
                style={{
                  transform: `translate3d(${viewState.x}px, ${viewState.y}px, 0) scale(${viewState.scale})`,
                  transformOrigin: "center center",
                  cursor,
                }}
              />
            </div>

            <div className="pointer-events-none absolute inset-x-4 bottom-4 flex items-center justify-between gap-3 text-xs text-white/70">
              <span className="rounded-full bg-black/35 px-3 py-1.5 backdrop-blur-sm">
                Clique e arraste para mover
              </span>
              <span className="rounded-full bg-black/35 px-3 py-1.5 backdrop-blur-sm">
                Roda do mouse para zoom
              </span>
            </div>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}
