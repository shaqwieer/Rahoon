import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { getMe } from "@/lib/api/server";
import { safeNext } from "@/lib/api/types";
import { PublicPage } from "../PublicPage";
import { SignInForm } from "./SignInForm";

export const metadata: Metadata = { title: "الدخول إلى حسابي", robots: { index: false, follow: false } };

/** Owners and buyers sign in with their mobile. The Rahoon team uses /login. */
export default async function SignInPage({ searchParams }: PageProps<"/signin">) {
  const sp = await searchParams;
  const next = safeNext(typeof sp.next === "string" ? sp.next : null, "/account");
  const me = await getMe().catch(() => null);
  if (me?.authenticated && me.stage === "active" && me.scope === "individual") redirect(next);
  return (
    <PublicPage centered title="الدخول إلى حسابي" lead="حساب واحد لكل رقم جوال: تتابع منه طلبات البيع وطلب الشراء واهتماماتك.">
      <div className="rounded-lg border border-line bg-white p-5 text-start shadow-2 md:p-7">
        <SignInForm next={next} />
      </div>
      <p className="mt-5 text-center text-14 text-muted">
        من فريق رهون؟ <a href="/login">دخول الفريق</a>
      </p>
    </PublicPage>
  );
}
