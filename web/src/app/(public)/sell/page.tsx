import type { Metadata } from "next";
import Link from "next/link";
import { M } from "@/components/market/copy";
import { buttonClasses } from "@/components/ui/buttonStyles";
import { Icon } from "@/components/ui/Icon";
import { PublicPage } from "../PublicPage";

export const metadata: Metadata = { title: M.sell.metaTitle };

/** Seller landing: what the first request asks, what happens after, no promises. */
export default function SellPage() {
  const S = M.sell;
  return (
    <PublicPage eyebrow={S.eyebrow} title={S.title} lead={S.lead}>
      <div className="flex flex-col gap-10">
        <div className="flex flex-col gap-3 sm:flex-row">
          <Link href="/sell/new" className={buttonClasses({ variant: "primary", size: "xl", className: "min-h-[54px] shadow-2" })}>
            {S.cta}
            <Icon name="arrow_back" size={20} mirror />
          </Link>
          <Link href="/account" className={buttonClasses({ variant: "secondary", size: "xl", className: "min-h-[54px]" })}>
            {S.resume}
          </Link>
        </div>
        <section aria-labelledby="need-h">
          <h2 id="need-h" className="m-0 mb-4 text-22 leading-8 font-bold">{S.needTitle}</h2>
          <ol className="m-0 grid list-none grid-cols-1 gap-4 p-0 md:grid-cols-3">
            {S.need.map((n, i) => (
              <li key={n.title} className="flex flex-col gap-3 rounded-lg border border-line bg-white p-5 shadow-1">
                <span className="flex items-center gap-3">
                  <span className="flex size-10 items-center justify-center rounded-md bg-rust-50 text-rust">
                    <Icon name={n.icon} size={22} />
                  </span>
                  <span className="text-13 font-semibold text-muted">الخطوة {i + 1} من 3</span>
                </span>
                <strong className="text-17">{n.title}</strong>
                <span className="text-15 leading-[25px] text-charcoal">{n.body}</span>
              </li>
            ))}
          </ol>
        </section>
        <section aria-labelledby="after-h" className="rounded-lg border border-line bg-white p-6">
          <h2 id="after-h" className="m-0 mb-3 text-22 leading-8 font-bold">{S.afterTitle}</h2>
          <ul className="m-0 flex list-none flex-col gap-2 p-0">
            {S.after.map((a) => (
              <li key={a} className="flex gap-2 text-15 leading-7 text-charcoal">
                <Icon name="check" size={20} className="mt-1 flex-none text-ok" />
                {a}
              </li>
            ))}
          </ul>
        </section>
        <p className="m-0 flex gap-2 rounded-md border border-warn-line bg-warn-bg p-4 text-14 leading-6 text-charcoal">
          <Icon name="info" size={20} className="flex-none text-warn" />
          <span>
            <strong>{S.notTitle}: </strong>
            {S.not}
          </span>
        </p>
      </div>
    </PublicPage>
  );
}
