"use client";

import { useEffect, useId, useRef, type CSSProperties, type ReactNode, type RefObject } from "react";
import { cn } from "@/lib/cn";
import { useI18n } from "@/lib/i18n/client";
import { IconButton } from "./IconButton";

interface ModalBaseProps {
  open: boolean;
  /** Called on Esc, the close button, or a backdrop click. Focus returns to the element that opened it. */
  onClose: () => void;
  title: ReactNode;
  /** Hide the visible title (still used as the accessible name). */
  hideTitle?: boolean;
  description?: ReactNode;
  children?: ReactNode;
  footer?: ReactNode;
  /** Element to focus on open (defaults to the first focusable control inside). */
  initialFocusRef?: RefObject<HTMLElement | null>;
  closeOnBackdrop?: boolean;
  className?: string;
}

/**
 * Native <dialog> + showModal(): gives a real focus trap, inert background and Esc handling.
 * We only add focus restoration and backdrop-click dismissal.
 */
function useModalDialog({ open, onClose, initialFocusRef }: Pick<ModalBaseProps, "open" | "onClose" | "initialFocusRef">) {
  const ref = useRef<HTMLDialogElement>(null);
  const returnTo = useRef<HTMLElement | null>(null);
  const onCloseRef = useRef(onClose);

  useEffect(() => {
    onCloseRef.current = onClose;
  }, [onClose]);

  useEffect(() => {
    const dialog = ref.current;
    if (!dialog) return;
    if (open && !dialog.open) {
      returnTo.current = document.activeElement as HTMLElement | null;
      dialog.showModal();
      initialFocusRef?.current?.focus();
    } else if (!open && dialog.open) {
      dialog.close();
    }
  }, [open, initialFocusRef]);

  useEffect(() => {
    const dialog = ref.current;
    if (!dialog) return;
    const onCancel = (e: Event) => {
      e.preventDefault(); // keep React state as the source of truth
      onCloseRef.current();
    };
    const onClosed = () => {
      const target = returnTo.current;
      returnTo.current = null;
      if (target && document.contains(target)) target.focus();
    };
    dialog.addEventListener("cancel", onCancel);
    dialog.addEventListener("close", onClosed);
    return () => {
      dialog.removeEventListener("cancel", onCancel);
      dialog.removeEventListener("close", onClosed);
    };
  }, []);

  return ref;
}

export interface DialogProps extends ModalBaseProps {
  size?: "sm" | "md" | "lg";
  /** Position from the top (command palette uses 96px) instead of vertical centering. */
  top?: number;
}

const WIDTH = { sm: "w-[min(440px,calc(100vw-32px))]", md: "w-[min(560px,calc(100vw-32px))]", lg: "w-[min(720px,calc(100vw-32px))]" };

export function Dialog({ open, onClose, title, hideTitle, description, children, footer, initialFocusRef, closeOnBackdrop = true, size = "md", top, className }: DialogProps) {
  const ref = useModalDialog({ open, onClose, initialFocusRef });
  const titleId = useId();
  const descId = useId();
  const { t } = useI18n();
  return (
    <dialog
      ref={ref}
      aria-labelledby={titleId}
      aria-describedby={description ? descId : undefined}
      onClick={(e) => {
        if (closeOnBackdrop && e.target === e.currentTarget) onClose();
      }}
      className={cn(
        "m-auto max-h-[calc(100dvh-32px)] overflow-visible rounded-lg bg-white p-0 text-ink shadow-3 backdrop:bg-ink/40",
        WIDTH[size],
        top !== undefined && "mt-[var(--dlg-top)]",
        className,
      )}
      style={top !== undefined ? ({ "--dlg-top": `${top}px` } as CSSProperties) : undefined}
    >
      {open ? (
        <div className="flex max-h-[calc(100dvh-32px)] flex-col">
          <div className={cn("flex items-center gap-3 border-b border-divider px-5 py-4", hideTitle && "sr-only")}>
            <h2 id={titleId} className="m-0 flex-1 text-19 font-bold">
              {title}
            </h2>
            <IconButton label={t.dialog.close} icon="close" size={40} onClick={onClose} />
          </div>
          {description ? (
            <p id={descId} className="m-0 px-5 pt-4 text-14 leading-[22px] text-muted">
              {description}
            </p>
          ) : null}
          <div className="min-h-0 flex-1 overflow-y-auto">{children}</div>
          {footer ? <div className="flex flex-wrap gap-2.5 border-t border-divider px-5 py-3.5">{footer}</div> : null}
        </div>
      ) : null}
    </dialog>
  );
}

export interface DrawerProps extends ModalBaseProps {
  /** `end` = side panel on the inline-end edge; `bottom` = mobile sheet. */
  placement?: "end" | "bottom";
}

/** Side panel / bottom sheet with the same focus trap and Esc behaviour as Dialog. */
export function Drawer({ open, onClose, title, hideTitle, description, children, footer, initialFocusRef, closeOnBackdrop = true, placement = "end", className }: DrawerProps) {
  const ref = useModalDialog({ open, onClose, initialFocusRef });
  const titleId = useId();
  const { t } = useI18n();
  return (
    <dialog
      ref={ref}
      aria-labelledby={titleId}
      onClick={(e) => {
        if (closeOnBackdrop && e.target === e.currentTarget) onClose();
      }}
      className={cn(
        "max-h-none max-w-none bg-white p-0 text-ink shadow-3 backdrop:bg-ink/40",
        placement === "end"
          ? "ms-auto me-0 mt-0 mb-0 h-dvh w-[min(420px,100vw)]"
          : "mx-0 mt-auto mb-0 max-h-[85dvh] w-screen rounded-t-lg",
        className,
      )}
    >
      {open ? (
        <div className={cn("flex flex-col", placement === "end" ? "h-full" : "max-h-[85dvh]")}>
          <div className={cn("flex items-center gap-3 border-b border-divider px-5 py-3", hideTitle && "sr-only")}>
            <h2 id={titleId} className="m-0 flex-1 text-17 font-bold">
              {title}
            </h2>
            <IconButton label={t.dialog.close} icon="close" size={44} onClick={onClose} />
          </div>
          {description ? <p className="m-0 px-5 pt-3 text-14 text-muted">{description}</p> : null}
          <div className="min-h-0 flex-1 overflow-y-auto">{children}</div>
          {footer ? <div className="flex gap-2.5 border-t border-divider px-5 py-3.5">{footer}</div> : null}
        </div>
      ) : null}
    </dialog>
  );
}
