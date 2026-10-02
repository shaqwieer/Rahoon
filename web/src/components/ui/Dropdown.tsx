"use client";

import { useEffect, useId, useMemo, useRef, useState, type KeyboardEvent, type ReactNode } from "react";
import { cn } from "@/lib/cn";
import { Icon } from "./Icon";
import { usePopover } from "./usePopover";

export interface DropdownOption {
  value: string;
  label: string;
  /** Second line in the list (e.g. an English name or a type). */
  hint?: string;
  disabled?: boolean;
}

export type DropdownSize = "sm" | "md" | "lg";

const SIZE: Record<DropdownSize, string> = { sm: "min-h-10 text-14", md: "min-h-11 text-15", lg: "min-h-12 text-16" };

/** Strips Arabic diacritics/tatweel and unifies alef/yaa/taa forms so «مكة» finds «مكّة المكرمة». */
export function normalizeAr(s: string) {
  return s
    .toLowerCase()
    .replace(/[ً-ٰٟـ]/g, "")
    .replace(/[أإآ]/g, "ا")
    .replace(/ى/g, "ي")
    .replace(/ة/g, "ه")
    .trim();
}

export interface DropdownProps {
  value: string;
  onChange: (value: string) => void;
  options: DropdownOption[];
  /** Shown when no option has the current value. */
  placeholder?: string;
  id?: string;
  /** Accessible name when there is no <label htmlFor={id}>. */
  ariaLabel?: string;
  describedBy?: string;
  /** Search box at the top of the list; on by default for long lists. */
  searchable?: boolean;
  size?: DropdownSize;
  disabled?: boolean;
  invalid?: boolean;
  className?: string;
  /** Icon before the value (e.g. «location_on»). */
  icon?: string;
  /** Extra content rendered under the options (e.g. a note). */
  footer?: ReactNode;
  /** Submits the value with a plain <form> (hidden input). */
  name?: string;
}

/**
 * The system dropdown (replaces native <select>): a white panel under the field with the rust highlight of the directory
 * picker, a check on the chosen option, a search box for long lists, and full keyboard use (arrows, Home/End, Enter, Escape,
 * type to jump). Opens upwards near the bottom of the screen.
 */
