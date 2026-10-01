"use client";

import { useSyncExternalStore } from "react";

/**
 * Opportunities picked for comparison: up to four references kept in this browser only (nothing is sent until the comparison
 * page asks the API, which answers for published opportunities only). Synced across tabs.
 */
export const COMPARE_MAX = 4;
const KEY = "rahoon.compare.v1";
const EVENT = "rahoon:compare";
const EMPTY: string[] = [];
let cache: { raw: string | null; list: string[] } = { raw: null, list: EMPTY };

function read(): string[] {
  let raw: string | null = null;
  try {
    raw = window.localStorage.getItem(KEY);
  } catch {
    return EMPTY;
  }
  if (raw === cache.raw) return cache.list;
  let list: string[] = EMPTY;
  try {
    const parsed: unknown = raw ? JSON.parse(raw) : [];
    if (Array.isArray(parsed)) list = parsed.filter((x): x is string => typeof x === "string" && x.length <= 20).slice(0, COMPARE_MAX);
  } catch {
    list = EMPTY;
  }
  cache = { raw, list };
  return list;
}

function write(list: string[]) {
  try {
    window.localStorage.setItem(KEY, JSON.stringify(list.slice(0, COMPARE_MAX)));
  } catch {
    /* private mode: the selection lives for this page only */
  }
  window.dispatchEvent(new Event(EVENT));
}

function subscribe(cb: () => void) {
  const onStorage = (e: StorageEvent) => {
    if (e.key === KEY) cb();
  };
  window.addEventListener("storage", onStorage);
  window.addEventListener(EVENT, cb);
  return () => {
    window.removeEventListener("storage", onStorage);
    window.removeEventListener(EVENT, cb);
  };
}

export function useCompare() {
  const list = useSyncExternalStore(subscribe, read, () => EMPTY);
  return {
    list,
    has: (ref: string) => list.includes(ref),
    full: list.length >= COMPARE_MAX,
    toggle: (ref: string) => {
      const cur = read();
      if (cur.includes(ref)) write(cur.filter((r) => r !== ref));
      else if (cur.length < COMPARE_MAX) write([...cur, ref]);
    },
    remove: (ref: string) => write(read().filter((r) => r !== ref)),
    set: (refs: string[]) => write(refs),
    clear: () => write([]),
  };
}
