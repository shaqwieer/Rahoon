"use client";

import {
  forwardRef,
  useId,
  useState,
  type InputHTMLAttributes,
  type ReactNode,
  type TextareaHTMLAttributes,
} from "react";
import { cn } from "@/lib/cn";
import { formatHijri, formatNumber } from "@/lib/format";
import { useI18n } from "@/lib/i18n/client";
import { DatePicker } from "./DatePicker";
import { Dropdown } from "./Dropdown";
import { Icon } from "./Icon";
import { IconButton } from "./IconButton";

/* ───────────────────────── Shell: label above, help below, error replaces help (C06) ───────────────────────── */

export type FieldSize = "md" | "lg" | "xl";
const HEIGHT: Record<FieldSize, string> = { md: "h-11", lg: "h-12", xl: "h-[54px]" };
const TEXT: Record<FieldSize, string> = { md: "text-15", lg: "text-16", xl: "text-17" };
const RADIUS: Record<FieldSize, string> = { md: "rounded-sm", lg: "rounded-sm", xl: "rounded-[8px]" };

export interface FieldShellProps {
  id: string;
  label: ReactNode;
  /** Adds «(إلزامي)» after the label. */
  required?: boolean;
  /** Adds «(اختياري)» after the label. */
  optional?: boolean;
  help?: ReactNode;
  error?: ReactNode;
  disabled?: boolean;
  /** Content placed at the end of the label row (e.g. «نسيت كلمة المرور؟»). */
  labelAside?: ReactNode;
  size?: FieldSize;
  className?: string;
  children: ReactNode;
}

export const helpId = (id: string) => `${id}-help`;
export const errorId = (id: string) => `${id}-error`;

/** aria-describedby value for a control inside FieldShell. */
export function describedBy(id: string, { help, error }: { help?: ReactNode; error?: ReactNode }, extra?: string) {
  return [error ? errorId(id) : help ? helpId(id) : null, extra].filter(Boolean).join(" ") || undefined;
}

export function FieldShell({ id, label, required, optional, help, error, disabled, labelAside, size = "md", className, children }: FieldShellProps) {
  const { t } = useI18n();
  return (
    <div className={cn("flex min-w-0 flex-col gap-1.5", className)}>
      <div className="flex items-baseline justify-between gap-3">
        <label htmlFor={id} className={cn("font-semibold", size === "xl" ? "text-16" : "text-14", disabled && "text-soft")}>
          {label}
          {required ? <span className="ms-1 font-normal text-muted">{t.common.required}</span> : null}
          {optional ? <span className="ms-1 font-normal text-muted">{t.common.optional}</span> : null}
        </label>
        {labelAside}
      </div>
      {children}
      {error ? (
        <span id={errorId(id)} className="flex items-center gap-1 text-13 text-err">
          <Icon name="error" size={16} />
          <span>{error}</span>
        </span>
      ) : help ? (
        <span id={helpId(id)} className="flex items-center gap-1 text-13 text-muted">
          {help}
        </span>
      ) : null}
    </div>
  );
}

/** Border/focus/error/disabled look shared by inputs and grouped (addon) controls. */
function controlClasses({ error, disabled, size = "md", within }: { error?: boolean; disabled?: boolean; size?: FieldSize; within?: boolean }) {
  const focus = within
    ? "focus-within:border-2 focus-within:border-ink focus-within:outline-2 focus-within:outline-offset-2 focus-within:outline-ink"
    : "focus:border-2 focus:border-ink";
  return cn(
    "w-full min-w-0 bg-white text-ink",
    HEIGHT[size],
    RADIUS[size],
    TEXT[size],
    disabled ? "border border-line bg-subtle text-muted" : error ? "border-2 border-err" : cn("border border-line-strong", focus),
  );
}

/** Inline-direction alignment for LTR values (IDs, amounts, emails) inside an RTL form. */
function useLtrAlign() {
  const { dir } = useI18n();
  return dir === "rtl" ? "text-right" : "text-left";
}

/* ───────────────────────── TextField ───────────────────────── */

