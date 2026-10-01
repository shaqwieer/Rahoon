import { applyNumerals, formatDate, formatDateTime } from "@/lib/format";

/** Whole riyals when the amount is whole, else 2 decimals: 1005000 → "1,005,000". Null → null (never "0"). */
export function sar(value: number | null | undefined): string | null {
  if (value === null || value === undefined || !Number.isFinite(value)) return null;
  const whole = Number.isInteger(value);
  return applyNumerals(
    new Intl.NumberFormat("en-US", { minimumFractionDigits: whole ? 0 : 2, maximumFractionDigits: whole ? 0 : 2 }).format(value),
    "latn",
  );
}

/** Short form for cards: 1,005,000 → "1.01 مليون". */
export function sarShort(value: number | null | undefined): string | null {
  if (value === null || value === undefined || !Number.isFinite(value)) return null;
  if (Math.abs(value) >= 1_000_000) return `${(value / 1_000_000).toFixed(2).replace(/\.?0+$/, "")} مليون`;
  if (Math.abs(value) >= 1_000) return `${Math.round(value / 1_000)} ألف`;
  return sar(value);
}

const MONTHS = ["يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو", "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر"];

/** "2027-06" → "يونيو 2027". */
export function monthLabel(ym: string | null | undefined): string | null {
  if (!ym) return null;
  const m = /^(\d{4})-(\d{2})$/.exec(ym);
  if (!m) return ym;
  return `${MONTHS[Number(m[2]) - 1]} ${m[1]}`;
}

/** Riyadh calendar date "2026-09-23". */
export const day = (v: string | null | undefined) => (v ? formatDate(v) : "—");
/** Riyadh date and time "2026-09-23 10:12". */
export const dayTime = (v: string | null | undefined) => (v ? formatDateTime(v) : "—");

export const FREQ_LABEL: Record<string, string> = { monthly: "شهري", quarterly: "ربع سنوي", semiannual: "نصف سنوي", annual: "سنوي" };
export const FREQ_PER: Record<string, string> = { monthly: "شهريًا", quarterly: "كل 3 أشهر", semiannual: "كل 6 أشهر", annual: "سنويًا" };
