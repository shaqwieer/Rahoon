"use client";

import { useRef, useState, type KeyboardEvent } from "react";
import { Alert } from "@/components/ui/Alert";
import { Avatar } from "@/components/ui/Avatar";
import { Icon } from "@/components/ui/Icon";
import { apiSend, isApiError } from "@/lib/api/client";
import type { MeMembership } from "@/lib/api/types";
import { cn } from "@/lib/cn";
import { useI18n } from "@/lib/i18n/client";
import { hardNavigate } from "@/lib/navigation";

/**
 * S06 radiogroup of workspaces. Arrow keys move the selection, Enter/Space or a click enters the workspace
 * (POST /api/auth/context → full reload of `next`, so nothing cached crosses organizations).
 */
export function ContextList({ memberships, next }: { memberships: MeMembership[]; next: string }) {
  const { t, dir } = useI18n();
  const S = t.auth.selectContext;
  const initial = Math.max(0, memberships.findIndex((m) => m.current));
  const [focusIndex, setFocusIndex] = useState(initial);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const refs = useRef<Array<HTMLButtonElement | null>>([]);

  if (memberships.length === 0) {
    return <Alert tone="info">{S.empty}</Alert>;
  }

  const enter = async (m: MeMembership) => {
    if (busyId) return;
    setBusyId(m.id);
    setError(null);
    try {
      const res = await apiSend<{ next: string }>("POST", "/auth/context", { membershipId: m.id });
      // A deep link from before sign-in wins only when it belongs to the chosen workspace type.
      const target = next && sameArea(next, res.next) ? next : res.next;
      hardNavigate(target, "/");
    } catch (e) {
      setBusyId(null);
      setError(isApiError(e) && e.title ? e.title : t.auth.login.network);
    }
  };

  const onKeyDown = (e: KeyboardEvent<HTMLButtonElement>, i: number) => {
    let target: number | null = null;
    const nextKey = dir === "rtl" ? "ArrowLeft" : "ArrowRight";
    const prevKey = dir === "rtl" ? "ArrowRight" : "ArrowLeft";
    if (e.key === "ArrowDown" || e.key === nextKey) target = (i + 1) % memberships.length;
    else if (e.key === "ArrowUp" || e.key === prevKey) target = (i - 1 + memberships.length) % memberships.length;
    else if (e.key === "Home") target = 0;
    else if (e.key === "End") target = memberships.length - 1;
    if (target === null) return;
    e.preventDefault();
    setFocusIndex(target);
    refs.current[target]?.focus();
  };

  return (
    <>
      {error ? <Alert tone="err">{error}</Alert> : null}
      <div role="radiogroup" aria-label={S.listLabel} aria-busy={busyId ? true : undefined} className="flex flex-col gap-2.5">
        {memberships.map((m, i) => {
          const checked = i === focusIndex;
          const busy = busyId === m.id;
          const tile = m.kind === "platform" ? "rust" : m.current || checked ? "ink" : "subtle";
          return (
            <button
              key={m.id}
              ref={(el) => {
                refs.current[i] = el;
              }}
              type="button"
              role="radio"
              aria-checked={checked}
              tabIndex={checked ? 0 : -1}
              onKeyDown={(e) => onKeyDown(e, i)}
              onFocus={() => setFocusIndex(i)}
              onClick={() => enter(m)}
              className={cn(
                "flex min-h-[72px] w-full items-center gap-3.5 rounded-md bg-white px-4 py-3.5 text-start",
                checked ? "border-2 border-ink px-[15px] py-[13px]" : "border border-line hover:border-line-strong",
              )}
            >
              <Avatar initials={m.initials} size={44} shape="tile" tone={tile} />
              <span className="flex min-w-0 flex-1 flex-col gap-0.5">
                <strong className="text-16">{m.organization}</strong>
                {m.role ? <span className="text-14 text-charcoal">{m.role}</span> : null}
                <span className="text-12 text-muted">
                  {busy ? S.entering : m.current ? S.current : m.kind === "platform" ? S.platformStepUp : null}
                </span>
              </span>
              <Icon name={busy ? "progress_activity" : "chevron_left"} size={22} mirror={!busy} className={cn("text-muted", busy && "animate-rh-spin")} />
            </button>
          );
        })}
      </div>
    </>
  );
}

/** True when both paths live in the same portal (e.g. /cases/… and /portfolio are both lender routes). */
function sameArea(deepLink: string, home: string) {
  const portal = (p: string) => (["/provider", "/agent", "/platform", "/owner"].find((x) => p === x || p.startsWith(`${x}/`)) ?? "lender");
  return portal(deepLink) === portal(home);
}
