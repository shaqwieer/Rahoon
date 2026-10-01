/**
 * Input normalisation shared by every market form. Arabic-Indic (٠-٩) and Eastern Arabic/Persian (۰-۹) digits are
 * accepted exactly like Latin digits; the Arabic decimal and thousands separators too. The server applies the same rules.
 */
export function toLatinDigits(input: string): string {
  return input
    .replace(/[٠-٩]/g, (d) => String(d.charCodeAt(0) - 0x0660))
    .replace(/[۰-۹]/g, (d) => String(d.charCodeAt(0) - 0x06f0))
    .replace(/٫/g, ".")
    .replace(/[٬،,]/g, "");
}

/** Parses a typed amount ("1,250,000", "١٢٥٠٠٠٠", "1250000.5"). Empty → null; anything else that isn't a number → NaN. */
export function parseAmount(input: string): number | null {
  const s = toLatinDigits(input).replace(/\s/g, "");
  if (s === "") return null;
  if (!/^\d+(\.\d{1,2})?$/.test(s)) return Number.NaN;
  return Number(s);
}

/** Saudi mobile in the 05XXXXXXXX form (also accepts +9665…, 009665…, 9665…). Returns null when invalid. */
export function normalizeSaudiMobile(input: string): string | null {
  let s = toLatinDigits(input).replace(/[\s\-()]/g, "");
  if (s.startsWith("+")) s = s.slice(1);
  if (s.startsWith("00")) s = s.slice(2);
  if (s.startsWith("966")) s = `0${s.slice(3)}`;
  if (s.startsWith("5") && s.length === 9) s = `0${s}`;
  return /^05\d{8}$/.test(s) ? s : null;
}
