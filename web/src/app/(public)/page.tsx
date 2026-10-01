import type { Metadata } from "next";
import Image from "next/image";
import Link from "next/link";
import { M } from "@/components/market/copy";
import { LatestOpportunities } from "@/components/market/LatestOpportunities";
import { Reveal } from "@/components/market/Reveal";
import { PublicFooter, PublicHeader } from "@/components/shell/Public";
import { buttonClasses } from "@/components/ui/buttonStyles";
import { Icon } from "@/components/ui/Icon";
import { cn } from "@/lib/cn";
import heroImage from "../../../public/images/rahoon-marketplace-hero-v2.png";

export const metadata: Metadata = { title: { absolute: M.home.metaTitle }, description: M.meta.description };

function Journey({ steps, tone }: { steps: readonly { title: string; body: string }[]; tone: "light" | "dark" }) {
  return (
    <ol className="m-0 grid list-none grid-cols-1 gap-3 p-0 sm:grid-cols-2 lg:grid-cols-5">
      {steps.map((s, i) => (
        <li
          key={s.title}
          className={cn(
            "relative flex flex-col gap-3 rounded-lg border p-5",
            tone === "light" ? "border-line bg-white shadow-1" : "border-inv-line bg-inv-raised",
          )}
        >
          <span
            className={cn(
              "flex size-9 items-center justify-center rounded-full text-15 font-bold",
              tone === "light" ? "bg-charcoal text-white" : "bg-orange text-ink",
            )}
          >
            {i + 1}
          </span>
          <strong className={cn("text-17", tone === "dark" && "text-white")}>{s.title}</strong>
          <span className={cn("text-14 leading-6", tone === "light" ? "text-charcoal" : "text-inv-2")}>{s.body}</span>
        </li>
      ))}
    </ol>
  );
}

