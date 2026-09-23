import type { Metadata } from "next";
import Link from "next/link";
import { EmptyState, buttonClasses } from "@/components/ui";
import { apiGet, getMe } from "@/lib/api/server";
import type { BreachData } from "@/lib/api/lender";
import { BreachView } from "./BreachView";

export const metadata: Metadata = { title: "مراجعة الإخلال" };

/**
 * L21 — Breach review: two consecutive missed installments open a review with a cure period. The public case
 * state stays «تسوية نشطة»; there is never an automatic referral. Shows the latest review, preferring an open one.
 */
export default async function BreachPage({ params }: PageProps<"/cases/[ref]/payments/breach">) {
  const { ref } = await params;
  const [d, me] = await Promise.all([apiGet<BreachData>(`/cases/${encodeURIComponent(ref)}/breach`), getMe()]);
  const review = d.reviews.find((r) => r.status === "Open") ?? d.reviews[0] ?? null;
  if (!review) {
    return (
      <EmptyState
        icon="verified"
        title="لا توجد مراجعة إخلال"
        body="تُفتح مراجعة الإخلال تلقائياً عند تأخر قسطين متتاليين حسب شرط الاتفاق، مع مهلة تصحيح وإشعار داعم للمالك."
        action={<Link href={`/cases/${ref}/payments`} className={buttonClasses({ variant: "secondary" })}>جدول السداد</Link>}
      />
    );
  }
  const perms = me.authenticated ? me.permissions : [];
  return (
    <BreachView
      reference={ref}
      d={d}
      review={review}
      canDecide={perms.includes("breach.manage")}
      canContact={perms.includes("comms.send")}
    />
  );
}
