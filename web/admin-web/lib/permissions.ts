import type { AdminRole } from "./types";

// 액션 키. 요소·페이지에 RoleGate 로 매달면 권한 없는 사용자에게는 숨겨지거나 disable.
export type AdminAction =
  // 콘텐츠 심사
  | "review.claim"
  | "review.approve"
  | "review.reject"
  | "review.info_request"
  | "review.bulk_decide"
  // 가입 심사
  | "signup.decide"
  // 모더레이션
  | "moderation.hide"
  | "moderation.delete"
  | "moderation.warn"
  | "moderation.suspend_temp"
  | "moderation.suspend_perm"
  | "moderation.bulk"
  // 사용자 관리
  | "user.force_logout"
  | "user.rename_nickname"
  | "user.require_relogin"
  | "user.suspend_temp"
  | "user.suspend_perm"
  | "user.anonymize"
  | "user.adjust_balance"
  // 정산
  | "withdrawal.approve_first"
  | "withdrawal.approve_settlement"
  | "withdrawal.reject"
  | "settlement.adjust"
  // 운영
  | "ops.announce"
  | "ops.policy_publish"
  | "ops.maintenance_toggle"
  | "ops.role_assign"
  // 감사 로그
  | "audit.export"
  // 에셋 운영
  | "asset.manage"
  | "asset.publish";

const ALL_ROLES: AdminRole[] = [
  "super_admin",
  "content_reviewer",
  "moderator",
  "settlement",
  "cs",
  "read_only",
];

const SUPER_ONLY: AdminRole[] = ["super_admin"];

export const ROLE_ALLOW: Record<AdminAction, AdminRole[]> = {
  // 심사
  "review.claim": ["super_admin", "content_reviewer"],
  "review.approve": ["super_admin", "content_reviewer"],
  "review.reject": ["super_admin", "content_reviewer"],
  "review.info_request": ["super_admin", "content_reviewer"],
  "review.bulk_decide": SUPER_ONLY,
  // 가입
  "signup.decide": ["super_admin", "content_reviewer"],
  // 모더레이션
  "moderation.hide": ["super_admin", "moderator"],
  "moderation.delete": ["super_admin", "moderator"],
  "moderation.warn": ["super_admin", "moderator", "cs"],
  "moderation.suspend_temp": ["super_admin", "moderator"],
  "moderation.suspend_perm": SUPER_ONLY,
  "moderation.bulk": ["super_admin", "moderator"],
  // 사용자
  "user.force_logout": ["super_admin", "moderator", "cs"],
  "user.rename_nickname": ["super_admin", "moderator"],
  "user.require_relogin": ["super_admin", "cs"],
  "user.suspend_temp": ["super_admin", "moderator"],
  "user.suspend_perm": SUPER_ONLY,
  "user.anonymize": SUPER_ONLY,
  "user.adjust_balance": SUPER_ONLY,
  // 정산
  "withdrawal.approve_first": ["super_admin", "settlement"],
  "withdrawal.approve_settlement": ["super_admin", "settlement"],
  "withdrawal.reject": ["super_admin", "settlement"],
  "settlement.adjust": SUPER_ONLY,
  // 운영
  "ops.announce": ["super_admin", "cs"],
  "ops.policy_publish": SUPER_ONLY,
  "ops.maintenance_toggle": SUPER_ONLY,
  "ops.role_assign": SUPER_ONLY,
  // 감사
  "audit.export": ["super_admin"],
  // 에셋 운영 — BE의 requireAdmin 도 super_admin 만 통과시키므로 일치시킨다.
  "asset.manage": SUPER_ONLY,
  "asset.publish": SUPER_ONLY,
};

export type NavKey =
  | "dashboard"
  | "reviews"
  | "signups"
  | "moderation"
  | "settlements"
  | "users"
  | "assets"
  | "stats"
  | "operations"
  | "audit";

export const ROLE_NAV_ACCESS: Record<NavKey, AdminRole[]> = {
  dashboard: ALL_ROLES,
  reviews: ["super_admin", "content_reviewer", "read_only"],
  signups: ["super_admin", "content_reviewer", "read_only"],
  moderation: ["super_admin", "moderator", "cs", "read_only"],
  settlements: ["super_admin", "settlement", "read_only"],
  users: ["super_admin", "moderator", "cs", "read_only"],
  assets: ["super_admin", "read_only"],
  stats: ALL_ROLES,
  operations: ["super_admin", "cs", "read_only"],
  audit: ["super_admin", "read_only"],
};

export function canRole(role: AdminRole | undefined, action: AdminAction): boolean {
  if (!role) return false;
  return ROLE_ALLOW[action].includes(role);
}

export function canRoleSeeNav(role: AdminRole | undefined, key: NavKey): boolean {
  if (!role) return false;
  return ROLE_NAV_ACCESS[key].includes(role);
}
