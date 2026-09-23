"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useRef, useState, type KeyboardEvent, type ReactNode } from "react";
import { cn } from "@/lib/cn";
import { formatMoney, type SlaTone } from "@/lib/format";
import { useI18n } from "@/lib/i18n/client";
import { Icon } from "./Icon";
import { SlaBadge, StatusChip } from "./Status";
import type { CaseStatusKey } from "./tones";

/** One case in a list (C09). Values arrive pre-masked from the API («عبدالله م.», «سارة ق.»). */
export interface CaseRowData {
  id: string;
  ref: string;
  /** «عبدالله م. · الرياض». */
  owner: string;
  status: CaseStatusKey;
  nextAction: ReactNode;
  sla: { tone: SlaTone; text: string };
  outstanding: number | null;
  assignee: string;
  href: string;
}

export interface CaseTableProps {
  rows: CaseRowData[];
  /** Accessible name of the grid (e.g. «حالاتي»). */
  label: string;
  selectable?: boolean;
  selected?: ReadonlySet<string>;
  onSelectedChange?: (next: Set<string>) => void;
  className?: string;
}

const COLS = 7;

/**
 * Responsive case list: ARIA grid table from 768px (arrow keys move between cells, Enter opens the case,
 * Space toggles selection); cards below 768 that keep ref, status, deadline, next action and assignee.
 */
