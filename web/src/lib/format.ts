/**
 * Pure formatting helpers (no React, safe on server and client).
 * Conventions from 09 Handoff: Western digits by default (`ar-SA-u-nu-latn`), amounts with 2 decimals
 * and «ر.س», Gregorian ISO dates as the system record, Hijri (Umm al-Qura) for display, Asia/Riyadh time.
 * Wrap every returned value in <bdi dir="ltr"> when rendering (see Money / DateText / Ref helpers).
 */

export type Numerals = "latn" | "arab";
export type FormatLocale = "ar" | "en";

export interface FormatOptions {
  locale?: FormatLocale;
  /** User preference from /api/auth/me (`numerals`); Western digits unless `arab`. */
  numerals?: Numerals;
}

export const TIME_ZONE = "Asia/Riyadh";
export const CURRENCY_UNIT: Record<FormatLocale, string> = { ar: "ر.س", en: "SAR" };

// Bidi control characters ICU sometimes inserts (LRM, RLM, ALM, LRI…PDI).
const BIDI_MARKS = /[؜‎‏‪-‮⁦-⁩]/g;
const ARABIC_DIGITS = ["٠", "١", "٢", "٣", "٤", "٥", "٦", "٧", "٨", "٩"];

const clean = (s: string) => s.replace(BIDI_MARKS, "");

/** Converts Western digits (and , .) to Arabic-Indic when the user chose `arab` numerals. */
export function applyNumerals(s: string, numerals: Numerals = "latn"): string {
  if (numerals !== "arab") return s;
  return s.replace(/[0-9]/g, (d) => ARABIC_DIGITS[Number(d)]).replace(/,/g, "٬").replace(/\./g, "٫");
}

function toNumber(value: number | string | null | undefined): number | null {
  if (value === null || value === undefined || value === "") return null;
  const n = typeof value === "number" ? value : Number(value);
  return Number.isFinite(n) ? n : null;
}

function toDate(value: Date | string | number | null | undefined): Date | null {
  if (value === null || value === undefined || value === "") return null;
  // A bare `YYYY-MM-DD` is a calendar date: pin it to Riyadh noon so no timezone shifts the day.
  if (typeof value === "string" && /^\d{4}-\d{2}-\d{2}$/.test(value)) return new Date(`${value}T12:00:00+03:00`);
  const d = value instanceof Date ? value : new Date(value);
  return Number.isNaN(d.getTime()) ? null : d;
}

const numberFormatters = new Map<string, Intl.NumberFormat>();
function nf(key: string, locale: string, opts: Intl.NumberFormatOptions) {
  const k = `${locale}|${key}`;
  let f = numberFormatters.get(k);
  if (!f) {
    f = new Intl.NumberFormat(locale, opts);
    numberFormatters.set(k, f);
  }
  return f;
}

/** Plain grouped number with fixed decimals: 1284560 → "1,284,560.00" (digits only, no unit). */
export function formatNumber(value: number | string | null | undefined, fractionDigits = 0, opts: FormatOptions = {}): string {
  const n = toNumber(value);
  if (n === null) return "—";
  const s = nf(`n${fractionDigits}`, "en-US", { minimumFractionDigits: fractionDigits, maximumFractionDigits: fractionDigits }).format(n);
  return applyNumerals(clean(s), opts.numerals);
}

export interface MoneyOptions extends FormatOptions {
  /** Append «ر.س» (ar) / prefix "SAR" (en). Default false — the Money component renders the unit outside the LTR number. */
  withUnit?: boolean;
  /** Chart / KPI form, rounded and labelled: 1.62 مليار ر.س. */
  compact?: boolean;
}

/** 1284560 → "1,284,560.00" (+ «ر.س» when `withUnit`). Compact: "1.62 مليار ر.س". */
export function formatMoney(value: number | string | null | undefined, opts: MoneyOptions = {}): string {
  const n = toNumber(value);
  if (n === null) return "—";
  const locale = opts.locale ?? "ar";
  const body = opts.compact ? formatCompact(n, opts) : formatNumber(n, 2, opts);
  if (!opts.withUnit) return body;
  return locale === "en" ? `${CURRENCY_UNIT.en} ${body}` : `${body} ${CURRENCY_UNIT.ar}`;
}

