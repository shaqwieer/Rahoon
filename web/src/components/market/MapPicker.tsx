"use client";

import "leaflet/dist/leaflet.css";
import type * as Leaflet from "leaflet";
import { useEffect, useRef, useState } from "react";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";

/**
 * Map tiles and geocoding come from OpenStreetMap by default (attributed on the map). Production must point these at a
 * provider whose terms fit the expected traffic (decision D3 in docs/product/product-definition.md).
 */
export const TILE_URL = process.env.NEXT_PUBLIC_MAP_TILES ?? "https://tile.openstreetmap.org/{z}/{x}/{y}.png";
export const TILE_ATTRIBUTION = process.env.NEXT_PUBLIC_MAP_ATTRIBUTION ?? '&copy; <a href="https://www.openstreetmap.org/copyright">مساهمو OpenStreetMap</a>';
/** OSM's tile policy requires a Referer; the site default (same-origin) sends none, so tiles ask for the origin only. */
export const TILE_OPTIONS = { attribution: TILE_ATTRIBUTION, maxZoom: 19, referrerPolicy: "strict-origin-when-cross-origin" as const };
const GEOCODER = process.env.NEXT_PUBLIC_GEOCODER_URL ?? "https://nominatim.openstreetmap.org/search";

export function pinIcon(L: typeof Leaflet, tone: "rust" | "ink" = "rust") {
  return L.divIcon({
    className: "",
    html: `<span style="display:block;width:28px;height:28px;border-radius:50% 50% 50% 0;transform:rotate(-45deg);background:${tone === "rust" ? "#aa4528" : "#151513"};border:3px solid #fff;box-shadow:0 2px 6px rgba(0,0,0,.35)"></span>`,
    iconSize: [28, 28],
    iconAnchor: [14, 28],
  });
}

interface Point {
  lat: number;
  lng: number;
}

/**
 * Pick the property's real location: search (OpenStreetMap), tap the map, drag the pin, or use the device location.
 * No pin is shown until the person places one — the map never shows a default point as if it were the property.
 */
