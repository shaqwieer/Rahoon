"use client";

import { useEffect, useId, useRef, useState, type KeyboardEvent } from "react";
import { cn } from "@/lib/cn";
import { Icon } from "./Icon";
import { usePopover } from "./usePopover";

export const MONTHS_AR = ["يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو", "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر"];
const WEEKDAYS = ["ح", "ن", "ث", "ر", "خ", "ج", "س"]; // Sunday first (Saudi working week)
const WEEKDAY_NAMES = ["الأحد", "الاثنين", "الثلاثاء", "الأربعاء", "الخميس", "الجمعة", "السبت"];

const pad = (n: number) => String(n).padStart(2, "0");
const iso = (y: number, m: number, d?: number) => (d === undefined ? `${y}-${pad(m + 1)}` : `${y}-${pad(m + 1)}-${pad(d)}`);
const daysIn = (y: number, m: number) => new Date(Date.UTC(y, m + 1, 0)).getUTCDate();

function parse(v: string | undefined | null): { y: number; m: number; d: number } | null {
  const r = /^(\d{4})-(\d{2})(?:-(\d{2}))?$/.exec(v ?? "");
  return r ? { y: Number(r[1]), m: Number(r[2]) - 1, d: r[3] ? Number(r[3]) : 1 } : null;
}

/** «15 يناير 2027» / «يناير 2027» (Gregorian, Latin digits like the rest of the figures). */
export function formatPickerValue(v: string, mode: "date" | "month") {
  const p = parse(v);
  if (!p) return "";
  return mode === "month" ? `${MONTHS_AR[p.m]} ${p.y}` : `${p.d} ${MONTHS_AR[p.m]} ${p.y}`;
}

function todayParts() {
  const t = new Date();
  return { y: t.getFullYear(), m: t.getMonth(), d: t.getDate() };
}

export interface DatePickerProps {
  /** "YYYY-MM-DD" (date) or "YYYY-MM" (month); "" = empty. */
  value: string;
  onChange: (value: string) => void;
  mode?: "date" | "month";
  id?: string;
  ariaLabel?: string;
  describedBy?: string;
  placeholder?: string;
  /** Inclusive bounds in the same format as the value. */
  min?: string;
  max?: string;
  disabled?: boolean;
  invalid?: boolean;
  size?: "sm" | "md" | "lg";
  className?: string;
  clearable?: boolean;
}

const SIZE = { sm: "min-h-10 text-14", md: "min-h-11 text-15", lg: "min-h-12 text-16" };

/**
 * The system calendar (replaces native date/month inputs): a field showing «15 يناير 2027», and a panel with a day grid
 * (Sunday first), a month grid and a year grid. Arrow keys move the day, PageUp/PageDown the month, Enter picks, Escape closes.
 */
