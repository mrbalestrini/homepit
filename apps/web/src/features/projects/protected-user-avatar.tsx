"use client";

import { useEffect, useState } from "react";
import { ApiError, apiFetchBlob, type User } from "@/lib/api";
import { cn } from "@/lib/utils";
import { getInitials } from "./project-dashboard.utils";

export function AvatarCircle({
  name,
  imageUrl,
  className,
}: {
  name: string;
  imageUrl?: string | null;
  className?: string;
}) {
  const [failedImageUrl, setFailedImageUrl] = useState<string | null>(null);
  const showImage = Boolean(imageUrl && failedImageUrl !== imageUrl);

  return (
    <div
      className={cn(
        "grid shrink-0 place-items-center overflow-hidden rounded-full bg-accent text-[11px] font-semibold text-accent-foreground",
        className,
      )}
    >
      {showImage ? (
        // eslint-disable-next-line @next/next/no-img-element
        <img
          alt={name}
          className="h-full w-full object-cover"
          src={imageUrl ?? undefined}
          onError={() => setFailedImageUrl(imageUrl ?? null)}
        />
      ) : (
        <span>{getInitials(name)}</span>
      )}
    </div>
  );
}

export function ProtectedUserAvatar({
  user,
  token,
  className,
}: {
  user: User;
  token: string;
  className?: string;
}) {
  const imageUrl = useProtectedUserPhoto(user, token);
  return <AvatarCircle name={user.displayName} imageUrl={imageUrl} className={className} />;
}

export function useProtectedUserPhoto(user: User, token: string) {
  const [imageUrl, setImageUrl] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    if (!user.hasProfilePhoto || !token) {
      return () => {
        cancelled = true;
      };
    }

    void apiFetchBlob("/api/users/me/profile-photo", { token })
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
  }, [token, user.hasProfilePhoto, user.id, user.profilePhotoUpdatedAt]);

  useEffect(() => {
    return () => {
      if (imageUrl) {
        URL.revokeObjectURL(imageUrl);
      }
    };
  }, [imageUrl]);

  return user.hasProfilePhoto && token ? imageUrl : null;
}