export interface TextFieldProps extends Omit<InputHTMLAttributes<HTMLInputElement>, "size" | "prefix"> {
  label: ReactNode;
  help?: ReactNode;
  error?: ReactNode;
  requiredMark?: boolean;
  optionalMark?: boolean;
  size?: FieldSize;
  /** LTR value (email, phone, IDs, refs) inside RTL — typed and shown left-to-right, aligned to the reading edge. */
  ltr?: boolean;
  /** IBM Plex Mono for references and codes. */
  mono?: boolean;
  /** Fixed addon before the value in reading order (e.g. «+966»). */
  prefix?: ReactNode;
  /** Button or icon at the inline end (e.g. show-password). */
  endAdornment?: ReactNode;
  labelAside?: ReactNode;
  /** Error border without a message (e.g. the password field under a credential alert). */
  invalid?: boolean;
  containerClassName?: string;
}

export const TextField = forwardRef<HTMLInputElement, TextFieldProps>(function TextField(
  { label, help, error, requiredMark, optionalMark, size = "md", ltr, mono, prefix, endAdornment, labelAside, invalid, containerClassName, id: idProp, disabled, className, ...rest },
  ref,
) {
  const autoId = useId();
  const id = idProp ?? autoId;
  const align = useLtrAlign();
  const grouped = Boolean(prefix || endAdornment);
  const hasError = Boolean(error) || Boolean(invalid);
  const input = (
    <input
      ref={ref}
      id={id}
      disabled={disabled}
      dir={ltr ? "ltr" : undefined}
      aria-invalid={hasError || undefined}
      aria-describedby={describedBy(id, { help, error }, rest["aria-describedby"])}
      className={cn(
        grouped ? "h-full min-w-0 flex-1 border-0 bg-transparent px-3 outline-none" : cn(controlClasses({ error: hasError, disabled, size }), "px-3 focus:px-[11px]"),
        ltr && align,
        mono ? "font-mono font-medium" : ltr ? "font-latin" : null,
        "placeholder:text-soft",
        className,
      )}
      {...rest}
    />
  );
  return (
    <FieldShell id={id} label={label} help={help} error={error} disabled={disabled} required={requiredMark} optional={optionalMark} labelAside={labelAside} size={size} className={containerClassName}>
      {grouped ? (
        <div className={cn("flex items-stretch overflow-hidden", controlClasses({ error: hasError, disabled, size, within: true }))}>
          {prefix ? (
            <span dir="ltr" className="flex items-center border-e border-line bg-subtle px-3 font-mono text-14 font-medium text-muted">
              {prefix}
            </span>
          ) : null}
          {input}
          {endAdornment ? <span className="flex items-center">{endAdornment}</span> : null}
        </div>
      ) : (
        input
      )}
    </FieldShell>
  );
});

/* ───────────────────────── Textarea (with counter) ───────────────────────── */

export interface TextareaProps extends TextareaHTMLAttributes<HTMLTextAreaElement> {
  label: ReactNode;
  help?: ReactNode;
  error?: ReactNode;
  requiredMark?: boolean;
  optionalMark?: boolean;
  /** Shows «n / max» and enforces maxLength. */
  maxLength?: number;
  containerClassName?: string;
}

