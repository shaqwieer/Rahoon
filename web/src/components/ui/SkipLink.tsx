"use client";

import { useI18n } from "@/lib/i18n/client";

/** First focusable element on every page: «تخطٍ إلى المحتوى» → #main. */
export function SkipLink({ target = "main" }: { target?: string }) {
  const { t } = useI18n();
  return (
    <a
      href={`#${target}`}
      className="sr-only-focusable fixed start-3 top-3 z-[60] rounded-sm bg-ink px-4 py-2.5 text-14 font-semibold text-white no-underline shadow-3 hover:text-white"
      onClick={(e) => {
        const el = document.getElementById(target);
        if (el) {
          e.preventDefault();
          el.setAttribute("tabindex", "-1");
          el.focus();
        }
      }}
    >
      {t.common.skipToContent}
    </a>
  );
}