/** Rounded compact number for charts: 1620000000 → "1.62 مليار" / "1.62B". */
export function formatCompact(value: number | string | null | undefined, opts: FormatOptions = {}): string {
  const n = toNumber(value);
  if (n === null) return "—";
  const locale = opts.locale === "en" ? "en-US" : "ar-SA-u-nu-latn";
  const s = nf("compact", locale, { notation: "compact", maximumFractionDigits: 2 }).format(n);
  return applyNumerals(clean(s), opts.numerals);
}

/** Percent from a value already in percent units: 46.5 → "46.5%". Use `fromFraction` for 0.465. */
export function formatPercent(
  value: number | string | null | undefined,
  opts: FormatOptions & { fractionDigits?: number; fromFraction?: boolean } = {},
): string {
  const n = toNumber(value);
  if (n === null) return "—";
  const pct = opts.fromFraction ? n * 100 : n;
  const digits = opts.fractionDigits ?? (Number.isInteger(pct) ? 0 : Math.min(2, (String(pct).split(".")[1] ?? "").length));
  return `${formatNumber(pct, digits, opts)}%`;
}

const gregorianParts = new Intl.DateTimeFormat("en-CA-u-ca-gregory-nu-latn", {
  year: "numeric",
  month: "2-digit",
  day: "2-digit",
  hour: "2-digit",
  minute: "2-digit",
  hourCycle: "h23",
  timeZone: TIME_ZONE,
});

function riyadhParts(d: Date) {
  const parts = Object.fromEntries(gregorianParts.formatToParts(d).map((p) => [p.type, p.value]));
  return { y: parts.year, m: parts.month, d: parts.day, hh: parts.hour, mm: parts.minute };
}

/** ISO calendar date in Riyadh: "2026-09-23". */
export function formatDate(value: Date | string | number | null | undefined, opts: FormatOptions = {}): string {
  const d = toDate(value);
  if (!d) return "—";
  const p = riyadhParts(d);
  return applyNumerals(`${p.y}-${p.m}-${p.d}`, opts.numerals);
}

/** "2026-09-23 10:12" (24h, Asia/Riyadh). */
export function formatDateTime(value: Date | string | number | null | undefined, opts: FormatOptions = {}): string {
  const d = toDate(value);
  if (!d) return "—";
  const p = riyadhParts(d);
  return applyNumerals(`${p.y}-${p.m}-${p.d} ${p.hh}:${p.mm}`, opts.numerals);
}

/** "10:12" (24h, Asia/Riyadh). */
export function formatTime(value: Date | string | number | null | undefined, opts: FormatOptions = {}): string {
  const d = toDate(value);
  if (!d) return "—";
  const p = riyadhParts(d);
  return applyNumerals(`${p.hh}:${p.mm}`, opts.numerals);
}

const hijriAr = new Intl.DateTimeFormat("ar-SA-u-ca-islamic-umalqura-nu-latn", {
  day: "numeric",
  month: "long",
  year: "numeric",
  timeZone: TIME_ZONE,
});
const hijriEn = new Intl.DateTimeFormat("en-US-u-ca-islamic-umalqura-nu-latn", {
  day: "numeric",
  month: "long",
  year: "numeric",
  timeZone: TIME_ZONE,
});

/** Umm al-Qura Hijri display date: "11 ربيع الآخر 1448هـ" (`withSuffix:false` drops «هـ»). */
export function formatHijri(
  value: Date | string | number | null | undefined,
  opts: FormatOptions & { withSuffix?: boolean } = {},
): string {
  const d = toDate(value);
  if (!d) return "—";
  const withSuffix = opts.withSuffix ?? true;
  if (opts.locale === "en") {
    const parts = hijriEn.formatToParts(d).filter((p) => p.type !== "era" && p.type !== "literal");
    const get = (t: string) => parts.find((p) => p.type === t)?.value ?? "";
    return `${get("day")} ${get("month")} ${get("year")}${withSuffix ? " AH" : ""}`;
  }
  const parts = hijriAr.formatToParts(d);
  const get = (t: string) => clean(parts.find((p) => p.type === t)?.value ?? "");
  return applyNumerals(`${get("day")} ${get("month")} ${get("year")}${withSuffix ? "هـ" : ""}`, opts.numerals);
}

