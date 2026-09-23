"use client";

import { useSyncExternalStore } from "react";

/* ───────── useNow: shared 1-second clock (0 during SSR/hydration so markup matches) ───────── */

let nowValue = 0;
const nowListeners = new Set<() => void>();
let nowTimer: number | undefined;

function subscribeNow(cb: () => void) {
  nowListeners.add(cb);
  if (nowTimer === undefined) {
    nowValue = Date.now();
    nowTimer = window.setInterval(() => {
      nowValue = Date.now();
      nowListeners.forEach((l) => l());
    }, 1000);
  }
  return () => {
    nowListeners.delete(cb);
    if (nowListeners.size === 0 && nowTimer !== undefined) {
      window.clearInterval(nowTimer);
      nowTimer = undefined;
    }
  };
}

/** Current epoch ms, ticking every second on the client; `0` on the server and during hydration. */
export function useNow(): number {
  return useSyncExternalStore(
    subscribeNow,
    () => nowValue,
    () => 0,
  );
}

/* ───────── sessionStorage JSON (per-tab hand-off, e.g. login → MFA) ───────── */

const storageListeners = new Set<() => void>();

function subscribeStorage(cb: () => void) {
  storageListeners.add(cb);
  window.addEventListener("storage", cb);
  return () => {
    storageListeners.delete(cb);
    window.removeEventListener("storage", cb);
  };
}

function readRaw(key: string): string | null {
  try {
    return window.sessionStorage.getItem(key);
  } catch {
    return null;
  }
}

/** Raw string snapshot of a sessionStorage key (null on the server). Parse with useMemo in the caller. */
export function useSessionValue(key: string): string | null {
  return useSyncExternalStore(
    subscribeStorage,
    () => readRaw(key),
    () => null,
  );
}

export function writeSessionValue(key: string, value: unknown): void {
  try {
    if (value === null || value === undefined) window.sessionStorage.removeItem(key);
    else window.sessionStorage.setItem(key, JSON.stringify(value));
  } catch {
    /* storage unavailable (private mode) — the flow still works with generic copy */
  }
  storageListeners.forEach((l) => l());
}

export function parseJson<T>(raw: string | null): T | null {
  if (!raw) return null;
  try {
    return JSON.parse(raw) as T;
  } catch {
    return null;
  }
}
