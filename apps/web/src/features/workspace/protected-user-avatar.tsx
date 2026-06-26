"use client";

import { useEffect, useState } from "react";
import { ApiError, apiFetchBlob, type HouseholdMember, type User } from "@/lib/api";
import { cn } from "@/lib/utils";

const photoCache = new Map<string, string>();
const photoRequests = new Map<string, Promise<string>>();

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
  householdId,
  className,
}: {
  user: User;
  token: string;
  householdId?: string;
  className?: string;
}) {
  const imageUrl = useProtectedUserPhoto(user.id, user.hasProfilePhoto, user.profilePhotoUpdatedAt, token, householdId);
  return <AvatarCircle name={user.displayName} imageUrl={imageUrl} className={className} />;
}

export function useProtectedUserPhoto(
  userId: string,
  hasProfilePhoto: boolean,
  profilePhotoUpdatedAt: string | null | undefined,
  token: string,
  householdId?: string,
) {
  const cacheKey = hasProfilePhoto && token
    ? householdId
      ? `member:${userId}:${householdId}:${profilePhotoUpdatedAt ?? ""}`
      : `me:${userId}:${profilePhotoUpdatedAt ?? ""}`
    : null;
  const path = householdId
    ? `/api/users/${userId}/profile-photo`
    : "/api/users/me/profile-photo";

  return useCachedProtectedUserPhoto(cacheKey, path, token, householdId);
}

export function useProtectedUserPhotoById(
  userId: string,
  hasProfilePhoto: boolean,
  profilePhotoUpdatedAt: string | null | undefined,
  token: string,
  householdId?: string,
) {
  const cacheKey = hasProfilePhoto && token && householdId
    ? `member:${userId}:${householdId}:${profilePhotoUpdatedAt ?? ""}`
    : null;

  return useCachedProtectedUserPhoto(cacheKey, `/api/users/${userId}/profile-photo`, token, householdId);
}

export function HouseholdMemberAvatar({
  member,
  token,
  householdId,
  className,
}: {
  member: HouseholdMember;
  token?: string;
  householdId?: string;
  className?: string;
}) {
  const imageUrl = useProtectedUserPhotoById(
    member.userId,
    member.hasProfilePhoto,
    member.profilePhotoUpdatedAt,
    token ?? "",
    householdId,
  );

  return <AvatarCircle name={member.displayName} imageUrl={imageUrl} className={className} />;
}

function useCachedProtectedUserPhoto(
  cacheKey: string | null,
  path: string,
  token: string,
  householdId?: string,
) {
  const [fetchedImage, setFetchedImage] = useState<{ cacheKey: string; imageUrl: string | null } | null>(null);

  useEffect(() => {
    let cancelled = false;

    if (!cacheKey || !token) {
      return () => {
        cancelled = true;
      };
    }

    const cachedImageUrl = photoCache.get(cacheKey);
    if (cachedImageUrl) {
      return () => {
        cancelled = true;
      };
    }

    const pendingRequest =
      photoRequests.get(cacheKey) ??
      apiFetchBlob(path, { token, householdId })
        .then((blob) => {
          const objectUrl = URL.createObjectURL(blob);
          photoCache.set(cacheKey, objectUrl);
          return objectUrl;
        })
        .finally(() => {
          photoRequests.delete(cacheKey);
        });

    photoRequests.set(cacheKey, pendingRequest);

    pendingRequest
      .then((imageUrl) => {
        if (cancelled) {
          return;
        }

        setFetchedImage({ cacheKey, imageUrl });
      })
      .catch((exception) => {
        if (cancelled) {
          return;
        }

        setFetchedImage({ cacheKey, imageUrl: null });
        if (!(exception instanceof ApiError && exception.status === 404)) {
          console.error(exception);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [cacheKey, householdId, path, token]);

  if (!cacheKey) {
    return null;
  }

  return photoCache.get(cacheKey) ?? (fetchedImage?.cacheKey === cacheKey ? fetchedImage.imageUrl : null);
}

function getInitials(name: string) {
  return name
    .split(" ")
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join("");
}
