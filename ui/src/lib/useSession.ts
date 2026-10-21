"use client";

import { useQuery } from "@tanstack/react-query";
import type { SessionUser } from "@/lib/roles";

/** Current user from the Next session route handler (null when signed out). */
export function useSession() {
  return useQuery({
    queryKey: ["session"],
    queryFn: async (): Promise<SessionUser | null> => {
      const response = await fetch("/api/auth/session", { cache: "no-store" });
      if (!response.ok) return null;
      return ((await response.json()) as { user: SessionUser }).user;
    },
    staleTime: 60_000,
  });
}
