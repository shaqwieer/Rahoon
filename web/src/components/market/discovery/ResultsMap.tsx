"use client";

import "leaflet/dist/leaflet.css";
import type * as Leaflet from "leaflet";
import { useEffect, useRef, useState } from "react";
import { TILE_OPTIONS, TILE_URL } from "@/components/market/MapPicker";
import { Icon } from "@/components/ui/Icon";
import { sarShort } from "@/lib/market/format";
import type { MapMarker } from "@/lib/market/types";

const SAUDI: [number, number] = [24.2, 45.1];
const CELL = 54; // px: markers closer than this at the current zoom are drawn as one cluster
const MAX_SPAN = 40;

function esc(s: string) {
  return s.replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c]!);
}

function markerIcon(L: typeof Leaflet, m: MapMarker, selected: boolean) {
  const price = sarShort(m.dueNow) ?? "؟";
  const ring = m.precision === "exact" ? "solid" : "dashed";
  const bg = selected ? "#151513" : "#ffffff";
  const fg = selected ? "#ffffff" : "#151513";
  return L.divIcon({
    className: "",
    html: `<span dir="rtl" style="display:inline-flex;align-items:center;gap:4px;white-space:nowrap;transform:translate(-50%,-100%) scale(${selected ? 1.12 : 1});transform-origin:50% 100%;padding:4px 10px;border-radius:999px;border:2px ${ring} ${selected ? "#151513" : "#aa4528"};background:${bg};color:${fg};font:600 12px/18px system-ui,sans-serif;box-shadow:0 2px 8px rgba(0,0,0,.28)">${esc(price)}${m.precision === "exact" ? "" : ' <span style="opacity:.7;font-weight:400">تقريبي</span>'}</span>`,
    iconSize: [0, 0],
  });
}

function clusterIcon(L: typeof Leaflet, count: number) {
  const size = count < 10 ? 38 : count < 100 ? 46 : 54;
  return L.divIcon({
    className: "",
    html: `<span style="display:flex;align-items:center;justify-content:center;width:${size}px;height:${size}px;border-radius:50%;background:#aa4528;color:#fff;font:700 14px system-ui,sans-serif;border:3px solid #fff;box-shadow:0 2px 10px rgba(0,0,0,.3)">${count}</span>`,
    iconSize: [size, size],
    iconAnchor: [size / 2, size / 2],
  });
}

/** "s,w,n,e" rounded for the URL. */
export function boundsParam(b: Leaflet.LatLngBounds) {
  const f = (v: number) => v.toFixed(4);
  return [f(b.getSouth()), f(b.getWest()), f(b.getNorth()), f(b.getEast())].join(",");
}

/** Fit the results (or Saudi Arabia when there are none). Skipped while the map box has no size yet. */
function fitResults(L: typeof Leaflet, m: Leaflet.Map, list: MapMarker[], bottomInset: number) {
  const size = m.getSize();
  if (size.x < 10 || size.y < 10) return;
  if (list.length === 0) m.setView(SAUDI, 5, { animate: false });
  else
    m.fitBounds(L.latLngBounds(list.map((x) => [x.lat, x.lng] as [number, number])), {
      paddingTopLeft: [56, 64], paddingBottomRight: [56, 40 + bottomInset], maxZoom: 14, animate: false,
    });
}

const ctrl = "pointer-events-auto inline-flex size-[34px] items-center justify-center rounded-sm border border-line-strong bg-white text-ink shadow-1 hover:bg-subtle";

/**
 * The search map: the same filtered set as the list (the API applies the same filters and visibility), drawn at the public
 * point only — an approximate location is labelled «تقريبي» and never shown as the exact spot. Nearby markers cluster; the card
 * selected in the strip is highlighted and brought into view; «ابحث في هذه المنطقة» appears only after the person moves the map
 * themselves. The map redraws whenever its box changes size: phones (notably iOS Safari) size the page after the map starts,
 * which otherwise leaves a grey map with no tiles and no markers.
 */
