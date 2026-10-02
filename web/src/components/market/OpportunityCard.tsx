import Link from "next/link";
import { CompareToggle } from "@/components/market/discovery/CompareControls";
import { FitSummary } from "@/components/market/discovery/FitSummary";
import { SaveButton } from "@/components/market/SaveButton";
import { Amount, Badge, DemoBadge, QualityBadge } from "@/components/market/ui";
import { Icon } from "@/components/ui/Icon";
import { InfoTip } from "@/components/ui/InfoTip";
import { cn } from "@/lib/cn";
import { FREQ_PER, monthLabel, sar } from "@/lib/market/format";
import type { Fit, MatchExplanation, OpportunityCard as Card } from "@/lib/market/types";

const FIT_PILL = {
  fits: { text: "تناسب أرقامك", icon: "check_circle", cls: "border-ok-line bg-ok-bg text-ok" },
  does_not_fit: { text: "لا تناسب كل أرقامك", icon: "do_not_disturb_on", cls: "border-warn-line bg-warn-bg text-warn" },
  incomplete: { text: "لا يمكن المقارنة بعد", icon: "help", cls: "border-line bg-subtle text-charcoal" },
} as const;

/** The figures behind the headline, in the order a buyer pays them (shown in the ⓘ tip). */
function FiguresTip({ c }: { c: Card }) {
  const rows: [string, React.ReactNode][] = [
    [c.track === "financier" ? "سعر الشراء مع التكاليف" : "إجمالي الالتزام", <Amount key="t" value={c.buyerTotal} size="sm" unknown="غير مكتمل" />],
  ];
  if (c.track !== "financier") {
    rows.push(["الرصيد المستقبلي للمطور", <Amount key="f" value={c.futureBalance} size="sm" unknown="غير معروف" />]);
    if (c.installment !== null) rows.push(["القسط", <span key="i"><Amount value={c.installment} size="sm" /> <span className="text-muted">{FREQ_PER[c.installmentFrequency ?? ""] ?? ""}</span></span>]);
    if (c.installmentFrequency && c.installmentFrequency !== "monthly" && c.installmentMonthlyEquivalent !== null)
      rows.push(["مكافئه الشهري للمقارنة", <Amount key="m" value={c.installmentMonthlyEquivalent} size="sm" />]);
    if (c.largestExtraPayment)
      rows.push(["دفعة إضافية", <span key="x"><Amount value={c.largestExtraPayment} size="sm" /> <span className="text-muted">{c.extraPaymentRecurrence === "annual" ? "سنويًا" : c.extraPaymentRecurrence === "once" ? "مرة واحدة" : ""}</span></span>]);
    if (c.remainingMonths) rows.push(["المدة المتبقية", `${c.remainingMonths} شهرًا`]);
  }
  if (c.deliveryMonth) rows.push(["التسليم المتوقع", monthLabel(c.deliveryMonth)]);
  if (c.developerName) rows.push(["المطور", c.developerName]);
  return (
    <div className="flex flex-col gap-2">
      <dl className="m-0 flex flex-col gap-1">
        {rows.map(([k, v]) => (
          <div key={k} className="flex items-baseline justify-between gap-3">
            <dt className="text-muted">{k}</dt>
            <dd className="m-0 text-end font-semibold">{v}</dd>
          </div>
        ))}
      </dl>
      {c.needsNewFinancing ? <p className="m-0 text-12 text-info">يمكن الشراء نقدًا أو بتمويل جديد يخضع لموافقة جهتك؛ قسط صاحب العقار لا ينتقل إليك.</p> : null}
      <p className="m-0 text-12 text-muted">{c.quality === "complete_verified" ? "راجع الفريق هذه الأرقام من مستنداتها." : c.quality === "complete_estimate" ? "تقدير مبدئي؛ لم يتحقق الفريق من كل الأرقام." : "تقدير غير مكتمل؛ بعض الأرقام غير معروفة."}</p>
    </div>
  );
}

/**
 * A compact card for one specific property: photo, title, place and key specs, then the amount due now as the headline with one
 * line of what follows (total, installment). The rest of the figures, and why it fits, open from small tips instead of making
 * every card long. No owner data, no «متعثر» label. `layout="row"` is the horizontal preview used over the map.
 */
