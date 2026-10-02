"use client";

import { useCallback, useEffect, useLayoutEffect, useRef, useState, type CSSProperties } from "react";

const GAP = 4;
const EDGE = 8;

/**
 * Open/close state for an anchored panel (dropdown, calendar, info tip). The panel is positioned with fixed coordinates taken
 * from the trigger, so no scrolling or clipping container (a sheet, a card, a table) can cut it off; it follows scroll and
 * resize, opens upwards when there isn't room below, and stays inside the screen. Closes on an outside press or Escape (focus
 * returns to the trigger).
 */
export function usePopover<T extends HTMLElement = HTMLButtonElement>(panelHeight = 320) {
  const [open, setOpenState] = useState(false);
  const [rect, setRect] = useState<DOMRect | null>(null);
  // eslint-disable-next-line @typescript-eslint/no-explicit-any -- the root is a div or a span depending on the component
  const rootRef = useRef<any>(null);
  const triggerRef = useRef<T>(null);

  const measure = useCallback(() => {
    if (triggerRef.current) setRect(triggerRef.current.getBoundingClientRect());
  }, []);

  const setOpen = useCallback(
    (next: boolean) => {
      if (next) measure();
      setOpenState(next);
    },
    [measure],
  );

  const close = useCallback((refocus = true) => {
    setOpenState(false);
    if (refocus) triggerRef.current?.focus();
  }, []);

  useLayoutEffect(() => {
    if (!open) return;
    measure();
    const onMove = () => measure();
    window.addEventListener("scroll", onMove, true);
    window.addEventListener("resize", onMove);
    return () => {
      window.removeEventListener("scroll", onMove, true);
      window.removeEventListener("resize", onMove);
    };
  }, [open, measure]);

  useEffect(() => {
    if (!open) return;
    const onDown = (e: PointerEvent) => {
      if (rootRef.current && !rootRef.current.contains(e.target as Node)) setOpenState(false);
    };
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        e.stopPropagation();
        e.preventDefault();
        close();
      }
    };
    document.addEventListener("pointerdown", onDown);
    document.addEventListener("keydown", onKey, true);
    return () => {
      document.removeEventListener("pointerdown", onDown);
      document.removeEventListener("keydown", onKey, true);
    };
  }, [open, close]);

  const vh = typeof window === "undefined" ? 800 : window.innerHeight;
  const vw = typeof window === "undefined" ? 1200 : window.innerWidth;
  const up = rect ? vh - rect.bottom < panelHeight + GAP && rect.top > vh - rect.bottom : false;

  /**
   * Fixed-position style for the panel. `width`: "trigger" (same width as the field, at least `minWidth`) or a pixel width.
   * `align`: "start" lines the panel up with the trigger's reading start (right in Arabic), "end" with its end.
   */
  const panelStyle = useCallback(
    ({ width = "trigger", minWidth = 192, align = "start" }: { width?: "trigger" | number; minWidth?: number; align?: "start" | "end" } = {}): CSSProperties => {
      if (!rect) return { position: "fixed", visibility: "hidden" };
      const rtl = typeof document !== "undefined" && document.documentElement.dir === "rtl";
      const w = Math.min(width === "trigger" ? Math.max(rect.width, minWidth) : width, vw - 2 * EDGE);
      const fromRight = (align === "start") === rtl;
      let left = fromRight ? rect.right - w : rect.left;
      left = Math.max(EDGE, Math.min(left, vw - w - EDGE));
      const space = up ? rect.top - GAP - EDGE : vh - rect.bottom - GAP - EDGE;
      return {
        position: "fixed",
        left,
        width: w,
        maxHeight: Math.max(160, space),
        ...(up ? { bottom: vh - rect.top + GAP } : { top: rect.bottom + GAP }),
      };
    },
    [rect, up, vh, vw],
  );

  return { open, setOpen, close, up, rootRef, triggerRef, panelStyle };
}
