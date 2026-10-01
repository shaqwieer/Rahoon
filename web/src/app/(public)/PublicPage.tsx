import type { ReactNode } from "react";
import { PublicFooter, PublicHeader } from "@/components/shell/Public";

/** Header + titled main column + footer for the simple public pages. */
export function PublicPage({ eyebrow, title, lead, children, wide }: { eyebrow?: string; title: string; lead?: ReactNode; children: ReactNode; wide?: boolean }) {
  return (
    <>
      <PublicHeader />
      <main id="main" tabIndex={-1} className="flex-1 outline-none">
        <section className="border-b border-line bg-white px-4 py-10 md:px-10 md:py-14 xl:px-20">
          <div className={wide ? "mx-auto max-w-[1440px]" : "mx-auto max-w-[960px]"}>
            {eyebrow ? <span className="text-14 font-semibold text-rust">{eyebrow}</span> : null}
            <h1 className="m-0 mt-1 text-30 leading-[44px] font-bold text-pretty md:text-40 md:leading-[56px]">{title}</h1>
            {lead ? <p className="m-0 mt-3 max-w-[46em] text-16 leading-7 text-charcoal md:text-18 md:leading-8">{lead}</p> : null}
          </div>
        </section>
        <div className={wide ? "mx-auto w-full max-w-[1440px] px-4 py-10 md:px-10 md:py-14 xl:px-20" : "mx-auto w-full max-w-[960px] px-4 py-10 md:px-10 md:py-14"}>
          {children}
        </div>
      </main>
      <PublicFooter />
    </>
  );
}
