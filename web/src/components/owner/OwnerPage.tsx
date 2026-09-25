import Link from "next/link";
import type { ReactNode } from "react";
import { OwnerShell } from "@/components/shell/OwnerShell";
import type { OwnerNavKey } from "@/components/shell/nav";
import { Icon } from "@/components/ui/Icon";
import { cn } from "@/lib/cn";
import type { OwnerShellData } from "./server";

export interface OwnerScreenProps {
  shell: OwnerShellData;
  /** DebtorTop title (mobile) and the page h1. */
  title: ReactNode;
  sub?: ReactNode;
  /** Back target; shows the back button on mobile and a back link above the h1 on desktop. */
  backHref?: string;
  backLabel?: string;
  /** Focused flows (D07–D09, D12, D13) hide the bottom nav. */
  hideNav?: boolean;
  active: OwnerNavKey | null;
  /**
   * `auto`: the title is the page h1 — visually hidden on mobile (DebtorTop shows it), visible on desktop where
   * DebtorTop is hidden. `none`: the page renders its own visible h1 (D09 success, D14).
   */
  heading?: "auto" | "none";
  /** `column` = 560px reading column on tablet/desktop; `wide` = the shell's 1040px grid (D02). */
  width?: "column" | "wide";
  children: ReactNode;
}

/** Page heading: sr-only h1 on mobile, back link + visible h1 + sub from 1024px (DebtorTop is mobile-only). */
export function OwnerHeading({ title, sub, backHref, backLabel, visible = false }: { title: ReactNode; sub?: ReactNode; backHref?: string; backLabel?: string; visible?: boolean }) {
  return (
    <div className={cn("flex flex-col gap-1", visible ? "mb-2" : "lg:mb-3")}>
      {backHref ? (
        <Link href={backHref} className="hidden min-h-11 items-center gap-1 self-start text-15 font-semibold lg:inline-flex print:hidden">
          <Icon name="arrow_forward" size={20} mirror />
          {backLabel}
        </Link>
      ) : null}
      <h1 className={cn("m-0 font-bold", visible ? "text-26 leading-[38px]" : "sr-only lg:not-sr-only lg:text-28 lg:leading-10")}>{title}</h1>
      {sub ? <p className="m-0 hidden text-16 text-muted lg:block">{sub}</p> : null}
    </div>
  );
}

/** Server-side owner screen: OwnerShell with per-screen DebtorTop title/sub/back/nav + the page heading. */
export function OwnerPage({ shell, title, sub, backHref, backLabel, hideNav, active, heading = "auto", width = "column", children }: OwnerScreenProps) {
  return (
    <OwnerShell
      title={title}
      sub={sub}
      userLabel={shell.userLabel}
      unread={shell.unread}
      numerals={shell.numerals}
      back={backHref ? { href: backHref } : undefined}
      hideNav={hideNav}
      active={active}
    >
      <div className={cn("flex w-full flex-col gap-3", width === "column" && "mx-auto max-w-[560px]")}>
        {heading === "auto" ? <OwnerHeading title={title} sub={sub} backHref={backHref} backLabel={backLabel} /> : null}
        {children}
      </div>
    </OwnerShell>
  );
}
