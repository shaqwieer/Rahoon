import Link from "next/link";
import { CompareToggle } from "@/components/market/discovery/CompareControls";
import { FitSummary } from "@/components/market/discovery/FitSummary";
import { SaveButton } from "@/components/market/SaveButton";
import { Amount, Badge, DemoBadge, QualityBadge } from "@/components/market/ui";
import { buttonClasses } from "@/components/ui/buttonStyles";
import { Icon } from "@/components/ui/Icon";
import { cn } from "@/lib/cn";
import { FREQ_PER, monthLabel } from "@/lib/market/format";
import type { Fit, MatchExplanation, OpportunityCard as Card } from "@/lib/market/types";

/**
 * A card for one specific property (not a project launch with «يبدأ من» prices). The amount due now from the buyer is the
 * headline; the price or total commitment, the future balance and the installment are labelled separately on a plain
 * surface (not over the photo). Unknown figures say so. No owner data, no «متعثر» label.
 */
export function OpportunityCard({ card: c, fit, match, signedIn, highlighted, compare = true }: {
  card: Card;
  fit?: Fit | null;
  match?: MatchExplanation | null;
  signedIn: boolean;
  /** Selected on the map (list ↔ map). */
  highlighted?: boolean;
  compare?: boolean;
}) {
  const href = `/opportunities/${c.reference}`;
  const specs: string[] = [];
  if (c.area) specs.push(`${c.area} م²`);
  if (c.bedrooms !== null && c.bedrooms !== undefined) specs.push(`${c.bedrooms} غرف`);
  if (c.bathrooms) specs.push(`${c.bathrooms} دورات مياه`);
  for (const s of c.specs) if (specs.length < 4) specs.push(`${s.label}: ${s.value}`);
  return (
    <article data-ref={c.reference} className={cn("group relative flex h-full flex-col overflow-hidden rounded-lg border bg-white shadow-1 transition-shadow duration-200 hover:shadow-2",
      highlighted ? "border-rust ring-2 ring-rust/40" : "border-line")}>
      <div className="relative aspect-[4/3] bg-subtle">
        {c.coverUrl ? (
          // eslint-disable-next-line @next/next/no-img-element -- API-served listing photo
          <img src={c.coverUrl} alt={c.title} className="size-full object-cover transition-transform duration-500 group-hover:scale-[1.02]" loading="lazy" />
        ) : (
          <div className="flex size-full items-center justify-center text-muted">
            <Icon name="image_not_supported" size={40} />
          </div>
        )}
        <div className="absolute inset-x-3 top-3 flex items-start justify-between gap-2">
          <span className="flex flex-wrap gap-1.5">
            {c.readinessLabel ? <Badge tone={c.readiness === "ready" ? "ok" : "info"} className="bg-white/95">{c.readinessLabel}</Badge> : null}
            {c.isDemo ? <DemoBadge className="bg-white/95" /> : null}
          </span>
          <SaveButton reference={c.reference} saved={c.saved} signedIn={signedIn} />
        </div>
        {c.photoCount > 1 ? (
          <span className="absolute end-3 bottom-3 inline-flex items-center gap-1 rounded-pill bg-ink/70 px-2 py-0.5 text-12 text-white">
            <Icon name="photo_library" size={14} />
            {c.photoCount}
          </span>
        ) : null}
      </div>
      <div className="flex flex-1 flex-col gap-3 p-4">
        <div className="flex flex-col gap-1">
          <h3 className="m-0 text-17 leading-7 font-bold">
            <Link href={href} className="text-ink no-underline after:absolute after:inset-0 hover:text-rust-700 focus-visible:outline-none">
              {c.title}
            </Link>
          </h3>
          <span className="flex items-center gap-1 text-13 text-muted">
            <Icon name="location_on" size={16} />
            {c.cityLabel}
            {c.district ? `، ${c.district}` : ""}
            {c.project ? ` · ${c.project}` : ""}
          </span>
          {c.developerName ? <span className="text-12 text-muted">المطور: {c.developerName}</span> : null}
        </div>
        {specs.length ? (
          <ul className="m-0 flex list-none flex-wrap gap-1.5 p-0 text-12 text-charcoal">
            {[c.propertyTypeLabel, ...specs].map((s) => (
              <li key={s} className="rounded-xs bg-subtle px-2 py-0.5">{s}</li>
            ))}
          </ul>
        ) : null}
        <div className="flex flex-col gap-2 rounded-md bg-warm p-3">
          <div className="flex items-end justify-between gap-2">
            <span className="text-13 font-semibold text-charcoal">المطلوب منك الآن</span>
            <Amount value={c.dueNow} size="lg" strong unknown="غير مكتمل بعد" />
          </div>
          <dl className="m-0 flex flex-col gap-1 text-13">
            <div className="flex justify-between gap-2">
              <dt className="text-muted">{c.track === "financier" ? "سعر الشراء مع التكاليف" : "إجمالي الالتزام"}</dt>
              <dd className="m-0"><Amount value={c.buyerTotal} size="sm" unknown="غير مكتمل" /></dd>
            </div>
            {c.track !== "financier" ? (
              <>
                <div className="flex justify-between gap-2">
                  <dt className="text-muted">الرصيد المستقبلي للمطور</dt>
                  <dd className="m-0"><Amount value={c.futureBalance} size="sm" unknown="غير معروف" /></dd>
                </div>
                {c.installment !== null ? (
                  <div className="flex justify-between gap-2">
                    <dt className="text-muted">القسط</dt>
                    <dd className="m-0 flex items-center gap-1">
                      <Amount value={c.installment} size="sm" /> <span className="text-muted">{FREQ_PER[c.installmentFrequency ?? ""] ?? ""}</span>
                    </dd>
                  </div>
                ) : null}
                {c.largestExtraPayment ? (
                  <div className="flex justify-between gap-2">
                    <dt className="text-muted">دفعة إضافية</dt>
                    <dd className="m-0 flex items-center gap-1">
                      <Amount value={c.largestExtraPayment} size="sm" />
                      <span className="text-muted">{c.extraPaymentRecurrence === "annual" ? "سنويًا" : c.extraPaymentRecurrence === "once" ? "مرة واحدة" : ""}</span>
                    </dd>
                  </div>
                ) : null}
              </>
            ) : null}
          </dl>
          {c.needsNewFinancing ? <span className="text-12 text-info">يمكن الشراء نقدًا أو بتمويل جديد يخضع لموافقة جهتك.</span> : null}
          {c.deliveryMonth ? <span className="text-12 text-muted">التسليم المتوقع: {monthLabel(c.deliveryMonth)}</span> : null}
        </div>
        <div className="flex flex-wrap items-center justify-between gap-2">
          <QualityBadge quality={c.quality} />
          {compare ? <CompareToggle reference={c.reference} /> : null}
        </div>
        {fit ? <FitSummary fit={fit} match={match} compact /> : null}
        <div className="relative z-10 mt-auto flex gap-2 pt-1">
          <Link href={href} className={buttonClasses({ variant: "secondary", size: "md", className: "flex-1" })}>عرض التفاصيل</Link>
          <Link href={`${href}#interest`} className={buttonClasses({ variant: "primary", size: "md", className: "flex-1" })}>مهتم بالفرصة</Link>
        </div>
      </div>
    </article>
  );
}
