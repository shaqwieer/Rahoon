import Link from "next/link";
import { AccountClient } from "@/components/market/AccountClient";
import { Badge, Card, StatusBadge } from "@/components/market/ui";
import { buttonClasses } from "@/components/ui/buttonStyles";
import { Icon } from "@/components/ui/Icon";
import { apiGet } from "@/lib/api/server";
import { day } from "@/lib/market/format";
import type { AccountSummary } from "@/lib/market/types";

/** Account overview: sale requests, the buyer request, interests and in-app notifications (the events visible to the person). */
export default async function AccountPage() {
  const [s, account] = await Promise.all([apiGet<AccountSummary>("/market/me"), apiGet<{ name: string | null; phoneVerification: string }>("/account")]);
  return (
    <div className="flex flex-col gap-6">
      <AccountClient needsName={!account.name} unread={s.unread} smsVerified={account.phoneVerification === "sms_code"} />
      <div className="grid gap-6 lg:grid-cols-[minmax(0,1.4fr)_minmax(0,1fr)]">
        <div className="flex flex-col gap-6">
          <Card title="طلبات البيع" actions={<Link href="/sell/new" className={buttonClasses({ variant: "primary", size: "md" })}>طلب بيع جديد</Link>}>
            {s.saleRequests.length === 0 ? (
              <p className="m-0 text-15 text-muted">لا توجد طلبات بيع بعد. ابدأ بطلب بسيط من ثلاث خطوات.</p>
            ) : (
              <ul className="m-0 flex list-none flex-col gap-3 p-0">
                {s.saleRequests.map((r) => (
                  <li key={r.reference}>
                    <Link href={r.status === "draft" ? "/sell/new" : `/account/sell/${r.reference}`}
                      className="flex flex-col gap-2 rounded-md border border-line p-4 text-ink no-underline transition-colors hover:border-rust-200 hover:bg-rust-50/40">
                      <span className="flex flex-wrap items-center justify-between gap-2">
                        <strong className="text-16">
                          {r.propertyTypeLabel ?? "طلب بيع"}
                          {r.cityLabel ? ` · ${r.cityLabel}` : ""}
                          {r.district ? `، ${r.district}` : ""}
                        </strong>
                        <StatusBadge status={r.status} label={r.statusLabel} />
                      </span>
                      <span className="text-14 leading-6 text-charcoal">{r.nextStep}</span>
                      <span className="flex flex-wrap items-center gap-3 text-12 text-muted">
                        <bdi dir="ltr" className="font-mono">{r.reference}</bdi>
                        <span>آخر تحديث {day(r.updatedAt)}</span>
                      </span>
                      {r.opportunity?.awaitingYou ? (
                        <Badge tone="rust" icon="pending_actions">ملخص الفرصة بانتظار تأكيدك</Badge>
                      ) : r.opportunity ? (
                        <Badge tone="info">الفرصة: {r.opportunity.statusLabel}</Badge>
                      ) : null}
                    </Link>
                  </li>
                ))}
              </ul>
            )}
          </Card>
          <Card title="طلب الشراء">
            {s.buyerRequest ? (
              <Link href="/account/buy" className="flex flex-col gap-2 rounded-md border border-line p-4 text-ink no-underline hover:border-rust-200">
                <span className="flex items-center justify-between gap-2">
                  <bdi dir="ltr" className="font-mono text-14">{s.buyerRequest.reference}</bdi>
                  <StatusBadge status={s.buyerRequest.status} label={s.buyerRequest.statusLabel} />
                </span>
                <span className="text-14 text-charcoal">{s.buyerRequest.nextStep}</span>
              </Link>
            ) : (
              <div className="flex flex-col items-start gap-3">
                <p className="m-0 text-15 text-muted">حدد قدرتك الشرائية وتفضيلاتك ليقترح عليك الفريق الفرص المناسبة.</p>
                <Link href="/buy/new" className={buttonClasses({ variant: "secondary", size: "md" })}>سجّل قدرتك الشرائية</Link>
              </div>
            )}
            <div className="mt-4 grid grid-cols-2 gap-3">
              <Link href="/account/interests" className="flex items-center gap-2 rounded-md bg-subtle p-3 text-ink no-underline">
                <Icon name="handshake" size={20} className="text-rust" />
                <span className="text-14">اهتماماتي <strong>({s.interests})</strong></span>
              </Link>
              <Link href="/account/saved" className="flex items-center gap-2 rounded-md bg-subtle p-3 text-ink no-underline">
                <Icon name="favorite" size={20} className="text-rust" />
                <span className="text-14">المحفوظة <strong>({s.saved})</strong></span>
              </Link>
            </div>
          </Card>
        </div>
        <Card title={<span className="flex items-center gap-2">التنبيهات {s.unread > 0 ? <Badge tone="rust">{s.unread} جديدة</Badge> : null}</span>}>
          {s.notifications.length === 0 ? (
            <p className="m-0 text-14 text-muted">لا توجد تنبيهات بعد.</p>
          ) : (
            <ul className="m-0 flex list-none flex-col gap-1 p-0">
              {s.notifications.map((n) => (
                <li key={n.id}>
                  <Link href={n.link} className="flex gap-3 rounded-sm p-2.5 text-ink no-underline hover:bg-subtle">
                    <span className={n.read ? "mt-2 size-2 flex-none rounded-full bg-line" : "mt-2 size-2 flex-none rounded-full bg-orange"} aria-hidden="true" />
                    <span className="flex min-w-0 flex-col gap-0.5">
                      <span className={n.read ? "text-14" : "text-14 font-bold"}>{n.title}</span>
                      {n.reason ? <span className="text-13 text-charcoal">السبب: {n.reason}</span> : n.body ? <span className="line-clamp-2 text-13 text-charcoal">{n.body}</span> : null}
                      <span className="text-12 text-muted"><bdi dir="ltr">{day(n.at)}</bdi></span>
                    </span>
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </Card>
      </div>
    </div>
  );
}
