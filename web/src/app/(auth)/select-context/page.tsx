import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { Logo } from "@/components/ui/Logo";
import { SkipLink } from "@/components/ui/SkipLink";
import { getMe, loginUrl } from "@/lib/api/server";
import { safeNext } from "@/lib/api/types";
import { getServerDictionary } from "@/lib/i18n/server";
import { ContextList } from "./ContextList";

export async function generateMetadata(): Promise<Metadata> {
  const { t } = await getServerDictionary();
  return { title: t.auth.selectContext.title };
}

/** S06 — choose one organization/role for this session (C12: switching re-creates the session). */
export default async function SelectContextPage({ searchParams }: PageProps<"/select-context">) {
  const sp = await searchParams;
  const next = safeNext(typeof sp.next === "string" ? sp.next : null, "");
  const me = await getMe();
  if (!me.authenticated) redirect(loginUrl("/select-context"));
  if (me.stage === "mfapending") redirect("/login/mfa");
  if (me.scope === "owner") redirect("/owner");
  const { t } = await getServerDictionary();
  const S = t.auth.selectContext;

  return (
    <div className="flex min-h-dvh flex-col items-center gap-6 bg-warm px-5 py-12 md:px-12 md:py-16">
      <SkipLink />
      <Logo variant="horizontal" width={176} alt={t.brand.name} priority />
      <main id="main" tabIndex={-1} className="flex w-full max-w-[560px] flex-col gap-4 outline-none">
        <h1 className="m-0 text-28 leading-10 font-bold">{S.title}</h1>
        <p className="m-0 text-15 leading-6 text-muted">{S.body}</p>
        <ContextList memberships={me.memberships} next={next} />
      </main>
    </div>
  );
}
