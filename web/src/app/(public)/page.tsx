import Link from "next/link";
import { PublicFooter, PublicHeader } from "@/components/shell/Public";
import { buttonClasses } from "@/components/ui/buttonStyles";
import { Icon } from "@/components/ui/Icon";
import { getServerDictionary } from "@/lib/i18n/server";

/**
 * TEMPORARY S01 landing: public header, hero and «ما لا تقوم به رهون» band from the B2 spec.
 * The remaining sections (كيف تعمل، الجمهوران، الحوكمة) are added when S01 is implemented.
 */
export default async function LandingPage() {
  const { t } = await getServerDictionary();
  const P = t.public;
  return (
    <>
      <PublicHeader />
      <main id="main" tabIndex={-1} className="flex-1 outline-none">
        <section className="grid grid-cols-1 items-center gap-10 px-5 pt-8 pb-10 md:px-10 md:py-16 lg:grid-cols-[minmax(0,1fr)_minmax(0,560px)] lg:gap-16 xl:px-20 xl:pt-24 xl:pb-20">
          <div className="flex flex-col gap-[18px] md:gap-6">
            <span className="text-14 font-semibold text-rust md:text-15">
              <span className="md:hidden">{P.heroEyebrowShort}</span>
              <span className="max-md:hidden">{P.heroEyebrow}</span>
            </span>
            <h1 className="m-0 text-32 leading-[46px] font-bold text-pretty md:text-52 md:leading-[72px]">{P.heroTitle}</h1>
            <p className="m-0 max-w-[34em] text-17 leading-[29px] text-pretty text-charcoal md:text-20 md:leading-[34px]">
              <span className="md:hidden">{P.heroLeadShort}</span>
              <span className="max-md:hidden">{P.heroLead}</span>
            </p>
            <div className="flex flex-col gap-3 sm:flex-row">
              <Link href="/demo" className={buttonClasses({ variant: "primary", size: "xl", className: "min-h-[52px] rounded-sm px-6 text-16 md:text-17" })}>
                {P.demo}
              </Link>
              <Link href="/#owners" className={buttonClasses({ variant: "secondary", size: "xl", className: "min-h-[52px] rounded-sm px-6 text-16 md:text-17" })}>
                {P.heroOwners}
              </Link>
            </div>
          </div>
          <div role="img" aria-label={P.heroShotAlt} className="hatch hidden h-[420px] items-center justify-center rounded-lg border border-line md:flex">
            <span dir="ltr" className="rounded-xs bg-white px-2.5 py-1.5 font-mono text-13 font-medium text-muted">
              product screenshot — case workspace (masked data)
            </span>
          </div>
        </section>
        <section
          aria-labelledby="notwhat"
          className="mx-5 mb-16 grid grid-cols-1 items-center gap-4 rounded-md border border-line bg-white p-4 md:mx-10 md:gap-6 md:rounded-lg md:px-7 md:py-6 lg:grid-cols-[220px_repeat(4,minmax(0,1fr))] xl:mx-20"
        >
          <h2 id="notwhat" className="m-0 text-15 leading-7 font-bold md:text-18">
            {P.notWhatTitle}
          </h2>
          <p className="m-0 text-14 leading-[22px] md:hidden">{P.notWhatShort}</p>
          {P.notWhat.map((item) => (
            <span key={item} className="flex gap-2 text-15 leading-6 max-md:hidden">
              <Icon name="do_not_disturb_on" size={20} className="text-muted" />
              {item}
            </span>
          ))}
        </section>
      </main>
      <PublicFooter />
    </>
  );
}
