/** Joins truthy class names. Components keep variant classes internal; `className` is for layout only. */
export function cn(...parts: Array<string | false | null | undefined | 0>): string {
  return parts.filter(Boolean).join(" ");
}
