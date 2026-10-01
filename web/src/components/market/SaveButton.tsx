"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { Icon } from "@/components/ui/Icon";
import { apiSend } from "@/lib/api/client";
import { cn } from "@/lib/cn";

/** Save an opportunity to «المحفوظة». Visitors are sent to sign in first (saving belongs to an account). */
export function SaveButton({ reference, saved: initial, signedIn }: { reference: string; saved: boolean; signedIn: boolean }) {
  const router = useRouter();
  const [saved, setSaved] = useState(initial);
  const [busy, setBusy] = useState(false);
  const toggle = async () => {
    if (!signedIn) {
      router.push(`/signin?next=${encodeURIComponent(`/opportunities/${reference}`)}`);
      return;
    }
    setBusy(true);
    const next = !saved;
    setSaved(next);
    try {
      await apiSend("POST", `/market/opportunities/${reference}/${next ? "save" : "unsave"}`);
    } catch {
      setSaved(!next);
    } finally {
      setBusy(false);
    }
  };
  return (
    <button type="button" onClick={() => void toggle()} aria-pressed={saved} aria-label={saved ? "إزالة من المحفوظة" : "حفظ الفرصة"} disabled={busy}
      className={cn("relative z-10 inline-flex size-10 items-center justify-center rounded-full bg-white/95 shadow-1 transition-colors hover:bg-white", saved ? "text-rust" : "text-charcoal")}>
      <Icon name="favorite" size={22} filled={saved} />
    </button>
  );
}
