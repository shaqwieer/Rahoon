"use client";

import type { ReactNode } from "react";
import { cn } from "@/lib/cn";
import { useI18n } from "@/lib/i18n/client";
import { Icon } from "./Icon";
import { Tag } from "./Status";

export type HumanOpinion = "agree" | "override" | "not_used";

export interface DecisionSupportTagProps {
  /** What the model estimates, e.g. «احتمال الالتزام بالقسط». */
  label: ReactNode;
  /** A range instead of a false-precision number, e.g. «62–74%». */
  range?: string;
  confidence: "low" | "medium" | "high";
  factors?: Array<{ label: ReactNode; direction: "up" | "down" | "neutral" }>;
  /** Data source line («كشف الراتب v2 · سجل الأقساط 24 شهراً»). */
  source: ReactNode;
  modelVersion: string;
  dataAsOf?: ReactNode;
  limitations: ReactNode;
  /** Required before the output is attached to a decision. */
  opinion?: { value: HumanOpinion; reason?: ReactNode; by?: ReactNode; at?: ReactNode };
  className?: string;
}

const OPINION: Record<HumanOpinion, { tone: "ok" | "warn" | "neutral"; icon: string }> = {
  agree: { tone: "ok", icon: "check_circle" },
  override: { tone: "warn", icon: "edit_note" },
  not_used: { tone: "neutral", icon: "remove_circle_outline" },
};

/**
 * Handoff «دعم القرار»: every automated output shows tag, range, confidence, factors, source + model version,
 * limitations and the human opinion. Never shown to owners; never changes case state.
 */
export function DecisionSupportTag({ label, range, confidence, factors, source, modelVersion, dataAsOf, limitations, opinion, className }: DecisionSupportTagProps) {
  const { t } = useI18n();
  const ds = t.decisionSupport;
  const opinionLabel = opinion ? { agree: ds.agree, override: ds.override, not_used: ds.notUsed }[opinion.value] : ds.pending;
  return (
    <section className={cn("flex flex-col gap-3 rounded-md border border-dashed border-line-strong bg-white p-4", className)}>
      <div className="flex flex-wrap items-center gap-2">
        <span className="inline-flex items-center gap-1 rounded-xs border border-line-strong bg-warm px-2 py-0.5 text-12 font-semibold text-charcoal">
          <Icon name="insights" size={14} />
          {ds.tag}
        </span>
        <span className="text-12 text-muted">
          {ds.confidence}: <strong className="text-ink">{ds.confidenceLevels[confidence]}</strong>
        </span>
      </div>
      <div className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
        <strong className="text-15">{label}</strong>
        {range ? (
          <bdi dir="ltr" className="text-20 font-bold tabular-nums">
            {range}
          </bdi>
        ) : null}
      </div>
      {factors?.length ? (
        <div className="flex flex-col gap-1">
          <span className="text-12 font-semibold text-muted">{ds.factors}</span>
          <ul className="m-0 flex list-none flex-col gap-1 p-0 text-14">
            {factors.map((f, i) => (
              <li key={i} className="flex items-center gap-2">
                <Icon
                  name={f.direction === "up" ? "arrow_upward" : f.direction === "down" ? "arrow_downward" : "remove"}
                  size={16}
                  className={f.direction === "up" ? "text-ok" : f.direction === "down" ? "text-err" : "text-muted"}
                />
                {f.label}
              </li>
            ))}
          </ul>
        </div>
      ) : null}
      <dl className="m-0 grid grid-cols-[auto_minmax(0,1fr)] gap-x-3 gap-y-1 text-13">
        <dt className="text-muted">{ds.source}</dt>
        <dd className="m-0">
          {source}
          {dataAsOf ? <> · {dataAsOf}</> : null}
        </dd>
        <dt className="text-muted">{ds.model}</dt>
        <dd className="m-0">
          <bdi dir="ltr" className="font-mono text-12">
            {modelVersion}
          </bdi>
        </dd>
        <dt className="text-muted">{ds.limitations}</dt>
        <dd className="m-0">{limitations}</dd>
      </dl>
      <div className="flex flex-wrap items-center gap-2 border-t border-divider pt-2.5 text-13">
        <span className="text-muted">{ds.humanOpinion}:</span>
        {opinion ? (
          <Tag tone={OPINION[opinion.value].tone} icon={OPINION[opinion.value].icon}>
            {opinionLabel}
          </Tag>
        ) : (
          <Tag tone="info" icon="hourglass_top">
            {opinionLabel}
          </Tag>
        )}
        {opinion?.reason ? <span>«{opinion.reason}»</span> : null}
        {opinion?.by ? (
          <span className="text-muted">
            {opinion.by}
            {opinion.at ? <> · {opinion.at}</> : null}
          </span>
        ) : null}
      </div>
      <p className="m-0 text-12 text-muted">{ds.note}</p>
    </section>
  );
}
