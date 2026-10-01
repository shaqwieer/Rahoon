"use client";

import { useState } from "react";
import { apiSend } from "@/lib/api/client";
import { hardNavigate } from "@/lib/navigation";

/**
 * Sign-out for the shells. Ends with a full document load (`hardNavigate`) so no client cache, router cache or
 * in-memory data survives it.
 */
export function useSessionActions() {
  const [busy, setBusy] = useState<"logout" | null>(null);

  const logout = async () => {
    setBusy("logout");
    try {
      const res = await apiSend<{ next?: string }>("POST", "/auth/logout");
      hardNavigate(res?.next, "/login");
    } catch {
      // Session already gone (401) or network error: the cookie may be stale either way — go to login.
      hardNavigate("/login");
    }
  };

  return { logout, busy };
}
