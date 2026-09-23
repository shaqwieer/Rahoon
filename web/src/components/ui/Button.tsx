"use client";

import Link from "next/link";
import type { ButtonHTMLAttributes, ReactNode } from "react";
import { cn } from "@/lib/cn";
import { buttonClasses, type ButtonSize, type ButtonVariant } from "./buttonStyles";
import { Icon } from "./Icon";

export { buttonClasses, type ButtonSize, type ButtonVariant } from "./buttonStyles";

export interface ButtonProps extends Omit<ButtonHTMLAttributes<HTMLButtonElement>, "children"> {
  variant?: ButtonVariant;
  /** md = 40 (institutional), lg = 48 (owner / mobile), xl = 54 (owner primary), sm = 36 (inline rows). */
  size?: ButtonSize;
  children: ReactNode;
  /** Leading icon (precedes the label in reading order). */
  icon?: string;
  /** Trailing icon; pass `iconEndMirror` for directional arrows. */
  iconEnd?: string;
  iconEndMirror?: boolean;
  /** Keeps the button width, shows progress_activity + `loadingLabel`, sets aria-busy. */
  loading?: boolean;
  loadingLabel?: ReactNode;
  /** «…» convention: the button opens a review screen instead of acting immediately. */
  review?: boolean;
  fullWidth?: boolean;
  /**
   * Disabled but still focusable (aria-disabled) so a linked reason (aria-describedby) stays reachable.
   * Clicks are swallowed.
   */
  softDisabled?: boolean;
  /** Renders a next/link styled as a button (ignored while disabled). */
  href?: string;
  /** For `href`: open with a full document load (e.g. context switch). */
  hardNavigation?: boolean;
}

/** Appends «…» once for review-screen buttons. */
function withEllipsis(children: ReactNode, review?: boolean) {
  if (!review) return children;
  if (typeof children === "string") return children.endsWith("…") ? children : `${children}…`;
  return (
    <>
      {children}…
    </>
  );
}

export function Button({
  variant = "primary",
  size = "md",
  children,
  icon,
  iconEnd,
  iconEndMirror,
  loading,
  loadingLabel,
  review,
  fullWidth,
  softDisabled,
  href,
  hardNavigation,
  disabled,
  className,
  type = "button",
  onClick,
  ...rest
}: ButtonProps) {
  const isDisabled = Boolean(disabled || softDisabled);
  const iconSize = size === "xl" ? 22 : 18;
  const label = (
    <span className="inline-flex items-center justify-center gap-1.5">
      {icon ? <Icon name={icon} size={iconSize} /> : null}
      <span>{withEllipsis(children, review)}</span>
      {iconEnd ? <Icon name={iconEnd} size={iconSize} mirror={iconEndMirror} /> : null}
    </span>
  );
  // Both layers share one grid cell so the button keeps the widest width while loading.
  const content = (
    <>
      <span className={cn("col-start-1 row-start-1", loading && "invisible")}>{label}</span>
      {loading ? (
        <span className="col-start-1 row-start-1 inline-flex items-center justify-center gap-1.5">
          <Icon name="progress_activity" size={iconSize} className="animate-rh-spin" />
          <span>{loadingLabel ?? children}</span>
        </span>
      ) : null}
    </>
  );
  const classes = buttonClasses({ variant, size, disabled: isDisabled && !loading, fullWidth, className });

  if (href && !isDisabled && !loading) {
    if (hardNavigation) {
      return (
        <a href={href} className={classes}>
          {content}
        </a>
      );
    }
    return (
      <Link href={href} className={classes}>
        {content}
      </Link>
    );
  }

  return (
    <button
      type={type}
      className={classes}
      disabled={disabled}
      aria-disabled={softDisabled || undefined}
      aria-busy={loading || undefined}
      onClick={(e) => {
        if (softDisabled || loading) {
          e.preventDefault();
          return;
        }
        onClick?.(e);
      }}
      {...rest}
    >
      {content}
    </button>
  );
}
