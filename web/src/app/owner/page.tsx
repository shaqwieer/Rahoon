import Link from "next/link";
import type { ReactNode } from "react";
import { OwnerPage } from "@/components/owner/OwnerPage";
import { getOwnerContext, ownerGet, ownerMetadata } from "@/components/owner/server";
import type { OwnerHome } from "@/components/owner/types";
import { Card, SegmentProgress } from "@/components/owner/ui";
import { ServerText } from "@/components/owner/values";
import { LocaleSwitch } from "@/components/shell/LocaleSwitch";
import { Avatar, Button, Icon, IconButton } from "@/components/ui";
import { cn } from "@/lib/cn";
import { daysText, formatDate } from "@/lib/format";
import { getServerDictionary } from "@/lib/i18n/server";

export const generateMetadata = () => ownerMetadata((c) => c.home.title);

/**
 * D02 — owner home: one next step with its deadline, the 5-stage progress, quick tiles and the case manager.
 * Mobile 390 stacks; from 1024 the next-step card sits beside a side column (no sidebar).
 */
export default async function OwnerHomePage() {
  const { c, shell, fmt } = await getOwnerContext();
  const home = await ownerGet<OwnerHome>("/home");
  const { t } = await getServerDictionary();
  const H = c.home;
  const ns = home.nextStep;
  const stageLabel = fmt.locale === "ar" ? home.journey.label : (c.stages[home.journey.current - 1] ?? home.journey.label);
  const docsNeed = Number.parseInt(home.docsSummary, 10);
  const docsSub = Number.isFinite(docsNeed) && docsNeed > 0 ? H.docsNeed(docsNeed) : H.docsDone;
  const mgr = home.caseManager;
  const appt = mgr?.nextAppointment ?? null;
  const apptWhen = appt ? appt.replace(/^\S+\s/, "") : null;

  return (
    <OwnerPage
      shell={shell}
      title={t.owner.greeting(home.greetingName)}
      sub={home.lenderName ? t.owner.sub(home.lenderName) : undefined}
      active="home"
      width="wide"
    >
      <div className="grid items-start gap-3.5 lg:grid-cols-[minmax(0,1.4fr)_minmax(0,1fr)] lg:gap-6">
        {/* Next step — one primary action. */}
        <Card as="article" accent aria-labelledby="next-step-title" className="gap-2.5 p-[18px] lg:gap-3 lg:rounded-lg lg:p-7">
          <span className="text-14 font-bold text-muted lg:text-15">{H.nextStepEyebrow}</span>
          {ns ? (
            <>
              <h2 id="next-step-title" className="m-0 text-22 leading-8 font-bold lg:text-30 lg:leading-[42px]">
                {H.localizeNext ? H.nextTitles[ns.type] : <ServerText text={ns.title} />}
              </h2>
              <ServerText as="p" text={ns.body} className="m-0 text-17 leading-7 lg:text-19 lg:leading-8" />
              {ns.deadline ? (
                <p className={cn("m-0 flex items-center gap-1.5 text-15 lg:text-16", ns.daysLeft !== null && ns.daysLeft < 0 ? "text-err" : "text-warn")}>
                  <Icon name="schedule" size={18} />
                  {ns.daysLeft !== null && ns.daysLeft < 0 ? (
                    H.passed
                  ) : (
                    <span>
                      {H.until}{" "}
                      <bdi dir="ltr">{formatDate(ns.deadline, fmt)}</bdi>
                      {ns.daysLeft !== null ? (
                        <span className="lg:hidden"> {ns.daysLeft === 0 ? H.today : c.daysLeft(daysText(ns.daysLeft, fmt))}</span>
                      ) : null}
                    </span>
                  )}
                </p>
              ) : null}
              <div className="mt-1 flex flex-col gap-3 lg:mt-2 lg:flex-row">
                <Button href={ns.route} size="xl" className="min-h-[52px] text-17 lg:px-7">
                  {H.localizeNext ? H.nextCtas[ns.type] : ns.cta}
                </Button>
                <Button href="/owner/help" variant="secondary" size="xl" className="hidden min-h-[52px] text-17 lg:inline-grid">
                  {c.needHelp}
                </Button>
              </div>
            </>
          ) : (
            <>
              <h2 id="next-step-title" className="m-0 flex items-center gap-2 text-22 leading-8 font-bold lg:text-30 lg:leading-[42px]">
                <Icon name="task_alt" size={26} className="text-ok" />
                {H.nothingTitle}
              </h2>
              <p className="m-0 text-17 leading-7">{H.nothingBody}</p>
              <Button href="/owner/help" variant="secondary" size="xl" className="mt-1 hidden min-h-[52px] self-start text-17 lg:inline-grid">
                {c.needHelp}
              </Button>
            </>
          )}
        </Card>

        <div className="flex flex-col gap-3.5 lg:gap-4">
          <Card as="section" aria-labelledby="progress-title" className="gap-2 px-4 py-3.5 lg:gap-2.5 lg:rounded-lg lg:p-5">
            <div className="flex items-center justify-between gap-2 text-15">
              <h2 id="progress-title" className="m-0 text-15 font-normal text-muted">
                {H.progressTitle}
              </h2>
              <Link href="/owner/journey" className="inline-flex min-h-11 items-center font-semibold lg:hidden">
                {H.allStages}
              </Link>
            </div>
            <SegmentProgress current={home.journey.current} total={home.journey.total} label={H.progressLabel(home.journey.current, home.journey.total, stageLabel)} />
            <strong className="text-17 lg:text-18">{H.progressLabel(home.journey.current, home.journey.total, stageLabel)}</strong>
            <Link href="/owner/journey" className="hidden min-h-11 items-center self-start text-15 font-semibold lg:inline-flex">
              {H.allStages}
            </Link>
          </Card>

          <div className="grid grid-cols-2 gap-2.5">
            <Tile href="/owner/documents" icon={docsSub === H.docsDone ? "task_alt" : "upload_file"} iconClass={docsSub === H.docsDone ? "text-ok" : "text-warn"} title={H.docsTile} sub={fmt.locale === "ar" ? <ServerText text={home.docsSummary} /> : docsSub} />
            <Tile href="/owner/debt" icon="receipt_long" iconClass="text-charcoal" title={H.debtTile} sub={fmt.locale === "ar" ? <ServerText text={home.debtSummary} /> : H.debtSub} />
          </div>

          <Card as="section" aria-labelledby="manager-title" className="flex-row items-center gap-3 px-4 py-3.5 lg:rounded-lg lg:p-5">
            {mgr ? (
              <>
                <Avatar initials={mgr.initials} size={44} />
                <div className="flex min-w-0 flex-1 flex-col">
                  <h2 id="manager-title" className="m-0 text-15 font-bold lg:text-16">
                    {mgr.firstName} · {fmt.locale === "ar" ? mgr.role : H.managerRole}
                  </h2>
                  {appt ? (
                    fmt.locale === "ar" ? (
                      <ServerText text={appt} className="text-13 text-muted lg:text-14" />
                    ) : (
                      <span className="text-13 text-muted lg:text-14">
                        {appt.startsWith("مكالمتكم") ? H.appointmentCall : H.appointmentVisit} <bdi dir="ltr">{apptWhen}</bdi>
                      </span>
                    )
                  ) : null}
                </div>
                <IconButton href="/owner/messages" label={H.message(mgr.firstName)} icon="chat" size={48} iconSize={22} variant="outline" className="border-line-strong lg:hidden" />
                <Button href="/owner/messages" variant="secondary" className="hidden min-h-11 text-15 lg:inline-grid" aria-label={H.message(mgr.firstName)}>
                  {H.messageShort}
                </Button>
              </>
            ) : (
              <>
                <h2 id="manager-title" className="sr-only">
                  {H.messages}
                </h2>
                <span className="flex-1 text-15 leading-6">{H.noManager}</span>
                <IconButton href="/owner/messages" label={H.messages} icon="chat" size={48} iconSize={22} variant="outline" className="border-line-strong" />
              </>
            )}
          </Card>
        </div>
      </div>
      <div className="flex justify-center lg:hidden">
        <LocaleSwitch variant="link" />
      </div>
    </OwnerPage>
  );
}

function Tile({ href, icon, iconClass, title, sub }: { href: string; icon: string; iconClass: string; title: string; sub: ReactNode }) {
  return (
    <Link href={href} className="flex min-h-24 flex-col gap-1.5 rounded-[12px] border border-line bg-white p-3.5 text-ink no-underline hover:bg-subtle hover:text-ink">
      <Icon name={icon} size={24} className={iconClass} />
      <strong className="text-15">{title}</strong>
      <span className="text-13 text-muted">{sub}</span>
    </Link>
  );
}
