"use client";

import { cn } from "@/lib/cn";
import { formatDate, formatDateTime, formatHijri, formatMoney, formatPercent } from "@/lib/format";
import { useI18n } from "@/lib/i18n/client";

/**
 * Bidi-safe value helpers. Numbers, references, dates, phones and emails never mirror:
 * each is isolated in <bdi dir="ltr"> while units / words stay in the reading direction.
 */

export interface MoneyProps {
  value: number | string | null | undefined;
  /** Show «ر.س» (outside the LTR number). Default true. */
  unit?: boolean;
  /** Rounded chart form: 1.62 مليار ر.س. */
  compact?: boolean;
  strong?: boolean;
  className?: string;
}

export function Money({ value, unit = true, compact, strong, className }: MoneyProps) {
  const { locale, numerals, t } = useI18n();
  const num = formatMoney(value, { locale, numerals, compact });
  const bdi = (
    <bdi dir={compact && locale === "ar" ? undefined : "ltr"} className={cn("tabular-nums", strong && "font-semibold")}>
      {num}
    </bdi>
  );
  if (!unit || value === null || value === undefined) return <span className={className}>{bdi}</span>;
  return (
    <span className={cn("whitespace-nowrap", className)}>
      {locale === "en" ? (
        <>
          {t.common.unitSar} {bdi}
        </>
      ) : (
        <>
          {bdi} {t.common.unitSar}
        </>
      )}
    </span>
  );
}

export interface DateTextProps {
  value: Date | string | number | null | undefined;
  /** `date` 2026-09-23 · `datetime` 2026-09-23 10:12 · `hijri` 11 ربيع الآخر 1448هـ · `both` Gregorian + Hijri. */
  mode?: "date" | "datetime" | "hijri" | "both";
  className?: string;
}

export function DateText({ value, mode = "date", className }: DateTextProps) {
  const { locale, numerals } = useI18n();
  const iso = typeof value === "string" ? value : value instanceof Date ? value.toISOString() : value === null || value === undefined ? undefined : new Date(value).toISOString();
  if (mode === "hijri") return <span className={className}>{formatHijri(value, { locale, numerals })}</span>;
  const g = mode === "datetime" ? formatDateTime(value, { numerals }) : formatDate(value, { numerals });
  const time = (
    <time dateTime={iso}>
      <bdi dir="ltr" className="tabular-nums">
        {g}
      </bdi>
    </time>
  );
  if (mode !== "both") return <span className={className}>{time}</span>;
  return (
    <span className={className}>
      {time} · {formatHijri(value, { locale, numerals })}
    </span>
  );
}

/** Case / contract / request references in IBM Plex Mono: RH-2026-004172. */
export function Ref({ children, className, strong = true }: { children: string; className?: string; strong?: boolean }) {
  return (
    <bdi dir="ltr" className={cn("font-mono", strong ? "font-semibold" : "font-medium", className)}>
      {children}
    </bdi>
  );
}

/** Any LTR value (email, phone, masked ID) isolated from the surrounding text. */
export function Ltr({ children, mono, className }: { children: string; mono?: boolean; className?: string }) {
  return (
    <bdi dir="ltr" className={cn(mono && "font-mono", className)}>
      {children}
    </bdi>
  );
}

export function Percent({ value, className }: { value: number | string | null | undefined; className?: string }) {
  const { numerals } = useI18n();
  return (
    <bdi dir="ltr" className={cn("tabular-nums", className)}>
      {formatPercent(value, { numerals })}
    </bdi>
  );
}
