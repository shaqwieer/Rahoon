"use client";

import { useState, type ReactNode } from "react";
import { DebtorTop, type DebtorTopProps } from "@/components/shell/OwnerShell";
import { Icon } from "@/components/ui/Icon";
import { SkipLink } from "@/components/ui/SkipLink";
import { TONES, type Tone } from "@/components/ui/tones";
import { isApiError, useIdempotencyKey } from "@/lib/api/client";
import { cn } from "@/lib/cn";
import { useI18n } from "@/lib/i18n/client";
import { requestCopy, type RequestCopy, type RequestStatusKey, type WaitingKey } from "./copy";

export function useRequestCopy(): RequestCopy {
  const { locale } = useI18n();
  return requestCopy(locale);
}

/**
 * Individual pages (mobile-first 390): DebtorTop (back or symbol) + one reading column. `wide` lets the tracker use a
 * two-column grid from 1024px (D-3 1440 variant). No bottom navigation in the MVP.
 */
export function IndividualFrame({
  title,
  sub,
  back,
  trailing,
  width = "column",
  children,
}: {
  title: ReactNode;
  sub?: ReactNode;
  back?: DebtorTopProps["back"];
  trailing?: ReactNode;
  width?: "column" | "wide";
  children: ReactNode;
}) {
  return (
    <div className="flex min-h-dvh flex-col bg-warm">
      <SkipLink />
      <DebtorTop title={title} sub={sub} back={back} showBell={false} trailing={trailing} />
      <main
        id="main"
        tabIndex={-1}
        className={cn(
          "mx-auto flex w-full flex-1 flex-col gap-4 px-[18px] py-5 text-16 outline-none",
          width === "column" ? "max-w-[560px] md:max-w-[560px]" : "max-w-[560px] lg:max-w-[1040px] lg:py-8",
        )}
      >
        {children}
      </main>
    </div>
  );
}

const STATUS_STYLE: Record<RequestStatusKey, { tone: Tone; icon: string }> = {
  draft: { tone: "neutral", icon: "edit_note" },
  submitted: { tone: "info", icon: "send" },
  team_review: { tone: "info", icon: "manage_search" },
  info_requested: { tone: "warn", icon: "help" },
  lender_coordination: { tone: "info", icon: "forum" },
  offer_available: { tone: "sel", icon: "local_offer" },
  response_recorded: { tone: "info", icon: "task_alt" },
  closed: { tone: "neutral", icon: "lock" },
  not_eligible: { tone: "err", icon: "block" },
  withdrawn: { tone: "neutral", icon: "undo" },
};

/** Request status chip: icon + text carry the meaning (never colour alone). */
export function RequestStatusChip({ status, className }: { status: RequestStatusKey; className?: string }) {
  const c = useRequestCopy();
  const s = STATUS_STYLE[status];
  const t = TONES[s.tone];
  return (
    <span
      className={cn("inline-flex min-h-7 items-center gap-1.5 self-start rounded-pill border px-2.5 text-13 font-semibold", className)}
      style={{ color: t.fg, background: t.bg, borderColor: t.bd }}
    >
      <Icon name={s.icon} size={16} />
      {c.status[status]}
    </span>
  );
}

/** «ننتظر: فريق رهون / أنت / جهتك الممولة» (Q6 — shown instead of any deadline). */
export function WaitingOnLine({ waitingOn, className }: { waitingOn: WaitingKey; className?: string }) {
  const c = useRequestCopy();
  if (waitingOn === "none") return null;
  return (
    <p className={cn("m-0 flex items-center gap-1.5 text-15", waitingOn === "applicant" ? "font-semibold text-warn" : "text-charcoal", className)}>
      <Icon name={waitingOn === "applicant" ? "person" : waitingOn === "lender" ? "account_balance" : "groups"} size={18} />
      <span>
        {c.waitingLabel}: <strong>{c.waiting[waitingOn]}</strong>
      </span>
    </p>
  );
}

export function Panel({ children, className, as: Tag = "section", ...rest }: { children: ReactNode; className?: string; as?: "section" | "div" | "article" } & {
  "aria-labelledby"?: string;
  id?: string;
}) {
  return (
    <Tag className={cn("flex flex-col gap-3 rounded-[12px] border border-line bg-white p-4 lg:p-5", className)} {...rest}>
      {children}
    </Tag>
  );
}

/** Human message for a failed request call (network/offline copy, else the server's Arabic title or first field error). */
export function requestErrorText(e: unknown, c: RequestCopy): string {
  if (!isApiError(e)) return c.genericError;
  if (e.code === "network" || e.code === "offline") return c.networkError;
  if (e.code === "unavailable") return c.genericError;
  const field = e.errors ? Object.values(e.errors)[0]?.[0] : undefined;
  return field ?? (e.title || c.genericError);
}

/** One logical submit = one idempotency key; kept only across network failures so a retry replays. */
export function useRequestSubmit() {
  const key = useIdempotencyKey();
  const c = useRequestCopy();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function run<T>(fn: (idempotencyKey: string) => Promise<T>, onError?: (e: unknown) => string | null | undefined): Promise<{ ok: true; data: T } | { ok: false; error: unknown }> {
    setBusy(true);
    setError(null);
    try {
      const data = await fn(key.get());
      key.reset();
      return { ok: true, data };
    } catch (e) {
      if (!(isApiError(e) && (e.code === "network" || e.code === "offline"))) key.reset();
      const custom = onError?.(e);
      setError(custom === undefined ? requestErrorText(e, c) : custom);
      return { ok: false, error: e };
    } finally {
      setBusy(false);
    }
  }

  return { run, busy, error, setError };
}
