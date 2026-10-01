import Link from "next/link";
import { Card, StatusBadge } from "@/components/market/ui";
import { buttonClasses } from "@/components/ui/buttonStyles";
import { apiGet } from "@/lib/api/server";
import { day } from "@/lib/market/format";
import type { AccountSummary } from "@/lib/market/types";

export default async function MySaleRequestsPage() {
  const s = await apiGet<AccountSummary>("/market/me");
  return (
    <Card title="طلبات البيع" actions={<Link href="/sell/new" className={buttonClasses({ variant: "primary", size: "md" })}>طلب بيع جديد</Link>}>
      {s.saleRequests.length === 0 ? (
        <p className="m-0 text-15 text-muted">لا توجد طلبات بيع بعد.</p>
      ) : (
        <ul className="m-0 flex list-none flex-col gap-3 p-0">
          {s.saleRequests.map((r) => (
            <li key={r.reference}>
              <Link href={`/account/sell/${r.reference}`} className="flex flex-col gap-1.5 rounded-md border border-line p-4 text-ink no-underline hover:border-rust-200">
                <span className="flex flex-wrap items-center justify-between gap-2">
                  <strong>{r.propertyTypeLabel ?? "طلب بيع"} {r.cityLabel ? `· ${r.cityLabel}` : ""}</strong>
                  <StatusBadge status={r.status} label={r.statusLabel} />
                </span>
                <span className="text-14 text-charcoal">{r.nextStep}</span>
                <span className="text-12 text-muted"><bdi dir="ltr" className="font-mono">{r.reference}</bdi> · {day(r.updatedAt)}</span>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </Card>
  );
}