export function DatePicker({ value, onChange, mode = "date", id: idProp, ariaLabel, describedBy, placeholder, min, max, disabled, invalid, size = "md", className, clearable = true }: DatePickerProps) {
  const autoId = useId();
  const id = idProp ?? autoId;
  const { open, setOpen, close, rootRef, triggerRef, panelStyle } = usePopover<HTMLButtonElement>(380);
  const sel = parse(value);
  const today = todayParts();
  const [view, setView] = useState<"days" | "months" | "years">(mode === "month" ? "months" : "days");
  const [cursor, setCursor] = useState(() => sel ?? today);
  const gridRef = useRef<HTMLDivElement>(null);

  const minP = parse(min);
  const maxP = parse(max);
  const outOfRange = (y: number, m: number, d?: number) => {
    const key = d === undefined ? y * 100 + m : (y * 100 + m) * 100 + d;
    const lo = minP ? (d === undefined ? minP.y * 100 + minP.m : (minP.y * 100 + minP.m) * 100 + (min && min.length > 7 ? minP.d : 1)) : null;
    const hi = maxP ? (d === undefined ? maxP.y * 100 + maxP.m : (maxP.y * 100 + maxP.m) * 100 + (max && max.length > 7 ? maxP.d : 31)) : null;
    return (lo !== null && key < lo) || (hi !== null && key > hi);
  };

  const openPicker = () => {
    if (disabled) return;
    setCursor(sel ?? today);
    setView(mode === "month" ? "months" : "days");
    setOpen(true);
  };

  useEffect(() => {
    if (!open) return;
    gridRef.current?.querySelector<HTMLElement>('[data-focus="true"]')?.focus();
  }, [open, view, cursor]);

  const pick = (v: string) => {
    onChange(v);
    close();
  };

  const shiftMonth = (delta: number) => {
    setCursor((c) => {
      const t = new Date(Date.UTC(c.y, c.m + delta, 1));
      return { y: t.getUTCFullYear(), m: t.getUTCMonth(), d: Math.min(c.d, daysIn(t.getUTCFullYear(), t.getUTCMonth())) };
    });
  };
  const shiftDay = (delta: number) => {
    setCursor((c) => {
      const t = new Date(Date.UTC(c.y, c.m, c.d + delta));
      return { y: t.getUTCFullYear(), m: t.getUTCMonth(), d: t.getUTCDate() };
    });
  };

  const onDaysKey = (e: KeyboardEvent) => {
    const map: Record<string, () => void> = {
      // RTL: the visual left is the next day.
      ArrowLeft: () => shiftDay(1), ArrowRight: () => shiftDay(-1), ArrowDown: () => shiftDay(7), ArrowUp: () => shiftDay(-7),
      PageDown: () => shiftMonth(1), PageUp: () => shiftMonth(-1),
      Enter: () => !outOfRange(cursor.y, cursor.m, cursor.d) && pick(iso(cursor.y, cursor.m, cursor.d)),
      " ": () => !outOfRange(cursor.y, cursor.m, cursor.d) && pick(iso(cursor.y, cursor.m, cursor.d)),
    };
    if (map[e.key]) {
      e.preventDefault();
      map[e.key]();
    }
  };
  const onMonthsKey = (e: KeyboardEvent) => {
    const step: Record<string, number> = { ArrowLeft: 1, ArrowRight: -1, ArrowDown: 3, ArrowUp: -3 };
    if (step[e.key] !== undefined) {
      e.preventDefault();
      shiftMonth(step[e.key]);
    } else if (e.key === "Enter" || e.key === " ") {
      e.preventDefault();
      chooseMonth(cursor.m);
    }
  };

  const chooseMonth = (m: number) => {
    if (outOfRange(cursor.y, m)) return;
    if (mode === "month") pick(iso(cursor.y, m));
    else {
      setCursor((c) => ({ y: c.y, m, d: Math.min(c.d, daysIn(c.y, m)) }));
      setView("days");
    }
  };

  const display = value ? formatPickerValue(value, mode) : "";
  const firstDow = new Date(Date.UTC(cursor.y, cursor.m, 1)).getUTCDay();
  const nDays = daysIn(cursor.y, cursor.m);
  const yearStart = cursor.y - (cursor.y % 12);

  const navBtn = "inline-flex size-9 items-center justify-center rounded-sm text-charcoal hover:bg-subtle";
  return (
    <div ref={rootRef} className={cn("relative", className)}>
      <button
        ref={triggerRef}
        id={id}
        type="button"
        disabled={disabled}
        aria-haspopup="dialog"
        aria-expanded={open}
        aria-label={ariaLabel ? `${ariaLabel}: ${display || "غير محدد"}` : undefined}
        aria-describedby={describedBy}
        data-invalid={invalid || undefined}
        onClick={() => (open ? close(false) : openPicker())}
        className={cn(
          "flex w-full items-center gap-2 rounded-sm border bg-white px-3 text-start transition-colors",
          SIZE[size],
          open ? "border-rust ring-2 ring-rust-50" : invalid ? "border-err" : "border-line-strong hover:border-ink",
          disabled && "cursor-not-allowed bg-subtle text-soft",
        )}
      >
        <Icon name={mode === "month" ? "calendar_view_month" : "calendar_month"} size={18} className="flex-none text-muted" />
        <span className={cn("min-w-0 flex-1 truncate", !display && "text-muted")}>{display || placeholder || (mode === "month" ? "اختر الشهر" : "اختر التاريخ")}</span>
        <Icon name="expand_more" size={20} className={cn("flex-none text-muted transition-transform", open && "rotate-180")} />
      </button>
      {open ? (
        <div role="dialog" aria-label={mode === "month" ? "اختر الشهر" : "اختر التاريخ"} style={panelStyle({ width: 304 })}
          className="z-[70] overflow-y-auto rounded-md border border-line bg-white p-3 shadow-3">
          <div className="mb-2 flex items-center justify-between gap-1">
            <button type="button" className={navBtn} aria-label={view === "days" ? "الشهر السابق" : view === "months" ? "السنة السابقة" : "السنوات السابقة"}
              onClick={() => (view === "days" ? shiftMonth(-1) : setCursor((c) => ({ ...c, y: c.y - (view === "months" ? 1 : 12) })))}>
              <Icon name="chevron_right" size={20} />
            </button>
            <button type="button" className="inline-flex min-h-9 items-center gap-1 rounded-sm px-3 text-15 font-bold hover:bg-subtle"
              aria-label="تغيير الشهر أو السنة"
              onClick={() => setView(view === "days" ? "months" : view === "months" ? "years" : mode === "month" ? "months" : "days")}>
              {view === "days" ? `${MONTHS_AR[cursor.m]} ${cursor.y}` : view === "months" ? cursor.y : `${yearStart} – ${yearStart + 11}`}
              <Icon name="unfold_more" size={16} className="text-muted" />
            </button>
            <button type="button" className={navBtn} aria-label={view === "days" ? "الشهر التالي" : view === "months" ? "السنة التالية" : "السنوات التالية"}
              onClick={() => (view === "days" ? shiftMonth(1) : setCursor((c) => ({ ...c, y: c.y + (view === "months" ? 1 : 12) })))}>
              <Icon name="chevron_left" size={20} />
            </button>
          </div>

          <div ref={gridRef}>
            {view === "days" ? (
              <div role="grid" aria-label={`${MONTHS_AR[cursor.m]} ${cursor.y}`} onKeyDown={onDaysKey}>
                <div role="row" className="mb-1 grid grid-cols-7 text-center text-12 font-semibold text-muted">
                  {WEEKDAYS.map((w, i) => <span key={w} role="columnheader" aria-label={WEEKDAY_NAMES[i]} className="py-1">{w}</span>)}
                </div>
                <div role="row" className="grid grid-cols-7 gap-0.5">
                  {Array.from({ length: firstDow }, (_, i) => <span key={`b${i}`} />)}
                  {Array.from({ length: nDays }, (_, i) => {
                    const d = i + 1;
                    const isSel = sel?.y === cursor.y && sel?.m === cursor.m && sel?.d === d && value.length > 7;
                    const isToday = today.y === cursor.y && today.m === cursor.m && today.d === d;
                    const off = outOfRange(cursor.y, cursor.m, d);
                    const focus = cursor.d === d;
                    return (
                      <button key={d} type="button" role="gridcell" aria-selected={isSel} disabled={off}
                        tabIndex={focus ? 0 : -1} data-focus={focus}
                        aria-label={`${d} ${MONTHS_AR[cursor.m]} ${cursor.y}`}
                        onClick={() => pick(iso(cursor.y, cursor.m, d))}
                        className={cn("flex h-9 items-center justify-center rounded-sm text-14 tabular-nums outline-none focus-visible:ring-2 focus-visible:ring-ink",
                          isSel ? "bg-rust font-bold text-white" : isToday ? "border border-rust text-rust-700" : "hover:bg-rust-50",
                          off && "cursor-not-allowed text-soft opacity-50 hover:bg-transparent")}>
                        {d}
                      </button>
                    );
                  })}
                </div>
              </div>
            ) : view === "months" ? (
              <div role="grid" aria-label={String(cursor.y)} className="grid grid-cols-3 gap-1.5" onKeyDown={onMonthsKey}>
                {MONTHS_AR.map((name, m) => {
                  const isSel = sel?.y === cursor.y && sel?.m === m;
                  const off = outOfRange(cursor.y, m);
                  const focus = cursor.m === m;
                  return (
                    <button key={name} type="button" role="gridcell" aria-selected={isSel} disabled={off} tabIndex={focus ? 0 : -1} data-focus={focus}
                      onClick={() => chooseMonth(m)}
                      className={cn("min-h-10 rounded-sm text-14 outline-none focus-visible:ring-2 focus-visible:ring-ink",
                        isSel ? "bg-rust font-bold text-white" : "hover:bg-rust-50", off && "cursor-not-allowed opacity-40")}>
                      {name}
                    </button>
                  );
                })}
              </div>
            ) : (
              <div role="grid" className="grid grid-cols-3 gap-1.5">
                {Array.from({ length: 12 }, (_, i) => yearStart + i).map((y) => (
                  <button key={y} type="button" role="gridcell" aria-selected={sel?.y === y} tabIndex={y === cursor.y ? 0 : -1} data-focus={y === cursor.y}
                    onClick={() => {
                      setCursor((c) => ({ ...c, y }));
                      setView("months");
                    }}
                    className={cn("min-h-10 rounded-sm text-14 tabular-nums outline-none focus-visible:ring-2 focus-visible:ring-ink",
                      sel?.y === y ? "bg-rust font-bold text-white" : y === today.y ? "border border-rust" : "hover:bg-rust-50")}>
                    {y}
                  </button>
                ))}
              </div>
            )}
          </div>

          <div className="mt-2 flex items-center justify-between border-t border-divider pt-2">
            <button type="button" className="min-h-9 rounded-sm px-2 text-13 font-semibold text-rust hover:bg-rust-50"
              onClick={() => (mode === "month" ? pick(iso(today.y, today.m)) : pick(iso(today.y, today.m, today.d)))}>
              {mode === "month" ? "هذا الشهر" : "اليوم"}
            </button>
            {clearable && value ? (
              <button type="button" className="min-h-9 rounded-sm px-2 text-13 font-semibold text-muted hover:bg-subtle" onClick={() => pick("")}>
                مسح
              </button>
            ) : null}
          </div>
        </div>
      ) : null}
    </div>
  );
}
