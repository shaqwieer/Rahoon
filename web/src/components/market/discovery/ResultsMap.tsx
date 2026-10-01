"use client";

import "leaflet/dist/leaflet.css";
import type * as Leaflet from "leaflet";
import { useEffect, useRef, useState } from "react";
import { TILE_OPTIONS, TILE_URL } from "@/components/market/MapPicker";
import { Icon } from "@/components/ui/Icon";
import { sar, sarShort } from "@/lib/market/format";
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
    html: `<span dir="rtl" style="display:inline-flex;align-items:center;gap:4px;white-space:nowrap;transform:translate(-50%,-100%);padding:3px 9px;border-radius:999px;border:2px ${ring} ${selected ? "#151513" : "#aa4528"};background:${bg};color:${fg};font:600 12px/18px system-ui,sans-serif;box-shadow:0 2px 6px rgba(0,0,0,.25)">${esc(price)}${m.precision === "exact" ? "" : ' <span style="opacity:.7;font-weight:400">تقريبي</span>'}</span>`,
    iconSize: [0, 0],
  });
}

function clusterIcon(L: typeof Leaflet, count: number) {
  const size = count < 10 ? 36 : count < 100 ? 44 : 52;
  return L.divIcon({
    className: "",
    html: `<span style="display:flex;align-items:center;justify-content:center;width:${size}px;height:${size}px;border-radius:50%;background:#aa4528;color:#fff;font:700 14px system-ui,sans-serif;border:3px solid #fff;box-shadow:0 2px 8px rgba(0,0,0,.3)">${count}</span>`,
    iconSize: [size, size],
    iconAnchor: [size / 2, size / 2],
  });
}

/** "s,w,n,e" rounded for the URL. */
export function boundsParam(b: Leaflet.LatLngBounds) {
  const f = (v: number) => v.toFixed(4);
  return [f(b.getSouth()), f(b.getWest()), f(b.getNorth()), f(b.getEast())].join(",");
}

/**
 * The search map: the same filtered set as the list (the API applies the same filters and visibility), drawn at the public
 * point only — an approximate location is labelled «تقريبي» and never shown as the exact spot. Clusters nearby markers,
 * highlights the card selected in the list, and offers «ابحث في هذه المنطقة» once the person moves the map.
 */
export function ResultsMap({ markers, selected, onSelect, onSearchArea, onClearArea, areaActive, loading, height = "100%" }: {
  markers: MapMarker[];
  selected: string | null;
  onSelect: (reference: string) => void;
  onSearchArea: (bbox: string) => void;
  onClearArea: () => void;
  areaActive: boolean;
  loading?: boolean;
  height?: number | string;
}) {
  const box = useRef<HTMLDivElement>(null);
  const lib = useRef<typeof Leaflet | null>(null);
  const map = useRef<Leaflet.Map | null>(null);
  const layer = useRef<Leaflet.LayerGroup | null>(null);
  const programmatic = useRef(false);
  const [ready, setReady] = useState(false);
  const [moved, setMoved] = useState(false);
  const [tooWide, setTooWide] = useState(false);
  const [zoom, setZoom] = useState(0);
  const onSelectRef = useRef(onSelect);
  useEffect(() => {
    onSelectRef.current = onSelect;
  }, [onSelect]);

  useEffect(() => {
    let cancelled = false;
    void import("leaflet").then((L) => {
      if (cancelled || !box.current) return;
      lib.current = L;
      const m = L.map(box.current, { center: SAUDI, zoom: 5, scrollWheelZoom: true, zoomControl: true });
      L.tileLayer(TILE_URL, TILE_OPTIONS).addTo(m);
      layer.current = L.layerGroup().addTo(m);
      m.on("zoomend", () => setZoom(m.getZoom()));
      // A move the page made (fitting the results) is not the person's: only theirs offers «search this area».
      m.on("moveend", () => {
        if (!programmatic.current) setMoved(true);
      });
      map.current = m;
      setReady(true);
    });
    return () => {
      cancelled = true;
      map.current?.remove();
      map.current = null;
    };
  }, []);

  // Fit the view to the results (not while an area is applied: the person chose that window).
  useEffect(() => {
    const L = lib.current;
    const m = map.current;
    if (!ready || !L || !m || areaActive) return;
    programmatic.current = true;
    if (markers.length === 0) m.setView(SAUDI, 5, { animate: false });
    else m.fitBounds(L.latLngBounds(markers.map((x) => [x.lat, x.lng] as [number, number])), { padding: [36, 36], maxZoom: 14, animate: false });
    const t = window.setTimeout(() => {
      programmatic.current = false;
    }, 400);
    setMoved(false);
    return () => window.clearTimeout(t);
  }, [ready, markers, areaActive]);

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
        mk.bindPopup(
          `<div dir="rtl" style="font:13px/20px system-ui,sans-serif;min-width:180px"><strong>${esc(x.title)}</strong><br/>${esc(x.cityLabel)}${x.district ? `، ${esc(x.district)}` : ""}<br/>المطلوب الآن: <b>${esc(sar(x.dueNow) ?? "غير مكتمل")}</b>${x.dueNow !== null ? " ر.س" : ""}${x.precision === "approximate" ? '<br/><span style="color:#666">موقع تقريبي</span>' : ""}<br/><a href="/opportunities/${encodeURIComponent(x.reference)}">عرض التفاصيل</a></div>`,
        );
        g.addLayer(mk);
      } else {
        const b = L.latLngBounds(group.map((x) => [x.lat, x.lng] as [number, number]));
        const c = L.marker(b.getCenter(), { icon: clusterIcon(L, group.length), title: `${group.length} فرص`, keyboard: true });
        c.on("click", () => {
          if (b.getNorthEast().equals(b.getSouthWest(), 1e-6) || m.getZoom() >= 16) {
            // Same public point (approximate cells): list them instead of zooming forever.
            c.bindPopup(`<div dir="rtl" style="font:13px/20px system-ui,sans-serif">${group.slice(0, 8).map((x) => `<a href="/opportunities/${encodeURIComponent(x.reference)}">${esc(x.title)}</a>`).join("<br/>")}${group.length > 8 ? `<br/>و${group.length - 8} أخرى` : ""}</div>`).openPopup();
          } else {
            m.fitBounds(b, { padding: [48, 48], maxZoom: 16 });
          }
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

  return (
    <div dir="ltr" className="relative overflow-hidden rounded-lg border border-line bg-subtle" style={{ height }}>
      <div ref={box} className="absolute inset-0 z-0" role="region" aria-label="خريطة نتائج البحث" />
      <div dir="rtl" className="pointer-events-none absolute inset-x-0 top-3 z-[500] flex flex-wrap justify-center gap-2 px-3">
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
      {tooWide ? (
        <p dir="rtl" role="status" className="absolute inset-x-3 bottom-10 z-[500] m-0 rounded-md bg-white p-2 text-center text-13 shadow-2">المنطقة واسعة جدًا؛ قرّب الخريطة ثم ابحث فيها.</p>
      ) : null}
      <div dir="rtl" className="absolute bottom-2 start-2 z-[500] flex gap-3 rounded-sm bg-white/90 px-2 py-1 text-12 text-charcoal">
        <span className="inline-flex items-center gap-1"><span className="inline-block size-3 rounded-full border-2 border-solid border-rust" />موقع دقيق</span>
        <span className="inline-flex items-center gap-1"><span className="inline-block size-3 rounded-full border-2 border-dashed border-rust" />موقع تقريبي</span>
      </div>
    </div>
  );
}
