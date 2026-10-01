import type { Metadata } from "next";
import { QueueTabs, TeamHeader, TeamTable } from "@/components/market/team/TeamBits";
import { StatusBadge } from "@/components/market/ui";
import { apiGet } from "@/lib/api/server";
import { day } from "@/lib/market/format";

export const metadata: Metadata = { title: "الفرص" };

interface Row { reference: string; title: string; status: string; statusLabel: string; cityLabel: string | null; district: string | null; propertyTypeLabel: string; publishedAt: string | null; statusChangedAt: string; assignedTo: string | null; interests: number }

const TABS = [
  { key: "", label: "الكل" }, { key: "Preparing", label: "قيد الإعداد" }, { key: "AwaitingOwnerConfirmation", label: "بانتظار المالك" },
  { key: "ReadyToPublish", label: "جاهزة للنشر" }, { key: "Published", label: "منشورة" }, { key: "Paused", label: "موقوفة" }, { key: "Withdrawn", label: "مسحوبة" },
];

export default async function TeamOpportunities({ searchParams }: PageProps<"/team/opportunities">) {
  const sp = await searchParams;
  const status = typeof sp.status === "string" ? sp.status : "";
  const rows = await apiGet<Row[]>(`/team/market/opportunities${status ? `?status=${encodeURIComponent(status)}` : ""}`);
  return (
    <div>
      <TeamHeader title="الفرص" sub="تُجهز من طلب بيع معتمد، يؤكد المالك ملخصها، ثم تُنشر بعد اكتمال شروط النشر." />
      <QueueTabs current={status} tabs={TABS.map((t) => ({ ...t, href: t.key ? `/team/opportunities?status=${t.key}` : "/team/opportunities" }))} />
      <TeamTable empty="لا توجد فرص هنا." head={["الفرصة", "العنوان", "الحالة", "الاهتمامات", "المسؤول", "آخر تغيير"]}
        rows={rows.map((r) => ({
          key: r.reference, href: `/team/opportunities/${r.reference}`,
          cells: [<bdi key="r" dir="ltr" className="font-mono">{r.reference}</bdi>, r.title, <StatusBadge key="s" status={r.status.charAt(0).toLowerCase() + r.status.slice(1)} label={r.statusLabel} />, r.interests, r.assignedTo ?? "—", day(r.statusChangedAt)],
        }))} />
    </div>
  );
}
