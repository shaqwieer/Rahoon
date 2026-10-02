"use client";

import "leaflet/dist/leaflet.css";
import type * as Leaflet from "leaflet";
import { useEffect, useRef } from "react";
import { pinIcon, TILE_OPTIONS, TILE_URL } from "./MapPicker";

/**
 * Read-only map of the location allowed for display. An approximate location is drawn as an area (circle), never as a pin
 * that would suggest the exact point; the exact pin is used only when the owner agreed to show it.
 */
export function LocationMap({ lat, lng, precision, height = 300 }: { lat: number; lng: number; precision: "exact" | "approximate"; height?: number }) {
  const box = useRef<HTMLDivElement>(null);
  useEffect(() => {
    let map: Leaflet.Map | null = null;
    let observer: ResizeObserver | null = null;
    let cancelled = false;
    void import("leaflet").then((L) => {
      if (cancelled || !box.current) return;
      map = L.map(box.current, { center: [lat, lng], zoom: precision === "exact" ? 15 : 14, scrollWheelZoom: false });
      L.tileLayer(TILE_URL, TILE_OPTIONS).addTo(map);
      if (precision === "exact") L.marker([lat, lng], { icon: pinIcon(L), keyboard: false, title: "موقع العقار" }).addTo(map);
      else L.circle([lat, lng], { radius: 650, color: "#aa4528", weight: 2, fillColor: "#f4633a", fillOpacity: 0.15 }).addTo(map);
      // iOS Safari can size the box after the map starts: redraw on every size change (otherwise a grey map).
      const m = map;
      observer = new ResizeObserver(() => {
        m.invalidateSize({ animate: false });
        m.setView([lat, lng], m.getZoom(), { animate: false });
      });
      observer.observe(box.current);
    });
    return () => {
      cancelled = true;
      observer?.disconnect();
      map?.remove();
    };
  }, [lat, lng, precision]);
  return (
    <div dir="ltr" className="relative overflow-hidden rounded-md border border-line" style={{ height }}>
      <div ref={box} className="absolute inset-0 z-0" role="img" aria-label={precision === "exact" ? "موقع العقار على الخريطة" : "المنطقة التقريبية للعقار على الخريطة"} />
    </div>
  );
}
