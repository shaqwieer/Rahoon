"use client";

import Link from "next/link";
import type { ReactNode } from "react";
import { buttonClasses } from "@/components/ui/buttonStyles";
import { Logo } from "@/components/ui/Logo";
import { Menu } from "@/components/ui/Menu";
import { SkipLink } from "@/components/ui/SkipLink";
import { cn } from "@/lib/cn";
import { useI18n } from "@/lib/i18n/client";
import { LocaleSwitch } from "./LocaleSwitch";

export interface PublicHeaderProps {
  /** `full` landing header · `back` minimal with «العودة للرئيسية» (S02) · `logo` logo only (S05/S12). */
  variant?: "full" | "back" | "logo";
}

/** B2 public header: 80px desktop (padding 0 80), 60px mobile with a menu popover. */
export function PublicHeader({ variant = "full" }: PublicHeaderProps) {
  const { t } = useI18n();
  // Individual-first (B13): the person in default is the audience; institutions get a secondary link.
  const N = t.individual.nav;
  const links = [
    { key: "how", label: N.how, href: "/#how" },
    { key: "paths", label: N.paths, href: "/#paths" },
    { key: "rights", label: N.rights, href: "/privacy" },
    { key: "lenders", label: N.lenders, href: "/#lenders" },
  ];
  return (
    <>
      <SkipLink />
      <header className="flex h-[60px] items-center gap-8 border-b border-line bg-white ps-4 pe-2 md:h-20 md:px-8 xl:px-20">
        <Link href="/" className="inline-flex flex-none rounded-xs">
          <Logo variant="horizontal" width={176} alt={t.brand.homeAlt} priority className="max-md:!h-auto max-md:!w-[172px]" />
        </Link>
        {variant === "full" ? (
          <>
            <nav aria-label={t.public.nav} className="hidden lg:block">
              <ul className="m-0 flex list-none gap-7 p-0 text-15 font-medium">
                {links.map((l) => (
                  <li key={l.key}>
                    <Link href={l.href} className="text-ink no-underline hover:underline">
                      {l.label}
                    </Link>
                  </li>
                ))}
              </ul>
            </nav>
            <div className="ms-auto hidden items-center gap-2.5 md:flex">
              <LocaleSwitch variant="link" />
              <Link href="/start?mode=signin" className={buttonClasses({ variant: "secondary", size: "lg", className: "min-h-11 text-15" })}>
                {N.signIn}
              </Link>
              <Link href="/start" className={buttonClasses({ variant: "primary", size: "lg", className: "min-h-11 px-[18px] text-15" })}>
                {N.start}
              </Link>
            </div>
            <div className="ms-auto flex items-center gap-1 md:hidden">
              {/* B13 mobile header: «دخول» beside the menu. */}
              <Link href="/start?mode=signin" className="flex min-h-11 items-center px-2 text-15 font-semibold">
                {N.signInShort}
              </Link>
              <Menu
                label={t.common.menu}
                align="end"
                triggerLabel={t.common.menu}
                triggerClassName="inline-flex size-11 items-center justify-center rounded-sm hover:bg-subtle"
                trigger={<span className="ms text-[24px]" aria-hidden="true">menu</span>}
                items={[
                  ...links.map((l) => ({ key: l.key, label: l.label, href: l.href })),
                  { key: "signin", label: N.signIn, href: "/start?mode=signin", icon: "login" },
                  { key: "start", label: N.start, href: "/start", icon: "arrow_back" },
                ]}
                footer={<LocaleSwitch variant="link" className="px-0" />}
              />
            </div>
          </>
        ) : variant === "back" ? (
          <Link href="/" className="ms-auto text-15 font-semibold">
            {t.public.backHome}
          </Link>
        ) : null}
      </header>
    </>
  );
}

/** B2 dark footer: dark logo, legal links, copyright. */
export function PublicFooter() {
  const { t } = useI18n();
  return (
    <footer className="surface-dark flex flex-col gap-6 bg-inv px-5 py-10 text-white md:flex-row md:items-center md:gap-10 xl:px-20">
      <Logo variant="horizontal-dark" width={176} alt={t.brand.name} />
      <nav aria-label={t.public.footerNav}>
        <ul className="m-0 flex list-none flex-wrap gap-x-6 gap-y-2 p-0 text-14">
          {[
            { k: "privacy", l: t.public.privacy, href: "/privacy" },
            { k: "terms", l: t.public.terms, href: "/terms" },
            { k: "staff", l: t.individual.landing.staffLogin, href: "/login" },
          ].map((x) => (
            <li key={x.k}>
              <Link href={x.href} className="text-white hover:text-white">
                {x.l}
              </Link>
            </li>
          ))}
        </ul>
      </nav>
      <span className="text-13 text-inv-2 md:ms-auto">
        {t.individual.landing.footerStatement} {t.public.copyright}
      </span>
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
    <div className="grid min-h-dvh grid-cols-1 bg-warm lg:grid-cols-[minmax(0,1fr)_560px]">
      <SkipLink />
      <main id="main" tabIndex={-1} className="flex items-center justify-center px-5 py-10 outline-none md:p-12">
        <div className="w-full max-w-[420px]">{children}</div>
      </main>
      <aside className={cn("surface-dark hidden flex-col justify-end gap-5 bg-inv px-14 py-16 text-white lg:flex")}>
        {locale === "ar" ? <Logo variant="app-icon" width={72} alt="" /> : null}
        <p className="m-0 text-24 leading-[38px] font-semibold">{quote}</p>
        <span className="text-14 leading-[22px] text-inv-2">{note}</span>
      </aside>
    </div>
  );
}
