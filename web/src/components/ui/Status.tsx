"use client";

import type { ReactNode } from "react";
import { cn } from "@/lib/cn";
import type { SlaTone } from "@/lib/format";
import { getDictionary } from "@/lib/i18n";
import { useI18n } from "@/lib/i18n/client";
import { Icon } from "./Icon";
import { CASE_STATES, TONES, type CaseStatusKey, type Tone } from "./tones";

/* ───────── StatusChip: overall case state — round chip, icon + text (C02) ───────── */

export interface StatusChipProps {
  status: CaseStatusKey;
  /** md = 28px (headers, cards), sm = table rows. */
  size?: "md" | "sm";
  /** Adds the English name as a small secondary label (component gallery only). */
  showEnglish?: boolean;
  className?: string;
}

export function StatusChip({ status, size = "md", showEnglish, className }: StatusChipProps) {
  const { t } = useI18n();
  const meta = CASE_STATES[status];
  const c = TONES[meta.tone];
  return (
    <span
      className={cn(
        "inline-flex items-center gap-1.5 rounded-pill border font-semibold whitespace-nowrap",
        size === "md" ? "min-h-7 px-2.5 text-13" : "gap-1 px-2 py-0.5 text-12",
        className,
      )}
      style={{ color: c.fg, background: c.bg, borderColor: c.bd }}
    >
      <Icon name={meta.icon} size={size === "md" ? 16 : 14} />
      {t.caseStates[status]}
      {showEnglish ? (
        <span dir="ltr" className="font-latin text-11 font-normal opacity-85">
          {getDictionary("en").caseStates[status]}
        </span>
      ) : null}
    </span>
  );
}

/* ───────── SubStatusTag: square tag with kind prefix (task / approval / document / payment / external) ───────── */

export type SubStatusKind = "task" | "approval" | "document" | "payment" | "external";

export interface SubStatusTagProps {
  kind?: SubStatusKind;
  /** Custom prefix text instead of a known kind. */
  kindLabel?: string;
  label: ReactNode;
  icon: string;
  tone: Tone;
  className?: string;
}

export function SubStatusTag({ kind, kindLabel, label, icon, tone, className }: SubStatusTagProps) {
  const { t } = useI18n();
  const c = TONES[tone];
  const prefix = kindLabel ?? (kind ? t.subStateKinds[kind] : null);
  return (
    <span className={cn("inline-flex items-stretch overflow-hidden rounded-xs border text-12 font-semibold", className)} style={{ borderColor: c.bd }}>
      {prefix ? <span className="bg-subtle px-2 py-[3px] text-muted">{prefix}</span> : null}
      <span className="inline-flex items-center gap-1 px-2 py-[3px]" style={{ background: c.bg, color: c.fg }}>
        <Icon name={icon} size={14} />
        {label}
      </span>
    </span>
  );
}

/* ───────── SlaBadge: deadline state — icon + text carry the meaning ───────── */

const SLA: Record<SlaTone, { icon: string; fg: string; bg: string }> = {
  ok: { icon: "schedule", fg: "#1E6A45", bg: "#EAF4EE" },
  warn: { icon: "alarm", fg: "#8A5300", bg: "#FBF2DE" },
  err: { icon: "alarm_off", fg: "#B3261E", bg: "#FCECEA" },
  info: { icon: "hourglass_top", fg: "#1D5A8C", bg: "#EAF2F9" },
  paused: { icon: "pause_circle", fg: "#5E5D58", bg: "#F2F1ED" },
};

export interface SlaBadgeProps {
  tone: SlaTone;
  children: ReactNode;
  /** Text-only (no tinted background): table cells and the «ضمن المهلة» case. */
  plain?: boolean;
  size?: "md" | "sm";
  className?: string;
}

export function SlaBadge({ tone, children, plain, size = "md", className }: SlaBadgeProps) {
  const s = SLA[tone];
  return (
    <span
      className={cn(
        "inline-flex items-center gap-1.5 font-semibold whitespace-nowrap",
        size === "md" ? "text-13" : "gap-1 text-12",
        !plain && "rounded-xs px-2 py-0.5",
        className,
      )}
      style={{ color: s.fg, background: plain ? undefined : s.bg }}
    >
      <Icon name={s.icon} size={size === "md" ? 18 : 16} />
      <span>{children}</span>
    </span>
  );
}

/* ───────── IntegrationStateTag: enabled / simulated / pending / unavailable / failed ───────── */

export type IntegrationState = "enabled" | "simulated" | "pending" | "unavailable" | "failed";

const INTEGRATION: Record<IntegrationState, { tone: Tone; icon: string }> = {
  enabled: { tone: "ok", icon: "sync" },
  simulated: { tone: "info", icon: "science" },
  pending: { tone: "warn", icon: "hourglass_top" },
  unavailable: { tone: "neutral", icon: "cloud_off" },
  failed: { tone: "err", icon: "error" },
};

export function IntegrationStateTag({ state, showHint, className }: { state: IntegrationState; showHint?: boolean; className?: string }) {
  const { t } = useI18n();
  const meta = INTEGRATION[state];
  const c = TONES[meta.tone];
  return (
    <span className={cn("inline-flex items-center gap-2", className)}>
      <span
        className="inline-flex items-center gap-1 rounded-xs border px-2 py-0.5 text-12 font-semibold whitespace-nowrap"
        style={{ color: c.fg, background: c.bg, borderColor: c.bd }}
      >
        <Icon name={meta.icon} size={14} />
        {t.integration[state]}
      </span>
      {showHint ? <span className="text-12 text-muted">{t.integration[`${state}Hint`]}</span> : null}
    </span>
  );
}

/* ───────── Tag: generic tone tag (badges like «بانتظار ردك», «هذه الجلسة») ───────── */

export function Tag({ tone = "neutral", icon, children, className }: { tone?: Tone; icon?: string; children: ReactNode; className?: string }) {
  const c = TONES[tone];
  return (
    <span className={cn("inline-flex items-center gap-1 rounded-xs px-2 py-0.5 text-12 font-semibold whitespace-nowrap", className)} style={{ color: c.fg, background: c.bg }}>
      {icon ? <Icon name={icon} size={14} /> : null}
      {children}
    </span>
  );
}
