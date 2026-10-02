"use client";

import dynamic from "next/dynamic";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { CompareTray } from "@/components/market/discovery/CompareControls";
import { SaveSearch } from "@/components/market/discovery/SaveSearch";
import { SearchPanel } from "@/components/market/discovery/SearchPanel";
import { OpportunityCard } from "@/components/market/OpportunityCard";
import { Amount } from "@/components/market/ui";
import { Button } from "@/components/ui/Button";
import { buttonClasses } from "@/components/ui/buttonStyles";
import { Dropdown } from "@/components/ui/Dropdown";
import { Icon } from "@/components/ui/Icon";
import { apiSend, isApiError } from "@/lib/api/client";
import { cn } from "@/lib/cn";
import { chips, EMPTY, fromParams, hasFilters, PAGE_SIZE, SORTS, toQuery, type SearchState } from "@/lib/market/search";
import type { Catalog, MapResult, SearchResult } from "@/lib/market/types";

const ResultsMap = dynamic(() => import("./ResultsMap").then((m) => m.ResultsMap), {
  ssr: false,
  loading: () => <div className="size-full animate-rh-pulse rounded-lg bg-subtle" />,
});

const NO_MARKERS: MapResult["markers"] = [];

type Load<T> = { query: string; data: T | null; error: string | null; loading: boolean };

/**
 * /opportunities after the first server render: the URL is the state (shareable, reload-safe, back/forward through the native
 * history API, which Next keeps in step with useSearchParams). The list and the map are fetched from the same API filters; every
 * request is aborted when a newer one starts and a sequence number drops any late answer, so a slow response never overwrites
 * the latest search.
 */