export function CaseTable({ rows, label, selectable, selected, onSelectedChange, className }: CaseTableProps) {
  const { t, dir, locale, numerals } = useI18n();
  const router = useRouter();
  const [active, setActive] = useState<[number, number]>([0, selectable ? 0 : 1]);
  const cellRefs = useRef<Map<string, HTMLTableCellElement>>(new Map());
  const sel = selected ?? new Set<string>();
  const firstCol = selectable ? 0 : 1;

  const toggle = (id: string) => {
    if (!onSelectedChange) return;
    const next = new Set(sel);
    if (next.has(id)) next.delete(id);
    else next.add(id);
    onSelectedChange(next);
  };
  const toggleAll = () => onSelectedChange?.(sel.size === rows.length ? new Set() : new Set(rows.map((r) => r.id)));

  const focusCell = (r: number, c: number) => {
    setActive([r, c]);
    cellRefs.current.get(`${r}:${c}`)?.focus();
  };

  const onKeyDown = (e: KeyboardEvent<HTMLTableCellElement>, r: number, c: number) => {
    const next = dir === "rtl" ? "ArrowLeft" : "ArrowRight";
    const prev = dir === "rtl" ? "ArrowRight" : "ArrowLeft";
    const lastRow = rows.length - 1;
    if (e.key === next) focusCell(r, Math.min(COLS - 1, c + 1));
    else if (e.key === prev) focusCell(r, Math.max(firstCol, c - 1));
    else if (e.key === "ArrowDown") focusCell(Math.min(lastRow, r + 1), c);
    else if (e.key === "ArrowUp") focusCell(Math.max(0, r - 1), c);
    else if (e.key === "Home") focusCell(e.ctrlKey ? 0 : r, firstCol);
    else if (e.key === "End") focusCell(e.ctrlKey ? lastRow : r, COLS - 1);
    else if (e.key === "Enter") router.push(rows[r].href);
    else if (e.key === " " && selectable) toggle(rows[r].id);
    else return;
    e.preventDefault();
  };

  const cellProps = (r: number, c: number) => ({
    role: "gridcell" as const,
    tabIndex: active[0] === r && active[1] === c ? 0 : -1,
    ref: (el: HTMLTableCellElement | null) => {
      if (el) cellRefs.current.set(`${r}:${c}`, el);
      else cellRefs.current.delete(`${r}:${c}`);
    },
    onKeyDown: (e: KeyboardEvent<HTMLTableCellElement>) => onKeyDown(e, r, c),
    onFocus: () => setActive([r, c]),
    className: "px-2.5 py-3 align-top outline-offset-[-2px]",
  });

  const box = (checked: boolean) => (
    <span
      aria-hidden="true"
      className={cn("flex size-[18px] items-center justify-center rounded-xs", checked ? "bg-ink text-white" : "border border-line-strong bg-white")}
    >
      {checked ? <Icon name="check" size={14} /> : null}
    </span>
  );

  return (
    <div className={className}>
      {/* ≥768: grid table */}
      <div className="hidden overflow-x-auto rounded-lg border border-line bg-white md:block">
        <table role="grid" aria-label={label} aria-multiselectable={selectable || undefined} className="w-full min-w-[860px] border-collapse text-14 leading-[22px]">
          <thead>
            <tr className="bg-warm text-muted">
              {selectable ? (
                <th scope="col" className="w-7 px-3.5 py-2.5">
                  <button type="button" role="checkbox" aria-checked={sel.size === 0 ? false : sel.size === rows.length ? true : "mixed"} aria-label={t.table.selectAll} onClick={toggleAll} className="bg-transparent">
                    {box(sel.size > 0 && sel.size === rows.length)}
                  </button>
                </th>
              ) : null}
              <th scope="col" className="px-2.5 py-2.5 text-start font-semibold">{t.table.refOwner}</th>
              <th scope="col" className="px-2.5 py-2.5 text-start font-semibold">{t.table.status}</th>
              <th scope="col" className="px-2.5 py-2.5 text-start font-semibold">{t.table.nextAction}</th>
              <th scope="col" className="px-2.5 py-2.5 text-start font-semibold">{t.table.deadline}</th>
              <th scope="col" className="px-2.5 py-2.5 text-end font-semibold">{t.table.outstanding}</th>
              <th scope="col" className="px-2.5 py-2.5 text-start font-semibold">{t.table.assignee}</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row, r) => {
              const isSel = sel.has(row.id);
              return (
                <tr
                  key={row.id}
                  aria-selected={selectable ? isSel : undefined}
                  onClick={(e) => {
                    if ((e.target as HTMLElement).closest("[data-row-select]")) return;
                    router.push(row.href);
                  }}
                  className={cn("cursor-pointer border-t border-divider", isSel ? "bar-start bg-rust-50" : "bg-white hover:bg-warm")}
                >
                  {selectable ? (
                    <td {...cellProps(r, 0)} className="px-3.5 py-3 align-top outline-offset-[-2px]" data-row-select="">
                      <button type="button" role="checkbox" tabIndex={-1} aria-checked={isSel} aria-label={`${t.table.selectRow} ${row.ref}`} onClick={() => toggle(row.id)} className="bg-transparent">
                        {box(isSel)}
                      </button>
                    </td>
                  ) : null}
                  <td {...cellProps(r, 1)}>
                    <Link href={row.href} tabIndex={-1} className="font-mono text-13 font-semibold text-rust no-underline hover:underline">
                      <bdi dir="ltr">{row.ref}</bdi>
                    </Link>
                    <div className="text-13 text-muted">{row.owner}</div>
                  </td>
                  <td {...cellProps(r, 2)}>
                    <StatusChip status={row.status} size="sm" />
                  </td>
                  <td {...cellProps(r, 3)}>{row.nextAction}</td>
                  <td {...cellProps(r, 4)}>
                    <SlaBadge tone={row.sla.tone} plain size="sm">
                      {row.sla.text}
                    </SlaBadge>
                  </td>
                  <td {...cellProps(r, 5)} className="px-2.5 py-3 text-end align-top tabular-nums outline-offset-[-2px]">
                    <bdi dir="ltr">{formatMoney(row.outstanding, { locale, numerals })}</bdi>
                  </td>
                  <td {...cellProps(r, 6)}>{row.assignee}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
        <p className="m-0 border-t border-divider px-3.5 py-2 text-12 text-muted">{t.table.gridHint}</p>
      </div>

      {/* <768: cards */}
      <ul aria-label={label} className="m-0 flex list-none flex-col gap-2.5 p-0 md:hidden">
        {rows.map((row) => (
          <li key={row.id}>
            <CaseCard row={row} selectable={selectable} selected={sel.has(row.id)} onToggle={() => toggle(row.id)} />
          </li>
        ))}
      </ul>
    </div>
  );
}

/** Mobile case card: whole card opens the case; selection is a separate control. */
export function CaseCard({ row, selectable, selected, onToggle }: { row: CaseRowData; selectable?: boolean; selected?: boolean; onToggle?: () => void }) {
  const { t, locale, numerals } = useI18n();
  return (
    <article className={cn("relative flex flex-col gap-2.5 rounded-md border bg-white px-4 py-3.5", selected ? "bar-start border-line bg-rust-50" : "border-line")}>
      <div className="flex items-center justify-between gap-2">
        <Link href={row.href} className="font-mono text-13 font-semibold text-ink no-underline after:absolute after:inset-0 after:content-['']">
          <bdi dir="ltr">{row.ref}</bdi>
        </Link>
        <StatusChip status={row.status} size="sm" />
      </div>
      <div className="text-16 font-semibold">{row.nextAction}</div>
      <div className="flex justify-between gap-2 text-13 text-muted">
        <span>
          {row.owner} · {row.assignee}
        </span>
        <SlaBadge tone={row.sla.tone} plain size="sm">
          {row.sla.text}
        </SlaBadge>
      </div>
      <div className="flex items-center justify-between border-t border-divider pt-2 text-13 text-muted">
        <span>
          {t.table.outstandingShort}{" "}
          <bdi dir="ltr" className="font-semibold text-ink tabular-nums">
            {formatMoney(row.outstanding, { locale, numerals })}
          </bdi>{" "}
          {t.common.unitSar}
        </span>
        {selectable ? (
          <button
            type="button"
            role="checkbox"
            aria-checked={Boolean(selected)}
            aria-label={`${t.table.selectRow} ${row.ref}`}
            onClick={onToggle}
            className="relative z-10 -m-3 flex size-11 items-center justify-center bg-transparent"
          >
            <span className={cn("flex size-5 items-center justify-center rounded-xs", selected ? "bg-ink text-white" : "border border-line-strong bg-white")}>
              {selected ? <Icon name="check" size={16} /> : null}
            </span>
          </button>
        ) : null}
      </div>
    </article>
  );
}
