"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useSessionActions } from "@/components/shell/session";
import { Icon } from "@/components/ui/Icon";
import { cn } from "@/lib/cn";

const ITEMS = [
  { href: "/account", label: "نظرة عامة", icon: "dashboard", exact: true },
  { href: "/account/sell", label: "طلبات البيع", icon: "sell" },
  { href: "/account/buy", label: "طلب الشراء", icon: "travel_explore" },
  { href: "/account/interests", label: "اهتماماتي", icon: "handshake" },
  { href: "/account/saved", label: "المحفوظة", icon: "favorite" },
  { href: "/account/searches", label: "عمليات البحث", icon: "saved_search" },
];

/** «حسابي» sub-navigation: one account for selling and buying. Scrolls horizontally on phones. */
export function AccountNav({ name, phoneMasked }: { name: string | null; phoneMasked: string | null }) {
  const pathname = usePathname() ?? "/account";
  const { logout, busy } = useSessionActions();
  return (
    <div className="border-b border-line bg-white">
      <div className="mx-auto flex max-w-[1200px] flex-col gap-3 px-4 pt-5 md:px-8">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex items-center gap-3">
            <span className="flex size-11 items-center justify-center rounded-full bg-rust-50 text-rust">
              <Icon name="person" size={24} />
            </span>
            <div className="flex flex-col">
              <strong className="text-17">{name ?? "حسابي"}</strong>
              {phoneMasked ? (
                <bdi dir="ltr" className="text-13 text-muted">
                  {phoneMasked}
                </bdi>
              ) : null}
            </div>
          </div>
          <button type="button" onClick={logout} aria-busy={busy === "logout" || undefined}
            className="inline-flex min-h-10 items-center gap-1.5 rounded-sm px-3 text-14 text-err hover:bg-err-bg">
            <Icon name="logout" size={18} />
            خروج
          </button>
        </div>
        <nav aria-label="أقسام حسابي" className="-mx-4 overflow-x-auto px-4 md:mx-0 md:px-0">
          <ul className="m-0 flex min-w-max list-none gap-1 p-0">
            {ITEMS.map((i) => {
              const on = i.exact ? pathname === i.href : pathname === i.href || pathname.startsWith(`${i.href}/`);
              return (
                <li key={i.href}>
                  <Link href={i.href} aria-current={on ? "page" : undefined}
                    className={cn("inline-flex min-h-11 items-center gap-1.5 px-3 text-14 no-underline", on ? "bar-bottom font-bold text-rust-700" : "font-medium text-charcoal hover:text-ink")}>
                    <Icon name={i.icon} size={18} />
                    {i.label}
                  </Link>
                </li>
              );
            })}
          </ul>
        </nav>
      </div>
    </div>
  );
}