export const Textarea = forwardRef<HTMLTextAreaElement, TextareaProps>(function Textarea(
  { label, help, error, requiredMark, optionalMark, maxLength, containerClassName, id: idProp, disabled, className, value, defaultValue, onChange, ...rest },
  ref,
) {
  const autoId = useId();
  const id = idProp ?? autoId;
  const { t } = useI18n();
  const [uncontrolledLength, setUncontrolledLength] = useState(String(defaultValue ?? "").length);
  const length = value !== undefined ? String(value).length : uncontrolledLength;
  const counterId = `${id}-count`;
  return (
    <FieldShell id={id} label={label} help={help} error={error} disabled={disabled} required={requiredMark} optional={optionalMark} className={containerClassName}>
      <textarea
        ref={ref}
        id={id}
        disabled={disabled}
        maxLength={maxLength}
        value={value}
        defaultValue={defaultValue}
        onChange={(e) => {
          if (value === undefined) setUncontrolledLength(e.target.value.length);
          onChange?.(e);
        }}
        aria-invalid={error ? true : undefined}
        aria-describedby={describedBy(id, { help, error }, maxLength ? counterId : undefined)}
        className={cn(
          "min-h-[88px] w-full resize-y rounded-sm bg-white px-3 py-2.5 text-15 leading-6 text-ink placeholder:text-soft",
          disabled ? "border border-line bg-subtle text-muted" : error ? "border-2 border-err px-[11px]" : "border border-line-strong focus:border-2 focus:border-ink focus:px-[11px] focus:py-[9px]",
          className,
        )}
        {...rest}
      />
      {maxLength ? (
        <span id={counterId} className="self-end text-13 text-muted" aria-live="polite">
          <bdi dir="ltr">{t.common.charCount(length, maxLength)}</bdi>
        </span>
      ) : null}
    </FieldShell>
  );
});

/* ───────────────────────── Amount / Percent (LTR value + fixed unit) ───────────────────────── */

interface UnitFieldProps extends Omit<InputHTMLAttributes<HTMLInputElement>, "size" | "value" | "defaultValue" | "onChange" | "type"> {
  label: ReactNode;
  help?: ReactNode;
  error?: ReactNode;
  requiredMark?: boolean;
  size?: FieldSize;
  /** Controlled numeric value (null = empty). */
  value: number | null;
  onValueChange: (value: number | null) => void;
  containerClassName?: string;
}

function parseLooseNumber(raw: string): number | null {
  const western = raw.replace(/[٠-٩]/g, (d) => String("٠١٢٣٤٥٦٧٨٩".indexOf(d))).replace(/٫/g, ".").replace(/[^0-9.]/g, "");
  if (!western) return null;
  const n = Number(western);
  return Number.isFinite(n) ? n : null;
}

function UnitField({
  unit,
  fractionDigits,
  label,
  help,
  error,
  requiredMark,
  size = "md",
  value,
  onValueChange,
  containerClassName,
  id: idProp,
  disabled,
  onBlur,
  onFocus,
  ...rest
}: UnitFieldProps & { unit: string; fractionDigits: number }) {
  const autoId = useId();
  const id = idProp ?? autoId;
  const align = useLtrAlign();
  const [draft, setDraft] = useState<string | null>(null); // raw text while editing; null → show formatted value
  const shown = draft ?? (value === null ? "" : formatNumber(value, fractionDigits));
  return (
    <FieldShell id={id} label={label} help={help} error={error} disabled={disabled} required={requiredMark} size={size} className={containerClassName}>
      <div className={cn("flex items-stretch overflow-hidden", controlClasses({ error: Boolean(error), disabled, size, within: true }))}>
        <input
          id={id}
          dir="ltr"
          inputMode="decimal"
          autoComplete="off"
          disabled={disabled}
          value={shown}
          aria-invalid={error ? true : undefined}
          aria-describedby={describedBy(id, { help, error }, `${id}-unit`)}
          onFocus={(e) => {
            setDraft(value === null ? "" : String(value));
            onFocus?.(e);
          }}
          onChange={(e) => {
            setDraft(e.target.value);
            onValueChange(parseLooseNumber(e.target.value));
          }}
          onBlur={(e) => {
            setDraft(null);
            onBlur?.(e);
          }}
          className={cn("h-full min-w-0 flex-1 border-0 bg-transparent px-3 font-latin font-semibold tabular-nums outline-none", align)}
          {...rest}
        />
        <span id={`${id}-unit`} className="flex items-center border-s border-line bg-subtle px-3 text-14 text-muted">
          {unit}
        </span>
      </div>
    </FieldShell>
  );
}

/** Money input: LTR digits inside RTL, fixed «ر.س» unit, grouped 2-decimal display on blur. */
export function AmountField(props: UnitFieldProps) {
  const { t } = useI18n();
  return <UnitField {...props} unit={t.common.unitSar} fractionDigits={2} />;
}

