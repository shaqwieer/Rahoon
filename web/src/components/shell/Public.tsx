"use client";

import Image from "next/image";
import Link from "next/link";
import type { ReactNode } from "react";
import { buttonClasses } from "@/components/ui/buttonStyles";
import { Logo } from "@/components/ui/Logo";
import { Menu } from "@/components/ui/Menu";
import { SkipLink } from "@/components/ui/SkipLink";
import { usePathname } from "next/navigation";
import { M } from "@/components/market/copy";
import { Icon } from "@/components/ui/Icon";
import { cn } from "@/lib/cn";
import { useI18n } from "@/lib/i18n/client";
import staffLoginImage from "../../../public/images/rahoon-staff-login.png";

export interface PublicHeaderProps {
  /** `full` site header · `back` minimal with «العودة للرئيسية» · `logo` logo only. */
  variant?: "full" | "back" | "logo";
}

const NAV = [
  { key: "home", label: M.nav.home, href: "/" },
  { key: "sell", label: M.nav.sell, href: "/sell" },
  { key: "opportunities", label: M.nav.opportunities, href: "/opportunities" },
  { key: "how", label: M.nav.how, href: "/how-it-works" },
  { key: "calculators", label: M.nav.calculators, href: "/calculators" },
  { key: "contact", label: M.nav.contact, href: "/contact" },
];

function isActive(pathname: string, href: string) {
  return href === "/" ? pathname === "/" : pathname === href || pathname.startsWith(`${href}/`);
}

/** Public site header (2026-10-01 model): 64px mobile with a menu, 76px desktop with the full navigation. */
export function PublicHeader({ variant = "full" }: PublicHeaderProps) {
  const pathname = usePathname() ?? "/";
  return (
    <>
      <SkipLink />
      <header className="sticky top-0 z-30 flex h-16 items-center gap-6 border-b border-line bg-white/95 ps-4 pe-2 backdrop-blur md:h-[76px] md:px-8 xl:px-14">
        <Link href="/" className="inline-flex flex-none rounded-xs">
          <Logo variant="horizontal" width={170} alt={M.brand} priority className="max-md:!h-auto max-md:!w-[150px]" />
        </Link>
        {variant === "full" ? (
          <>
            <nav aria-label={M.nav.label} className="hidden xl:block">
              <ul className="m-0 flex list-none gap-1 p-0 text-15 font-medium">
                {NAV.map((l) => {
                  const active = isActive(pathname, l.href);
                  return (
                    <li key={l.key}>
                      <Link
                        href={l.href}
                        aria-current={active ? "page" : undefined}
                        className={cn(
                          "inline-flex min-h-11 items-center rounded-sm px-3 text-ink no-underline transition-colors duration-150 hover:bg-subtle",
                          active && "bg-rust-50 font-semibold text-rust hover:bg-rust-50",
                        )}
                      >
                        {l.label}
                      </Link>
                    </li>
                  );
                })}
              </ul>
            </nav>
            <div className="ms-auto hidden items-center gap-2 md:flex">
              <Link href="/account" className={buttonClasses({ variant: "secondary", size: "lg", className: "min-h-11 text-15" })}>
                <Icon name="person" size={20} />
                {M.nav.account}
              </Link>
              <Link href="/sell/new" className={buttonClasses({ variant: "primary", size: "lg", className: "min-h-11 px-[18px] text-15" })}>
                {M.nav.startSell}
              </Link>
            </div>
            <div className="ms-auto flex items-center gap-1 xl:hidden md:ms-0">
              <Link href="/account" className="flex min-h-11 items-center px-2 text-15 font-semibold md:hidden">
                {M.nav.account}
              </Link>
              <Menu
                label={M.nav.menu}
                align="end"
                triggerLabel={M.nav.menu}
                triggerClassName="inline-flex size-11 items-center justify-center rounded-sm hover:bg-subtle"
                trigger={<span className="ms text-[24px]" aria-hidden="true">menu</span>}
                items={[
                  ...NAV.map((l) => ({ key: l.key, label: l.label, href: l.href })),
                  { key: "account", label: M.nav.account, href: "/account", icon: "person" },
                  { key: "start", label: M.nav.startSell, href: "/sell/new", icon: "arrow_back" },
                ]}
              />
            </div>
          </>
        ) : variant === "back" ? (
          <Link href="/" className="ms-auto text-15 font-semibold">
            العودة للرئيسية
          </Link>
        ) : null}
      </header>
    </>
  );
}