export function DiscoveryView({ catalog, initial, initialQuery, signedIn }: { catalog: Catalog; initial: SearchResult | null; initialQuery: string; signedIn: boolean }) {
  const sp = useSearchParams();
  const state = useMemo(() => fromParams(sp), [sp]);
  const query = useMemo(() => toQuery(state), [state]);
  const mapQuery = useMemo(() => toQuery({ ...state, page: "" }, { page: false }), [state]);
  const [view, setViewState] = useState<"list" | "map">("list");
  const mapVisible = view === "map";
  // The chosen view is a per-browser preference (not part of the shareable search).
  useEffect(() => {
    try {
      // eslint-disable-next-line react-hooks/set-state-in-effect -- restore a stored preference after hydration
      if (window.localStorage.getItem("rahoon.discovery.view") === "map") setViewState("map");
    } catch {
      /* storage unavailable */
    }
  }, []);
  const setView = (v: "list" | "map") => {
    setViewState(v);
    try {
      window.localStorage.setItem("rahoon.discovery.view", v);
    } catch {
      /* storage unavailable */
    }
  };
  const [selected, setSelected] = useState<string | null>(null);
  const listTop = useRef<HTMLDivElement>(null);

  const [list, setList] = useState<Load<SearchResult>>({ query: initialQuery, data: initial, error: initial ? null : "تعذّر تحميل الفرص الآن.", loading: false });
  const [map, setMap] = useState<Load<MapResult>>({ query: "", data: null, error: null, loading: false });
  const listSeq = useRef(0);
  const mapSeq = useRef(0);
  const [retry, setRetry] = useState(0);

  useEffect(() => {
    if (list.query === query && retry === 0 && (list.data || list.error)) return;
    const id = ++listSeq.current;
    const ctrl = new AbortController();
    // eslint-disable-next-line react-hooks/set-state-in-effect -- a new search starts: show progress over the current results
    setList((l) => ({ ...l, loading: true, error: null }));
    apiSend<SearchResult>("GET", `/market/opportunities?${query ? `${query}&` : ""}pageSize=${PAGE_SIZE}`, undefined, { signal: ctrl.signal })
      .then((data) => {
        if (id === listSeq.current) setList({ query, data, error: null, loading: false });
      })
      .catch((err: unknown) => {
        if (ctrl.signal.aborted || id !== listSeq.current) return;
        setList({ query, data: null, error: isApiError(err) && err.status === 400 ? err.title : "تعذّر تحميل الفرص الآن. تحقق من اتصالك ثم أعد المحاولة.", loading: false });
      });
    return () => ctrl.abort();
    // eslint-disable-next-line react-hooks/exhaustive-deps -- runs per query (and on retry); `list` is read only to reuse the server render
  }, [query, retry]);

  useEffect(() => {
    if (!mapVisible || map.query === mapQuery) return;
    const id = ++mapSeq.current;
    const ctrl = new AbortController();
    // eslint-disable-next-line react-hooks/set-state-in-effect -- a new map request starts
    setMap((m) => ({ ...m, loading: true }));
    apiSend<MapResult>("GET", `/market/opportunities/map${mapQuery ? `?${mapQuery}` : ""}`, undefined, { signal: ctrl.signal })
      .then((data) => {
        if (id === mapSeq.current) setMap({ query: mapQuery, data, error: null, loading: false });
      })
      .catch(() => {
        if (ctrl.signal.aborted || id !== mapSeq.current) return;
        setMap({ query: mapQuery, data: null, error: "تعذّر تحميل الخريطة.", loading: false });
      });
    return () => ctrl.abort();
    // eslint-disable-next-line react-hooks/exhaustive-deps -- runs per map query while the map is shown
  }, [mapQuery, mapVisible]);

  const go = useCallback((next: SearchState) => {
    const q = toQuery(next);
    window.history.pushState(null, "", q ? `/opportunities?${q}` : "/opportunities");
    setSelected(null);
  }, []);

  const goPage = (p: number) => {
    go({ ...state, page: p > 1 ? String(p) : "" });
    listTop.current?.scrollIntoView({ behavior: "smooth", block: "start" });
  };

  const selectFromMap = (ref: string) => setSelected(ref);

  const data = list.data;
  const selectedItem = selected ? data?.items.find((i) => i.card.reference === selected) : null;
  const selectedMarker = selected && !selectedItem ? map.data?.markers.find((m) => m.reference === selected) : null;
  const suggestedName = chips(state, catalog).slice(0, 3).map((c) => c.text).join("، ") || "كل الفرص المنشورة";
  const saveQuery = toQuery({ ...state, page: "" }, { page: false });
  const sortOptions = [{ value: "", label: state.maxNow || state.maxInstallment || state.maxTotal || state.match ? "الأنسب (افتراضي)" : "الأحدث (افتراضي)" }, ...SORTS];

  return (
    <div className="flex flex-col gap-4 pb-16">
      <SearchPanel value={state} onApply={go} catalog={catalog} signedIn={signedIn} />

      {data?.profile && state.match === "me" ? (
        data.profile.applied ? (
          <p className="m-0 flex items-center gap-2 rounded-md border border-info-line bg-info-bg p-3 text-14">
            <Icon name="person_check" size={20} className="flex-none text-info" />
            نطبّق قدرتك الشرائية المسجلة (طلب <bdi dir="ltr" className="font-mono">{data.profile.reference}</bdi>، تحديث {data.profile.revision}) من حسابك؛ رابط هذه الصفحة لا يحمل أرقامك.
          </p>
        ) : (
          <p className="m-0 rounded-md border border-warn-line bg-warn-bg p-3 text-14">
            لا توجد قدرة شرائية مسجلة في حسابك بعد. <Link href="/buy/new">سجّلها</Link> لتطابق الفرص معها.
          </p>
        )
      ) : null}

      <div ref={listTop} className="flex scroll-mt-24 flex-wrap items-center justify-between gap-x-4 gap-y-2">
        <div role="status" aria-live="polite" className="flex min-w-0 flex-col">
          <span className="text-16">
            {data ? <><strong className="text-18">{data.total}</strong> {data.total === 1 ? "فرصة" : "فرص"}</> : "…"}
            {list.loading ? <Icon name="progress_activity" size={16} className="ms-2 inline animate-rh-spin text-muted" /> : null}
          </span>
          {data && data.excludedIncomplete > 0 ? (
            <span className="text-12 text-muted">استبعدنا {data.excludedIncomplete} {data.excludedIncomplete === 1 ? "فرصة أرقامها" : "فرص أرقامها"} غير مكتملة للمقارنة</span>
          ) : null}
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <Dropdown ariaLabel="الترتيب" icon="swap_vert" className="w-52" size="sm" value={state.sort} options={sortOptions} onChange={(v) => go({ ...state, sort: v, page: "" })} />
          <div role="group" aria-label="طريقة العرض" className="inline-flex rounded-sm border border-line-strong bg-white p-0.5">
            {(["list", "map"] as const).map((v) => (
              <button key={v} type="button" aria-pressed={view === v} onClick={() => setView(v)}
                className={cn("inline-flex min-h-9 items-center gap-1 rounded-xs px-3 text-13 font-semibold transition-colors", view === v ? "bg-ink text-white" : "text-charcoal hover:bg-subtle")}>
                <Icon name={v === "list" ? "grid_view" : "map"} size={18} />
                {v === "list" ? "القائمة" : "الخريطة"}
              </button>
            ))}
          </div>
          <SaveSearch query={saveQuery} suggestedName={suggestedName} signedIn={signedIn} />
        </div>
      </div>
      {data ? <p className="-mt-2 m-0 text-12 text-muted">الترتيب: {data.sortExplanation}</p> : null}

      {view === "list" ? (
        <div className="flex min-w-0 flex-col gap-5" aria-busy={list.loading || undefined}>
          {list.error && !data ? (
            <div role="alert" className="flex flex-col items-start gap-3 rounded-lg border border-err-line bg-err-bg p-5">
              <strong>{list.error}</strong>
              <Button variant="secondary" onClick={() => setRetry((r) => r + 1)}>إعادة المحاولة</Button>
            </div>
          ) : data && data.items.length === 0 ? (
            <EmptyState state={state} onApply={go} />
          ) : data ? (
            <>
              <ul className={cn("m-0 grid list-none grid-cols-1 gap-4 p-0 transition-opacity sm:grid-cols-2 lg:grid-cols-3 2xl:grid-cols-4", list.loading && "opacity-60")}>
                {data.items.map((i) => (
                  <li key={i.card.reference}>
                    <OpportunityCard card={i.card} fit={i.fit} match={i.match} signedIn={signedIn} />
                  </li>
                ))}
              </ul>
              {data.pages > 1 ? (
                <nav aria-label="الصفحات" className="flex items-center justify-center gap-2">
                  <Button variant="secondary" icon="chevron_right" disabled={data.page <= 1 || list.loading} onClick={() => goPage(data.page - 1)}>السابق</Button>
                  <span className="px-2 text-14">صفحة {data.page} من {data.pages}</span>
                  <Button variant="secondary" iconEnd="chevron_left" disabled={data.page >= data.pages || list.loading} onClick={() => goPage(data.page + 1)}>التالي</Button>
                </nav>
              ) : null}
            </>
          ) : (
            <div className="h-64 animate-rh-pulse rounded-lg bg-subtle" />
          )}
        </div>
      ) : (
        <div className="relative">
          <div className="h-[72vh] min-h-[420px]">
            {map.error ? (
              <div role="alert" className="flex size-full flex-col items-center justify-center gap-2 rounded-lg border border-err-line bg-err-bg p-4 text-14">{map.error}</div>
            ) : (
              <ResultsMap markers={map.data?.markers ?? NO_MARKERS} selected={selected} onSelect={selectFromMap} loading={map.loading}
                areaActive={Boolean(state.bbox)} onSearchArea={(bbox) => go({ ...state, bbox, page: "" })} onClearArea={() => go({ ...state, bbox: "", page: "" })} />
            )}
          </div>
          {selectedItem ? (
            <div className="absolute inset-x-3 bottom-10 z-[600] mx-auto max-w-sm">
              <button type="button" onClick={() => setSelected(null)} aria-label="إغلاق المعاينة"
                className="absolute -top-3 end-2 z-20 inline-flex size-8 items-center justify-center rounded-full border border-line bg-white shadow-2">
                <Icon name="close" size={18} />
              </button>
              <OpportunityCard card={selectedItem.card} fit={selectedItem.fit} match={selectedItem.match} signedIn={signedIn} layout="row" />
            </div>
          ) : selectedMarker ? (
            <div className="absolute inset-x-3 bottom-10 z-[600] mx-auto flex max-w-sm items-center gap-3 rounded-lg border border-line bg-white p-3 shadow-3">
              <span className="flex min-w-0 flex-1 flex-col">
                <strong className="truncate text-14">{selectedMarker.title}</strong>
                <span className="text-13">المطلوب الآن <Amount value={selectedMarker.dueNow} size="sm" strong unknown="غير مكتمل" /></span>
              </span>
              <Link href={`/opportunities/${selectedMarker.reference}`} className={buttonClasses({ variant: "primary", size: "sm" })}>التفاصيل</Link>
              <button type="button" onClick={() => setSelected(null)} aria-label="إغلاق المعاينة" className="inline-flex size-8 items-center justify-center rounded-full hover:bg-subtle">
                <Icon name="close" size={18} />
              </button>
            </div>
          ) : null}
          {map.data ? (
            <p className="m-0 mt-2 text-13 text-muted">
              {map.data.located} {map.data.located === 1 ? "فرصة" : "فرص"} على الخريطة
              {data && data.withoutLocation > 0 ? ` · ${data.withoutLocation} بلا موقع معروض` : ""}
              {map.data.capped ? ` · تعرض الخريطة أول ${map.data.cap} نتيجة؛ قرّب الخريطة أو ضيّق البحث` : ""}
            </p>
          ) : null}
        </div>
      )}

      <p className="m-0 text-12 leading-6 text-muted">
        الأرقام تقديرية ما لم يذكر أن الفريق راجعها، ولا تُعد عرضًا ملزمًا. «تناسب» تعني أرقامك المصرح بها فقط، وليست موافقة تمويل.
      </p>
      <CompareTray />
    </div>
  );
}

