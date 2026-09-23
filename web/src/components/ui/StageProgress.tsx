"use client";

import { useId, useState } from "react";
import { cn } from "@/lib/cn";
import { useI18n } from "@/lib/i18n/client";
import { Icon } from "./Icon";

export interface StageProgressProps {
  /** Zero-based index of the current stage (0–6). */
  current: number;
  /** Custom stage names (default: the 7 case stages). */
  names?: string[];
  /** Optional per-stage meta line (dates, «الحالية · منذ 13 يوماً»). */
  meta?: Array<string | null | undefined>;
  /**
   * `responsive` (default): full bars from 768px, compact «المرحلة n من 7» below.
   * `full` / `compact` force one form; `header` is the slimmer CaseHeader form (5px bars, 12px labels).
   */
  variant?: "responsive" | "full" | "compact" | "header";
  className?: string;
}

type StepState = "done" | "current" | "todo";
const BAR: Record<StepState, string> = { done: "bg-charcoal", current: "bg-orange", todo: "bg-track" };
const ICON: Record<StepState, string> = { done: "check_circle", current: "radio_button_checked", todo: "radio_button_unchecked" };

function stepState(i: number, current: number): StepState {
  return i < current ? "done" : i === current ? "current" : "todo";
}

/** C03: short timeline that reads from the start edge; current stage = orange bar + bold + «الحالية». */
export function StageProgress({ current, names, meta, variant = "responsive", className }: StageProgressProps) {
  const { t } = useI18n();
  const stages = names ?? t.stages.names;
  if (variant === "full" || variant === "header") return <FullBars stages={stages} current={current} meta={meta} slim={variant === "header"} className={className} />;
  if (variant === "compact") return <CompactProgress stages={stages} current={current} meta={meta} className={className} />;
  return (
    <div className={className}>
      <div className="hidden md:block">
        <FullBars stages={stages} current={current} meta={meta} />
      </div>
      <div className="md:hidden">
        <CompactProgress stages={stages} current={current} meta={meta} />
      </div>
    </div>
  );
}

function srState(state: StepState, t: ReturnType<typeof useI18n>["t"]) {
  return state === "done" ? t.stages.done : state === "current" ? t.stages.current : t.stages.todo;
}

function FullBars({ stages, current, meta, slim, className }: { stages: string[]; current: number; meta?: Array<string | null | undefined>; slim?: boolean; className?: string }) {
  const { t } = useI18n();
  return (
    <ol
      aria-label={t.stages.label}
      className={cn("m-0 grid list-none gap-1 p-0", className)}
      style={{ gridTemplateColumns: `repeat(${stages.length}, minmax(0, 1fr))` }}
    >
      {stages.map((label, i) => {
        const s = stepState(i, current);
        return (
          <li key={i} aria-current={s === "current" ? "step" : undefined} className={cn("flex min-w-0 flex-col", slim ? "gap-[5px]" : "gap-2")}>
            <span aria-hidden="true" className={cn("block rounded-[3px]", slim ? "h-[5px]" : "h-1.5", BAR[s])} />
            <span
              className={cn(
                "flex items-center gap-1",
                slim ? "text-12 leading-[18px]" : "text-13 leading-5",
                s === "current" ? "font-bold" : "font-medium",
                s === "todo" ? "text-muted" : "text-ink",
              )}
            >
              {slim ? null : <Icon name={ICON[s]} size={16} />}
              <span className="min-w-0 truncate">{label}</span>
              <span className="sr-only">، {srState(s, t)}</span>
            </span>
            {!slim && meta?.[i] ? <span className="text-12 leading-[18px] text-muted">{meta[i]}</span> : null}
          </li>
        );
      })}
    </ol>
  );
}

function CompactProgress({ stages, current, meta, className }: { stages: string[]; current: number; meta?: Array<string | null | undefined>; className?: string }) {
  const { t } = useI18n();
  const [open, setOpen] = useState(false);
  const listId = useId();
  return (
    <div className={cn("flex flex-col gap-2.5", className)}>
      <span className="text-13 font-semibold">{t.stages.stepOf(current + 1, stages.length)}</span>
      <div aria-hidden="true" className="grid gap-[3px]" style={{ gridTemplateColumns: `repeat(${stages.length}, 1fr)` }}>
        {stages.map((_, i) => (
          <span key={i} className={cn("h-1.5 rounded-[3px]", BAR[stepState(i, current)])} />
        ))}
      </div>
      <div className="text-16 font-semibold">{stages[current]}</div>
      <button
        type="button"
        aria-expanded={open}
        aria-controls={listId}
        onClick={() => setOpen((v) => !v)}
        className="min-h-11 self-start bg-transparent p-0 text-14 font-semibold text-rust underline underline-offset-[3px]"
      >
        {open ? t.stages.hideAll : t.stages.showAll}
      </button>
      <ol id={listId} hidden={!open} aria-label={t.stages.label} className="m-0 flex list-none flex-col gap-2 p-0">
        {stages.map((label, i) => {
          const s = stepState(i, current);
          return (
            <li key={i} aria-current={s === "current" ? "step" : undefined} className={cn("flex items-center gap-2 text-14", s === "current" ? "font-bold" : "", s === "todo" && "text-muted")}>
              <Icon name={ICON[s]} size={18} className={s === "current" ? "text-orange" : undefined} />
              <span className="flex-1">{label}</span>
              <span className="sr-only">، {srState(s, t)}</span>
              {meta?.[i] ? <span className="text-12 text-muted">{meta[i]}</span> : null}
            </li>
          );
        })}
      </ol>
    </div>
  );
}