export function Dropdown({ value, onChange, options, placeholder = "اختر", id: idProp, ariaLabel, describedBy, searchable, size = "md", disabled, invalid, className, icon, footer, name }: DropdownProps) {
  const autoId = useId();
  const id = idProp ?? autoId;
  const listId = `${id}-list`;
  const { open, setOpen, close, rootRef, triggerRef, panelStyle } = usePopover<HTMLButtonElement>(320);
  const [query, setQuery] = useState("");
  const [active, setActive] = useState(0);
  const listRef = useRef<HTMLUListElement>(null);
  const searchRef = useRef<HTMLInputElement>(null);
  const typed = useRef({ text: "", at: 0 });
  const withSearch = searchable ?? options.length > 9;

  const shown = useMemo(() => {
    if (!withSearch || !query.trim()) return options;
    const q = normalizeAr(query);
    return options.filter((o) => normalizeAr(o.label).includes(q) || (o.hint ? normalizeAr(o.hint).includes(q) : false));
  }, [options, query, withSearch]);
  const selected = options.find((o) => o.value === value);

  const openList = () => {
    if (disabled) return;
    setQuery("");
    const i = Math.max(0, options.findIndex((o) => o.value === value));
    setActive(i);
    setOpen(true);
  };

  useEffect(() => {
    if (!open) return;
    if (withSearch) searchRef.current?.focus();
    else listRef.current?.focus();
  }, [open, withSearch]);

  useEffect(() => {
    if (!open) return;
    listRef.current?.querySelector<HTMLElement>(`[data-i="${active}"]`)?.scrollIntoView({ block: "nearest" });
  }, [active, open]);

  const choose = (i: number) => {
    const o = shown[i];
    if (!o || o.disabled) return;
    onChange(o.value);
    close();
  };

  const move = (delta: number) => {
    if (shown.length === 0) return;
    let i = active;
    for (let n = 0; n < shown.length; n++) {
      i = (i + delta + shown.length) % shown.length;
      if (!shown[i].disabled) break;
    }
    setActive(i);
  };

  const onListKey = (e: KeyboardEvent) => {
    if (e.key === "ArrowDown") { e.preventDefault(); move(1); }
    else if (e.key === "ArrowUp") { e.preventDefault(); move(-1); }
    else if (e.key === "Home") { e.preventDefault(); setActive(0); }
    else if (e.key === "End") { e.preventDefault(); setActive(shown.length - 1); }
    else if (e.key === "Enter" || (e.key === " " && !withSearch)) { e.preventDefault(); choose(active); }
    else if (e.key === "Tab") close(false);
    else if (!withSearch && e.key.length === 1) {
      const now = Date.now();
      typed.current = { text: now - typed.current.at > 700 ? e.key : typed.current.text + e.key, at: now };
      const q = normalizeAr(typed.current.text);
      const i = shown.findIndex((o) => normalizeAr(o.label).startsWith(q));
      if (i >= 0) setActive(i);
    }
  };

  return (
    <div ref={rootRef} className={cn("relative", className)}>
      {name ? <input type="hidden" name={name} value={value} /> : null}
      <button
        ref={triggerRef}
        id={id}
        type="button"
        disabled={disabled}
        aria-haspopup="listbox"
        aria-expanded={open}
        aria-controls={open ? listId : undefined}
        aria-label={ariaLabel ? `${ariaLabel}: ${selected?.label ?? placeholder}` : undefined}
        aria-describedby={describedBy}
        data-invalid={invalid || undefined}
        onClick={() => (open ? close(false) : openList())}
        onKeyDown={(e) => {
          if (["ArrowDown", "ArrowUp", "Enter", " "].includes(e.key) && !open) {
            e.preventDefault();
            openList();
          }
        }}
        className={cn(
          "flex w-full items-center gap-2 rounded-sm border bg-white px-3 text-start transition-colors",
          SIZE[size],
          open ? "border-rust ring-2 ring-rust-50" : invalid ? "border-err" : "border-line-strong hover:border-ink",
          disabled && "cursor-not-allowed bg-subtle text-soft",
        )}
      >
        {icon ? <Icon name={icon} size={18} className="flex-none text-muted" /> : null}
        <span className={cn("min-w-0 flex-1 truncate", !selected && "text-muted")}>{selected?.label ?? placeholder}</span>
        <Icon name="expand_more" size={20} className={cn("flex-none text-muted transition-transform", open && "rotate-180")} />
      </button>
      {open ? (
        <div style={panelStyle()} className="z-[70] flex flex-col overflow-hidden rounded-md border border-line bg-white shadow-3">
          {withSearch ? (
            <div className="border-b border-divider p-2">
              <div className="relative">
                <Icon name="search" size={18} className="pointer-events-none absolute start-2.5 top-1/2 -translate-y-1/2 text-muted" />
                <input
                  ref={searchRef}
                  value={query}
                  onChange={(e) => {
                    setQuery(e.target.value);
                    setActive(0);
                  }}
                  onKeyDown={onListKey}
                  role="combobox"
                  aria-expanded
                  aria-controls={listId}
                  aria-activedescendant={shown[active] ? `${id}-o-${active}` : undefined}
                  aria-label="ابحث في القائمة"
                  placeholder="ابحث…"
                  className="min-h-10 w-full rounded-sm border border-line bg-warm ps-9 pe-3 text-15 outline-none focus:border-rust focus:bg-white"
                />
              </div>
            </div>
          ) : null}
          <ul
            ref={listRef}
            id={listId}
            role="listbox"
            tabIndex={withSearch ? undefined : -1}
            aria-labelledby={id}
            aria-activedescendant={!withSearch && shown[active] ? `${id}-o-${active}` : undefined}
            onKeyDown={withSearch ? undefined : onListKey}
            className="m-0 max-h-72 min-h-0 flex-1 list-none overflow-y-auto overscroll-contain p-1 outline-none"
          >
            {shown.map((o, i) => {
              const isSel = o.value === value;
              return (
                <li
                  key={o.value}
                  id={`${id}-o-${i}`}
                  data-i={i}
                  role="option"
                  aria-selected={isSel}
                  aria-disabled={o.disabled || undefined}
                  onPointerMove={() => !o.disabled && setActive(i)}
                  onClick={() => choose(i)}
                  className={cn(
                    "flex cursor-pointer items-center gap-2 rounded-sm px-3 py-2",
                    i === active && !o.disabled && "bg-rust-50",
                    o.disabled && "cursor-not-allowed text-soft",
                  )}
                >
                  <span className="flex min-w-0 flex-1 flex-col">
                    <span className={cn("text-15", isSel && "font-semibold text-rust-700")}>{o.label}</span>
                    {o.hint ? <span className="text-12 text-muted">{o.hint}</span> : null}
                  </span>
                  {isSel ? <Icon name="check" size={18} className="flex-none text-rust" /> : null}
                </li>
              );
            })}
            {shown.length === 0 ? <li className="px-3 py-3 text-14 text-muted">لا نتائج لهذا البحث.</li> : null}
          </ul>
          {footer ? <div className="border-t border-divider px-3 py-2 text-12 text-muted">{footer}</div> : null}
        </div>
      ) : null}
    </div>
  );
}

/** A dropdown that keeps its own value, for plain GET forms rendered on the server. */
export function FormDropdown({ defaultValue = "", ...props }: Omit<DropdownProps, "value" | "onChange"> & { defaultValue?: string; name: string }) {
  const [value, setValue] = useState(defaultValue);
  return <Dropdown {...props} value={value} onChange={setValue} />;
}

