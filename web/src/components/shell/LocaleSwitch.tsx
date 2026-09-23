"use client";

import { useRouter } from "next/navigation";
import { useTransition } from "react";
import { cn } from "@/lib/cn";
import { LOCALE_COOKIE, type Locale } from "@/lib/i18n";
import { useI18n } from "@/lib/i18n/client";

/** Persists the UI language for a year and re-renders server components in the new locale. */
export function useSetLocale() {
  const router = useRouter();
  const [pending, startTransition] = useTransition();
  const setLocale = (next: Locale) => {
    document.cookie = `${LOCALE_COOKIE}=${next}; path=/; max-age=31536000; samesite=lax`;
    startTransition(() => router.refresh());
  };
  return { setLocale, pending };
}

export interface LocaleSwitchProps {
  /** `button` = 40px «EN» / «ع» toggle (topbar); `link` = text link «English» / «العربية» (footers, login). */
  variant?: "button" | "link" | "inverse";
  className?: string;
}

export function LocaleSwitch({ variant = "button", className }: LocaleSwitchProps) {
  const { locale, t } = useI18n();
  const { setLocale, pending } = useSetLocale();
  const next: Locale = locale === "ar" ? "en" : "ar";
  const nextLang = next;

  if (variant === "link" || variant === "inverse") {
    return (
      <button
        type="button"
        lang={nextLang}
        aria-busy={pending || undefined}
        onClick={() => setLocale(next)}
        className={cn(
          "inline-flex min-h-11 items-center bg-transparent px-2 text-14 font-semibold",
          next === "en" ? "font-latin" : "font-ar",
          variant === "inverse" ? "text-white underline-offset-[3px] hover:underline" : "text-ink hover:underline underline-offset-[3px]",
          className,
        )}
      >
        {next === "en" ? t.locale.switchToEnglish : t.locale.switchToArabic}
      </button>
    );
  }
  return (
    <button
      type="button"
      lang={nextLang}
      aria-label={t.locale.toggleAria}
      aria-busy={pending || undefined}
      onClick={() => setLocale(next)}
      className={cn(
        "inline-flex h-10 min-w-10 flex-none items-center justify-center rounded-sm border border-line bg-white px-2 text-13 font-semibold text-ink hover:bg-subtle",
        next === "en" ? "font-latin" : "font-ar",
        className,
      )}
    >
      {t.locale.toggleShort}
    </button>
  );
}
