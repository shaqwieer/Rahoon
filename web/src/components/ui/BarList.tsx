"use client";

import { useId, useState, type ReactNode } from "react";
import { cn } from "@/lib/cn";
import { formatNumber } from "@/lib/format";
import { useI18n } from "@/lib/i18n/client";

export interface BarListItem {
  key: string;
  label: string;
  value: number;
  /** Pre-formatted display value (defaults to a grouped integer). */
  display?: string;
  /** Orange highlight — one category at most (C14). */
  highlight?: boolean;
}

export interface BarListProps {
  title: ReactNode;
  items: BarListItem[];
  /** Scale maximum (defaults to the largest value). */
  max?: number;
  /** Column headers for the table view. */
  columns?: { label: string; value: string };
  /** Unit spoken in each bar's accessible label, e.g. «حالة». */
  unit?: string;
  /** Source / rounding note under the chart («المصدر: سجل الانتقالات · 06:00 · القيم غير مقربة»). */
  footnote?: ReactNode;
  defaultView?: "chart" | "table";
  /** Width of the label column in chart view. */
  labelWidth?: number;
  headingLevel?: 2 | 3;
  className?: string;
}

/**
 * Accessible horizontal bar list (portfolio distribution, C14 pattern): every bar carries its text value,
 * bars read from the start edge, and «عرض كجدول» swaps to a real <table>.
 */
export function BarList({ title, items, max, columns, unit, footnote, defaultView = "chart", labelWidth = 150, headingLevel = 3, className }: BarListProps) {
  const { t, numerals } = useI18n();
  const [view, setView] = useState<"chart" | "table">(defaultView);
  const titleId = useId();
  const top = max ?? Math.max(1, ...items.map((i) => i.value));
  const H = `h${headingLevel}` as const;
  const show = (i: BarListItem) => i.display ?? formatNumber(i.value, 0, { numerals });
  return (
    <figure aria-labelledby={titleId} className={cn("m-0 flex flex-col gap-2.5 rounded-lg border border-line bg-white px-5 py-4", className)}>
      <figcaption className="flex items-baseline justify-between gap-3">
        <H id={titleId} className="m-0 text-18 leading-7 font-bold">
          {title}
        </H>
        <button
          type="button"
          aria-pressed={view === "table"}
          onClick={() => setView((v) => (v === "chart" ? "table" : "chart"))}
          className="min-h-11 bg-transparent text-13 font-semibold text-rust underline underline-offset-[3px] hover:text-rust-700"
        >
          {view === "chart" ? t.common.viewAsTable : t.common.viewAsChart}
        </button>
      </figcaption>
      {view === "chart" ? (
        <div role="list" className="flex flex-col gap-[5px]">
          {items.map((i) => (
            <div
              key={i.key}
              role="listitem"
              aria-label={`${i.label}: ${show(i)}${unit ? ` ${unit}` : ""}`}
              className="grid items-center gap-2.5 text-13 leading-[18px]"
              style={{ gridTemplateColumns: `${labelWidth}px minmax(0,1fr) 52px` }}
            >
              <span aria-hidden="true" className="truncate">
                {i.label}
              </span>
              <span aria-hidden="true" className="block h-2.5 overflow-hidden rounded-[2px] bg-subtle">
                <span className={cn("block h-full rounded-[2px]", i.highlight ? "bg-orange" : "bg-charcoal")} style={{ width: `${Math.round((i.value / top) * 100)}%` }} />
              </span>
              <bdi aria-hidden="true" dir="ltr" className="text-end font-semibold tabular-nums">
                {show(i)}
              </bdi>
            </div>
          ))}
        </div>
      ) : (
        <div className="overflow-hidden rounded-md border border-line">
          <table className="w-full border-collapse text-14">
            <thead>
              <tr className="bg-warm text-start text-muted">
                <th scope="col" className="px-3 py-2 text-start font-semibold">
                  {columns?.label ?? ""}
                </th>
                <th scope="col" className="px-3 py-2 text-end font-semibold">
                  {columns?.value ?? ""}
                </th>
              </tr>
            </thead>
            <tbody>
              {items.map((i) => (
                <tr key={i.key} className="border-t border-divider">
                  <th scope="row" className="px-3 py-2 text-start font-normal">
                    {i.label}
                  </th>
                  <td className="px-3 py-2 text-end">
                    <bdi dir="ltr" className="tabular-nums">
                      {show(i)}
                    </bdi>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {footnote ? <div className="text-12 text-muted">{footnote}</div> : null}
    </figure>
  );
}
