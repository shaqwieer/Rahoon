"use client";

import { forwardRef, useId, useState, type ReactNode } from "react";
import { cn } from "@/lib/cn";
import { useI18n } from "@/lib/i18n/client";

export interface OtpInputProps {
  value: string;
  onChange: (value: string) => void;
  /** Fired once all digits are present (typed, pasted or autofilled). */
  onComplete?: (value: string) => void;
  length?: number;
  /** Visible label above the boxes (optional; the group always has an accessible name). */
  label?: ReactNode;
  error?: boolean;
  disabled?: boolean;
  autoFocus?: boolean;
  /** 56 (auth screens) or 52 (owner consent). */
  boxHeight?: 52 | 56;
  id?: string;
  /** id(s) of hint / error text. */
  describedBy?: string;
  name?: string;
}

/**
 * One logical input (`autocomplete="one-time-code"`, numeric keypad, LTR) drawn as N boxes.
 * The real input sits transparently over the boxes so typing, paste and SMS autofill all work.
 */
export const OtpInput = forwardRef<HTMLInputElement, OtpInputProps>(function OtpInput(
  { value, onChange, onComplete, length = 6, label, error, disabled, autoFocus, boxHeight = 56, id: idProp, describedBy, name },
  ref,
) {
  const autoId = useId();
  const id = idProp ?? autoId;
  const { t } = useI18n();
  const [focused, setFocused] = useState(false);
  const digits = value.slice(0, length).split("");
  const active = Math.min(digits.length, length - 1);

  return (
    <div className="flex flex-col gap-1.5">
      {label ? (
        <label htmlFor={id} className="text-16 font-semibold">
          {label}
        </label>
      ) : null}
      <div role="group" aria-label={t.fields.otpGroup} dir="ltr" className="relative">
        <div className="grid gap-2" style={{ gridTemplateColumns: `repeat(${length}, minmax(0, 1fr))` }} aria-hidden="true">
          {Array.from({ length }, (_, i) => {
            const isActive = focused && i === active && !error;
            return (
              <span
                key={i}
                className={cn(
                  "flex items-center justify-center rounded-[8px] bg-white font-mono font-semibold text-ink",
                  boxHeight === 56 ? "h-14 text-22" : "h-[52px] text-20",
                  error ? "border-2 border-err" : isActive ? "border-2 border-ink" : "border border-line-strong",
                  disabled && "bg-subtle text-muted",
                )}
              >
                {digits[i] ?? ""}
              </span>
            );
          })}
        </div>
        <input
          ref={ref}
          id={id}
          name={name}
          type="text"
          inputMode="numeric"
          autoComplete="one-time-code"
          pattern="[0-9]*"
          maxLength={length}
          dir="ltr"
          disabled={disabled}
          autoFocus={autoFocus}
          value={value}
          aria-label={label ? undefined : t.fields.otpGroup}
          aria-invalid={error ? true : undefined}
          aria-describedby={describedBy}
          onFocus={() => setFocused(true)}
          onBlur={() => setFocused(false)}
          onChange={(e) => {
            // Accept Arabic-Indic digits too; keep only the first N digits.
            const next = e.target.value
              .replace(/[٠-٩]/g, (d) => String("٠١٢٣٤٥٦٧٨٩".indexOf(d)))
              .replace(/\D/g, "")
              .slice(0, length);
            onChange(next);
            if (next.length === length && next !== value) onComplete?.(next);
          }}
          className="absolute inset-0 h-full w-full cursor-text rounded-[8px] border-0 bg-transparent text-transparent caret-transparent outline-none selection:bg-transparent focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-ink"
        />
      </div>
    </div>
  );
});
