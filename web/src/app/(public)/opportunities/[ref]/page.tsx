import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { CompareToggle, CompareTray } from "@/components/market/discovery/CompareControls";
import { FitSummary, NextPayments } from "@/components/market/discovery/FitSummary";
import { Gallery } from "@/components/market/Gallery";
import { LocationMap } from "@/components/market/LocationMap";
import { InterestBox, OpportunityCalculator } from "@/components/market/OpportunityActions";
import { SaveButton } from "@/components/market/SaveButton";
import { TermsBreakdown } from "@/components/market/TermsBreakdown";
import { Amount, Badge, Card, DemoBadge, QualityBadge } from "@/components/market/ui";
import { PublicFooter, PublicHeader } from "@/components/shell/Public";
import { Icon } from "@/components/ui/Icon";
import { day, FREQ_PER, monthLabel } from "@/lib/market/format";
import { getCatalog, marketGet, optionalIndividual } from "@/lib/market/server";
import type { OpportunityDetail } from "@/lib/market/types";

export async function generateMetadata({ params }: PageProps<"/opportunities/[ref]">): Promise<Metadata> {
  const { ref } = await params;
  const d = await marketGet<OpportunityDetail>(`/market/opportunities/${encodeURIComponent(ref)}`).catch(() => null);
  if (!d) return { title: "فرصة غير متاحة" };
  return {
    title: d.content.title,
    description: `${d.content.propertyTypeLabel} في ${d.content.cityLabel}${d.content.district ? `، ${d.content.district}` : ""}. المبلغ المطلوب الآن والالتزامات اللاحقة موضحة منفصلة.`,
  };
}

