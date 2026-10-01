import type { Metadata } from "next";
import { AccountNav } from "@/components/market/AccountNav";
import { PublicFooter, PublicHeader } from "@/components/shell/Public";
import { apiFetch } from "@/lib/api/server";
import { requireIndividual } from "@/lib/market/server";

export const metadata: Metadata = { title: "حسابي", robots: { index: false, follow: false } };

/** «حسابي» — the owner's and buyer's area (mobile sign-in). The API authorizes every call. */
export default async function AccountLayout({ children }: LayoutProps<"/account">) {
  const me = await requireIndividual();
  const account = await apiFetch("/account").then((r) => (r.ok ? (r.json() as Promise<{ name: string | null; phoneMasked: string }>) : null)).catch(() => null);
  return (
    <div className="flex min-h-dvh flex-col bg-warm">
      <PublicHeader />
      <AccountNav name={account?.name ?? (me.user.name === "عميل رهون" ? null : me.user.name)} phoneMasked={account?.phoneMasked ?? null} />
      <main id="main" tabIndex={-1} className="mx-auto w-full max-w-[1200px] flex-1 px-4 py-6 outline-none md:px-8 md:py-8">
        {children}
      </main>
      <PublicFooter />
    </div>
  );
}