export function MapPicker({ value, center, onChange, disabled }: { value: Point | null; center: Point; onChange: (p: Point, label?: string) => void; disabled?: boolean }) {
  const box = useRef<HTMLDivElement>(null);
  const map = useRef<Leaflet.Map | null>(null);
  const marker = useRef<Leaflet.Marker | null>(null);
  const lib = useRef<typeof Leaflet | null>(null);
  const onChangeRef = useRef(onChange);
  const [query, setQuery] = useState("");
  const [searching, setSearching] = useState(false);
  const [message, setMessage] = useState<{ tone: "info" | "err"; text: string } | null>(null);
  const [results, setResults] = useState<{ label: string; lat: number; lng: number }[]>([]);

  useEffect(() => {
    onChangeRef.current = onChange;
  }, [onChange]);

  const place = (p: Point, label?: string, fly = true) => {
    const L = lib.current;
    if (!L || !map.current) return;
    if (!marker.current) {
      marker.current = L.marker([p.lat, p.lng], { draggable: !disabled, icon: pinIcon(L), keyboard: true, title: "موقع العقار" }).addTo(map.current);
      marker.current.on("dragend", () => {
        const ll = marker.current!.getLatLng();
        onChangeRef.current({ lat: ll.lat, lng: ll.lng });
      });
    } else marker.current.setLatLng([p.lat, p.lng]);
    if (fly) map.current.setView([p.lat, p.lng], Math.max(map.current.getZoom(), 15));
    onChangeRef.current(p, label);
  };

  useEffect(() => {
    let cancelled = false;
    let observer: ResizeObserver | null = null;
    void import("leaflet").then((L) => {
      if (cancelled || !box.current || map.current) return;
      lib.current = L;
      const m = L.map(box.current, { center: value ? [value.lat, value.lng] : [center.lat, center.lng], zoom: value ? 15 : 12, scrollWheelZoom: false });
      L.tileLayer(TILE_URL, TILE_OPTIONS).addTo(m);
      map.current = m;
      // iOS Safari can size the box after the map starts: redraw on every size change (otherwise a grey map).
      observer = new ResizeObserver(() => m.invalidateSize({ animate: false }));
      observer.observe(box.current);
      if (value) {
        marker.current = L.marker([value.lat, value.lng], { draggable: !disabled, icon: pinIcon(L), keyboard: true, title: "موقع العقار" }).addTo(m);
        marker.current.on("dragend", () => {
          const ll = marker.current!.getLatLng();
          onChangeRef.current({ lat: ll.lat, lng: ll.lng });
        });
      }
      if (!disabled) m.on("click", (e: Leaflet.LeafletMouseEvent) => place({ lat: e.latlng.lat, lng: e.latlng.lng }, undefined, false));
    });
    return () => {
      cancelled = true;
      observer?.disconnect();
      map.current?.remove();
      map.current = null;
      marker.current = null;
    };
    // The map is created once; later value changes move the pin through `place`.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Re-centre on the chosen city when no pin is placed yet.
  useEffect(() => {
    if (!marker.current && map.current) map.current.setView([center.lat, center.lng], 12);
  }, [center.lat, center.lng]);

  const search = async () => {
    if (query.trim().length < 3) return;
    setSearching(true);
    setMessage(null);
    setResults([]);
    try {
      const url = `${GEOCODER}?format=jsonv2&countrycodes=sa&accept-language=ar&limit=5&q=${encodeURIComponent(query.trim())}`;
      const res = await fetch(url, { headers: { accept: "application/json" } });
      if (!res.ok) throw new Error(String(res.status));
      const rows = (await res.json()) as { display_name: string; lat: string; lon: string }[];
      if (rows.length === 0) setMessage({ tone: "info", text: "لم نجد نتيجة. جرّب اسم الحي أو الشارع، أو ضع العلامة على الخريطة يدويًا." });
      setResults(rows.map((r) => ({ label: r.display_name, lat: Number(r.lat), lng: Number(r.lon) })));
    } catch {
      setMessage({ tone: "err", text: "البحث غير متاح الآن. حدد الموقع بالنقر على الخريطة أو بسحب العلامة." });
    } finally {
      setSearching(false);
    }
  };

  const locate = () => {
    if (!("geolocation" in navigator)) {
      setMessage({ tone: "err", text: "جهازك لا يتيح تحديد الموقع. ضع العلامة على الخريطة." });
      return;
    }
    navigator.geolocation.getCurrentPosition(
      (pos) => place({ lat: pos.coords.latitude, lng: pos.coords.longitude }),
      () => setMessage({ tone: "err", text: "لم نتمكن من معرفة موقعك. ضع العلامة على الخريطة يدويًا." }),
      { enableHighAccuracy: true, timeout: 10000 },
    );
  };

  return (
    <div className="flex flex-col gap-3">
      {!disabled ? (
        <div className="flex flex-col gap-2 sm:flex-row">
          <label className="sr-only" htmlFor="map-search">
            ابحث عن الموقع
          </label>
          <input id="map-search" value={query} onChange={(e) => setQuery(e.target.value)} onKeyDown={(e) => e.key === "Enter" && (e.preventDefault(), void search())}
            placeholder="ابحث باسم الحي أو الشارع أو المشروع" className="min-h-11 flex-1 rounded-sm border border-line-strong bg-white px-3 text-15" />
          <div className="flex gap-2">
            <Button variant="secondary" onClick={() => void search()} loading={searching}>
              <Icon name="search" size={18} />
              بحث
            </Button>
            <Button variant="secondary" onClick={locate}>
              <Icon name="my_location" size={18} />
              موقعي
            </Button>
          </div>
        </div>
      ) : null}
      {message ? <Alert tone={message.tone} compact>{message.text}</Alert> : null}
      {results.length ? (
        <ul className="m-0 flex list-none flex-col gap-1 p-0">
          {results.map((r) => (
            <li key={`${r.lat},${r.lng}`}>
              <button type="button" onClick={() => { place({ lat: r.lat, lng: r.lng }, r.label.split("،")[0]); setResults([]); }}
                className="flex min-h-10 w-full items-center gap-2 rounded-sm border border-line bg-white px-3 text-start text-14 hover:bg-subtle">
                <Icon name="location_on" size={18} className="text-rust" />
                <span className="line-clamp-1">{r.label}</span>
              </button>
            </li>
          ))}
        </ul>
      ) : null}
      <div dir="ltr" className="relative h-[320px] overflow-hidden rounded-md border border-line md:h-[380px]">
        <div ref={box} className="absolute inset-0 z-0" role="application" aria-label="خريطة لتحديد موقع العقار: انقر لوضع العلامة أو اسحبها" />
      </div>
      <p className="m-0 text-13 text-muted">
        {value ? "اسحب العلامة لضبط الموقع بدقة." : "لم يُحدد الموقع بعد: ابحث أو انقر على مكان العقار في الخريطة."}
      </p>
    </div>
  );
}
