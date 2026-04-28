"use client";

import { useEffect, useRef } from "react";

export type KeyBinding = {
  /** 'k' 같은 단일 키. 대소문자는 lowercased. */
  key: string;
  /** ⌘ (Mac) 또는 Ctrl (그 외) 동반 여부. */
  meta?: boolean;
  /** input/textarea/contentEditable 안에서도 발화시킬지 여부. 기본 false. */
  allowInInputs?: boolean;
};

const editableTags = new Set(["INPUT", "TEXTAREA", "SELECT"]);

function isInEditable(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) return false;
  if (editableTags.has(target.tagName)) return true;
  if (target.isContentEditable) return true;
  return false;
}

export function useKeyboardShortcut(
  binding: KeyBinding,
  handler: (e: KeyboardEvent) => void,
): void {
  // 항상 최신 핸들러를 호출하면서 effect 의존성은 단순하게 유지.
  const handlerRef = useRef(handler);
  useEffect(() => {
    handlerRef.current = handler;
  }, [handler]);

  useEffect(() => {
    const want = binding.key.toLowerCase();
    const wantMeta = !!binding.meta;
    function onKey(e: KeyboardEvent) {
      if (!binding.allowInInputs && isInEditable(e.target)) return;
      if (e.key.toLowerCase() !== want) return;
      const hasMeta = e.metaKey || e.ctrlKey;
      if (wantMeta !== hasMeta) return;
      e.preventDefault();
      handlerRef.current(e);
    }
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [binding.key, binding.meta, binding.allowInInputs]);
}

/** ⌘+K (또는 Ctrl+K). */
export function useCommandK(handler: () => void) {
  useKeyboardShortcut({ key: "k", meta: true, allowInInputs: true }, handler);
}
