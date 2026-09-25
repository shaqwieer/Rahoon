import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { getMe } from "@/lib/api/server";
import { safeNext } from "@/lib/api/types";
import { getServerDictionary } from "@/lib/i18n/server";
import { StartFlow } from "./StartFlow";

export async function generateMetadata({ searchParams }: PageProps<"/start">): Promise<Metadata> {
  const sp = await searchParams;
  const { t } = await getServerDictionary();
  return { title: sp.mode === "signin" ? t.individual.start.signInTitle : t.individual.start.registerTitle };
}

/** OR01/OR02 (B13) — individuals register or sign in with national ID/iqama + mobile code. Staff use /login. */
export default async function StartPage({ searchParams }: PageProps<"/start">) {
  const sp = await searchParams;
  const mode = sp.mode === "signin" ? "signin" : "register";
  // Only individual-area destinations are honoured after sign-in.
  const next = safeNext(typeof sp.next === "string" ? sp.next : null, "");
  const me = await getMe().catch(() => null);
  if (me?.authenticated && me.stage === "active" && me.scope === "individual") redirect(next.startsWith("/my") ? next : me.home);
  return <StartFlow mode={mode} next={next.startsWith("/my") ? next : ""} nationalIdState="unavailable" />;
}
