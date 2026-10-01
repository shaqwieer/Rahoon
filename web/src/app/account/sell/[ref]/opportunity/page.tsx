import Link from "next/link";
import { notFound } from "next/navigation";
import { LocationMap } from "@/components/market/LocationMap";
import { OwnerConfirm } from "@/components/market/OwnerConfirm";
import { TermsBreakdown } from "@/components/market/TermsBreakdown";
import { Badge, Card, StatusBadge } from "@/components/market/ui";
import { Icon } from "@/components/ui/Icon";
import { day, monthLabel } from "@/lib/market/format";
import { marketGet } from "@/lib/market/server";
import type { OpportunityContent, TermsView } from "@/lib/market/types";

interface OwnerOpportunity {
  reference: string;
  status: string;
  statusLabel: string;
  content: OpportunityContent;
  photos: { id: string; url: string }[];
  location: { precision: "exact" | "approximate"; lat: number | null; lng: number | null };
  terms: TermsView | null;
  awaitingConfirmation: boolean;
  publicUrl: string | null;
  history: { id: string; versionNo: number; status: string; sentToOwnerAt: string | null; ownerDecidedAt: string | null; ownerNote: string | null }[];
}

/** The summary the team prepared: the owner confirms it (or asks for changes) before anything is published. */
export default async function OwnerOpportunityPage({ params }: PageProps<"/account/sell/[ref]/opportunity">) {
  const { ref } = await params;
  const o = await marketGet<OwnerOpportunity>(`/market/sale-requests/${encodeURIComponent(ref)}/opportunity`);
  if (!o) notFound();
  const c = o.content;
  return (
    <div className="flex flex-col gap-5">
      <div className="flex flex-wrap items-center gap-2 text-14">
        <Link href={`/account/sell/${ref}`}>طلب البيع <bdi dir="ltr">{ref}</bdi></Link>
        <Icon name="chevron_left" size={18} mirror className="text-muted" />
        <span>ملخص الفرصة</span>
      </div>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="m-0 text-26 leading-9 font-bold">{c.title}</h1>
        <StatusBadge status={o.status} label={o.statusLabel} />
      </div>
      {o.awaitingConfirmation && o.terms ? (
        <OwnerConfirm reference={ref} termsId={o.terms.id} versionNo={o.terms.versionNo} />
      ) : o.publicUrl ? (
        <p className="m-0 flex items-center gap-2 text-15">
          <Icon name="public" size={20} className="text-ok" />
          الفرصة منشورة: <Link href={o.publicUrl}>عرض الصفحة العامة</Link>
        </p>
      ) : (
        <p className="m-0 text-15 text-muted">لا يوجد ملخص بانتظار تأكيدك الآن.</p>
      )}
      <div className="grid gap-5 lg:grid-cols-[minmax(0,1.3fr)_minmax(0,1fr)]">
        <div className="flex flex-col gap-5">
          <Card title="ما سيظهر للمشترين">
            {o.photos.length ? (
              <div className="mb-4 grid grid-cols-3 gap-2">
                {o.photos.slice(0, 6).map((p, i) => (
                  // eslint-disable-next-line @next/next/no-img-element -- private owner preview
                  <img key={p.id} src={p.url} alt={`صورة ${i + 1}`} className={i === 0 ? "col-span-3 aspect-[16/9] w-full rounded-md object-cover" : "aspect-[4/3] w-full rounded-md object-cover"} />
                ))}
              </div>
            ) : <p className="m-0 mb-3 text-14 text-warn">لم تُختر صور بعد.</p>}
            <p className="m-0 text-15 leading-7">{c.description}</p>
            <div className="mt-3 flex flex-wrap gap-2">
              <Badge>{c.propertyTypeLabel}</Badge>
              <Badge>{c.cityLabel}{c.district ? `، ${c.district}` : ""}</Badge>
              {c.area ? <Badge>{c.area} م²</Badge> : null}
              {c.bedrooms !== null ? <Badge>{c.bedrooms} غرف</Badge> : null}
              {c.readinessLabel ? <Badge>{c.readinessLabel}{c.deliveryMonth ? ` · التسليم ${monthLabel(c.deliveryMonth)}` : ""}</Badge> : null}
              {c.features.map((f) => <Badge key={f}>{f}</Badge>)}
            </div>
          </Card>
          <Card title="الموقع المعروض">
            <p className="m-0 mb-3 text-14 text-charcoal">
              {o.location.precision === "exact" ? "سيظهر الموقع الدقيق كما وافقت." : "ستظهر منطقة تقريبية فقط، دون الموقع الدقيق."}
            </p>
            {o.location.lat !== null && o.location.lng !== null ? <LocationMap lat={o.location.lat} lng={o.location.lng} precision={o.location.precision} height={240} /> : <p className="m-0 text-14 text-warn">لم يُحدد الموقع.</p>}
          </Card>
        </div>
        <div className="flex flex-col gap-5">
          {o.terms ? (
            <Card title={`الأرقام وشروط النقل (الإصدار ${o.terms.versionNo})`}>
              <TermsBreakdown result={o.terms} audience="team" />
              {o.terms.transferConditions ? (
                <div className="mt-4 rounded-md border border-line p-3">
                  <h3 className="m-0 mb-1 text-14 font-bold">شروط النقل</h3>
                  <p className="m-0 text-14 leading-6">{o.terms.transferConditions}</p>
                </div>
              ) : null}
              {o.terms.verificationScope ? (
                <p className="m-0 mt-3 text-13 text-muted">ما راجعه الفريق: {o.terms.verificationScope}{o.terms.verifiedOn ? ` (${day(o.terms.verifiedOn)})` : ""}</p>
              ) : null}
            </Card>
          ) : null}
          {o.history.length > 1 ? (
            <Card title="إصدارات الملخص">
              <ul className="m-0 flex list-none flex-col gap-2 p-0 text-14">
                {o.history.map((h) => (
                  <li key={h.id} className="flex flex-col">
                    <span>الإصدار {h.versionNo} — {h.status === "ownerConfirmed" ? "أكدته" : h.status === "ownerRequestedChanges" ? "طلبت تعديله" : h.status === "sentToOwner" ? "بانتظار تأكيدك" : h.status === "superseded" ? "استُبدل" : "قيد الإعداد"}</span>
                    {h.ownerNote ? <span className="text-13 text-muted">ملاحظتك: {h.ownerNote}</span> : null}
                  </li>
                ))}
              </ul>
            </Card>
          ) : null}
        </div>
      </div>
    </div>
  );
}
