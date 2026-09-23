"use client";

import Link from "next/link";
import { useCallback, useEffect, useId, useRef, useState, type KeyboardEvent, type ReactNode } from "react";
import { cn } from "@/lib/cn";
import { Icon } from "./Icon";

export interface MenuItemDef {
  key: string;
  label: ReactNode;
  description?: ReactNode;
  icon?: string;
  /** Leading visual (e.g. org avatar) instead of an icon. */
  leading?: ReactNode;
  href?: string;
  onSelect?: () => void;
  /** Renders as menuitemradio with a check mark when true/false (org switcher). */
  checked?: boolean;
  disabled?: boolean;
  tone?: "default" | "danger";
}

export interface MenuProps {
  /** Contents of the trigger button. */
  trigger: ReactNode;
  triggerClassName?: string;
  /** Accessible name when the trigger has no text. */
  triggerLabel?: string;
  items: MenuItemDef[];
  /** Menu accessible name. */
  label: string;
  /** Small heading inside the popup («منشآتك وأدوارك»). */
  header?: ReactNode;
  /** Note under the items (e.g. data isolation notice). */
  footer?: ReactNode;
  /** Which edge of the trigger the popup aligns to (logical). */
  align?: "start" | "end";
  placement?: "bottom" | "top";
  minWidth?: number;
  className?: string;
}

/**
 * Button + role="menu" popup. Arrow keys / Home / End move between items, Esc closes and returns
 * focus to the trigger, Tab or an outside click closes it.
 */
export function Menu({ trigger, triggerClassName, triggerLabel, items, label, header, footer, align = "start", placement = "bottom", minWidth = 240, className }: MenuProps) {
  const [open, setOpen] = useState(false);
  const rootRef = useRef<HTMLDivElement>(null);
  const buttonRef = useRef<HTMLButtonElement>(null);
  const listRef = useRef<HTMLDivElement>(null);
  const menuId = useId();

  const itemEls = () => Array.from(listRef.current?.querySelectorAll<HTMLElement>("[data-menu-item]:not([aria-disabled='true'])") ?? []);

  const close = useCallback((restoreFocus: boolean) => {
    setOpen(false);
    if (restoreFocus) buttonRef.current?.focus();
  }, []);

  useEffect(() => {
    if (!open) return;
    const first = itemEls().find((el) => el.getAttribute("aria-checked") === "true") ?? itemEls()[0];
    first?.focus();
    const onDown = (e: PointerEvent) => {
      if (!rootRef.current?.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener("pointerdown", onDown);
    return () => document.removeEventListener("pointerdown", onDown);
  }, [open]);

  const onKeyDown = (e: KeyboardEvent<HTMLDivElement>) => {
    const els = itemEls();
    const idx = els.indexOf(document.activeElement as HTMLElement);
    if (e.key === "ArrowDown") {
      e.preventDefault();
      els[(idx + 1) % els.length]?.focus();
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      els[(idx - 1 + els.length) % els.length]?.focus();
    } else if (e.key === "Home") {
      e.preventDefault();
      els[0]?.focus();
    } else if (e.key === "End") {
      e.preventDefault();
      els[els.length - 1]?.focus();
    } else if (e.key === "Escape") {
      e.preventDefault();
      close(true);
    } else if (e.key === "Tab") {
      close(false);
    }
  };

  return (
    <div ref={rootRef} className={cn("relative", className)}>
      <button
        ref={buttonRef}
        type="button"
        aria-haspopup="menu"
        aria-expanded={open}
        aria-controls={open ? menuId : undefined}
        aria-label={triggerLabel}
        className={triggerClassName}
        onClick={() => setOpen((v) => !v)}
        onKeyDown={(e) => {
          if (e.key === "ArrowDown" || e.key === "ArrowUp") {
            e.preventDefault();
            setOpen(true);
          }
        }}
      >
        {trigger}
      </button>
      {open ? (
        <div
          ref={listRef}
          id={menuId}
          role="menu"
          aria-label={label}
          onKeyDown={onKeyDown}
          className={cn(
            "absolute z-40 flex flex-col gap-0.5 rounded-md bg-white p-2 text-ink shadow-2",
            align === "start" ? "start-0" : "end-0",
            placement === "bottom" ? "top-[calc(100%+6px)]" : "bottom-[calc(100%+6px)]",
          )}
          style={{ minWidth }}
        >
          {header ? <div className="px-2.5 py-2 text-12 font-semibold text-muted">{header}</div> : null}
          {items.map((item) => {
            const radio = item.checked !== undefined;
            const content = (
              <>
                {item.leading ?? (item.icon ? <Icon name={item.icon} size={20} className={item.tone === "danger" ? "text-err" : "text-muted"} /> : null)}
                <span className="flex min-w-0 flex-1 flex-col text-start">
                  <span className={cn("text-14 font-semibold", item.tone === "danger" && "text-err")}>{item.label}</span>
                  {item.description ? <span className="text-12 font-normal text-muted">{item.description}</span> : null}
                </span>
                {radio && item.checked ? <Icon name="check" size={20} className="text-rust-700" /> : null}
              </>
            );
            const common = {
              "data-menu-item": "",
              role: radio ? "menuitemradio" : "menuitem",
              "aria-checked": radio ? item.checked : undefined,
              "aria-disabled": item.disabled || undefined,
              tabIndex: -1,
              className: cn(
                "flex min-h-11 w-full items-center gap-2.5 rounded-sm px-2.5 py-2 text-ink no-underline outline-offset-[-2px] hover:bg-subtle hover:text-ink focus-visible:bg-subtle",
                radio && item.checked && "bg-rust-50 hover:bg-rust-50",
                item.disabled && "cursor-not-allowed opacity-60",
              ),
            } as const;
            if (item.href && !item.disabled) {
              return (
                <Link key={item.key} href={item.href} {...common} onClick={() => close(false)}>
                  {content}
                </Link>
              );
            }
            return (
              <button
                key={item.key}
                type="button"
                {...common}
                onClick={() => {
                  if (item.disabled) return;
                  close(!item.onSelect);
                  item.onSelect?.();
                }}
              >
                {content}
              </button>
            );
          })}
          {footer ? <div className="mx-2.5 mt-1 mb-1.5 border-t border-divider pt-2 text-12 leading-[18px] text-muted">{footer}</div> : null}
        </div>
      ) : null}
    </div>
  );
}