/**
 * Several values from one list (e.g. property types): the trigger reads «شقة، فيلا» or the placeholder; the panel stays open
 * while ticking, with «مسح» to clear. Same look and keys as Dropdown (Space/Enter toggles).
 */
export function MultiDropdown({ values, onChange, options, placeholder = "الكل", id: idProp, ariaLabel, size = "md", className, icon }: {
  values: string[];
  onChange: (values: string[]) => void;
  options: DropdownOption[];
  placeholder?: string;
  id?: string;
  ariaLabel?: string;
  size?: DropdownSize;
  className?: string;
  icon?: string;
}) {
  const autoId = useId();
  const id = idProp ?? autoId;
  const listId = `${id}-list`;
  const { open, setOpen, close, rootRef, triggerRef, panelStyle } = usePopover<HTMLButtonElement>(320);
  const [active, setActive] = useState(0);
  const listRef = useRef<HTMLUListElement>(null);
  useEffect(() => {
    if (open) listRef.current?.focus();
  }, [open]);
  const toggle = (v: string) => onChange(values.includes(v) ? values.filter((x) => x !== v) : [...values, v]);
  const text = values.length === 0 ? placeholder : options.filter((o) => values.includes(o.value)).map((o) => o.label).join("، ");
  return (
    <div ref={rootRef} className={cn("relative", className)}>
      <button ref={triggerRef} id={id} type="button" aria-haspopup="listbox" aria-expanded={open} aria-controls={open ? listId : undefined}
        aria-label={ariaLabel ? `${ariaLabel}: ${text}` : undefined}
        onClick={() => (open ? close(false) : (setActive(0), setOpen(true)))}
        onKeyDown={(e) => {
          if (["ArrowDown", "Enter", " "].includes(e.key) && !open) {
            e.preventDefault();
            setActive(0);
            setOpen(true);
          }
        }}
        className={cn("flex w-full items-center gap-2 rounded-sm border bg-white px-3 text-start transition-colors", SIZE[size],
          open ? "border-rust ring-2 ring-rust-50" : "border-line-strong hover:border-ink")}>
        {icon ? <Icon name={icon} size={18} className="flex-none text-muted" /> : null}
        <span className={cn("min-w-0 flex-1 truncate", values.length === 0 && "text-muted")}>{text}</span>
        {values.length > 1 ? <span className="rounded-pill bg-rust px-1.5 text-12 font-bold text-white">{values.length}</span> : null}
        <Icon name="expand_more" size={20} className={cn("flex-none text-muted transition-transform", open && "rotate-180")} />
      </button>
      {open ? (
        <div style={panelStyle({ minWidth: 208 })} className="z-[70] flex flex-col overflow-hidden rounded-md border border-line bg-white shadow-3">
          <ul ref={listRef} id={listId} role="listbox" aria-multiselectable tabIndex={-1} aria-labelledby={id}
            aria-activedescendant={`${id}-o-${active}`}
            onKeyDown={(e) => {
              if (e.key === "ArrowDown") { e.preventDefault(); setActive((a) => Math.min(a + 1, options.length - 1)); }
              else if (e.key === "ArrowUp") { e.preventDefault(); setActive((a) => Math.max(a - 1, 0)); }
              else if (e.key === "Enter" || e.key === " ") { e.preventDefault(); toggle(options[active].value); }
              else if (e.key === "Tab") close(false);
            }}
            className="m-0 max-h-72 min-h-0 flex-1 list-none overflow-y-auto p-1 outline-none">
            {options.map((o, i) => {
              const on = values.includes(o.value);
              return (
                <li key={o.value} id={`${id}-o-${i}`} role="option" aria-selected={on} onPointerMove={() => setActive(i)} onClick={() => toggle(o.value)}
                  className={cn("flex cursor-pointer items-center gap-2.5 rounded-sm px-3 py-2 text-15", i === active && "bg-rust-50")}>
                  <span aria-hidden="true" className={cn("flex size-5 flex-none items-center justify-center rounded-xs border", on ? "border-rust bg-rust text-white" : "border-line-strong bg-white text-transparent")}>
                    <Icon name="check" size={16} />
                  </span>
                  <span className={cn(on && "font-semibold")}>{o.label}</span>
                </li>
              );
            })}
          </ul>
          <div className="flex items-center justify-between border-t border-divider px-2 py-1.5">
            <button type="button" className="min-h-9 rounded-sm px-2 text-13 font-semibold text-muted hover:bg-subtle" onClick={() => onChange([])}>مسح</button>
            <button type="button" className="min-h-9 rounded-sm px-3 text-13 font-semibold text-rust hover:bg-rust-50" onClick={() => close()}>تم</button>
          </div>
        </div>
      ) : null}
    </div>
  );
}
