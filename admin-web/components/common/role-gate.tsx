"use client";

import { cloneElement, isValidElement, type ReactElement, type ReactNode } from "react";

import { useAdminAccount } from "@/lib/use-admin-account";
import { canRole, type AdminAction } from "@/lib/permissions";

type RoleGateProps = {
  action: AdminAction;
  children: ReactNode;
  /** "hide" (기본): DOM 자체에서 제거. "disable": 자식 element에 disabled prop 부여 + tooltip. */
  fallback?: "hide" | "disable";
  /** disable 모드에서 표시할 사유 — title 속성으로 들어감. */
  reason?: string;
};

/**
 * RBAC 가드.
 *  <RoleGate action="user.suspend_perm">
 *    <Button>영구 정지</Button>
 *  </RoleGate>
 */
export function RoleGate({
  action,
  children,
  fallback = "hide",
  reason,
}: RoleGateProps) {
  const auth = useAdminAccount();
  const allowed = canRole(auth?.account.role, action);
  if (allowed) return <>{children}</>;
  if (fallback === "hide") return null;
  if (!isValidElement(children)) return null;
  const child = children as ReactElement<{ disabled?: boolean; title?: string }>;
  return cloneElement(child, {
    disabled: true,
    title: reason ?? "권한이 없습니다.",
  });
}
