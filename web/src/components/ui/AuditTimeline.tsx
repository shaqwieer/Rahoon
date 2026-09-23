"use client";

import type { ReactNode } from "react";
import { cn } from "@/lib/cn";
import { formatDateTime, formatHijri } from "@/lib/format";
import { useI18n } from "@/lib/i18n/client";
import { Icon } from "./Icon";

export type AuditKind = "transition" | "create" | "blocked" | "upload" | "reveal" | "integration" | "neutral";

const KIND: Record<AuditKind, { color: string; icon: string }> = {
  transition: { color: "#1D5A8C", icon: "swap_horiz" },
  create: { color: "#22262A", icon: "edit" },
  blocked: { color: "#B3261E", icon: "block" },
  upload: { color: "#1E6A45", icon: "upload_file" },
  reveal: { color: "#8A5300", icon: "visibility" },
  integration: { color: "#1D5A8C", icon: "sync" },
  neutral: { color: "#5E5D58", icon: "history" },
};

export interface AuditEvent {
  id: string;
  /** ISO instant; shown as Gregorian (Riyadh) + Umm al-Qura Hijri. */
  at: string | Date;
  kind: AuditKind;
  /** Override the kind icon. */
  icon?: string;
  /** «حل مقترح ← موافقة داخلية». */
  title: ReactNode;
  /** «سارة القحطاني · مديرة حالات». */
  actor?: ReactNode;
  /** Reason, evidence, blocker. */
  detail?: ReactNode;
}

/** C10 read-only audit trail: time column (Gregorian + Hijri), coloured rule, icon, title, actor, detail. Blocked transitions are listed too. */
export function AuditTimeline({ events, className }: { events: AuditEvent[]; className?: string }) {
  const { t, locale, numerals } = useI18n();
  return (
    <ol aria-label={t.audit.label} className={cn("m-0 flex list-none flex-col gap-[18px] rounded-lg border border-line bg-white p-5", className)}>
      {events.map((e) => {
        const k = KIND[e.kind];
        const iso = typeof e.at === "string" ? e.at : e.at.toISOString();
        return (
          <li key={e.id} className="grid grid-cols-1 gap-1.5 sm:grid-cols-[140px_minmax(0,1fr)] sm:gap-4">
            <div className="flex flex-row flex-wrap gap-x-2 text-12 leading-[18px] text-muted sm:flex-col">
              <time dateTime={iso}>
                <bdi dir="ltr" className="font-mono">
                  {formatDateTime(e.at, { numerals })}
                </bdi>
              </time>
              <span>{formatHijri(e.at, { locale, numerals, withSuffix: false })}</span>
            </div>
            <div className="flex flex-col gap-1 border-s-2 ps-3.5" style={{ borderColor: k.color }}>
              <div className={cn("flex items-center gap-1.5 text-14 font-semibold", e.kind === "blocked" && "text-err")}>
                <Icon name={e.icon ?? k.icon} size={18} style={{ color: k.color }} />
                <span>{e.title}</span>
              </div>
              {e.actor ? <span className="text-13 leading-5">{e.actor}</span> : null}
              {e.detail ? <span className="text-13 leading-5 text-muted">{e.detail}</span> : null}
            </div>
          </li>
        );
      })}
    </ol>
  );
}
