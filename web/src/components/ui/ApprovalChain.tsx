"use client";

import type { ReactNode } from "react";
import { cn } from "@/lib/cn";
import { useI18n } from "@/lib/i18n/client";
import { Icon } from "./Icon";
import { TONES, type Tone } from "./tones";

export type ApprovalStepStatus = "prepared" | "reviewed" | "pending" | "approved" | "returned" | "rejected" | "escalated" | "notice";

const STEP: Record<ApprovalStepStatus, { icon: string; tone: Tone }> = {
  prepared: { icon: "edit", tone: "neutral" },
  reviewed: { icon: "check", tone: "ok" },
  approved: { icon: "check", tone: "ok" },
  pending: { icon: "hourglass_top", tone: "warn" },
  returned: { icon: "undo", tone: "err" },
  rejected: { icon: "cancel", tone: "err" },
  escalated: { icon: "north", tone: "warn" },
  notice: { icon: "notifications", tone: "info" },
};

export interface ApprovalStep {
  /** Role and limit, e.g. «المعتمِد (حتى 2,000,000)». */
  role: ReactNode;
  /** Person or «آلي». */
  who?: ReactNode;
  status: ApprovalStepStatus;
  /** Decision text shown in the status colour («بانتظار القرار · متبقٍ يومان»). */
  state: ReactNode;
  /** Time, reason, attachments. */
  meta?: ReactNode;
}

/** C08 maker–checker chain: ordered list, always vertical; each step shows role, person, decision, time, reason. */
export function ApprovalChain({ steps, className }: { steps: ApprovalStep[]; className?: string }) {
  const { t } = useI18n();
  return (
    <ol aria-label={t.approvalChain.label} className={cn("m-0 flex list-none flex-col rounded-lg border border-line bg-white p-5", className)}>
      {steps.map((s, i) => {
        const meta = STEP[s.status];
        const c = TONES[meta.tone];
        const last = i === steps.length - 1;
        return (
          <li key={i} className={cn("grid grid-cols-[32px_minmax(0,1fr)] gap-3", !last && "pb-4")}>
            <div className="flex flex-col items-center gap-1">
              <span className="flex size-8 flex-none items-center justify-center rounded-full border" style={{ background: c.bg, color: c.fg, borderColor: c.bd }}>
                <Icon name={meta.icon} size={18} />
              </span>
              {!last ? <span aria-hidden="true" className="min-h-4 w-0.5 flex-1 bg-track" /> : null}
            </div>
            <div className="flex flex-col gap-0.5">
              <div className="flex flex-wrap items-baseline gap-2">
                <strong className="text-15">{s.role}</strong>
                {s.who ? <span className="text-14">{s.who}</span> : null}
              </div>
              <span className="text-13 font-semibold" style={{ color: meta.tone === "neutral" ? "#22262A" : c.fg }}>
                {s.state}
              </span>
              {s.meta ? <span className="text-13 leading-5 text-muted">{s.meta}</span> : null}
            </div>
          </li>
        );
      })}
    </ol>
  );
}

export interface VersionDiffRow {
  label: ReactNode;
  before?: ReactNode;
  after?: ReactNode;
  /** Unchanged rows show a single merged value («دون تغيير · 1,284,560.00»). */
  unchanged?: ReactNode;
}

export interface VersionDiffProps {
  fromLabel: string;
  toLabel: string;
  rows: VersionDiffRow[];
  title?: ReactNode;
  className?: string;
}

/** Version differences table: before struck through, after emphasised — and «قبل/بعد» in text for screen readers. */
export function VersionDiff({ fromLabel, toLabel, rows, title, className }: VersionDiffProps) {
  const { t } = useI18n();
  const changes = rows.filter((r) => r.unchanged === undefined).length;
  return (
    <div className={cn("overflow-hidden rounded-lg border border-line bg-white", className)}>
      <div className="flex items-center justify-between border-b border-divider px-[18px] py-3.5">
        <strong className="text-15">
          {title ?? t.diff.title(fromLabel, toLabel)}
        </strong>
        <span className="text-12 text-muted">{t.diff.changes(changes)}</span>
      </div>
      <div className="overflow-x-auto">
        <table className="w-full border-collapse text-14">
          <thead>
            <tr className="bg-warm text-muted">
              <th scope="col" className="px-[18px] py-2 text-start font-semibold">
                {t.diff.item}
              </th>
              <th scope="col" className="px-2 py-2 text-start font-semibold">
                <bdi dir="ltr">{fromLabel}</bdi>
              </th>
              <th scope="col" className="px-2 py-2 text-start font-semibold">
                <bdi dir="ltr">{toLabel}</bdi>
              </th>
            </tr>
          </thead>
          <tbody>
            {rows.map((r, i) => (
              <tr key={i} className="border-t border-divider">
                <th scope="row" className="px-[18px] py-2.5 text-start font-normal">
                  {r.label}
                </th>
                {r.unchanged !== undefined ? (
                  <td colSpan={2} className="px-2 py-2.5">
                    {t.diff.unchanged} · {r.unchanged}
                  </td>
                ) : (
                  <>
                    <td className="px-2 py-2.5 text-muted line-through">
                      <span className="sr-only">{t.diff.before}: </span>
                      {r.before}
                    </td>
                    <td className="bg-rust-50 px-2 py-2.5 font-semibold">
                      <span className="sr-only">{t.diff.after}: </span>
                      {r.after}
                    </td>
                  </>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="border-t border-divider px-[18px] py-2.5 text-12 text-muted">{t.diff.note}</div>
    </div>
  );
}