/** Dark footer: logo, statement (no promises), site and legal links, team sign-in. */
export function PublicFooter() {
  return (
    <footer className="surface-dark bg-inv px-5 py-10 text-white md:px-10 xl:px-20">
      <div className="mx-auto flex max-w-[1440px] flex-col gap-8 md:flex-row md:items-start md:justify-between">
        <div className="flex max-w-[30em] flex-col gap-4">
          <Logo variant="horizontal-dark" width={170} alt={M.brand} />
          <p className="m-0 text-14 leading-6 text-inv-2">{M.footer.statement}</p>
        </div>
        <nav aria-label={M.footer.nav}>
          <ul className="m-0 grid list-none grid-cols-2 gap-x-10 gap-y-2.5 p-0 text-14">
            {[...NAV, { key: "privacy", label: M.footer.privacy, href: "/privacy" }, { key: "terms", label: M.footer.terms, href: "/terms" }, { key: "team", label: M.footer.team, href: "/login" }].map((x) => (
              <li key={x.key}>
                <Link href={x.href} className="text-white hover:text-white">
                  {x.label}
                </Link>
              </li>
            ))}
          </ul>
        </nav>
      </div>
      <div className="mx-auto mt-8 max-w-[1440px] border-t border-inv-line pt-5 text-13 text-inv-2">{M.footer.copyright}</div>
    </footer>
  );
}

export interface AuthSplitProps {
  children: ReactNode;
  /** Dark aside quote / footnote (S03). The app icon appears in Arabic only, per design. */
  quote: string;
  note: string;
}

/** S03 login split: form column + 560px dark aside (hidden below 1024 — only the form shows). */
export function AuthSplit({ children, quote, note }: AuthSplitProps) {
  const { locale } = useI18n();
  return (
    <div className="grid min-h-dvh grid-cols-1 bg-warm lg:grid-cols-[minmax(0,0.92fr)_minmax(520px,1.08fr)]">
      <SkipLink />
      <main id="main" tabIndex={-1} className="relative flex items-center justify-center px-5 py-10 outline-none md:p-12 lg:px-16">
        <div className="absolute inset-x-0 top-0 h-40 bg-[radial-gradient(circle_at_50%_0%,rgba(244,99,58,0.1),transparent_70%)]" />
        <div className="relative w-full max-w-[420px]">{children}</div>
      </main>
      <aside className={cn("surface-dark relative hidden min-h-dvh overflow-hidden text-white lg:block")}>
        <Image src={staffLoginImage} alt="" fill priority sizes="(min-width: 1024px) 55vw" className="object-cover object-center" />
        <div className="absolute inset-0 bg-gradient-to-t from-ink via-ink/40 to-ink/20" />
        <div className="absolute inset-x-0 bottom-0 flex flex-col gap-5 p-12 xl:p-16">
          {locale === "ar" ? <Logo variant="app-icon" width={64} alt="" /> : null}
          <p className="m-0 max-w-[22em] text-24 leading-[38px] font-semibold text-pretty">{quote}</p>
          <span className="flex items-center gap-2 text-14 leading-[22px] text-white/75">
            <span className="ms text-[18px] text-orange" aria-hidden="true">verified_user</span>
            {note}
          </span>
        </div>
      </aside>
    </div>
  );
}
