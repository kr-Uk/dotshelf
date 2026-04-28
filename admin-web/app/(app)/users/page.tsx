"use client";

import { useState } from "react";
import { toast } from "sonner";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Download, Eye, MessageSquare, Receipt } from "lucide-react";

import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Textarea } from "@/components/ui/textarea";
import { Badge } from "@/components/ui/badge";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { BalanceAdjustDialog } from "@/components/common/balance-adjust-dialog";
import { Kbd } from "@/components/common/kbd";
import { PageHeader } from "@/components/common/page-header";
import { RefundDialog } from "@/components/common/refund-dialog";
import { RoleGate } from "@/components/common/role-gate";
import { StatusBadge } from "@/components/common/status-badge";
import { TableSkeletonRows } from "@/components/common/table-skeleton";
import { api } from "@/lib/api";
import { canRole } from "@/lib/permissions";
import {
  ADMIN_ROLE_LABEL,
  USER_HISTORY_KIND_LABEL,
  USER_PURCHASE_KIND_LABEL,
  USER_STATE_LABEL,
  type UserHistoryItem,
  type UserNote,
  type UserPurchase,
  type UserState,
  type UserSummary,
} from "@/lib/types";
import { useAdminAccount } from "@/lib/use-admin-account";
import {
  formatDate,
  formatDateTime,
  formatNumber,
  formatRelative,
  formatWon,
  maskEmail,
} from "@/lib/format";
import { useKeyboardShortcut } from "@/lib/use-keyboard-shortcut";

const STATE_TONE: Record<UserState, "success" | "warn" | "danger"> = {
  active: "success",
  suspended_temp: "warn",
  suspended_perm: "danger",
};

