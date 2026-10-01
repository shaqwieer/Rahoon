/** The product is Arabic-only (Saudi market): right to left. */
export const locales = ["ar"] as const;
export type Locale = (typeof locales)[number];
export const defaultLocale: Locale = "ar";

export function isLocale(value: unknown): value is Locale {
  return typeof value === "string" && (locales as readonly string[]).includes(value);
}

/** Every page is right to left. */
export function dirFor(locale: Locale): "rtl" {
  void locale;
  return "rtl";
}
