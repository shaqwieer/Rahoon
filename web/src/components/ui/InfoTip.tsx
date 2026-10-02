"use client";

import { useId, useRef, type ReactNode } from "react";
import { cn } from "@/lib/cn";
import { Icon } from "./Icon";
import { usePopover } from "./usePopover";

/**
 * Extra detail behind a small trigger: opens on hover (pointer devices), on keyboard focus and on tap; closes on Escape or a
 * press outside. The content stays readable for screen readers through the trigger's description while open.
 */
export function InfoTip({ label, children, trigger, className, panelClassName, align = "start" }: {
  /** Accessible name of the trigger, e.g. «تفاصيل الأرقام». */
  label: string;
  children: ReactNode;
  /** Custom trigger content (default: an ⓘ icon). */
  trigger?: ReactNode;
  className?: string;
  panelClassName?: string;
  align?: "start" | "end";
}) {
  const id = useId();
  const { open, setOpen, close, rootRef, triggerRef, panelStyle } = usePopover<HTMLButtonElement>(260);
  const hoverTimer = useRef<number | undefined>(undefined);
  const hover = (on: boolean) => {
    window.clearTimeout(hoverTimer.current);
    hoverTimer.current = window.setTimeout(() => (on ? setOpen(true) : close(false)), on ? 120 : 180);
  };
  return (
    <span ref={rootRef} className={cn("relative inline-flex", className)}
      onPointerEnter={(e) => e.pointerType === "mouse" && hover(true)} onPointerLeave={(e) => e.pointerType === "mouse" && hover(false)}>
      <button ref={triggerRef} type="button" aria-label={trigger ? undefined : label} aria-expanded={open} aria-describedby={open ? id : undefined}
        onClick={(e) => {
          e.preventDefault();
          e.stopPropagation();
          if (open) close(false);
          else setOpen(true);
        }}
        className={cn("relative z-10 inline-flex items-center gap-1 rounded-sm text-muted hover:text-ink focus-visible:outline-2 focus-visible:outline-ink",
          trigger ? "min-h-8 px-1 text-13 font-semibold" : "size-7 justify-center")}>
        {trigger ?? <Icon name="info" size={18} />}
        {trigger ? <span className="sr-only">{label}</span> : null}
      </button>
      {open ? (
        <span id={id} role="tooltip" style={panelStyle({ width: 288, align })}
          className={cn("z-[70] block overflow-y-auto rounded-md border border-line bg-white p-3 text-start text-13 leading-6 text-charcoal shadow-3", panelClassName)}>
          {children}
        </span>
      ) : null}
    </span>
  );
}