export function ResultsMap({ markers, selected, onSelect, onSearchArea, onClearArea, areaActive, loading, bottomInset = 0, height = "100%" }: {
  markers: MapMarker[];
  selected: string | null;
  onSelect: (reference: string) => void;
  onSearchArea: (bbox: string) => void;
  onClearArea: () => void;
  areaActive: boolean;
  loading?: boolean;
  /** Pixels covered at the bottom (the card strip), kept clear when fitting and panning. */
  bottomInset?: number;
  height?: number | string;
}) {
  const box = useRef<HTMLDivElement>(null);
  const lib = useRef<typeof Leaflet | null>(null);
  const map = useRef<Leaflet.Map | null>(null);
  const layer = useRef<Leaflet.LayerGroup | null>(null);
  const userMoving = useRef(false);
  const markersRef = useRef(markers);
  const areaRef = useRef(areaActive);
  const insetRef = useRef(bottomInset);
  const onSelectRef = useRef(onSelect);
  const [ready, setReady] = useState(false);
  const [moved, setMoved] = useState(false);
  const [tooWide, setTooWide] = useState(false);
  const [zoom, setZoom] = useState(0);
  const [tileTrouble, setTileTrouble] = useState(false);
  const [locating, setLocating] = useState(false);
  useEffect(() => {
    onSelectRef.current = onSelect;
    markersRef.current = markers;
    areaRef.current = areaActive;
    insetRef.current = bottomInset;
  }, [onSelect, markers, areaActive, bottomInset]);

  useEffect(() => {
    let cancelled = false;
    let observer: ResizeObserver | null = null;
    const el = box.current;
    const touched = () => {
      userMoving.current = true;
    };
    void import("leaflet").then((L) => {
      if (cancelled || !el) return;
      lib.current = L;
      const m = L.map(el, { center: SAUDI, zoom: 5, scrollWheelZoom: true, zoomControl: true, tapTolerance: 20 });
      let errors = 0;
      L.tileLayer(TILE_URL, TILE_OPTIONS)
        .on("tileerror", () => {
          errors += 1;
          if (errors >= 4) setTileTrouble(true);
        })
        .on("tileload", () => {
          errors = 0;
          setTileTrouble(false);
        })
        .addTo(m);
      layer.current = L.layerGroup().addTo(m);
      m.on("zoomend", () => setZoom(m.getZoom()));
      // Only the person's own drag / pinch / scroll offers «search this area»; the page fitting the results does not.
      ["pointerdown", "wheel", "touchstart"].forEach((t) => el.addEventListener(t, touched, { passive: true }));
      m.on("moveend", () => {
        if (userMoving.current) setMoved(true);
      });
      map.current = m;
      // Redraw on every size change; the first real size gets the results fitted.
      let last = "";
      observer = new ResizeObserver(() => {
        const w = el.clientWidth;
        const h = el.clientHeight;
        const key = `${w}x${h}`;
        if (key === last) return;
        const wasEmpty = last === "" || last.startsWith("0x") || last.endsWith("x0");
        last = key;
        m.invalidateSize({ animate: false });
        if (wasEmpty && w > 10 && h > 10 && !areaRef.current) {
          userMoving.current = false;
          fitResults(L, m, markersRef.current, insetRef.current);
        }
      });
      observer.observe(el);
      setReady(true);
    });
    return () => {
      cancelled = true;
      observer?.disconnect();
      ["pointerdown", "wheel", "touchstart"].forEach((t) => el?.removeEventListener(t, touched));
      map.current?.remove();
      map.current = null;
    };
  }, []);

  const fit = () => {
    if (!lib.current || !map.current) return;
    userMoving.current = false;
    fitResults(lib.current, map.current, markersRef.current, insetRef.current);
  };

  // New results: fit them (not while an area is applied: the person chose that window).
  useEffect(() => {
    if (!ready || areaActive) return;
    fit();
    // eslint-disable-next-line react-hooks/set-state-in-effect -- the view was reset by the new result set
    setMoved(false);
  }, [ready, markers, areaActive]);

  // A card chosen in the strip: bring its marker into view when it is off screen.
  useEffect(() => {
    const m = map.current;
    if (!ready || !m || !selected) return;
    const x = markers.find((k) => k.reference === selected);
    if (!x) return;
    const pt = m.latLngToContainerPoint([x.lat, x.lng]);
    const size = m.getSize();
    if (pt.x < 40 || pt.y < 70 || pt.x > size.x - 40 || pt.y > size.y - 30 - bottomInset) {
      userMoving.current = false;
      m.panTo([x.lat, x.lng], { animate: true });
    }
  }, [ready, selected, markers, bottomInset]);

  // Draw markers / clusters for the current zoom and selection.
  useEffect(() => {
    const L = lib.current;
    const m = map.current;
    const g = layer.current;
    if (!ready || !L || !m || !g) return;
    g.clearLayers();
    const z = m.getZoom();
    const cells = new Map<string, MapMarker[]>();
    for (const x of markers) {
      const p = m.project([x.lat, x.lng], z);
      const key = x.reference === selected ? `sel:${x.reference}` : `${Math.floor(p.x / CELL)}:${Math.floor(p.y / CELL)}`;
      cells.set(key, [...(cells.get(key) ?? []), x]);
    }
    for (const group of cells.values()) {
      if (group.length === 1) {
        const x = group[0];
        const mk = L.marker([x.lat, x.lng], { icon: markerIcon(L, x, x.reference === selected), title: x.title, riseOnHover: true, zIndexOffset: x.reference === selected ? 1000 : 0 });
        mk.on("click", () => onSelectRef.current(x.reference));
        g.addLayer(mk);
      } else {
        const b = L.latLngBounds(group.map((x) => [x.lat, x.lng] as [number, number]));
        const c = L.marker(b.getCenter(), { icon: clusterIcon(L, group.length), title: `${group.length} فرص`, keyboard: true });
        c.on("click", () => {
          // Same public point (approximate cells) or fully zoomed: select the first; the strip lists them all.
          if (b.getNorthEast().equals(b.getSouthWest(), 1e-6) || m.getZoom() >= 16) onSelectRef.current(group[0].reference);
          else m.fitBounds(b, { padding: [60, 60], maxZoom: 16 });
        });
        g.addLayer(c);
      }
    }
  }, [ready, markers, selected, zoom]);

  const searchHere = () => {
    const m = map.current;
    if (!m) return;
    const b = m.getBounds();
    if (b.getNorth() - b.getSouth() > MAX_SPAN || b.getEast() - b.getWest() > MAX_SPAN) {
      setTooWide(true);
      return;
    }
    setTooWide(false);
    setMoved(false);
    onSearchArea(boundsParam(b));
  };

  const locate = () => {
    const m = map.current;
    if (!m || !("geolocation" in navigator)) return;
    setLocating(true);
    // Used only to move the map here; the position is not sent anywhere.
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        setLocating(false);
        userMoving.current = true;
        m.setView([pos.coords.latitude, pos.coords.longitude], 13);
      },
      () => setLocating(false),
      { enableHighAccuracy: false, timeout: 10_000, maximumAge: 300_000 },
    );
  };

  return (
    <div dir="ltr" className="relative overflow-hidden rounded-lg border border-line bg-[#e8e4dc]" style={{ height }}>
      <div ref={box} className="absolute inset-0 z-0" role="region" aria-label="خريطة نتائج البحث" />
      {/* Below Leaflet's zoom buttons (top left): fit all results, my location. */}
      <div className="pointer-events-none absolute top-[86px] left-[10px] z-[500] flex flex-col gap-2">
        <button type="button" onClick={fit} className={ctrl} aria-label="عرض كل النتائج على الخريطة" title="عرض كل النتائج">
          <Icon name="fit_screen" size={20} />
        </button>
        <button type="button" onClick={locate} className={ctrl} aria-label="انتقل إلى موقعي" title="موقعي (لا يُرسل لأحد)">
          <Icon name={locating ? "progress_activity" : "my_location"} size={20} className={locating ? "animate-rh-spin" : undefined} />
        </button>
      </div>
      <div dir="rtl" className="pointer-events-none absolute top-3 right-3 z-[500] flex flex-col items-start gap-1 rounded-md bg-white/95 px-2.5 py-1.5 text-12 text-charcoal shadow-1">
        <span className="inline-flex items-center gap-1.5"><span className="inline-block size-3 rounded-full border-2 border-solid border-rust" />موقع دقيق</span>
        <span className="inline-flex items-center gap-1.5"><span className="inline-block size-3 rounded-full border-2 border-dashed border-rust" />موقع تقريبي</span>
      </div>
      <div dir="rtl" className="pointer-events-none absolute inset-x-0 z-[500] flex flex-wrap justify-center gap-2 px-16" style={{ top: 64 }}>
        {moved ? (
          <button type="button" onClick={searchHere} className="pointer-events-auto inline-flex min-h-10 items-center gap-1.5 rounded-pill bg-ink px-4 text-14 font-semibold text-white shadow-2">
            <Icon name="search" size={18} />
            ابحث في هذه المنطقة
          </button>
        ) : null}
        {areaActive ? (
          <button type="button" onClick={onClearArea} className="pointer-events-auto inline-flex min-h-10 items-center gap-1.5 rounded-pill border border-line-strong bg-white px-4 text-14 font-semibold text-ink shadow-2">
            <Icon name="close" size={18} />
            إلغاء حدود المنطقة
          </button>
        ) : null}
        {loading ? <span className="pointer-events-auto inline-flex min-h-10 items-center gap-1.5 rounded-pill bg-white px-3 text-13 shadow-1"><Icon name="progress_activity" size={16} className="animate-rh-spin" />جارٍ التحديث…</span> : null}
      </div>
      {tooWide || tileTrouble ? (
        <p dir="rtl" role="status" className="absolute inset-x-3 z-[500] m-0 rounded-md bg-white p-2 text-center text-13 shadow-2" style={{ bottom: 12 + bottomInset }}>
          {tooWide ? "المنطقة واسعة جدًا؛ قرّب الخريطة ثم ابحث فيها." : "تعذّر تحميل صور الخريطة الآن. تحقق من الاتصال؛ النتائج في القائمة كما هي."}
        </p>
      ) : null}
    </div>
  );
}