/** One published opportunity: photos, summary, figures with verification dates, transfer terms, location, calculator, interest. */
export default async function OpportunityPage({ params }: PageProps<"/opportunities/[ref]">) {
  const { ref } = await params;
  const [d, catalog, me] = await Promise.all([marketGet<OpportunityDetail>(`/market/opportunities/${encodeURIComponent(ref)}`), getCatalog(), optionalIndividual()]);
  if (!d) notFound();
  const c = d.content;
  const t = d.terms;
  const name = me && me.user.name !== "عميل رهون" ? me.user.name : null;
  return (
    <>
      <PublicHeader />
      <main id="main" tabIndex={-1} className="mx-auto w-full max-w-[1280px] flex-1 px-4 py-6 outline-none md:px-8 md:py-8">
        <nav aria-label="مسار التنقل" className="mb-4 flex flex-wrap items-center gap-1.5 text-14">
          <Link href="/opportunities">الفرص المتاحة</Link>
          <Icon name="chevron_left" size={18} mirror className="text-muted" />
          <span className="text-muted">{c.cityLabel}</span>
        </nav>
        <div className="grid grid-cols-1 gap-6 lg:grid-cols-[minmax(0,1.6fr)_minmax(340px,1fr)]">
          <div className="flex min-w-0 flex-col gap-6">
            <Gallery photos={d.photos} title={c.title} />
            <header className="flex flex-col gap-2">
              <div className="flex flex-wrap items-center gap-2">
                {c.readinessLabel ? <Badge tone={c.readiness === "ready" ? "ok" : "info"}>{c.readinessLabel}</Badge> : null}
                <Badge>{c.trackLabel}</Badge>
                <QualityBadge quality={t.quality} />
                {d.card.isDemo ? <DemoBadge /> : null}
              </div>
              <div className="flex items-start justify-between gap-3">
                <h1 className="m-0 text-26 leading-10 font-bold md:text-32 md:leading-[46px]">{c.title}</h1>
                <span className="flex flex-none items-center gap-2">
                  <CompareToggle reference={ref} />
                  <SaveButton reference={ref} saved={d.card.saved} signedIn={Boolean(me)} />
                </span>
              </div>
              <span className="flex items-center gap-1 text-15 text-charcoal">
                <Icon name="location_on" size={18} className="text-rust" />
                {c.cityLabel}{c.district ? `، حي ${c.district}` : ""}{c.project ? ` · ${c.project}` : ""}
              </span>
            </header>

            <Card title="ملخص العقار">
              <dl className="m-0 grid grid-cols-2 gap-x-6 gap-y-3 sm:grid-cols-3">
                <div><dt className="text-13 text-muted">النوع</dt><dd className="m-0 font-semibold">{c.propertyTypeLabel}</dd></div>
                {c.area ? <div><dt className="text-13 text-muted">المساحة</dt><dd className="m-0 font-semibold">{c.area} م²</dd></div> : null}
                {c.bedrooms !== null ? <div><dt className="text-13 text-muted">غرف النوم</dt><dd className="m-0 font-semibold">{c.bedrooms}</dd></div> : null}
                {c.bathrooms ? <div><dt className="text-13 text-muted">دورات المياه</dt><dd className="m-0 font-semibold">{c.bathrooms}</dd></div> : null}
                {c.readinessLabel ? <div><dt className="text-13 text-muted">الحالة</dt><dd className="m-0 font-semibold">{c.readinessLabel}</dd></div> : null}
                {c.deliveryMonth ? <div><dt className="text-13 text-muted">التسليم المتوقع</dt><dd className="m-0 font-semibold">{monthLabel(c.deliveryMonth)}</dd></div> : null}
                {c.specs.map((s) => <div key={s.key}><dt className="text-13 text-muted">{s.label}</dt><dd className="m-0 font-semibold">{s.value}</dd></div>)}
              </dl>
              {c.description ? <p className="m-0 mt-4 text-15 leading-7">{c.description}</p> : null}
              {c.features.length ? (
                <ul className="m-0 mt-4 flex list-none flex-wrap gap-2 p-0">
                  {c.features.map((f) => <li key={f} className="inline-flex items-center gap-1 rounded-pill bg-subtle px-3 py-1 text-13"><Icon name="check" size={16} className="text-ok" />{f}</li>)}
                </ul>
              ) : null}
            </Card>

            <Card title="الأرقام: ما تدفعه الآن وما تلتزم به لاحقًا" id="figures">
              <TermsBreakdown result={t} audience="buyer" />
              {t.installment !== null && t.installmentFrequency !== "monthly" && t.installmentMonthlyEquivalent !== null ? (
                <p className="m-0 mt-3 text-13 text-muted">
                  القسط الفعلي <Amount value={t.installment} size="sm" /> {FREQ_PER[t.installmentFrequency ?? ""]}، ويعادل للمقارنة فقط <Amount value={t.installmentMonthlyEquivalent} size="sm" /> شهريًا.
                </p>
              ) : null}
              {d.schedule ? (
                <div className="mt-4 flex flex-col gap-2 rounded-md bg-warm p-3">
                  <h3 className="m-0 text-15 font-bold">الدفعات القادمة كما هي مسجلة</h3>
                  <NextPayments items={d.schedule.nextPayments} />
                  {d.schedule.caveats.length ? (
                    <ul className="m-0 flex list-none flex-col gap-1 p-0 text-13 text-charcoal">
                      {d.schedule.caveats.map((c) => <li key={c} className="flex gap-1.5"><Icon name="info" size={16} className="mt-0.5 flex-none text-muted" />{c}</li>)}
                    </ul>
                  ) : null}
                </div>
              ) : null}
              <p className="m-0 mt-3 text-13 text-muted">
                {t.verificationScope ? `ما راجعه الفريق: ${t.verificationScope}` : "لم يراجع الفريق مستندات هذه الأرقام بعد."}
                {t.verifiedOn ? ` تاريخ المراجعة: ${day(t.verifiedOn)}.` : ""} الإصدار {t.versionNo}. الشارة تبين نطاق ما راجعناه، ولا تضمن السعر أو النقل.
              </p>
            </Card>

            <Card title="النقل والموافقات">
              {t.transferConditions ? <p className="m-0 mb-3 text-15 leading-7">{t.transferConditions}</p> : null}
              <ul className="m-0 flex list-none flex-col gap-2 p-0">
                {d.approvals.map((a) => (
                  <li key={a.party} className="flex flex-wrap items-center justify-between gap-2 rounded-md bg-subtle px-3 py-2">
                    <span className="text-14">{a.party}</span>
                    <Badge tone={a.status === "approved" ? "ok" : a.status === "conditional" ? "info" : a.status === "rejected" || a.status === "expired" ? "err" : "neutral"}>{a.statusLabel}</Badge>
                    {a.conditions ? <span className="w-full text-13 text-charcoal">الشروط: {a.conditions}</span> : null}
                    {a.expiresOn ? <span className="w-full text-12 text-muted">صالحة حتى {day(a.expiresOn)}</span> : null}
                  </li>
                ))}
              </ul>
              <p className="m-0 mt-3 text-13 text-muted">موافقة المطور أو جهة التمويل منفصلة عن مراجعة رهون، ولا تنتقل أقساط صاحب العقار إلى المشتري تلقائيًا.</p>
            </Card>

            {d.location ? (
              <Card title="الموقع">
                <p className="m-0 mb-3 text-14 text-charcoal">{d.location.precision === "approximate" ? "موقع تقريبي: تظهر المنطقة فقط حفاظًا على خصوصية صاحب العقار." : "الموقع كما وافق صاحب العقار على عرضه."}</p>
                <LocationMap lat={d.location.lat} lng={d.location.lng} precision={d.location.precision} />
                <a href={d.location.googleMapsUrl} target="_blank" rel="noopener noreferrer" className="mt-3 inline-flex items-center gap-1 text-14 font-semibold">
                  <Icon name="open_in_new" size={18} />
                  فتح في Google Maps {d.location.precision === "approximate" ? "(المنطقة التقريبية)" : ""}
                </a>
              </Card>
            ) : null}
          </div>

          <aside className="flex flex-col gap-5 lg:sticky lg:top-24 lg:self-start">
            <section className="flex flex-col gap-3 rounded-lg border border-line bg-white p-5 shadow-2">
              <span className="text-14 font-semibold text-charcoal">المطلوب منك الآن</span>
              <Amount value={t.dueNow} size="xl" strong unknown="غير مكتمل بعد" />
              <dl className="m-0 flex flex-col gap-1.5 border-t border-divider pt-3 text-14">
                <div className="flex justify-between gap-2"><dt className="text-muted">{c.track === "financier" ? "سعر الشراء مع التكاليف" : "إجمالي الالتزام"}</dt><dd className="m-0"><Amount value={t.buyerTotal} size="sm" unknown="غير مكتمل" /></dd></div>
                {c.track !== "financier" ? <div className="flex justify-between gap-2"><dt className="text-muted">الرصيد المستقبلي</dt><dd className="m-0"><Amount value={t.futureBalance} size="sm" /></dd></div> : null}
                {t.installment !== null ? <div className="flex justify-between gap-2"><dt className="text-muted">القسط</dt><dd className="m-0"><Amount value={t.installment} size="sm" /> <span className="text-muted">{FREQ_PER[t.installmentFrequency ?? ""]}</span></dd></div> : null}
              </dl>
              {t.needsNewFinancing ? <p className="m-0 text-13 text-info">عند الشراء بتمويل جديد، المبلغ الآن والكلفة الكلية تعتمد على شروط جهة تمويلك وموافقتها.</p> : null}
              {d.fit ? <FitSummary fit={d.fit.fit} match={d.fit.match} title={`مع طلب الشراء المسجل لك: ${d.fit.fit.headline}`} /> : null}
            </section>
            <section id="interest" className="flex scroll-mt-24 flex-col gap-3 rounded-lg border border-rust-200 bg-white p-5 shadow-1">
              <h2 className="m-0 text-18 font-bold">مهتم بالفرصة؟</h2>
              <InterestBox reference={ref} signedIn={Boolean(me)} existing={d.myInterest} defaultName={name} />
            </section>
            <section className="flex flex-col gap-3 rounded-lg border border-line bg-white p-5">
              <h2 className="m-0 text-17 font-bold">حاسبة هذه الفرصة</h2>
              <OpportunityCalculator reference={ref} frequencies={catalog.frequencies} />
            </section>
          </aside>
        </div>
      </main>
      <CompareTray />
      <PublicFooter />
    </>
  );
}