function EmptyState({ state, onApply }: { state: SearchState; onApply: (s: SearchState) => void }) {
  const maxNow = Number(state.maxNow || 0);
  const b = (text: string, next: Partial<SearchState>) => (
    <li><button type="button" className={buttonClasses({ variant: "secondary", size: "sm" })} onClick={() => onApply({ ...state, ...next, page: "" })}>{text}</button></li>
  );
  return (
    <div className="flex flex-col gap-4 rounded-lg border border-dashed border-line-strong bg-white p-6">
      <span className="flex items-center gap-2 text-17 font-bold">
        <Icon name="search_off" size={24} className="text-muted" />
        لا توجد فرص منشورة تطابق اختياراتك الآن
      </span>
      {hasFilters(state) ? (
        <>
          <span className="text-14 text-charcoal">يمكنك تعديل البحث بنفسك — لن نوسّعه دون علمك:</span>
          <ul className="m-0 flex list-none flex-wrap gap-2 p-0">
            {state.bbox ? b("ابحث خارج المنطقة المحددة", { bbox: "" }) : null}
            {state.city ? b("ابحث في كل المدن", { city: "" }) : null}
            {state.types ? b("كل أنواع العقار", { types: "" }) : null}
            {maxNow > 0 ? b(`ارفع المبلغ الآن إلى ${Math.round(maxNow * 1.2).toLocaleString("en-US")} ر.س`, { maxNow: String(Math.round(maxNow * 1.2)) }) : null}
            {state.maxInstallment ? b("دون حد للقسط", { maxInstallment: "", freq: "" }) : null}
            <li><button type="button" className={buttonClasses({ variant: "text", size: "sm" })} onClick={() => onApply({ ...EMPTY })}>مسح كل الفلاتر</button></li>
          </ul>
        </>
      ) : null}
      <span className="text-14 text-muted">
        أو <Link href="/buy/new">سجّل قدرتك الشرائية</Link> واحفظ بحثًا لتصلك الفرص الجديدة عند نشرها.
      </span>
    </div>
  );
}
