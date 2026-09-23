"use client";

import { useState, type ReactNode } from "react";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import { cn } from "@/lib/cn";
import { useI18n } from "@/lib/i18n/client";

export function AccessDeniedBody({
  title,
  body,
  primary,
  requestAccess,
  code,
  at,
  tone,
  icon,
}: {
  title: string;
  body: ReactNode;
  primary: { label: string; href: string };
  requestAccess?: boolean;
  code: string;
  at: string;
  tone: "info" | "warn";
  icon: string;
}) {
  const { t } = useI18n();
  const A = t.auth.accessDenied;
  const [asked, setAsked] = useState(false);
  return (
    <div role="status" className="flex w-full max-w-[560px] flex-col gap-4">
      <span className={cn("flex size-14 items-center justify-center rounded-full", tone === "info" ? "bg-info-bg text-info" : "bg-warn-bg text-warn")}>
        <Icon name={icon} size={30} />
      </span>
      <h1 className="m-0 text-26 leading-[38px] font-bold md:text-28 md:leading-10">{title}</h1>
      <p className="m-0 text-17 leading-7 text-charcoal">{body}</p>
      <div className="flex flex-wrap gap-3">
        <Button size="lg" href={primary.href} className="min-h-11 px-[18px] text-15">
          {primary.label}
        </Button>
        {requestAccess ? (
          <Button variant="secondary" size="lg" aria-expanded={asked} onClick={() => setAsked(true)} className="min-h-11 text-15">
            {A.requestAccess}
          </Button>
        ) : null}
      </div>
      {asked ? (
        <Alert tone="info" compact>
          {A.requestAccessPending}
        </Alert>
      ) : null}
      <span className="text-13 text-muted">
        {A.codeLabel}{" "}
        <bdi dir="ltr" className="font-mono">
          {code}
        </bdi>{" "}
        · <bdi dir="ltr">{at}</bdi> · {A.logged}
      </span>
    </div>
  );
}
