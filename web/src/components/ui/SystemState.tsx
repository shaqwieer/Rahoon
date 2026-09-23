"use client";

import type { ReactNode } from "react";
import { cn } from "@/lib/cn";
import { useI18n } from "@/lib/i18n/client";
import { Button } from "./Button";
import { Icon } from "./Icon";
import { Skeleton } from "./Skeleton";

export type SystemStateKind = "loading" | "empty" | "error" | "offline" | "forbidden" | "success";

const KIND: Record<Exclude<SystemStateKind, "loading">, { icon: string; fg: string; bg: string; role: "status" | "alert" }> = {
  empty: { icon: "inbox", fg: "#22262A", bg: "#F2F1ED", role: "status" },
  error: { icon: "error", fg: "#B3261E", bg: "#FCECEA", role: "alert" },
  offline: { icon: "wifi_off", fg: "#8A5300", bg: "#FBF2DE", role: "alert" },
  forbidden: { icon: "visibility_off", fg: "#1D5A8C", bg: "#EAF2F9", role: "status" },
  success: { icon: "check_circle", fg: "#1E6A45", bg: "#EAF4EE", role: "status" },
};

export interface SystemStateProps {
  kind: SystemStateKind;
  title?: ReactNode;
  body?: ReactNode;
  /** Error reference shown in the default error copy («المرجع: ERR-7F2A»). */
  errorRef?: string;
  /** Button under the body; `label` defaults to the kind's design copy (e.g. «إعادة المحاولة»). */
  action?: { label?: string; onClick?: () => void; href?: string };
  /** Extra actions / content under the body. */
  children?: ReactNode;
  /** `card` = bordered tile (C11 gallery), `page` = centered block inside a page, `inline` = no chrome. */
  layout?: "card" | "page" | "inline";
  /** Heading level for the title when `layout="page"` (h1 when it replaces a whole page). */
  headingLevel?: 1 | 2 | 3;
  /** Small kind label above the icon (gallery only). */
  showKind?: boolean;
  className?: string;
}

/** C11 system states: loading skeleton, empty, error (with ERR ref), offline, forbidden, success. Copy from 02 Components `sysStates`. */
export function SystemState({ kind, title, body, errorRef, action, children, layout = "card", headingLevel = 2, showKind, className }: SystemStateProps) {
  const { t } = useI18n();
  const s = t.sysStates;
  const chrome = layout === "card" ? "min-h-[200px] rounded-lg border border-line bg-white p-5" : layout === "page" ? "mx-auto w-full max-w-[560px] py-10" : "";

  if (kind === "loading") {
    return (
      <div aria-busy="true" aria-live="polite" className={cn("flex flex-col gap-3", chrome, className)}>
        {showKind ? <span className="text-12 font-semibold text-muted">{s.loading}</span> : <span className="sr-only">{t.common.loading}</span>}
        <Skeleton width="60%" height={14} />
        <Skeleton width="85%" height={28} />
        <Skeleton width="40%" height={14} />
        <Skeleton width="100%" height={40} radius={6} style={{ marginTop: "auto" }} />
      </div>
    );
  }

  const k = KIND[kind];
  const defaults: Record<typeof kind, { kind: string; title: string; body: string; action: string }> = {
    empty: { kind: s.emptyKind, title: s.emptyTitle, body: s.emptyBody, action: s.emptyAction },
    error: { kind: s.errorKind, title: s.errorTitle, body: s.errorBody(errorRef ?? "ERR-0000"), action: s.errorAction },
    offline: { kind: s.offlineKind, title: s.offlineTitle, body: s.offlineBody, action: s.offlineAction },
    forbidden: { kind: s.forbiddenKind, title: s.forbiddenTitle, body: s.forbiddenBody, action: s.forbiddenAction },
    success: { kind: s.successKind, title: s.successTitle, body: s.successBody, action: s.successAction },
  };
  const d = defaults[kind];
  const H = layout === "page" ? (`h${headingLevel}` as const) : "strong";
  return (
    <div role={k.role} className={cn("flex flex-col gap-2.5", chrome, layout === "page" && "gap-4", className)}>
      {showKind ? <span className="text-12 font-semibold text-muted">{d.kind}</span> : null}
      <span
        className={cn("flex flex-none items-center justify-center rounded-full", layout === "page" ? "size-14" : "size-11")}
        style={{ background: k.bg }}
      >
        <Icon name={k.icon} size={layout === "page" ? 30 : 24} style={{ color: k.fg }} />
      </span>
      <H className={cn("m-0 font-bold", layout === "page" ? "text-28 leading-10" : "text-16")}>{title ?? d.title}</H>
      <span className={cn("text-muted", layout === "page" ? "text-17 leading-7 text-ink" : "text-14 leading-[22px]")}>{body ?? d.body}</span>
      {children}
      {action ? (
        <Button variant={layout === "page" ? "primary" : "secondary"} size={layout === "page" ? "lg" : "md"} onClick={action.onClick} href={action.href} className={cn("self-start", layout === "card" && "mt-auto")}>
          {action.label ?? d.action}
        </Button>
      ) : null}
    </div>
  );
}

export interface EmptyStateProps {
  icon?: string;
  title: ReactNode;
  body?: ReactNode;
  action?: ReactNode;
  headingLevel?: 1 | 2 | 3;
  className?: string;
}

/** Calm empty placeholder (no decoration) — also used by portal placeholders while screens are built. */
export function EmptyState({ icon = "inbox", title, body, action, headingLevel = 2, className }: EmptyStateProps) {
  const H = `h${headingLevel}` as const;
  return (
    <div role="status" className={cn("flex flex-col items-start gap-3 rounded-lg border border-line bg-white p-6 sm:p-8", className)}>
      <span className="flex size-11 items-center justify-center rounded-full bg-subtle">
        <Icon name={icon} size={24} className="text-charcoal" />
      </span>
      <H className="m-0 text-20 leading-[30px] font-semibold">{title}</H>
      {body ? <p className="m-0 max-w-[60ch] text-15 leading-6 text-muted">{body}</p> : null}
      {action}
    </div>
  );
}
