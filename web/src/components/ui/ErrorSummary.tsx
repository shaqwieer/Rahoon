"use client";

import { useEffect, useId, useRef } from "react";
import { useI18n } from "@/lib/i18n/client";
import { Icon } from "./Icon";

export interface ErrorSummaryItem {
  /** id of the invalid control (the link target). */
  fieldId: string;
  message: string;
}

export interface ErrorSummaryProps {
  errors: ErrorSummaryItem[];
  /** Override the default «يوجد خطآن قبل المتابعة» heading. */
  title?: string;
  /** Changes whenever a submit fails, so focus moves to the summary again. */
  focusKey?: number | string;
}

/** Top-of-form error list (C06): role=alert, receives focus, each item links to its field. */
export function ErrorSummary({ errors, title, focusKey }: ErrorSummaryProps) {
  const { t } = useI18n();
  const ref = useRef<HTMLDivElement>(null);
  const titleId = useId();
  const count = errors.length;

  useEffect(() => {
    if (count > 0) ref.current?.focus();
  }, [count, focusKey]);

  if (count === 0) return null;
  return (
    <div
      ref={ref}
      role="alert"
      tabIndex={-1}
      aria-labelledby={titleId}
      className="flex flex-col gap-2 rounded-md border border-err-line bg-err-bg p-4"
    >
      <strong id={titleId} className="flex items-center gap-2 text-15 text-err">
        <Icon name="error" size={20} />
        {title ?? t.fields.errorSummary(count)}
      </strong>
      <ul className="m-0 ps-7 text-14 leading-6">
        {errors.map((e) => (
          <li key={e.fieldId}>
            <a
              href={`#${e.fieldId}`}
              onClick={(ev) => {
                const el = document.getElementById(e.fieldId);
                if (el) {
                  ev.preventDefault();
                  el.focus();
                  el.scrollIntoView({ block: "center" });
                }
              }}
            >
              {e.message}
            </a>
          </li>
        ))}
      </ul>
    </div>
  );
}
