"use client";

import Link from "next/link";
import { forwardRef, type ButtonHTMLAttributes } from "react";
import { cn } from "@/lib/cn";
import { Icon } from "./Icon";

export interface IconButtonProps extends Omit<ButtonHTMLAttributes<HTMLButtonElement>, "children" | "aria-label"> {
  /** Required accessible name — icon-only buttons are never unlabeled. */
  label: string;
  icon: string;
  /** 36 (inline in fields), 40 (toolbar), 44 (touch / mobile bars). */
  size?: 36 | 40 | 44 | 48;
  variant?: "ghost" | "outline" | "inverse";
  iconSize?: number;
  mirror?: boolean;
  /** Count badge (e.g. unread notifications). The label must already include the count. */
  badge?: number;
  /** Small dot instead of a count (debtor bell). */
  dot?: boolean;
  href?: string;
}

const VARIANT = {
  ghost: "bg-transparent text-ink hover:bg-subtle",
  outline: "bg-white text-ink border border-line hover:bg-subtle",
  inverse: "bg-transparent text-white hover:bg-inv-raised",
};

export const IconButton = forwardRef<HTMLButtonElement, IconButtonProps>(function IconButton(
  { label, icon, size = 40, variant = "ghost", iconSize, mirror, badge, dot, href, className, type = "button", ...rest },
  ref,
) {
  const classes = cn(
    "relative inline-flex flex-none items-center justify-center rounded-sm transition-colors duration-[var(--dur-fast)] disabled:cursor-not-allowed disabled:text-soft",
    VARIANT[variant],
    className,
  );
  const style = { width: size, height: size };
  const inner = (
    <>
      <Icon name={icon} size={iconSize ?? (size >= 44 ? 24 : 20)} mirror={mirror} />
      {badge ? (
        <span
          aria-hidden="true"
          className="absolute -top-1.5 -end-1.5 min-w-[18px] h-[18px] rounded-[9px] bg-rust px-1 text-center font-latin text-11 font-semibold leading-[18px] text-white"
        >
          {badge > 99 ? "99+" : badge}
        </span>
      ) : null}
      {dot ? <span aria-hidden="true" className="absolute top-2.5 end-2.5 size-[9px] rounded-full bg-rust" /> : null}
    </>
  );
  if (href) {
    return (
      <Link href={href} aria-label={label} className={cn(classes, "no-underline")} style={style}>
        {inner}
      </Link>
    );
  }
  return (
    <button ref={ref} type={type} aria-label={label} className={classes} style={style} {...rest}>
      {inner}
    </button>
  );
});