/** Whole days between two instants in Riyadh calendar terms (b − a). */
export function daysBetween(a: Date | string | number, b: Date | string | number): number {
  const da = toDate(a);
  const db = toDate(b);
  if (!da || !db) return 0;
  const toDay = (d: Date) => {
    const p = riyadhParts(d);
    return Date.UTC(Number(p.y), Number(p.m) - 1, Number(p.d));
  };
  return Math.round((toDay(db) - toDay(da)) / 86_400_000);
}

/** Arabic counted noun for days: 1 يوم واحد · 2 يومان · 3–10 أيام · 11+ يوماً. */
export function daysText(n: number, opts: FormatOptions = {}): string {
  const abs = Math.abs(Math.trunc(n));
  if (opts.locale === "en") return abs === 1 ? "1 day" : `${applyNumerals(String(abs), opts.numerals)} days`;
  const num = applyNumerals(String(abs), opts.numerals);
  if (abs === 0) return "اليوم";
  if (abs === 1) return "يوم واحد";
  if (abs === 2) return "يومان";
  if (abs <= 10) return `${num} أيام`;
  return `${num} يوماً`;
}

export type SlaTone = "ok" | "warn" | "err" | "info" | "paused";

export interface SlaInput extends FormatOptions {
  dueAt?: Date | string | null;
  now?: Date | string;
  paused?: boolean;
  /** e.g. «بانتظار طرف خارجي». */
  pausedReason?: string;
  /** Remaining days at or below which the tone becomes a warning (default 2). */
  warnWithinDays?: number;
  /** Append the ISO due date in the warning state («متبقٍ يومان · 2026-09-25»). */
  showDate?: boolean;
}

/**
 * SLA badge text + tone (C02): «ضمن المهلة · 6 أيام», «متبقٍ يومان · 2026-09-25», «متأخر 3 أيام»,
 * «المهلة موقوفة — بانتظار طرف خارجي». Pass `now` from the server for deterministic output.
 */
export function slaText(input: SlaInput): { tone: SlaTone; text: string } {
  const en = input.locale === "en";
  if (input.paused) {
    const reason = input.pausedReason;
    return {
      tone: "paused",
      text: en ? `SLA paused${reason ? ` — ${reason}` : ""}` : `المهلة موقوفة${reason ? ` — ${reason}` : ""}`,
    };
  }
  if (!input.dueAt) return { tone: "info", text: en ? "No deadline" : "بلا مهلة" };
  const left = daysBetween(input.now ?? new Date(), input.dueAt);
  const warnWithin = input.warnWithinDays ?? 2;
  if (left < 0) return { tone: "err", text: en ? `Overdue ${daysText(-left, input)}` : `متأخر ${daysText(-left, input)}` };
  const date = formatDate(input.dueAt, input);
  if (left <= warnWithin) {
    const base = left === 0 ? (en ? "Due today" : "تنتهي اليوم") : en ? `${daysText(left, input)} left` : `متبقٍ ${daysText(left, input)}`;
    return { tone: "warn", text: input.showDate === false ? base : `${base} · ${date}` };
  }
  return { tone: "ok", text: en ? `Within SLA · ${daysText(left, input)}` : `ضمن المهلة · ${daysText(left, input)}` };
}

/** Countdown "mm:ss" for OTP resend timers. */
export function formatCountdown(totalSeconds: number): string {
  const s = Math.max(0, Math.trunc(totalSeconds));
  return `${String(Math.floor(s / 60)).padStart(2, "0")}:${String(s % 60).padStart(2, "0")}`;
}
