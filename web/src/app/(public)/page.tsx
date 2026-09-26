import type { Metadata } from "next";
import Image from "next/image";
import Link from "next/link";
import { PublicFooter, PublicHeader } from "@/components/shell/Public";
import { buttonClasses } from "@/components/ui/buttonStyles";
import { Icon } from "@/components/ui/Icon";
import { cn } from "@/lib/cn";
import { getServerDictionary } from "@/lib/i18n/server";
import heroImage from "../../../public/images/rahoon-homeowner-hero.png";

export async function generateMetadata(): Promise<Metadata> {
  const { t } = await getServerDictionary();
  return { title: t.individual.landing.metaTitle, description: t.individual.landing.metaDescription };
}

/** Individual-first public landing. Outcomes are never promised; the lender decides offers and the individual decides the response. */
export default async function LandingPage() {
  const { t } = await getServerDictionary();
  const L = t.individual.landing;
  const cta = "min-h-[54px] rounded-sm px-6 text-16 md:text-17";

  return (
    <>
      <PublicHeader />
      <main id="main" tabIndex={-1} className="flex-1 overflow-hidden bg-warm outline-none">
        <section className="relative border-b border-line bg-white">
          <div className="pointer-events-none absolute inset-x-0 top-0 h-40 bg-[radial-gradient(circle_at_15%_0%,rgba(244,99,58,0.11),transparent_55%)]" />
          <div className="relative mx-auto grid max-w-[1600px] grid-cols-1 items-center gap-9 px-5 py-8 md:px-10 md:py-14 lg:min-h-[650px] lg:grid-cols-[minmax(0,0.9fr)_minmax(520px,1.1fr)] lg:gap-14 xl:px-20 xl:py-16">
            <div className="z-10 flex flex-col gap-5 lg:py-8">
              <span className="flex w-fit items-center gap-2 rounded-pill border border-rust-200 bg-rust-50 px-3 py-1.5 text-13 font-semibold text-rust md:text-14">
                <span className="size-2 rounded-full bg-orange" />
                {L.eyebrow}
              </span>
              <h1 className="m-0 max-w-[14em] text-36 leading-[50px] font-bold tracking-[-0.02em] text-pretty md:text-52 md:leading-[68px]">{L.title}</h1>
              <p className="m-0 max-w-[36em] text-17 leading-[30px] text-pretty text-charcoal md:text-19 md:leading-[34px]">
                <span className="md:hidden">{L.leadShort}</span>
                <span className="max-md:hidden">{L.lead}</span>
              </p>
              <div className="flex flex-col gap-3 sm:flex-row">
                <Link href="/start" className={buttonClasses({ variant: "primary", size: "xl", className: cn(cta, "shadow-2") })}>
                  {L.cta}<Icon name="arrow_back" size={20} mirror />
                </Link>
                <Link href="/#how" className={buttonClasses({ variant: "secondary", size: "xl", className: cta })}>{L.howLink}</Link>
              </div>
              <ul className="m-0 flex list-none flex-col gap-2 border-t border-divider pt-4 text-14 text-charcoal sm:flex-row sm:flex-wrap sm:gap-x-5">
                {L.heroProof.map((item) => <li key={item} className="flex items-center gap-1.5"><Icon name="check_circle" size={18} className="text-ok" />{item}</li>)}
              </ul>
            </div>

            <div className="relative min-h-[360px] lg:min-h-[530px]">
              <div className="absolute inset-0 overflow-hidden rounded-[22px] bg-subtle shadow-3">
                <Image src={heroImage} alt={L.imageAlt} fill priority sizes="(max-width: 1023px) 100vw, 55vw" className="object-cover object-[64%_center]" />
                <div className="absolute inset-0 bg-gradient-to-t from-ink/35 via-transparent to-transparent" />
              </div>
              <div className="absolute inset-x-4 bottom-4 flex items-start gap-3 rounded-md border border-white/40 bg-white/95 p-4 shadow-2 backdrop-blur md:inset-x-auto md:bottom-6 md:start-6 md:max-w-[330px]">
                <span className="flex size-10 flex-none items-center justify-center rounded-full bg-rust-50 text-rust"><Icon name="support_agent" size={22} /></span>
                <div><strong className="block text-15">{L.imageBadgeTitle}</strong><span className="mt-0.5 block text-13 leading-5 text-muted">{L.imageBadgeBody}</span></div>
              </div>
            </div>
          </div>
        </section>

        <section id="how" aria-labelledby="how-h" className="scroll-mt-24 px-5 py-14 md:px-10 md:py-20 xl:px-20">
          <div className="mx-auto max-w-[1440px]">
            <div className="mb-8 max-w-[720px]">
              <span className="text-14 font-semibold text-rust">{L.howEyebrow}</span>
              <h2 id="how-h" className="m-0 mt-2 text-28 leading-[40px] font-bold md:text-36 md:leading-[50px]">{L.howTitle}</h2>
              <p className="m-0 mt-3 text-16 leading-7 text-muted md:text-17">{L.howIntro}</p>
            </div>
            <ol className="m-0 grid list-none grid-cols-1 gap-4 p-0 md:grid-cols-2 xl:grid-cols-4">
              {L.how.map((h, i) => (
                <li key={h.title} className="group relative flex min-h-[190px] flex-col gap-5 overflow-hidden rounded-lg border border-line bg-white p-5 shadow-1 transition-transform duration-200 hover:-translate-y-1">
                  <span className="absolute -end-3 -top-5 text-[86px] leading-none font-bold text-subtle" aria-hidden="true">{i + 1}</span>
                  <span className="relative flex size-10 items-center justify-center rounded-full bg-charcoal text-15 font-bold text-white">{i + 1}</span>
                  <span className="relative flex flex-col gap-1.5"><strong className="text-18">{h.title}</strong><span className="text-15 leading-[25px] text-charcoal">{h.body}</span></span>
                </li>
              ))}
            </ol>
          </div>
        </section>

        <section id="paths" aria-labelledby="paths-h" className="scroll-mt-24 bg-inv px-5 py-14 text-white md:px-10 md:py-20 xl:px-20">
          <div className="mx-auto max-w-[1440px]">
            <div className="grid gap-8 lg:grid-cols-[0.75fr_1.25fr] lg:gap-14">
              <div>
                <span className="text-14 font-semibold text-orange">{L.pathsEyebrow}</span>
                <h2 id="paths-h" className="m-0 mt-2 text-28 leading-[40px] font-bold md:text-36 md:leading-[50px]">{L.pathsTitle}</h2>
                <p className="m-0 mt-4 max-w-[34em] text-16 leading-7 text-inv-2">{L.pathsIntro}</p>
                <p className="m-0 mt-6 flex max-w-[38em] gap-2 rounded-md border border-inv-line bg-inv-raised p-4 text-13 leading-[22px] text-inv-2"><Icon name="info" size={19} className="flex-none text-orange" />{L.pathsNote}</p>
              </div>
              <ul className="m-0 grid list-none grid-cols-1 gap-3 p-0 sm:grid-cols-2">
                {L.paths.map((p) => (
                  <li key={p.title} className="flex flex-col gap-3 rounded-lg border border-inv-line bg-inv-raised p-5 transition-colors duration-200 hover:border-orange">
                    <span className="flex size-11 items-center justify-center rounded-md bg-white/5 text-orange"><Icon name={p.icon} size={25} /></span>
                    <strong className="text-18 text-white">{p.title}</strong><span className="text-15 leading-[25px] text-inv-2">{p.body}</span>
                  </li>
                ))}
              </ul>
            </div>
          </div>
        </section>

        <section aria-labelledby="promises-h" className="bg-white px-5 py-14 md:px-10 md:py-20 xl:px-20">
          <div className="mx-auto max-w-[1440px]">
            <h2 id="promises-h" className="m-0 max-w-[18em] text-28 leading-[40px] font-bold md:text-36 md:leading-[50px]">{L.promisesTitle}</h2>
            <ul className="m-0 mt-8 grid list-none grid-cols-1 gap-4 p-0 md:grid-cols-3">
              {L.promises.map((p) => (
                <li key={p.title} className="flex gap-4 rounded-lg border border-line bg-warm p-5 md:flex-col md:p-6">
                  <span className="flex size-11 flex-none items-center justify-center rounded-full bg-rust-50 text-rust"><Icon name={p.icon} size={24} /></span>
                  <span><strong className="block text-18">{p.title}</strong><span className="mt-1 block text-15 leading-[25px] text-charcoal">{p.body}</span></span>
                </li>
              ))}
            </ul>
          </div>
        </section>

        <section className="bg-white px-5 pb-14 md:px-10 md:pb-20 xl:px-20">
          <div className="mx-auto flex max-w-[1440px] flex-col items-start gap-6 rounded-[20px] bg-rust px-6 py-8 text-white shadow-2 md:flex-row md:items-center md:justify-between md:px-10 md:py-10">
            <div className="max-w-[720px]"><h2 className="m-0 text-26 leading-[38px] font-bold md:text-32 md:leading-[44px]">{L.finalTitle}</h2><p className="m-0 mt-2 text-15 leading-7 text-white/85 md:text-16">{L.finalBody}</p></div>
            <Link href="/start" className={buttonClasses({ variant: "secondary", size: "xl", className: "min-h-[52px] flex-none border-white bg-white px-6 text-16 text-ink hover:bg-subtle" })}>{L.finalCta}</Link>
          </div>
        </section>

        <section id="lenders" aria-labelledby="lenders-h" className="mx-5 mb-14 flex scroll-mt-24 flex-col gap-3 rounded-lg border border-line bg-white p-5 md:mx-10 md:flex-row md:items-center md:gap-6 xl:mx-auto xl:max-w-[1280px]">
          <div className="flex flex-1 flex-col gap-1"><h2 id="lenders-h" className="m-0 text-17 font-bold">{L.lendersTitle}</h2><span className="text-15 leading-[24px] text-charcoal">{L.lendersBody}</span></div>
          <Link href="/login" className={buttonClasses({ variant: "secondary", size: "lg", className: "min-h-11 text-15" })}>{L.staffLogin}</Link>
        </section>
      </main>
      <PublicFooter />
    </>
  );
}
