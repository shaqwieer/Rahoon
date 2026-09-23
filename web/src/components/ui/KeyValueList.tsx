import type { ReactNode } from "react";
import { cn } from "@/lib/cn";

export interface KeyValueRow {
  key: ReactNode;
  value: ReactNode;
  /** Emphasised value (e.g. monthly installment). */
  strong?: boolean;
}

/** Label / value rows (review aside «ملخص الحل», owner offer terms). Uses <dl> for semantics. */
export function KeyValueList({ rows, dense, className }: { rows: KeyValueRow[]; dense?: boolean; className?: string }) {
  return (
    <dl className={cn("m-0 flex flex-col", dense ? "gap-2 text-14" : "gap-2.5 text-15", className)}>
      {rows.map((r, i) => (
        <div key={i} className="flex items-baseline justify-between gap-4">
          <dt className="text-muted">{r.key}</dt>
          <dd className={cn("m-0 text-end", r.strong && "font-bold")}>{r.value}</dd>
        </div>
      ))}
    </dl>
  );
}
