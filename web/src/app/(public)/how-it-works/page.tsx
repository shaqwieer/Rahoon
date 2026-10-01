import type { Metadata } from "next";
import Link from "next/link";
import { M } from "@/components/market/copy";
import { buttonClasses } from "@/components/ui/buttonStyles";
import { Icon } from "@/components/ui/Icon";
import { PublicPage } from "../PublicPage";

export const metadata: Metadata = { title: M.how.metaTitle };

function Steps({ steps }: { steps: readonly { title: string; body: string }[] }) {
  return (
    <ol className="m-0 flex list-none flex-col p-0">
      {steps.map((s, i) => (
        <li key={s.title} className="relative flex gap-4 pb-6 last:pb-0">
          <span className="relative z-10 flex size-9 flex-none items-center justify-center rounded-full bg-charcoal text-15 font-bold text-white">{i + 1}</span>
          {i < steps.length - 1 ? <span className="absolute start-[17px] top-9 bottom-0 w-0.5 bg-line" aria-hidden="true" /> : null}
          <span className="flex flex-col gap-1 pt-1">
            <strong className="text-17">{s.title}</strong>
            <span className="text-15 leading-[25px] text-charcoal">{s.body}</span>
          </span>
        </li>
      ))}
    </ol>
  );
}

export default function HowItWorksPage() {
  const H = M.how;
  return (
    <PublicPage title={H.title} lead={H.intro}>
      <div className="flex flex-col gap-12">
        <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
          <section aria-labelledby="own-h" className="rounded-lg border border-line bg-white p-6 shadow-1">
            <h2 id="own-h" className="m-0 mb-5 text-22 leading-8 font-bold">{H.ownersTitle}</h2>
            <Steps steps={M.ownerJourney} />
            <Link href="/sell/new" className={buttonClasses({ variant: "primary", size: "lg", className: "mt-6 min-h-11" })}>
              {M.nav.startSell}
            </Link>
          </section>
          <section aria-labelledby="buy-h" className="rounded-lg border border-line bg-white p-6 shadow-1">
            <h2 id="buy-h" className="m-0 mb-5 text-22 leading-8 font-bold">{H.buyersTitle}</h2>
            <Steps steps={M.buyerJourney} />
            <Link href="/opportunities" className={buttonClasses({ variant: "secondary", size: "lg", className: "mt-6 min-h-11" })}>
              {M.home.ctaFind}
            </Link>
          </section>
        </div>

        <section aria-labelledby="tracks-h">
          <h2 id="tracks-h" className="m-0 mb-5 text-24 leading-9 font-bold">{H.tracksTitle}</h2>
          <ul className="m-0 grid list-none grid-cols-1 gap-4 p-0 md:grid-cols-3">
            {H.tracks.map((t) => (
              <li key={t.title} className="flex flex-col gap-3 rounded-lg border border-line bg-white p-5">
                <span className="flex size-11 items-center justify-center rounded-md bg-rust-50 text-rust">
                  <Icon name={t.icon} size={24} />
                </span>
                <strong className="text-18">{t.title}</strong>
                <span className="text-15 leading-[25px] text-charcoal">{t.body}</span>
              </li>
            ))}
          </ul>
        </section>

        <section aria-labelledby="review-h" className="surface-dark rounded-lg bg-inv p-6 text-white md:p-8">
          <h2 id="review-h" className="m-0 mb-4 text-22 leading-8 font-bold">{H.reviewTitle}</h2>
          <ul className="m-0 grid list-none grid-cols-1 gap-3 p-0 md:grid-cols-2">
            {H.review.map((r) => (
              <li key={r} className="flex gap-2 text-15 leading-6 text-inv-2">
                <Icon name="check_circle" size={20} className="flex-none text-orange" />
                {r}
              </li>
            ))}
          </ul>
        </section>

        <section aria-labelledby="fees-h" className="rounded-lg border border-line bg-white p-6">
          <h2 id="fees-h" className="m-0 mb-2 text-22 leading-8 font-bold">{H.feesTitle}</h2>
          <p className="m-0 text-15 leading-7 text-charcoal">{H.feesBody}</p>
        </section>

        <section aria-labelledby="faq-h">
          <h2 id="faq-h" className="m-0 mb-4 text-24 leading-9 font-bold">{H.faqTitle}</h2>
          <div className="flex flex-col gap-2">
            {H.faq.map((f) => (
              <details key={f.q} className="group rounded-md border border-line bg-white px-5 py-4 open:shadow-1">
                <summary className="flex cursor-pointer list-none items-center justify-between gap-4 text-16 font-semibold">
                  {f.q}
                  <Icon name="expand_more" size={22} className="transition-transform duration-200 group-open:rotate-180" />
                </summary>
                <p className="m-0 mt-2 text-15 leading-7 text-charcoal">{f.a}</p>
              </details>
            ))}
          </div>
        </section>
      </div>
    </PublicPage>
  );
}
