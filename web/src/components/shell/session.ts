"use client";

import { useState } from "react";
import { apiSend, isApiError } from "@/lib/api/client";
import { useToast } from "@/components/ui/Toast";
import { useI18n } from "@/lib/i18n/client";
import { hardNavigate } from "@/lib/navigation";

/**
 * Session actions used by every shell. Both end with a full document load (`hardNavigate`) so no
 * client cache, router cache or in-memory data survives a sign-out or an organization switch (C12).
 */
export function useSessionActions() {
  const { t } = useI18n();
  const { toast } = useToast();
  const [busy, setBusy] = useState<"logout" | "switch" | null>(null);

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

  const switchContext = async (membershipId: string) => {
    setBusy("switch");
    try {
      const res = await apiSend<{ next: string }>("POST", "/auth/context", { membershipId });
      hardNavigate(res.next, "/");
    } catch (e) {
      setBusy(null);
      toast({ tone: "err", message: isApiError(e) && e.title ? e.title : t.auth.login.network });
    }
  };

  return { logout, switchContext, busy };
}