export function OpportunityCard({ card: c, fit, match, signedIn, compare = true, layout = "card" }: {
  card: Card;
  fit?: Fit | null;
  match?: MatchExplanation | null;
  signedIn: boolean;
  compare?: boolean;
  layout?: "card" | "row";
}) {
  const href = `/opportunities/${c.reference}`;
  const specs = [c.propertyTypeLabel];
  if (c.area) specs.push(`${c.area} م²`);
  if (c.bedrooms !== null && c.bedrooms !== undefined) specs.push(`${c.bedrooms} غرف`);
  if (c.bathrooms) specs.push(`${c.bathrooms} حمام`);
  const follow: string[] = [];
  if (c.buyerTotal !== null) follow.push(`${c.track === "financier" ? "السعر" : "الإجمالي"} ${sar(c.buyerTotal)}`);
  if (c.track !== "financier" && c.installment !== null) follow.push(`قسط ${sar(c.installment)} ${FREQ_PER[c.installmentFrequency ?? ""] ?? ""}`.trim());
  if (c.largestExtraPayment) follow.push("+ دفعة إضافية");
  const pill = fit ? FIT_PILL[fit.outcome] : null;
  const row = layout === "row";

  return (
    <article data-ref={c.reference}
      className={cn("group relative flex h-full rounded-lg border border-line bg-white shadow-1 transition-[box-shadow,border-color] hover:border-line-strong hover:shadow-2",
        row ? "flex-row" : "flex-col")}>
      <div className={cn("relative flex-none overflow-hidden bg-subtle", row ? "w-28 rounded-s-lg" : "aspect-[16/10] rounded-t-lg")}>
        {c.coverUrl ? (
          // eslint-disable-next-line @next/next/no-img-element -- API-served listing photo
          <img src={c.coverUrl} alt="" className="size-full object-cover transition-transform duration-500 group-hover:scale-[1.02]" loading="lazy" />
        ) : (
          <div className="flex size-full items-center justify-center text-muted"><Icon name="image_not_supported" size={32} /></div>
        )}
        {!row ? (
          <>
            <div className="absolute inset-x-2.5 top-2.5 flex items-start justify-between gap-2">
              <span className="flex flex-wrap gap-1">
                {c.readinessLabel ? <Badge tone={c.readiness === "ready" ? "ok" : "info"} className="bg-white/95">{c.readinessLabel}</Badge> : null}
                {c.isDemo ? <DemoBadge className="bg-white/95" /> : null}
              </span>
              <span className="flex gap-1.5">
                {compare ? <CompareToggle reference={c.reference} compact /> : null}
                <SaveButton reference={c.reference} saved={c.saved} signedIn={signedIn} />
              </span>
            </div>
            {c.photoCount > 1 ? (
              <span className="absolute end-2.5 bottom-2.5 inline-flex items-center gap-1 rounded-pill bg-ink/70 px-2 py-0.5 text-12 text-white">
                <Icon name="photo_library" size={14} />
                {c.photoCount}
              </span>
            ) : null}
          </>
        ) : null}
      </div>

      <div className={cn("flex min-w-0 flex-1 flex-col", row ? "gap-1.5 p-3" : "gap-2.5 p-3.5")}>
        <div className="flex flex-col gap-0.5">
          <h3 className={cn("m-0 line-clamp-2 font-bold", row ? "text-14 leading-6" : "text-15 leading-6")}>
            <Link href={href} className="text-ink no-underline after:absolute after:inset-0 after:rounded-lg hover:text-rust-700 focus-visible:outline-none">
              {c.title}
            </Link>
          </h3>
          <span className="flex min-w-0 items-center gap-1 text-12 text-muted">
            <Icon name="location_on" size={14} className="flex-none" />
            <span className="truncate">{c.cityLabel}{c.district ? `، ${c.district}` : ""}{c.project ? ` · ${c.project}` : ""}</span>
          </span>
          {!row ? <span className="truncate text-12 text-charcoal">{specs.join(" · ")}</span> : null}
        </div>

        <div className={cn("flex flex-col rounded-md bg-warm", row ? "p-2" : "mt-auto p-2.5")}>
          <div className="flex items-center justify-between gap-2">
            <span className="text-12 font-semibold text-charcoal">المطلوب منك الآن</span>
            <InfoTip label="تفاصيل الأرقام" align="end"><FiguresTip c={c} /></InfoTip>
          </div>
          <Amount value={c.dueNow} size="lg" strong unknown="غير مكتمل بعد" />
          {follow.length ? <span className="truncate text-12 text-muted">{follow.join(" · ")}</span> : null}
        </div>

        {!row || pill ? (
          <div className="flex flex-wrap items-center justify-between gap-1.5">
            {!row ? <QualityBadge quality={c.quality} /> : <span />}
            {fit && pill ? (
              <InfoTip label="لماذا؟" align="start" panelClassName="w-[19rem] p-0 border-0 bg-transparent shadow-none"
                trigger={<span className={cn("inline-flex items-center gap-1 rounded-pill border px-2 py-0.5 text-12 font-semibold", pill.cls)}><Icon name={pill.icon} size={14} />{pill.text}</span>}>
                <span className="block rounded-md bg-white shadow-3"><FitSummary fit={fit} match={match} /></span>
              </InfoTip>
            ) : null}
          </div>
        ) : null}
      </div>
    </article>
  );
}
