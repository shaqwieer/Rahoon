"use client";

import { useId } from "react";
import { Icon } from "@/components/ui/Icon";
import { cn } from "@/lib/cn";
import { optionsFor, UNKNOWN } from "@/lib/market/catalog";
import { toLatinDigits } from "@/lib/market/numbers";
import type { FieldDef } from "@/lib/market/types";
import { DatePicker } from "@/components/ui/DatePicker";
import { Dropdown } from "@/components/ui/Dropdown";

const inputCls =
  "min-h-12 w-full rounded-sm border border-line-strong bg-white px-3 text-16 outline-none transition-colors focus-visible:border-ink focus-visible:ring-2 focus-visible:ring-ink/10 disabled:bg-subtle disabled:text-soft aria-[invalid=true]:border-err";

/** Toggle chips for short option lists (2–4): one tap, keyboard-friendly, announced as a radio group. */
export function Chips({ name, options, value, onChange, invalid, describedBy, multiple, labelledBy }: {
  name: string;
  options: { value: string; label: string }[];
  value: string | string[] | null | undefined;
  onChange: (v: string) => void;
  invalid?: boolean;
  describedBy?: string;
  multiple?: boolean;
  /** id of the visible question (fieldsets get their name from <legend>). */
  labelledBy?: string;
}) {
  const selected = (v: string) => (Array.isArray(value) ? value.includes(v) : value === v);
  return (
    <div role={multiple ? "group" : "radiogroup"} aria-labelledby={labelledBy} aria-describedby={describedBy} aria-invalid={invalid || undefined} className="flex flex-wrap gap-2">
      {options.map((o) => {
        const on = selected(o.value);
        return (
          <button
            key={o.value}
            type="button"
            role={multiple ? "checkbox" : "radio"}
            aria-checked={on}
            name={name}
            onClick={() => onChange(o.value)}
            className={cn(
              "inline-flex min-h-11 items-center gap-1.5 rounded-pill border px-4 text-15 font-medium transition-colors duration-150",
              on ? "border-rust bg-rust-50 text-rust-700" : "border-line-strong bg-white text-ink hover:bg-subtle",
              invalid && !on && "border-err-line",
            )}
          >
            {on ? <Icon name="check" size={18} /> : null}
            {o.label}
          </button>
        );
      })}
    </div>
  );
}

/**
 * Renders one catalog field. «لا أعرف» is a separate explicit choice (stored as __unknown), never an empty or zero value.
 * Numbers accept Arabic-Indic and Latin digits; the server normalises them the same way.
 */