/** Percentage input with fixed «%» unit. */
export function PercentField(props: UnitFieldProps & { fractionDigits?: number }) {
  return <UnitField {...props} unit="%" fractionDigits={props.fractionDigits ?? 1} />;
}

/* ───────────────────────── MaskedValue (audited reveal) ───────────────────────── */

export interface MaskedValueProps {
  label: ReactNode;
  /** Server-masked value, e.g. «1•••••••42». The UI never holds the full value until revealed. */
  masked: string;
  /** Calls the audited reveal endpoint and resolves to the full value. Omit when the user lacks pii.reveal. */
  onReveal?: () => Promise<string>;
  help?: ReactNode;
  error?: ReactNode;
  size?: FieldSize;
  className?: string;
}

export function MaskedValue({ label, masked, onReveal, help, error, size = "md", className }: MaskedValueProps) {
  const id = useId();
  const { t } = useI18n();
  const align = useLtrAlign();
  const [revealed, setRevealed] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [failed, setFailed] = useState<string | null>(null);
  return (
    <FieldShell id={id} label={label} help={help} error={error ?? failed} size={size} className={className}>
      <div className={cn("relative flex items-center", controlClasses({ error: Boolean(error ?? failed), size }), "pe-11")}>
        <output
          id={id}
          dir="ltr"
          aria-live="polite"
          className={cn("block flex-1 truncate px-3 font-mono font-medium tracking-[1px]", align)}
        >
          {revealed ?? masked}
        </output>
        {onReveal ? (
          <IconButton
            label={revealed ? t.common.concealAgain : t.common.reveal}
            icon={revealed ? "visibility_off" : busy ? "progress_activity" : "visibility"}
            size={36}
            className="absolute end-1 top-1/2 -translate-y-1/2 text-muted"
            aria-busy={busy || undefined}
            onClick={async () => {
              if (revealed) {
                setRevealed(null);
                return;
              }
              setBusy(true);
              setFailed(null);
              try {
                setRevealed(await onReveal());
              } catch (e) {
                setFailed(e instanceof Error && e.message ? e.message : t.sysStates.errorTitle);
              } finally {
                setBusy(false);
              }
            }}
          />
        ) : null}
      </div>
    </FieldShell>
  );
}

/* ───────────────────────── DateField / MonthField (system calendar) ───────────────────────── */

export interface DateFieldProps {
  label: ReactNode;
  help?: ReactNode;
  error?: ReactNode;
  requiredMark?: boolean;
  optionalMark?: boolean;
  size?: FieldSize;
  /** ISO "YYYY-MM-DD", or "" when empty. */
  value: string;
  onValueChange: (iso: string) => void;
  containerClassName?: string;
  id?: string;
  disabled?: boolean;
  min?: string;
  max?: string;
  /** Show the Hijri equivalent under the field (default on). */
  hijri?: boolean;
}

const PICKER_SIZE: Record<FieldSize, "md" | "lg"> = { md: "md", lg: "lg", xl: "lg" };

/** Date with the system calendar (Gregorian, «15 يناير 2027») and its Hijri equivalent under it. */
export function DateField({ label, help, error, requiredMark, optionalMark, size = "md", value, onValueChange, containerClassName, id: idProp, disabled, min, max, hijri = true }: DateFieldProps) {
  const autoId = useId();
  const id = idProp ?? autoId;
  const { locale, numerals } = useI18n();
  const caption = hijri && value ? formatHijri(value, { locale, numerals }) : null;
  return (
    <FieldShell id={id} label={label} help={help} error={error} disabled={disabled} required={requiredMark} optional={optionalMark} size={size} className={containerClassName}>
      <DatePicker id={id} value={value} onChange={onValueChange} disabled={disabled} invalid={Boolean(error)} min={min} max={max} size={PICKER_SIZE[size]}
        describedBy={describedBy(id, { help, error })} />
      {caption ? (
        <span aria-live="polite" className="flex items-center gap-1.5 text-13 text-muted">
          <Icon name="calendar_month" size={16} />
          {caption}
        </span>
      ) : null}
    </FieldShell>
  );
}

