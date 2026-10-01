import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { DirectoryActiveToggle, DirectoryForm, type DirectoryRecord } from "@/components/market/team/DirectoryForm";
import { TeamHeader } from "@/components/market/team/TeamBits";
import { Badge, Card, Row } from "@/components/market/ui";
import { can, requireMe } from "@/lib/api/server";
import { marketGet } from "@/lib/market/server";
import { ORG_TYPE_LABELS } from "@/lib/market/orgTypes";
import { day, dayTime } from "@/lib/market/format";

export const metadata: Metadata = { title: "جهة في الدليل" };

interface Detail extends DirectoryRecord {
  typeLabels: string[];
  origin: "import" | "manual";
  originLabel: string;
  importKeys: string[];
  lastImportedAt: string | null;
  adminEditedAt: string | null;
  adminProtected: boolean;
  createdAt: string;
  updatedAt: string;
  usage: number;
}

export default async function OrganizationDetail({ params }: PageProps<"/team/organizations/[id]">) {
  const { id } = await params;
  const me = await requireMe();
  const manage = can(me, "directory.manage");
  const d = await marketGet<Detail>(`/team/directory/${encodeURIComponent(id)}`);
  if (!d) notFound();
  return (
    <div className="grid max-w-[1100px] gap-6 lg:grid-cols-[1fr_340px]">
      <div className="flex flex-col gap-6">
        <TeamHeader title={d.nameAr} back={{ href: "/team/organizations", label: "دليل الجهات" }}
          sub={d.types.map((t) => ORG_TYPE_LABELS[t] ?? t).join("، ")} />
        {manage ? (
          <>
            <DirectoryActiveToggle record={d} />
            <DirectoryForm record={d} />
          </>
        ) : (
          <Card title="بيانات الجهة">
            <p className="m-0 mb-2 text-13 text-muted">للعرض فقط: التعديل يحتاج صلاحية إدارة الدليل.</p>
            <dl className="m-0 flex flex-col">
              <Row label="الحالة">{d.active ? "مفعّلة" : "موقوفة"}</Row>
              <Row label="الاسم بالإنجليزية">{d.nameEn ? <bdi dir="ltr">{d.nameEn}</bdi> : "—"}</Row>
              <Row label="الموقع الرسمي">{d.website ? <bdi dir="ltr" className="break-all">{d.website}</bdi> : "—"}</Row>
              <Row label="رقم الترخيص">{d.licenseNumber ?? "—"}</Row>
              <Row label="رقم السجل">{d.registrationNumber ?? "—"}</Row>
            </dl>
          </Card>
        )}
      </div>
      <aside className="flex flex-col gap-4">
        <Card title="الأصل والتحقق">
          <dl className="m-0 flex flex-col gap-3">
            <Row label="المصدر الأول">{d.originLabel}</Row>
            <Row label="آخر تحقق">{d.verifiedOn ? day(d.verifiedOn) : "—"}</Row>
            <Row label="آخر استيراد">{d.lastImportedAt ? dayTime(d.lastImportedAt) : "—"}</Row>
            <Row label="تعديل إداري">
              {d.adminEditedAt ? (
                <span className="flex flex-col gap-1">
                  {dayTime(d.adminEditedAt)}
                  <Badge tone="info">محمية من الاستيراد</Badge>
                </span>
              ) : (
                "لا — قد يحدّثها الاستيراد من مصدرها"
              )}
            </Row>
            {d.sourceUrl ? (
              <Row label="رابط المصدر">
                <a href={d.sourceUrl} target="_blank" rel="noreferrer noopener" className="break-all">
                  {d.sourceName ?? d.sourceUrl}
                </a>
              </Row>
            ) : null}
            {d.importKeys.length ? <Row label="مفاتيح الاستيراد"><bdi dir="ltr" className="text-12 break-all">{d.importKeys.join(" · ")}</bdi></Row> : null}
          </dl>
        </Card>
        <Card title="الاستخدام">
          <p className="m-0 text-15">
            {d.usage === 0 ? "لم تُذكر في أي طلب بعد." : `ذُكرت في ${d.usage} ${d.usage === 1 ? "طلب" : "طلبات"}. الطلبات تحتفظ بالاسم والمرجع المسجلين حتى لو عُدّلت الجهة أو أُوقفت.`}
          </p>
        </Card>
      </aside>
    </div>
  );
}
