"use client";

import { cn } from "@/lib/cn";
import { toLatinDigits } from "@/lib/market/numbers";

const clean = (v: string) => toLatinDigits(v).replace(/[^\d.]/g, "");

/**
 * A number with its unit (ر.س, م², شهر) in its own end segment, so the unit never sits on top of the placeholder or the value.
 * Arabic-Indic digits are accepted and stored as Latin digits.
 */
export function UnitInput({ id, value, onChange, unit = "ر.س", placeholder, ariaLabel, className, size = "md", invalid, describedBy }: {
  id?: string;
  value: string;
  onChange: (v: string) => void;
  unit?: string;
  placeholder?: string;
  ariaLabel?: string;
  className?: string;
  size?: "md" | "lg";
  invalid?: boolean;
  describedBy?: string;
}) {
  return (
    <div className={cn("flex items-stretch overflow-hidden rounded-sm border bg-white focus-within:border-rust focus-within:ring-2 focus-within:ring-rust-50",
      size === "lg" ? "min-h-12" : "min-h-11", invalid ? "border-err" : "border-line-strong", className)}>
      <input id={id} aria-label={ariaLabel} aria-invalid={invalid || undefined} aria-describedby={describedBy} inputMode="decimal" dir="ltr" value={value}
        placeholder={placeholder} onChange={(e) => onChange(clean(e.target.value))}
        className={cn("min-w-0 flex-1 bg-transparent px-3 text-end tabular-nums outline-none placeholder:text-13 placeholder:text-soft", size === "lg" ? "text-16" : "text-15")} />
      <span className="flex flex-none items-center border-s border-line bg-warm px-2.5 text-13 text-muted">{unit}</span>
    </div>
  );
}