/** Home: two clear entries (sell / find), both journeys, the team's role, live opportunities, no promises. */
export default function HomePage() {
  const H = M.home;
  const cta = "min-h-[54px] rounded-sm px-6 text-16 md:text-17";
  return (
    <>
      <PublicHeader />
      <main id="main" tabIndex={-1} className="flex-1 overflow-hidden bg-warm outline-none">
        <section className="relative border-b border-line bg-white">
          <div className="pointer-events-none absolute inset-x-0 top-0 h-40 bg-[radial-gradient(circle_at_15%_0%,rgba(244,99,58,0.11),transparent_55%)]" />
          <div className="relative mx-auto grid max-w-[1600px] grid-cols-1 items-center gap-9 px-4 py-8 md:px-10 md:py-14 lg:grid-cols-[minmax(0,1fr)_minmax(480px,1fr)] lg:gap-14 xl:px-20">
            <div className="z-10 flex flex-col gap-5">
              <span className="flex w-fit items-center gap-2 rounded-pill border border-rust-200 bg-rust-50 px-3 py-1.5 text-13 font-semibold text-rust md:text-14">
                <span className="size-2 rounded-full bg-orange" />
                {H.eyebrow}
              </span>
              <h1 className="m-0 max-w-[16em] text-30 leading-[44px] font-bold tracking-[-0.01em] text-pretty md:text-[44px] md:leading-[62px]">{H.title}</h1>
              <div className="flex flex-col gap-3">
                <p className="m-0 flex gap-2 text-16 leading-7 text-charcoal md:text-18 md:leading-8">
                  <Icon name="sell" size={22} className="mt-1 text-rust" />
                  {H.sellerLine}
                </p>
                <p className="m-0 flex gap-2 text-16 leading-7 text-charcoal md:text-18 md:leading-8">
                  <Icon name="search" size={22} className="mt-1 text-rust" />
                  {H.buyerLine}
                </p>
              </div>
              <div className="flex flex-col gap-3 sm:flex-row">
                <Link href="/sell/new" className={buttonClasses({ variant: "primary", size: "xl", className: cn(cta, "shadow-2") })}>
                  {H.ctaSell}
                  <Icon name="arrow_back" size={20} mirror />
                </Link>
                <Link href="/opportunities" className={buttonClasses({ variant: "secondary", size: "xl", className: cta })}>
                  <Icon name="travel_explore" size={20} />
                  {H.ctaFind}
                </Link>
              </div>
              <ul className="m-0 flex list-none flex-col gap-2 border-t border-divider pt-4 text-14 text-charcoal sm:flex-row sm:flex-wrap sm:gap-x-5">
                {H.heroFacts.map((item) => (
                  <li key={item} className="flex items-center gap-1.5">
                    <Icon name="check_circle" size={18} className="text-ok" />
                    {item}
                  </li>
                ))}
              </ul>
            </div>
            <div className="relative min-h-[300px] lg:min-h-[500px]">
              <div className="absolute inset-0 overflow-hidden rounded-[22px] bg-subtle shadow-3">
                <Image src={heroImage} alt={H.imageAlt} fill priority sizes="(max-width: 1023px) 100vw, 50vw" className="object-cover object-center" />
                <div className="absolute inset-0 bg-gradient-to-t from-ink/35 via-transparent to-transparent" />
              </div>
              <div className="absolute inset-x-4 bottom-4 flex items-start gap-3 rounded-md border border-white/40 bg-white/95 p-4 shadow-2 backdrop-blur md:inset-x-auto md:bottom-6 md:start-6 md:max-w-[340px]">
                <span className="flex size-10 flex-none items-center justify-center rounded-full bg-rust-50 text-rust">
                  <Icon name="verified_user" size={22} />
                </span>
                <div>
                  <strong className="block text-15">{H.badgeTitle}</strong>
                  <span className="mt-0.5 block text-13 leading-5 text-muted">{H.badgeBody}</span>
                </div>
              </div>
            </div>
          </div>
        </section>

        <section aria-labelledby="owners-h" className="px-4 py-14 md:px-10 md:py-20 xl:px-20">
          <Reveal className="mx-auto max-w-[1440px]">
            <div className="mb-8 flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
              <div className="max-w-[720px]">
                <span className="text-14 font-semibold text-rust">{H.ownersEyebrow}</span>
                <h2 id="owners-h" className="m-0 mt-2 text-26 leading-[38px] font-bold md:text-34 md:leading-[48px]">{H.ownersTitle}</h2>
                <p className="m-0 mt-3 text-16 leading-7 text-muted md:text-17">{H.ownersIntro}</p>
              </div>
              <Link href="/sell" className={buttonClasses({ variant: "secondary", size: "lg", className: "min-h-11 flex-none" })}>
                {M.nav.sell}
                <Icon name="arrow_back" size={18} mirror />
              </Link>
            </div>
            <Journey steps={M.ownerJourney} tone="light" />
          </Reveal>
        </section>

        <section aria-labelledby="buyers-h" className="bg-inv px-4 py-14 text-white md:px-10 md:py-20 xl:px-20">
          <Reveal className="mx-auto max-w-[1440px]">
            <div className="mb-8 flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
              <div className="max-w-[720px]">
                <span className="text-14 font-semibold text-orange">{H.buyersEyebrow}</span>
                <h2 id="buyers-h" className="m-0 mt-2 text-26 leading-[38px] font-bold md:text-34 md:leading-[48px]">{H.buyersTitle}</h2>
                <p className="m-0 mt-3 text-16 leading-7 text-inv-2 md:text-17">{H.buyersIntro}</p>
              </div>
              <Link href="/buy/new" className={buttonClasses({ variant: "inverse", size: "lg", className: "min-h-11 flex-none" })}>
                {H.latestEmptyCta}
                <Icon name="arrow_back" size={18} mirror />
              </Link>
            </div>
            <Journey steps={M.buyerJourney} tone="dark" />
          </Reveal>
        </section>

        <section aria-labelledby="latest-h" className="px-4 py-14 md:px-10 md:py-20 xl:px-20">
          <div className="mx-auto max-w-[1440px]">
            <div className="mb-6 flex items-end justify-between gap-4">
              <h2 id="latest-h" className="m-0 text-24 leading-9 font-bold md:text-30 md:leading-[44px]">{H.latestTitle}</h2>
              <Link href="/opportunities" className="text-15 font-semibold">{H.latestAll}</Link>
            </div>
            <LatestOpportunities />
          </div>
        </section>

        <section aria-labelledby="team-h" className="bg-white px-4 py-14 md:px-10 md:py-20 xl:px-20">
          <Reveal className="mx-auto max-w-[1440px]">
            <span className="text-14 font-semibold text-rust">{H.teamEyebrow}</span>
            <h2 id="team-h" className="m-0 mt-2 text-26 leading-[38px] font-bold md:text-34 md:leading-[48px]">{H.teamTitle}</h2>
            <ul className="m-0 mt-8 grid list-none grid-cols-1 gap-4 p-0 md:grid-cols-3">
              {H.team.map((p) => (
                <li key={p.title} className="flex gap-4 rounded-lg border border-line bg-warm p-5 md:flex-col md:p-6">
                  <span className="flex size-11 flex-none items-center justify-center rounded-full bg-rust-50 text-rust">
                    <Icon name={p.icon} size={24} />
                  </span>
                  <span>
                    <strong className="block text-18">{p.title}</strong>
                    <span className="mt-1 block text-15 leading-[25px] text-charcoal">{p.body}</span>
                  </span>
                </li>
              ))}
            </ul>
          </Reveal>
        </section>

        <section className="px-4 py-14 md:px-10 md:py-20 xl:px-20">
          <div className="mx-auto grid max-w-[1440px] grid-cols-1 gap-4 lg:grid-cols-2">
            <div className="flex flex-col items-start gap-4 rounded-[20px] border border-line bg-white p-6 shadow-1 md:p-8">
              <span className="flex size-12 items-center justify-center rounded-md bg-rust-50 text-rust">
                <Icon name="calculate" size={26} />
              </span>
              <h2 className="m-0 text-24 leading-9 font-bold">{H.calcTitle}</h2>
              <p className="m-0 text-15 leading-7 text-charcoal">{H.calcBody}</p>
              <Link href="/calculators" className={buttonClasses({ variant: "secondary", size: "lg", className: "min-h-11" })}>
                {H.calcCta}
              </Link>
            </div>
            <div className="flex flex-col gap-4 rounded-[20px] border border-warn-line bg-warn-bg p-6 md:p-8">
              <h2 className="m-0 flex items-center gap-2 text-24 leading-9 font-bold">
                <Icon name="info" size={24} className="text-warn" />
                {H.noPromisesTitle}
              </h2>
              <ul className="m-0 flex list-none flex-col gap-2 p-0 text-15 leading-7 text-charcoal">
                {H.noPromises.map((x) => (
                  <li key={x} className="flex gap-2">
                    <Icon name="remove" size={18} className="mt-1.5 text-warn" />
                    {x}
                  </li>
                ))}
              </ul>
            </div>
          </div>
        </section>

        <section className="px-4 pb-14 md:px-10 md:pb-20 xl:px-20">
          <div className="mx-auto flex max-w-[1440px] flex-col items-start gap-6 rounded-[20px] bg-rust px-6 py-8 text-white shadow-2 md:flex-row md:items-center md:justify-between md:px-10 md:py-10">
            <div className="max-w-[720px]">
              <h2 className="m-0 text-26 leading-[38px] font-bold md:text-32 md:leading-[44px]">{H.finalTitle}</h2>
              <p className="m-0 mt-2 text-15 leading-7 text-white/85 md:text-16">{H.finalBody}</p>
            </div>
            <div className="flex flex-col gap-3 sm:flex-row">
              <Link href="/sell/new" className={buttonClasses({ variant: "secondary", size: "xl", className: "min-h-[52px] flex-none border-white bg-white px-6 text-16 text-ink hover:bg-subtle" })}>
                {H.ctaSell}
              </Link>
              <Link href="/opportunities" className="inline-grid min-h-[52px] flex-none grid-flow-col items-center justify-center gap-1.5 rounded-[8px] border border-white/70 px-6 text-16 font-semibold text-white no-underline transition-colors duration-[var(--dur-fast)] hover:bg-white/10 hover:text-white">
                {H.ctaFind}
              </Link>
            </div>
          </div>
        </section>
      </main>
      <PublicFooter />
    </>
  );
}