export default function UsersPage() {
  const auth = useAdminAccount();
  const canSpaceView = canRole(auth?.account.role, "user.force_logout");

  const [q, setQ] = useState("");
  const { data, isLoading } = useQuery({
    queryKey: ["users", q],
    queryFn: () => api<UserSummary[]>(`/users?q=${encodeURIComponent(q)}`),
  });
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const items = data ?? [];
  const selected = items.find((u) => u.id === selectedId) ?? items[0] ?? null;

  const moveBy = (delta: number) => {
    if (items.length === 0) return;
    const i = Math.max(0, items.findIndex((u) => u.id === selected?.id));
    const next = items[(i + delta + items.length) % items.length];
    if (next) setSelectedId(next.id);
  };
  useKeyboardShortcut({ key: "j" }, () => moveBy(1));
  useKeyboardShortcut({ key: "k" }, () => moveBy(-1));
  useKeyboardShortcut({ key: "ArrowDown" }, () => moveBy(1));
  useKeyboardShortcut({ key: "ArrowUp" }, () => moveBy(-1));

  const action = async (kind: string) => {
    if (!selected) return;
    try {
      await api(`/users/${selected.id}/action`, {
        method: "POST",
        body: JSON.stringify({ kind }),
      });
      toast.success(`${selected.nickname} — ${kind}`);
    } catch {
      toast.error("처리에 실패했어요.");
    }
  };

  const [refundOpen, setRefundOpen] = useState(false);
  const [adjustOpen, setAdjustOpen] = useState(false);
  const [spaceOpen, setSpaceOpen] = useState(false);

  // 환불 모달이 열릴 때 사용할 구매 이력 — 모달 외부에서 prefetch.
  const purchasesQ = useQuery({
    queryKey: ["user-purchases", selected?.id],
    queryFn: () => api<UserPurchase[]>(`/users/${selected!.id}/purchases`),
    enabled: !!selected,
  });

  return (
    <div className="space-y-6">
      <PageHeader
        title="사용자 · 공간 관리"
        description="이메일·닉네임·UID 검색. 계정 조치, 환불, CS 메모, 공간 들여다보기, 데이터 다운로드."
      />

      <div className="grid gap-4 lg:grid-cols-[minmax(0,1.4fr)_minmax(0,1fr)]">
        <Card>
          <CardHeader>
            <div className="flex items-center justify-between gap-3">
              <CardTitle>사용자</CardTitle>
              <Input
                placeholder="이메일 · 닉네임 · UID"
                value={q}
                onChange={(e) => setQ(e.target.value)}
                className="max-w-xs"
              />
            </div>
            <CardDescription className="flex items-center gap-1">
              <span>최근 접속 / 신고 누적 등 위험 신호 표시 ·</span>
              <Kbd>J</Kbd>
              <Kbd>K</Kbd>
              <span>이동</span>
            </CardDescription>
          </CardHeader>
          <CardContent className="px-0">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>사용자</TableHead>
                  <TableHead>가입</TableHead>
                  <TableHead>최근 접속</TableHead>
                  <TableHead>상태</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading && <TableSkeletonRows rows={5} cols={4} />}
                {items.map((u) => (
                  <TableRow
                    key={u.id}
                    data-state={selected?.id === u.id ? "selected" : undefined}
                    onClick={() => setSelectedId(u.id)}
                    className="cursor-pointer"
                  >
                    <TableCell>
                      <div className="font-medium">{u.nickname}</div>
                      <div className="text-xs text-muted-foreground">
                        {maskEmail(u.email)} · {u.id}
                      </div>
                    </TableCell>
                    <TableCell>
                      <div className="text-sm">{formatDate(u.joinedAt)}</div>
                      <div className="text-xs text-muted-foreground capitalize">
                        {u.socialProvider}
                      </div>
                    </TableCell>
                    <TableCell>{formatRelative(u.lastSeenAt)}</TableCell>
                    <TableCell>
                      <StatusBadge tone={STATE_TONE[u.state]}>
                        {USER_STATE_LABEL[u.state]}
                      </StatusBadge>
                      {u.reportsAgainst > 0 && (
                        <div className="mt-1 text-xs text-muted-foreground">
                          신고 누적 {u.reportsAgainst}건
                        </div>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div className="flex items-start justify-between gap-2">
              <div>
                <CardTitle>{selected ? `@${selected.nickname}` : "상세"}</CardTitle>
                <CardDescription>
                  {selected
                    ? `${selected.email} · ${selected.id}`
                    : "검색 후 사용자를 선택하세요."}
                </CardDescription>
              </div>
              {selected && (
                <Button
                  size="sm"
                  variant="outline"
                  onClick={() => setSpaceOpen(true)}
                  disabled={!canSpaceView}
                  title={canSpaceView ? "" : "권한 없음"}
                >
                  <Eye className="size-3.5" /> 공간 들여다보기
                </Button>
              )}
            </div>
          </CardHeader>
          <CardContent>
            {selected && (
              <Tabs defaultValue="overview">
                <TabsList>
                  <TabsTrigger value="overview">개요</TabsTrigger>
                  <TabsTrigger value="notes">CS 메모</TabsTrigger>
                  <TabsTrigger value="purchases">구매 이력</TabsTrigger>
                  <TabsTrigger value="history">신고·차단</TabsTrigger>
                </TabsList>

                <TabsContent value="overview" className="pt-3">
                  <div className="grid grid-cols-2 gap-2 text-sm">
                    <span className="text-muted-foreground">UID</span>
                    <span className="font-mono text-xs">{selected.id}</span>
                    <span className="text-muted-foreground">이메일</span>
                    <span>{selected.email}</span>
                    <span className="text-muted-foreground">소셜</span>
                    <span className="capitalize">{selected.socialProvider}</span>
                    <span className="text-muted-foreground">책장 / 책</span>
                    <span>
                      {formatNumber(selected.shelves)} / {formatNumber(selected.books)}
                    </span>
                    <span className="text-muted-foreground">페이지 잔액</span>
                    <span>{formatNumber(selected.pageBalance)}p</span>
                    <span className="text-muted-foreground">신고 누적</span>
                    <span>{selected.reportsAgainst}건</span>
                  </div>

                  <div className="mt-4 flex flex-wrap items-center gap-2 border-t pt-4">
                    <RoleGate action="user.force_logout" fallback="disable">
                      <Button size="sm" variant="outline" onClick={() => action("force_logout")}>
                        강제 로그아웃
                      </Button>
                    </RoleGate>
                    <RoleGate action="user.rename_nickname" fallback="disable">
                      <Button size="sm" variant="outline" onClick={() => action("rename_nickname")}>
                        닉네임 강제 변경
                      </Button>
                    </RoleGate>
                    <RoleGate action="user.password_reset" fallback="disable">
                      <Button size="sm" variant="outline" onClick={() => action("password_reset")}>
                        비밀번호 재설정
                      </Button>
                    </RoleGate>
                    <RoleGate action="user.suspend_temp" fallback="disable">
                      <Button size="sm" variant="destructive" onClick={() => action("suspend_7d")}>
                        7일 정지
                      </Button>
                    </RoleGate>
                    <RoleGate action="user.suspend_perm" fallback="disable" reason="슈퍼 관리자만">
                      <Button size="sm" variant="destructive" onClick={() => action("suspend_perm")}>
                        영구 정지
                      </Button>
                    </RoleGate>
                    <RoleGate action="user.anonymize" fallback="disable" reason="슈퍼 관리자만">
                      <Button size="sm" variant="ghost" onClick={() => action("anonymize")}>
                        데이터 익명화·삭제
                      </Button>
                    </RoleGate>
                    <RoleGate action="user.password_reset" fallback="disable">
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={async () => {
                          try {
                            await api(`/users/${selected.id}/export`, { method: "POST" });
                            toast.success(
                              "데이터 다운로드 요청 등록 — 처리 후 이메일로 안내됩니다.",
                            );
                          } catch {
                            toast.error("요청 실패");
                          }
                        }}
                      >
                        <Download className="size-3.5" /> 데이터 다운로드 (법적 요청)
                      </Button>
                    </RoleGate>
                    <RoleGate action="user.force_logout" fallback="disable">
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() => setRefundOpen(true)}
                      >
                        <Receipt className="size-3.5" /> 환불 처리…
                      </Button>
                    </RoleGate>
                    <RoleGate
                      action="user.adjust_balance"
                      fallback="disable"
                      reason="슈퍼 관리자만"
                    >
                      <Button size="sm" variant="outline" onClick={() => setAdjustOpen(true)}>
                        페이지 잔액 조정…
                      </Button>
                    </RoleGate>
                  </div>
                </TabsContent>

                <TabsContent value="notes" className="pt-3">
                  <UserNotes userId={selected.id} />
                </TabsContent>

                <TabsContent value="purchases" className="pt-3">
                  <UserPurchases userId={selected.id} />
                </TabsContent>

                <TabsContent value="history" className="pt-3">
                  <UserHistoryView userId={selected.id} />
                </TabsContent>
              </Tabs>
            )}
          </CardContent>
        </Card>
      </div>

      {selected && (
        <>
          <RefundDialog
            open={refundOpen}
            onOpenChange={setRefundOpen}
            subjectName={`@${selected.nickname}`}
            purchases={purchasesQ.data ?? []}
            onSubmit={async (p) => {
              await api(`/users/${selected.id}/refund`, {
                method: "POST",
                body: JSON.stringify(p),
              });
              toast.success("환불 처리 완료");
            }}
          />

          <BalanceAdjustDialog
            open={adjustOpen}
            onOpenChange={setAdjustOpen}
            subjectName={`@${selected.nickname}`}
            currentPages={selected.pageBalance}
            onSubmit={async (p) => {
              await api(`/users/${selected.id}/adjust-balance`, {
                method: "POST",
                body: JSON.stringify(p),
              });
              toast.success(
                `잔액 ${p.pages > 0 ? "+" : ""}${p.pages}p 조정`,
              );
            }}
          />

          <SpaceSnapshotDialog
            open={spaceOpen}
            onOpenChange={setSpaceOpen}
            user={selected}
          />
        </>
      )}
    </div>
  );
}

function UserNotes({ userId }: { userId: string }) {
  const qc = useQueryClient();
  const { data, isLoading } = useQuery({
    queryKey: ["user-notes", userId],
    queryFn: () => api<UserNote[]>(`/users/${userId}/notes`),
  });
  const [draft, setDraft] = useState("");

  const submit = async () => {
    if (!draft.trim()) return;
    try {
      await api(`/users/${userId}/notes`, {
        method: "POST",
        body: JSON.stringify({ body: draft.trim() }),
      });
      toast.success("CS 메모를 추가했어요.");
      setDraft("");
      qc.invalidateQueries({ queryKey: ["user-notes", userId] });
    } catch {
      toast.error("메모 저장에 실패했어요.");
    }
  };

  return (
    <div className="space-y-2">
      <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
        <MessageSquare className="size-3" /> 운영자만 보이는 사용자 단위 메모. 감사 로그에 누적.
      </div>
      {isLoading && <div className="text-xs text-muted-foreground">불러오는 중…</div>}
      {(data ?? []).length === 0 && !isLoading && (
        <div className="text-xs text-muted-foreground">아직 메모가 없어요.</div>
      )}
      {(data ?? []).map((n) => (
        <div key={n.id} className="rounded-md border bg-muted/30 p-3 text-sm">
          <div className="flex items-center gap-2 text-xs text-muted-foreground">
            <span className="font-medium text-foreground">{n.authorName}</span>
            <Badge variant="outline" className="text-[10px]">
              {ADMIN_ROLE_LABEL[n.authorRole]}
            </Badge>
            <span>· {formatRelative(n.createdAt)}</span>
          </div>
          <p className="mt-1.5 whitespace-pre-wrap">{n.body}</p>
        </div>
      ))}
      <Textarea
        rows={2}
        placeholder="다른 운영자에게 공유할 메모를 적어주세요."
        value={draft}
        onChange={(e) => setDraft(e.target.value)}
      />
      <div className="flex justify-end">
        <Button size="sm" variant="outline" onClick={submit} disabled={!draft.trim()}>
          메모 추가
        </Button>
      </div>
    </div>
  );
}

function UserPurchases({ userId }: { userId: string }) {
  const { data, isLoading } = useQuery({
    queryKey: ["user-purchases", userId],
    queryFn: () => api<UserPurchase[]>(`/users/${userId}/purchases`),
  });
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>일시</TableHead>
          <TableHead>종류</TableHead>
          <TableHead>항목</TableHead>
          <TableHead className="text-right">금액</TableHead>
          <TableHead>상태</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {isLoading && <TableSkeletonRows rows={3} cols={5} />}
        {!isLoading && (data ?? []).length === 0 && (
          <TableRow>
            <TableCell colSpan={5} className="text-center text-muted-foreground">
              구매 이력이 없어요.
            </TableCell>
          </TableRow>
        )}
        {(data ?? []).map((p) => (
          <TableRow key={p.id}>
            <TableCell className="text-sm">{formatDateTime(p.purchasedAt)}</TableCell>
            <TableCell>
              <Badge variant="outline" className="text-[10px]">
                {USER_PURCHASE_KIND_LABEL[p.kind]}
              </Badge>
            </TableCell>
            <TableCell className="max-w-[260px]">
              <div className="line-clamp-1">{p.itemName}</div>
              <div className="text-xs text-muted-foreground">{p.pages.toLocaleString()}p</div>
            </TableCell>
            <TableCell className="text-right tabular-nums">{formatWon(p.amountWon)}</TableCell>
            <TableCell>
              {p.refunded ? (
                <StatusBadge tone="warn">환불됨</StatusBadge>
              ) : (
                <StatusBadge tone="success">완료</StatusBadge>
              )}
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

function UserHistoryView({ userId }: { userId: string }) {
  const { data, isLoading } = useQuery({
    queryKey: ["user-history", userId],
    queryFn: () => api<UserHistoryItem[]>(`/users/${userId}/history`),
  });
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>일시</TableHead>
          <TableHead>종류</TableHead>
          <TableHead>요약</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {isLoading && <TableSkeletonRows rows={3} cols={3} />}
        {!isLoading && (data ?? []).length === 0 && (
          <TableRow>
            <TableCell colSpan={3} className="text-center text-muted-foreground">
              신고·차단 이력이 없어요.
            </TableCell>
          </TableRow>
        )}
        {(data ?? []).map((h) => (
          <TableRow key={h.id}>
            <TableCell className="whitespace-nowrap text-sm">
              {formatDateTime(h.occurredAt)}
            </TableCell>
            <TableCell>
              <Badge variant="outline" className="text-[10px]">
                {USER_HISTORY_KIND_LABEL[h.kind]}
              </Badge>
            </TableCell>
            <TableCell className="text-sm">{h.excerpt}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

function SpaceSnapshotDialog({
  open,
  onOpenChange,
  user,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  user: UserSummary;
}) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <Eye className="size-4" /> @{user.nickname} 공간 (읽기 전용)
          </DialogTitle>
          <DialogDescription>
            운영 진단용 스냅샷입니다. 어떤 조작도 사용자에게 반영되지 않습니다.
          </DialogDescription>
        </DialogHeader>

        <div className="grid grid-cols-3 gap-3 text-sm">
          <Stat label="책장" value={`${formatNumber(user.shelves)}개`} />
          <Stat label="책" value={`${formatNumber(user.books)}권`} />
          <Stat label="페이지 잔액" value={`${formatNumber(user.pageBalance)}p`} />
          <Stat label="가입" value={formatDate(user.joinedAt)} />
          <Stat label="최근 접속" value={formatRelative(user.lastSeenAt)} />
          <Stat label="상태" value={USER_STATE_LABEL[user.state]} />
        </div>

        <div className="rounded-md border bg-muted/40 p-4 text-sm text-muted-foreground">
          <p className="font-medium text-foreground">3D 공간 미리보기</p>
          <p className="mt-1 text-xs">
            v1.0 mock — 실제 공간 렌더는 실데이터 + 3D 뷰어 임베드가 필요합니다. 명세 §6에 따른 읽기 전용 스냅샷 자리.
          </p>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>닫기</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function Stat({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="rounded-md border bg-card p-3">
      <div className="text-xs text-muted-foreground">{label}</div>
      <div className="mt-1 text-base font-semibold tabular-nums">{value}</div>
    </div>
  );
}