export function DynamicField({ def, value, onChange, propertyType, error, idPrefix = "f" }: {
  def: FieldDef;
  value: string | undefined;
  onChange: (v: string | undefined) => void;
  propertyType?: string | null;
  error?: string;
  idPrefix?: string;
}) {
  const uid = useId();
  const id = `${idPrefix}-${def.key}`;
  const helpId = `${uid}-help`;
  const errId = `${uid}-err`;
  const unknown = value === UNKNOWN;
  const described = [def.help ? helpId : null, error ? errId : null].filter(Boolean).join(" ") || undefined;
  const opts = optionsFor(def, propertyType);

  const unit = def.type === "money" ? "ر.س" : def.unit;
  let control: React.ReactNode;
  switch (def.type) {
    case "money":
    case "decimal":
    case "integer":
      control = (
        <div className={cn("flex min-h-12 items-stretch overflow-hidden rounded-sm border bg-white transition-colors focus-within:border-rust focus-within:ring-2 focus-within:ring-rust-50",
          error ? "border-err" : "border-line-strong", unknown && "bg-subtle")}>
          <input
            id={id}
            inputMode={def.type === "integer" ? "numeric" : "decimal"}
            dir="ltr"
            autoComplete="off"
            className="min-w-0 flex-1 bg-transparent px-3 text-end text-16 tabular-nums outline-none disabled:text-soft"
            value={unknown ? "" : (value ?? "")}
            disabled={unknown}
            aria-invalid={Boolean(error) || undefined}
            aria-describedby={described}
            onChange={(e) => onChange(e.target.value === "" ? undefined : e.target.value)}
            onBlur={(e) => {
              const v = toLatinDigits(e.target.value).replace(/\s/g, "");
              if (v !== e.target.value) onChange(v === "" ? undefined : v);
            }}
            placeholder={unknown ? "لا أعرف" : undefined}
          />
          {unit ? <span className="flex flex-none items-center border-s border-line bg-warm px-3 text-13 text-muted">{unit}</span> : null}
        </div>
      );
      break;
    case "select":
      control =
        opts.length <= 4 ? (
          <Chips name={id} labelledBy={`${id}-label`} options={opts} value={value} onChange={(v) => onChange(v === value ? undefined : v)} invalid={Boolean(error)} describedBy={described} />
        ) : (
          <Dropdown id={id} value={unknown ? "" : (value ?? "")} disabled={unknown} invalid={Boolean(error)} describedBy={described} size="lg"
            options={opts} placeholder="اختر" onChange={(v) => onChange(v || undefined)} />
        );
      break;
    case "boolean":
      control = (
        <Chips name={id} labelledBy={`${id}-label`} options={[{ value: "true", label: "نعم" }, { value: "false", label: "لا" }]} value={value} onChange={(v) => onChange(v === value ? undefined : v)}
          invalid={Boolean(error)} describedBy={described} />
      );
      break;
    case "multiSelect": {
      const list = (value ?? "").split(",").filter(Boolean);
      control = (
        <Chips name={id} labelledBy={`${id}-label`} multiple options={opts} value={list} invalid={Boolean(error)} describedBy={described}
          onChange={(v) => {
            const next = list.includes(v) ? list.filter((x) => x !== v) : [...list, v];
            onChange(next.length ? next.join(",") : undefined);
          }} />
      );
      break;
    }
    case "date":
      control = <DatePicker id={id} size="lg" value={unknown ? "" : (value ?? "")} disabled={unknown} invalid={Boolean(error)} describedBy={described}
        onChange={(v) => onChange(v || undefined)} />;
      break;
    case "month":
      control = <DatePicker id={id} mode="month" size="lg" value={unknown ? "" : (value ?? "")} disabled={unknown} invalid={Boolean(error)} describedBy={described}
        onChange={(v) => onChange(v || undefined)} />;
      break;
    case "longText":
      control = <textarea id={id} rows={4} maxLength={def.max ?? 2000} className={cn(inputCls, "py-2.5 leading-7")} value={value ?? ""}
        aria-invalid={Boolean(error) || undefined} aria-describedby={described} onChange={(e) => onChange(e.target.value || undefined)} />;
      break;
    default:
      control = <input id={id} className={inputCls} value={value ?? ""} aria-invalid={Boolean(error) || undefined} aria-describedby={described}
        onChange={(e) => onChange(e.target.value || undefined)} />;
  }

  const isChips = (def.type === "select" && opts.length <= 4) || def.type === "boolean" || def.type === "multiSelect";
  return (
    <div className="flex flex-col gap-1.5" data-field={def.key}>
      <div className="flex flex-wrap items-center justify-between gap-2">
        {isChips ? (
          <span id={`${id}-label`} className="text-15 font-semibold">
            {def.label}
          </span>
        ) : (
          <label htmlFor={id} className="text-15 font-semibold">
            {def.label}
          </label>
        )}
        {def.allowUnknown ? (
          <label className="inline-flex min-h-9 cursor-pointer items-center gap-1.5 rounded-pill border border-line px-3 text-13 font-medium has-[:checked]:border-rust has-[:checked]:bg-rust-50 has-[:checked]:text-rust-700">
            <input type="checkbox" className="size-4 accent-[var(--p-rust-600)]" checked={unknown} onChange={(e) => onChange(e.target.checked ? UNKNOWN : undefined)} />
            لا أعرف
          </label>
        ) : null}
      </div>
      {control}
      {def.help ? (
        <span id={helpId} className="text-13 leading-5 text-muted">
          {def.help}
        </span>
      ) : null}
      {error ? (
        <span id={errId} className="flex items-center gap-1 text-13 text-err">
          <Icon name="error" size={16} />
          {error}
        </span>
      ) : null}
    </div>
  );
}