/** Month and year («يناير 2027»), value "YYYY-MM". */
export function MonthField({ label, help, error, requiredMark, optionalMark, size = "md", value, onValueChange, containerClassName, id: idProp, disabled, min, max }: Omit<DateFieldProps, "hijri">) {
  const autoId = useId();
  const id = idProp ?? autoId;
  return (
    <FieldShell id={id} label={label} help={help} error={error} disabled={disabled} required={requiredMark} optional={optionalMark} size={size} className={containerClassName}>
      <DatePicker id={id} mode="month" value={value} onChange={onValueChange} disabled={disabled} invalid={Boolean(error)} min={min} max={max} size={PICKER_SIZE[size]}
        describedBy={describedBy(id, { help, error })} />
    </FieldShell>
  );
}

/* ───────────────────────── Select (system dropdown) ───────────────────────── */

export interface SelectOption {
  value: string;
  label: string;
  hint?: string;
  disabled?: boolean;
}

export interface SelectProps {
  label: ReactNode;
  options: SelectOption[];
  value: string;
  onValueChange: (value: string) => void;
  placeholder?: string;
  help?: ReactNode;
  error?: ReactNode;
  requiredMark?: boolean;
  optionalMark?: boolean;
  size?: FieldSize;
  containerClassName?: string;
  id?: string;
  disabled?: boolean;
  searchable?: boolean;
}

/** Labelled field around the system dropdown (no native <select>). */
export function Select({ label, options, value, onValueChange, placeholder, help, error, requiredMark, optionalMark, size = "md", containerClassName, id: idProp, disabled, searchable }: SelectProps) {
  const autoId = useId();
  const id = idProp ?? autoId;
  return (
    <FieldShell id={id} label={label} help={help} error={error} disabled={disabled} required={requiredMark} optional={optionalMark} size={size} className={containerClassName}>
      <Dropdown id={id} value={value} onChange={onValueChange} options={options} placeholder={placeholder} disabled={disabled} invalid={Boolean(error)}
        size={PICKER_SIZE[size]} searchable={searchable} describedBy={describedBy(id, { help, error })} />
    </FieldShell>
  );
}

/* ───────────────────────── Checkbox ───────────────────────── */

export interface CheckboxProps extends Omit<InputHTMLAttributes<HTMLInputElement>, "type" | "size"> {
  label: ReactNode;
  description?: ReactNode;
  error?: ReactNode;
  /** 20 (default) or 24 (owner flows). */
  boxSize?: 20 | 24;
}

export const Checkbox = forwardRef<HTMLInputElement, CheckboxProps>(function Checkbox(
  { label, description, error, boxSize = 20, id: idProp, disabled, className, ...rest },
  ref,
) {
  const autoId = useId();
  const id = idProp ?? autoId;
  return (
    <div className={cn("flex flex-col gap-1", className)}>
      <label htmlFor={id} className={cn("flex items-start gap-2.5", boxSize === 24 ? "text-16 leading-[26px]" : "text-14 leading-[22px]", disabled ? "cursor-not-allowed text-soft" : "cursor-pointer")}>
        <span className="relative mt-px flex flex-none items-center justify-center" style={{ width: boxSize, height: boxSize }}>
          <input
            ref={ref}
            id={id}
            type="checkbox"
            disabled={disabled}
            aria-invalid={error ? true : undefined}
            aria-describedby={error ? errorId(id) : description ? helpId(id) : undefined}
            className="peer absolute inset-0 m-0 cursor-[inherit] opacity-0"
            {...rest}
          />
          <span
            aria-hidden="true"
            className={cn(
              "pointer-events-none flex size-full items-center justify-center rounded-xs border bg-white text-transparent peer-checked:border-ink peer-checked:bg-ink peer-checked:text-white peer-focus-visible:outline-2 peer-focus-visible:outline-offset-2 peer-focus-visible:outline-ink",
              error ? "border-2 border-err" : disabled ? "border-line bg-subtle" : "border-line-strong",
            )}
          >
            <Icon name="check" size={boxSize === 24 ? 18 : 16} />
          </span>
        </span>
        <span className="flex-1">{label}</span>
      </label>
      {error ? (
        <span id={errorId(id)} className="flex items-center gap-1 ps-[30px] text-13 text-err">
          <Icon name="error" size={16} />
          {error}
        </span>
      ) : description ? (
        <span id={helpId(id)} className="ps-[30px] text-13 text-muted">
          {description}
        </span>
      ) : null}
    </div>
  );
});

