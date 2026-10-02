import type { Metadata } from "next";
import { QueueTabs, TeamHeader, TeamTable } from "@/components/market/team/TeamBits";
import { Badge, StatusBadge } from "@/components/market/ui";
import { apiGet } from "@/lib/api/server";
import { day } from "@/lib/market/format";
import { Pagination } from "@/components/ui/Pagination";

export const metadata: Metadata = { title: "طلبات البيع" };

interface Row {
  reference: string;
  status: string;
  statusLabel: string;
  propertyTypeLabel: string;
  cityLabel: string | null;
  district: string | null;
  obligationLabel: string;
  applicantName: string | null;
  submittedAt: string | null;
  statusChangedAt: string;
  assignedTo: string | null;
  ownerMarkedComplete: boolean;
}

interface Paged<T> { items: T[]; total: number; page: number; pageSize: number; pages: number }

const QUEUES = [
  { key: "new", label: "جديدة" },
  { key: "review", label: "قيد المراجعة" },
  { key: "completion", label: "بانتظار الاستكمال" },
  { key: "approved", label: "معتمدة لإعداد فرصة" },
  { key: "closed", label: "مرفوضة ومسحوبة" },
  { key: "all", label: "الكل" },
];

export default async function TeamSaleList({ searchParams }: PageProps<"/team/sale">) {
  const sp = await searchParams;
  const queue = typeof sp.queue === "string" ? sp.queue : "all";
  const assigned = typeof sp.assigned === "string" ? sp.assigned : "";
  const page = Number(typeof sp.page === "string" ? sp.page : "1") || 1;
  const base = `/team/sale?queue=${encodeURIComponent(queue)}${assigned ? `&assigned=${encodeURIComponent(assigned)}` : ""}`;
  const data = await apiGet<Paged<Row>>(`/team/market/sale-requests?queue=${encodeURIComponent(queue)}${assigned ? `&assigned=${encodeURIComponent(assigned)}` : ""}&page=${page}`);
  const rows = data.items;
  return (
    <div>
      <TeamHeader title="طلبات البيع" sub="المسودات التي لم يرسلها أصحابها لا تظهر هنا." />
      <QueueTabs current={queue} tabs={QUEUES.map((q) => ({ ...q, href: `/team/sale?queue=${q.key}` }))} />
      <div className="mb-3 flex gap-3 text-14">
        <a href={`/team/sale?queue=${queue}&assigned=me`} aria-current={assigned === "me" ? "true" : undefined}>المسندة إليّ</a>
        <a href={`/team/sale?queue=${queue}&assigned=none`} aria-current={assigned === "none" ? "true" : undefined}>بدون مسؤول</a>
        <a href={`/team/sale?queue=${queue}`}>الكل</a>
      </div>
      <TeamTable
        empty="لا توجد طلبات في هذه القائمة."
        head={["الطلب", "العقار", "الجهة", "صاحب الطلب", "الحالة", "المسؤول", "آخر تغيير"]}
        rows={rows.map((r) => ({
          key: r.reference,
          href: `/team/sale/${r.reference}`,
          cells: [
            <bdi key="r" dir="ltr" className="font-mono">{r.reference}</bdi>,
            `${r.propertyTypeLabel} · ${r.cityLabel ?? ""}${r.district ? `، ${r.district}` : ""}`,
            r.obligationLabel,
            r.applicantName ?? "—",
            <span key="s" className="flex flex-wrap gap-1"><StatusBadge status={r.status} label={r.statusLabel} />{r.ownerMarkedComplete ? <Badge tone="info">أكمل العميل ملفه</Badge> : null}</span>,
            r.assignedTo ?? <Badge key="a" tone="warn">بدون</Badge>,
            day(r.statusChangedAt),
          ],
        }))}
      />
      <Pagination className="mt-4" page={data.page} pages={data.pages} total={data.total} pageSize={data.pageSize} hrefFor={(p) => `${base}${p > 1 ? `&page=${p}` : ""}`} />
    </div>
  );
}
