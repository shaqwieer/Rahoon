import type { Metadata } from "next";
import Link from "next/link";
import { PublicFooter, PublicHeader } from "@/components/shell/Public";
import { buttonClasses } from "@/components/ui/buttonStyles";
import { Icon } from "@/components/ui/Icon";
import { cn } from "@/lib/cn";
import { getServerDictionary } from "@/lib/i18n/server";

export async function generateMetadata(): Promise<Metadata> {
  const { t } = await getServerDictionary();
  return { title: t.individual.landing.metaTitle, description: t.individual.landing.metaDescription };
}

/**
 * Individual-first public landing (B13 `P1-Public-Landing-*-OwnerFirst`, replaces S01) with the proposed D-1 text
 * (help paths, Rahoon team step). Pending design approval (D-1). No «مجاني» (Q9), no deadlines or promised
 * outcomes (Q6/Q12); the consent line describes what the platform enforces rather than a legal commitment.
 */
export default async function LandingPage() {
  const { t } = await getServerDictionary();
  const L = t.individual.landing;
  const cta = "min-h-[54px] rounded-sm px-6 text-16 md:text-17";
  return (
    <>
      <PublicHeader />
      <main id="main" tabIndex={-1} className="flex-1 outline-none">
        {/* Hero */}
        <section className="grid grid-cols-1 items-center gap-8 px-5 pt-8 pb-10 md:px-10 md:py-14 lg:grid-cols-[minmax(0,1.1fr)_minmax(0,1fr)] lg:gap-14 xl:px-20 xl:pt-20">
          <div className="flex flex-col gap-4 md:gap-5">
            <span className="text-14 font-semibold text-rust md:text-15">{L.eyebrow}</span>
            <h1 className="m-0 text-30 leading-[44px] font-bold text-pretty md:text-40 md:leading-[56px]">{L.title}</h1>
            <p className="m-0 max-w-[34em] text-17 leading-[29px] text-pretty text-charcoal md:text-19 md:leading-[32px]">
              <span className="md:hidden">{L.leadShort}</span>
              <span className="max-md:hidden">{L.lead}</span>
            </p>
            <span className="flex w-fit items-center gap-2 rounded-sm bg-info-bg px-3 py-2 text-14 font-medium text-info">
              <Icon name="info" size={18} />
              <span className="md:hidden">{L.noNewFinanceShort}</span>
              <span className="max-md:hidden">{L.noNewFinance}</span>
            </span>
            <div className="flex flex-col gap-3 sm:flex-row">
              <Link href="/start" className={buttonClasses({ variant: "primary", size: "xl", className: cta })}>
                {L.cta}
              </Link>
              <Link href="/#how" className={buttonClasses({ variant: "secondary", size: "xl", className: cn(cta, "max-sm:hidden") })}>
                {L.howLink}
              </Link>
            </div>
          </div>
          <div role="img" aria-label={L.imageAlt} className="hatch hidden h-[420px] items-center justify-center rounded-lg border border-line lg:flex">
            <span dir="ltr" className="rounded-xs bg-white px-2.5 py-1.5 font-mono text-13 font-medium text-muted">
              calm lifestyle photo — person reviewing papers at home
            </span>
          </div>
        </section>

        {/* How it works */}
        <section id="how" aria-labelledby="how-h" className="scroll-mt-24 px-5 pb-12 md:px-10 xl:px-20">
          <h2 id="how-h" className="m-0 mb-5 text-22 font-bold md:text-26">
            {L.howTitle}
          </h2>
          <ol className="m-0 grid list-none grid-cols-1 gap-3 p-0 md:grid-cols-2 xl:grid-cols-4">
            {L.how.map((h, i) => (
              <li key={h.title} className={cn("flex gap-3 rounded-lg border bg-white p-4 md:flex-col md:p-5", i === 3 ? "border-orange" : "border-line")}>
                <span className={cn("flex size-8 flex-none items-center justify-center rounded-full text-15 font-bold", i === 3 ? "bg-orange text-ink" : "bg-charcoal text-white")}>
                  {i + 1}
                </span>
                <span className="flex flex-col gap-1">
                  <span className="text-13 text-muted max-md:hidden">{L.stepLabel(i + 1)}</span>
                  <strong className="text-17">{h.title}</strong>
                  <span className="text-15 leading-[24px] text-charcoal">
                    <span className="md:hidden">{h.short}</span>
                    <span className="max-md:hidden">{h.body}</span>
                  </span>
                </span>
              </li>
            ))}
          </ol>
        </section>

        {/* Help paths (proposed D-1; confirmed paths Q11) */}
        <section id="paths" aria-labelledby="paths-h" className="scroll-mt-24 bg-white px-5 py-12 md:px-10 xl:px-20">
          <h2 id="paths-h" className="m-0 mb-5 text-22 font-bold md:text-26">
            {L.pathsTitle}
          </h2>
          <ul className="m-0 grid list-none grid-cols-1 gap-3 p-0 md:grid-cols-2 xl:grid-cols-4">
            {L.paths.map((p) => (
              <li key={p.title} className="flex flex-col gap-2 rounded-lg border border-line bg-warm p-5">
                <Icon name={p.icon} size={26} className="text-rust" />
                <strong className="text-17">{p.title}</strong>
                <span className="text-15 leading-[25px] text-charcoal">{p.body}</span>
              </li>
            ))}
          </ul>
          <p className="m-0 mt-4 flex max-w-[60em] gap-2 text-14 leading-[22px] text-muted">
            <Icon name="info" size={18} className="flex-none" />
            {L.pathsNote}
          </p>
        </section>

        {/* What you find on Rahoon */}
        <section aria-labelledby="promises-h" className="px-5 py-12 md:px-10 xl:px-20">
          <h2 id="promises-h" className="m-0 mb-5 text-22 font-bold md:text-26">
            {L.promisesTitle}
          </h2>
          <ul className="m-0 grid list-none grid-cols-1 gap-3 p-0 md:grid-cols-3">
            {L.promises.map((p) => (
              <li key={p.title} className="flex flex-col gap-2 rounded-lg border border-line bg-white p-5">
                <Icon name={p.icon} size={26} className="text-charcoal" />
                <strong className="text-17">{p.title}</strong>
                <span className="text-15 leading-[25px] text-charcoal">{p.body}</span>
              </li>
            ))}
          </ul>
        </section>

        {/* Secondary: institutions */}
        <section id="lenders" aria-labelledby="lenders-h" className="mx-5 mb-14 flex scroll-mt-24 flex-col gap-3 rounded-lg border border-line bg-white p-5 md:mx-10 md:flex-row md:items-center md:gap-6 xl:mx-20">
          <div className="flex flex-1 flex-col gap-1">
            <h2 id="lenders-h" className="m-0 text-17 font-bold">
              {L.lendersTitle}
            </h2>
            <span className="text-15 leading-[24px] text-charcoal">{L.lendersBody}</span>
          </div>
          <Link href="/login" className={buttonClasses({ variant: "secondary", size: "lg", className: "min-h-11 text-15" })}>
            {L.staffLogin}
          </Link>
        </section>
      </main>
      <PublicFooter />
    </>
  );
}