/* ───────────────────────── RadioCardGroup (solution-type cards) ───────────────────────── */

export interface RadioCardOption {
  value: string;
  label: ReactNode;
  description?: ReactNode;
  disabled?: boolean;
  /** Shown instead of / after the description when disabled (why the option is not available). */
  disabledReason?: ReactNode;
}

export interface RadioCardGroupProps {
  legend: ReactNode;
  name: string;
  options: RadioCardOption[];
  value: string | null;
  onChange: (value: string) => void;
  /** Grid columns on wide screens (cards stack on mobile). */
  columns?: 1 | 2 | 3 | 4;
  error?: ReactNode;
  className?: string;
}

const COLS = { 1: "", 2: "sm:grid-cols-2", 3: "sm:grid-cols-2 lg:grid-cols-3", 4: "sm:grid-cols-2 lg:grid-cols-4" };

export function RadioCardGroup({ legend, name, options, value, onChange, columns = 4, error, className }: RadioCardGroupProps) {
  const baseId = useId();
  return (
    <fieldset className={cn("m-0 flex min-w-0 flex-col gap-2 border-0 p-0", className)} aria-describedby={error ? errorId(baseId) : undefined}>
      <legend className="mb-2 p-0 text-15 font-semibold">{legend}</legend>
      <div className={cn("grid grid-cols-1 gap-2.5", COLS[columns])}>
        {options.map((o) => {
          const selected = value === o.value;
          const optId = `${baseId}-${o.value}`;
          const reasonId = o.disabled && o.disabledReason ? `${optId}-reason` : undefined;
          return (
            <label
              key={o.value}
              htmlFor={optId}
              className={cn(
                "relative flex min-h-[72px] flex-col gap-1 rounded-md px-3.5 py-3 has-[:focus-visible]:outline-2 has-[:focus-visible]:outline-offset-2 has-[:focus-visible]:outline-ink",
                selected ? "border-2 border-ink bg-rust-50 px-[13px] py-[11px]" : o.disabled ? "border border-line bg-warm text-soft" : "border border-line-strong bg-white",
                o.disabled ? "cursor-not-allowed" : "cursor-pointer",
              )}
            >
              <input
                id={optId}
                type="radio"
                name={name}
                value={o.value}
                checked={selected}
                disabled={o.disabled}
                aria-describedby={reasonId}
                onChange={() => onChange(o.value)}
                className="absolute inset-0 m-0 cursor-[inherit] opacity-0"
              />
              <span className="flex items-center gap-2 text-14 font-bold">
                <span
                  aria-hidden="true"
                  className={cn("box-border size-4 flex-none rounded-full", selected ? "border-[5px] border-ink" : "border border-line-strong bg-white")}
                />
                {o.label}
              </span>
              {o.description ? <span className="text-12 leading-[18px] text-muted">{o.description}</span> : null}
              {reasonId ? (
                <span id={reasonId} className="flex items-center gap-1 text-12 leading-[18px] text-muted">
                  <Icon name="block" size={14} />
                  {o.disabledReason}
                </span>
              ) : null}
            </label>
          );
        })}
      </div>
      {error ? (
        <span id={errorId(baseId)} className="flex items-center gap-1 text-13 text-err">
          <Icon name="error" size={16} />
          {error}
        </span>
      ) : null}
    </fieldset>
  );
}
