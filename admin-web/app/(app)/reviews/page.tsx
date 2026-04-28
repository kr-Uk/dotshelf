"use client";

import { useMemo, useState } from "react";
import { useQueries, useQuery } from "@tanstack/react-query";
import { toast } from "sonner";
import {
  AlarmClock,
  CheckCircle2,
  CircleDashed,
  HelpCircle,
  MessageSquare,
  Scale,
  XCircle,
} from "lucide-react";

import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Checkbox } from "@/components/ui/checkbox";
import { Textarea } from "@/components/ui/textarea";
import { Badge } from "@/components/ui/badge";
import { Kbd } from "@/components/common/kbd";
import { PageHeader } from "@/components/common/page-header";
import { RoleGate } from "@/components/common/role-gate";
import { TableSkeletonRows } from "@/components/common/table-skeleton";
import {
  WorkflowStepper,
  type StepperStep,
} from "@/components/common/workflow-stepper";
import { useKeyboardShortcut } from "@/lib/use-keyboard-shortcut";
import { useNow } from "@/lib/use-now";

const CONSIGNMENT_STEPS: StepperStep[] = [
  { key: "received", label: "수령" },
  { key: "scanning", label: "스캔" },
  { key: "post_processing", label: "후처리" },
  { key: "applied", label: "적용" },
];

const REVIEW_GUIDELINES = {
  book: [
    "ISBN 진위 검증을 통과해야 함. 자가출판은 ISBN 없이도 가능하나 권리 증빙 필수.",
    "라이선스 계약서 — 출판사·작가 모두 서명일·유효기간이 식별 가능해야 함.",
    "위탁 제작 신청 시 실제 책 1부 발송 주소를 함께 첨부.",
  ],
  asset: [
    "USDZ/glTF 폴리곤 한도, 텍스처 해상도(>=1024px) 자동 검사 통과 필수.",
    "외부 에셋 라이선스(CC0·유료) 증빙이 명시되어야 함.",
    "전체 테마는 의존 카테고리 모두 첨부 (책장·대시보드·노트북·책상 등).",
  ],
  ad: [
    "책의 장르·연령 등급과 매칭 하드 필터를 통과해야 입찰 가능.",
    "카피는 40자 이내. 부적절·과장·차별 표현 금지.",
    "신간(평점 데이터 없음)은 가드레일 시뮬레이션 결과를 함께 검토.",
  ],
};
import {
  RejectionDialog,
  type RejectionPayload,
} from "@/components/common/rejection-dialog";
import { StatusBadge } from "@/components/common/status-badge";
import { api } from "@/lib/api";
import {
  ADMIN_ROLE_LABEL,
  REJECTION_REASON_LABEL,
  REVIEW_STATUS_LABEL,
  REVIEW_TYPE_LABEL,
  type AutoCheckResult,
  type RejectionReason,
  type ReviewAppeal,
  type ReviewItem,
  type ReviewNote,
  type ReviewStatus,
  type ReviewType,
} from "@/lib/types";
import {
  formatDateTime,
  formatRelative,
  formatWon,
} from "@/lib/format";
import { cn } from "@/lib/utils";

const STATUS_TONE: Record<ReviewStatus, "neutral" | "info" | "warn" | "danger" | "success"> = {
  submitted: "info",
  in_review: "warn",
  approved: "success",
  rejected: "danger",
  info_requested: "warn",
  appealed: "danger",
};

const REJECTION_REASONS_BY_TYPE: Record<ReviewType, RejectionReason[]> = {
  book: ["license_proof_missing", "duplicate_isbn", "nsfw_violation", "other"],
  asset: [
    "polygon_over_budget",
    "texture_low_resolution",
    "nsfw_violation",
    "category_mismatch",
    "other",
  ],
  ad: [
    "ad_copy_inappropriate",
    "ad_genre_age_mismatch",
    "ad_rating_below_threshold",
    "other",
  ],
};

export default function ReviewsPage() {
  const [tab, setTab] = useState<ReviewType>("book");
  const { data, isLoading } = useQuery({
    queryKey: ["reviews"],
    queryFn: () => api<ReviewItem[]>("/reviews"),
  });
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [bulkIds, setBulkIds] = useState<string[]>([]);
  const [bulkRejectOpen, setBulkRejectOpen] = useState(false);
  const [singleRejectOpen, setSingleRejectOpen] = useState(false);
  const [guideOpen, setGuideOpen] = useState(false);
  // SLA 카운트다운(분 단위)을 render-pure하게 받기 위한 hook.
  const now = useNow(60_000);

  const filtered = useMemo(
    () => (data ?? []).filter((r) => r.type === tab),
    [data, tab],
  );
  const selected =
    filtered.find((r) => r.id === selectedId) ?? filtered[0] ?? null;

  // 큐 행 이동 — j/↓ : 다음, k/↑ : 이전
  const moveBy = (delta: number) => {
    if (filtered.length === 0) return;
    const i = Math.max(
      0,
      filtered.findIndex((r) => r.id === selected?.id),
    );
    const next = filtered[(i + delta + filtered.length) % filtered.length];
    if (next) setSelectedId(next.id);
  };
  useKeyboardShortcut({ key: "j" }, () => moveBy(1));
  useKeyboardShortcut({ key: "k" }, () => moveBy(-1));
  useKeyboardShortcut({ key: "ArrowDown" }, () => moveBy(1));
  useKeyboardShortcut({ key: "ArrowUp" }, () => moveBy(-1));

  const handleBulkApprove = async () => {
    if (bulkIds.length === 0) return;
    try {
      await api("/reviews/bulk", {
        method: "POST",
        body: JSON.stringify({ ids: bulkIds, decision: "approve" }),
      });
      toast.success(`${bulkIds.length}건 일괄 승인`);
      setBulkIds([]);
    } catch {
      toast.error("일괄 처리에 실패했어요.");
    }
  };

  const handleBulkReject = async (payload: RejectionPayload) => {
    if (bulkIds.length === 0) return;
    try {
      await api("/reviews/bulk", {
        method: "POST",
        body: JSON.stringify({
          ids: bulkIds,
          decision: "reject",
          reason: payload.reason,
          comment: payload.comment,
        }),
      });
      toast.success(
        `${bulkIds.length}건 일괄 반려 — ${REJECTION_REASON_LABEL[payload.reason]}`,
      );
      setBulkIds([]);
    } catch {
      toast.error("일괄 처리에 실패했어요.");
    }
  };

  const handleSingle = async (id: string, action: string) => {
    try {
      await api(`/reviews/${id}/${action}`, { method: "POST" });
      toast.success(`「${selected?.title ?? id}」 ${action}`);
    } catch {
      toast.error("처리에 실패했어요.");
    }
  };

  const handleSingleReject = async (payload: RejectionPayload) => {
    if (!selected) return;
    try {
      await api(`/reviews/${selected.id}/reject`, {
        method: "POST",
        body: JSON.stringify(payload),
      });
      toast.success(
        `반려 — ${REJECTION_REASON_LABEL[payload.reason]}${payload.comment ? " · 코멘트 포함" : ""}`,
      );
    } catch {
      toast.error("처리에 실패했어요.");
    }
  };

  // 심사 단축키 — A: 승인 / R: 반려 모달 열기
  useKeyboardShortcut({ key: "a" }, () => {
    if (selected) handleSingle(selected.id, "approve");
  });
  useKeyboardShortcut({ key: "r" }, () => {
    if (selected) setSingleRejectOpen(true);
  });

  const visibleIds = filtered.map((it) => it.id);
  const allChecked = visibleIds.length > 0 && bulkIds.length === visibleIds.length;
  const someChecked = bulkIds.length > 0 && !allChecked;

  return (
    <div className="space-y-6">
      <PageHeader
        title="콘텐츠 심사"
        description="책 / 에셋 / 광고를 한 화면에서 검수합니다. 동일한 큐 → 상세 → 결정 패턴."
        actions={
          <>
            <Button
              variant={guideOpen ? "default" : "outline"}
              size="sm"
              onClick={() => setGuideOpen((v) => !v)}
            >
              가이드라인 {guideOpen ? "닫기" : "열기"}
            </Button>
            <Button variant="outline" size="sm">CSV 다운로드</Button>
          </>
        }
      />

      <Tabs
        value={tab}
        onValueChange={(v) => {
          setTab(v as ReviewType);
          setSelectedId(null);
          setBulkIds([]);
        }}
      >
        <TabsList>
          <TabsTrigger value="book">책</TabsTrigger>
          <TabsTrigger value="asset">에셋</TabsTrigger>
          <TabsTrigger value="ad">광고</TabsTrigger>
        </TabsList>

        {(["book", "asset", "ad"] as ReviewType[]).map((t) => (
          <TabsContent key={t} value={t}>
            <div className="grid gap-4 lg:grid-cols-[minmax(0,1.2fr)_minmax(0,1fr)]">
              <Card>
                <CardHeader>
                  <div className="flex items-center justify-between">
                    <div>
                      <CardTitle>큐 ({filtered.length}건)</CardTitle>
                      <CardDescription className="flex items-center gap-1">
                        <span>체크박스로 선택 후 일괄 처리는 슈퍼 관리자만 ·</span>
                        <Kbd>J</Kbd>
                        <Kbd>K</Kbd>
                        <span>이동 ·</span>
                        <Kbd>A</Kbd>
                        <span>승인 ·</span>
                        <Kbd>R</Kbd>
                        <span>반려</span>
                      </CardDescription>
                    </div>
                    {bulkIds.length > 0 && (
                      <RoleGate action="review.bulk_decide" fallback="hide">
                        <div className="inline-flex gap-2">
                          <Button size="sm" onClick={handleBulkApprove}>
                            {bulkIds.length}건 일괄 승인
                          </Button>
                          <Button
                            size="sm"
                            variant="destructive"
                            onClick={() => setBulkRejectOpen(true)}
                          >
                            {bulkIds.length}건 일괄 반려
                          </Button>
                        </div>
                      </RoleGate>
                    )}
                  </div>
                </CardHeader>
                <CardContent className="px-0">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead className="w-10">
                          <Checkbox
                            checked={allChecked}
                            indeterminate={someChecked}
                            onCheckedChange={(checked) => {
                              setBulkIds(checked === true ? visibleIds : []);
                            }}
                          />
                        </TableHead>
                        <TableHead>제목</TableHead>
                        <TableHead>신청자</TableHead>
                        <TableHead>상태</TableHead>
                        <TableHead>SLA</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {isLoading && <TableSkeletonRows rows={6} cols={5} />}
                      {!isLoading && filtered.length === 0 && (
                        <TableRow>
                          <TableCell colSpan={5} className="text-center text-muted-foreground">
                            큐가 비었어요.
                          </TableCell>
                        </TableRow>
                      )}
                      {filtered.map((it) => {
                        const slaDue = new Date(it.slaDueAt);
                        const slaHours = (slaDue.getTime() - now) / 3_600_000;
                        const slaTone =
                          slaHours < 0 ? "danger" : slaHours < 12 ? "warn" : "neutral";
                        const checked = bulkIds.includes(it.id);
                        return (
                          <TableRow
                            key={it.id}
                            data-state={selected?.id === it.id ? "selected" : undefined}
                          >
                            <TableCell onClick={(e) => e.stopPropagation()}>
                              <Checkbox
                                checked={checked}
                                onCheckedChange={(c) =>
                                  setBulkIds((prev) =>
                                    c === true
                                      ? [...prev, it.id]
                                      : prev.filter((x) => x !== it.id),
                                  )
                                }
                              />
                            </TableCell>
                            <TableCell
                              className="font-medium cursor-pointer"
                              onClick={() => setSelectedId(it.id)}
                            >
                              <div className="line-clamp-1">{it.title}</div>
                              <div className="text-xs text-muted-foreground">
                                접수 {formatRelative(it.submittedAt)}
                              </div>
                            </TableCell>
                            <TableCell
                              className="cursor-pointer"
                              onClick={() => setSelectedId(it.id)}
                            >
                              <div className="text-sm">{it.applicantName}</div>
                              <div className="text-xs text-muted-foreground">
                                {it.applicantId}
                              </div>
                            </TableCell>
                            <TableCell
                              className="cursor-pointer"
                              onClick={() => setSelectedId(it.id)}
                            >
                              <StatusBadge tone={STATUS_TONE[it.status]}>
                                {REVIEW_STATUS_LABEL[it.status]}
                              </StatusBadge>
                            </TableCell>
                            <TableCell
                              className="cursor-pointer"
                              onClick={() => setSelectedId(it.id)}
                            >
                              <StatusBadge tone={slaTone}>
                                {slaHours < 0 ? "지남" : `${Math.floor(slaHours)}h`}
                              </StatusBadge>
                            </TableCell>
                          </TableRow>
                        );
                      })}
                    </TableBody>
                  </Table>
                </CardContent>
              </Card>

              <ReviewDetail
                item={selected ?? null}
                onSingleAction={handleSingle}
                onSingleReject={() => setSingleRejectOpen(true)}
                guideOpen={guideOpen}
                onGuideClose={() => setGuideOpen(false)}
              />
            </div>
          </TabsContent>
        ))}
      </Tabs>

      <RejectionDialog
        open={bulkRejectOpen}
        onOpenChange={setBulkRejectOpen}
        title={`${bulkIds.length}건 일괄 반려`}
        description="동일 사유로 묶어 처리합니다. 신청자에게 동일한 가이드가 발송됩니다."
        reasons={REJECTION_REASONS_BY_TYPE[tab]}
        onSubmit={handleBulkReject}
      />
      <RejectionDialog
        open={singleRejectOpen}
        onOpenChange={setSingleRejectOpen}
        title={selected ? `「${selected.title}」 반려` : "반려"}
        reasons={selected ? REJECTION_REASONS_BY_TYPE[selected.type] : ["other"]}
        onSubmit={handleSingleReject}
      />
    </div>
  );
}

function ReviewDetail({
  item,
  onSingleAction,
  onSingleReject,
  guideOpen,
  onGuideClose,
}: {
  item: ReviewItem | null;
  onSingleAction: (id: string, action: string) => void;
  onSingleReject: () => void;
  guideOpen: boolean;
  onGuideClose: () => void;
}) {
  const [notesQ, appealQ] = useQueries({
    queries: [
      {
        queryKey: ["review-notes", item?.id],
        queryFn: () => api<ReviewNote[]>(`/reviews/${item!.id}/notes`),
        enabled: !!item,
      },
      {
        queryKey: ["review-appeal", item?.id],
        queryFn: () => api<ReviewAppeal | null>(`/reviews/${item!.id}/appeal`),
        enabled: !!item,
      },
    ],
  });
  const [draft, setDraft] = useState("");
  // hook 순서 보존을 위해 early return 전에 호출.
  const now = useNow(60_000); // SLA 표시는 분 단위면 충분.

  if (!item) {
    return (
      <div className="space-y-3">
        {guideOpen && <GuidelinePanel kind="book" onClose={onGuideClose} />}
        <Card>
          <CardHeader>
            <CardTitle>상세</CardTitle>
            <CardDescription>좌측 큐에서 항목을 선택하세요.</CardDescription>
          </CardHeader>
        </Card>
      </div>
    );
  }

  const slaDue = new Date(item.slaDueAt);
  const slaHours = (slaDue.getTime() - now) / 3_600_000;
  const appeal = appealQ.data ?? null;
  const notes = notesQ.data ?? [];

  const submitNote = async () => {
    if (!draft.trim()) return;
    try {
      await api(`/reviews/${item.id}/notes`, {
        method: "POST",
        body: JSON.stringify({ body: draft.trim() }),
      });
      toast.success("메모를 추가했어요.");
      setDraft("");
    } catch {
      toast.error("메모 저장에 실패했어요.");
    }
  };

  return (
    <div className="space-y-3">
      {guideOpen && <GuidelinePanel kind={item.type} onClose={onGuideClose} />}
      <Card>
      <CardHeader>
        <div className="flex items-start justify-between gap-4">
          <div className="space-y-1">
            <CardTitle className="line-clamp-2">{item.title}</CardTitle>
            <CardDescription className="flex flex-wrap items-center gap-2">
              <StatusBadge tone={STATUS_TONE[item.status]}>
                {REVIEW_STATUS_LABEL[item.status]}
              </StatusBadge>
              <span>· 접수 {formatDateTime(item.submittedAt)}</span>
              <span className="inline-flex items-center gap-1">
                <AlarmClock className="size-3" /> SLA {slaHours < 0 ? "지남" : `${Math.floor(slaHours)}h 남음`}
              </span>
              {item.claimedBy ? (
                <span>· 검토자: {item.claimedBy}</span>
              ) : (
                <span>· 미배정</span>
              )}
            </CardDescription>
          </div>
        </div>
      </CardHeader>

      <CardContent className="space-y-4">
        <DetailBlock label="신청자">
          <div className="text-sm">{item.applicantName}</div>
          <div className="text-xs text-muted-foreground">{item.applicantId}</div>
        </DetailBlock>

        {item.detail.kind === "book" && (
          <DetailBlock label="책 정보">
            <div className="grid grid-cols-2 gap-2 text-sm">
              <span className="text-muted-foreground">ISBN</span>
              <span>{item.detail.isbn ?? "자가출판"}</span>
              <span className="text-muted-foreground">출판사</span>
              <span>{item.detail.publisher}</span>
              <span className="text-muted-foreground">증빙</span>
              <span className="font-mono text-xs">{item.detail.licenseProof}</span>
              <span className="text-muted-foreground">위탁</span>
              <span>{item.detail.consignmentStage ? "위탁 제작" : "직접 업로드"}</span>
            </div>
            {item.detail.consignmentStage && (
              <div className="mt-3 rounded-md border bg-muted/30 p-3">
                <div className="mb-2 text-xs font-medium text-muted-foreground">
                  위탁 제작 진행 단계
                </div>
                <WorkflowStepper
                  steps={CONSIGNMENT_STEPS}
                  currentKey={item.detail.consignmentStage}
                />
              </div>
            )}
          </DetailBlock>
        )}

        {item.detail.kind === "asset" && (
          <DetailBlock label="에셋 정보">
            <div className="grid grid-cols-2 gap-2 text-sm">
              <span className="text-muted-foreground">카테고리</span>
              <span>{item.detail.category}</span>
              <span className="text-muted-foreground">호환 타겟</span>
              <span className="font-mono text-xs">
                {item.detail.compatibleTargets.join(", ")}
              </span>
              <span className="text-muted-foreground">미리보기</span>
              <span className="font-mono text-xs">{item.detail.preview}</span>
            </div>
          </DetailBlock>
        )}

        {item.detail.kind === "ad" && (
          <DetailBlock label="광고 정보">
            <div className="grid grid-cols-2 gap-2 text-sm">
              <span className="text-muted-foreground">대상 책</span>
              <span>{item.detail.bookTitle}</span>
              <span className="text-muted-foreground">섹션</span>
              <span>{item.detail.section}</span>
              <span className="text-muted-foreground">주간 입찰</span>
              <span>{formatWon(item.detail.weeklyBidWon)}</span>
              <span className="text-muted-foreground">카피</span>
              <span>{item.detail.copy}</span>
            </div>
            {item.detail.guardrailWarnings.length > 0 && (
              <ul className="mt-2 list-disc pl-5 text-xs text-amber-700">
                {item.detail.guardrailWarnings.map((w) => (
                  <li key={w}>{w}</li>
                ))}
              </ul>
            )}
          </DetailBlock>
        )}

        <DetailBlock label="자동 검사 결과">
          <ul className="space-y-1.5">
            {item.autoChecks.map((c) => (
              <AutoCheckRow key={c.name} check={c} />
            ))}
          </ul>
        </DetailBlock>

        {appeal && (
          <DetailBlock
            label={
              <span className="inline-flex items-center gap-1.5 text-destructive">
                <Scale className="size-3.5" /> 이의 제기
              </span>
            }
          >
            <div className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm">
              <div className="flex items-center gap-2 text-xs text-muted-foreground">
                <span>{formatRelative(appeal.submittedAt)}</span>
                <Badge variant="outline" className="text-[10px] capitalize">
                  {appeal.status}
                </Badge>
                {appeal.reassignedTo && (
                  <span>재검토자: {appeal.reassignedTo}</span>
                )}
              </div>
              <p className="mt-2 leading-relaxed">{appeal.reason}</p>
            </div>
          </DetailBlock>
        )}

        <DetailBlock
          label={
            <span className="inline-flex items-center gap-1.5">
              <MessageSquare className="size-3.5" /> 심사자 메모 ({notes.length})
            </span>
          }
        >
          <div className="space-y-2">
            {notesQ.isLoading && (
              <div className="text-xs text-muted-foreground">불러오는 중…</div>
            )}
            {notes.length === 0 && !notesQ.isLoading && (
              <div className="text-xs text-muted-foreground">아직 메모가 없어요.</div>
            )}
            {notes.map((n) => (
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

            <div className="space-y-1.5">
              <Textarea
                placeholder="다른 심사자와 공유할 메모를 적어주세요."
                value={draft}
                onChange={(e) => setDraft(e.target.value)}
                rows={2}
              />
              <div className="flex justify-end">
                <Button size="sm" variant="outline" onClick={submitNote} disabled={!draft.trim()}>
                  메모 추가
                </Button>
              </div>
            </div>
          </div>
        </DetailBlock>

        <div className="flex flex-wrap items-center gap-2 border-t pt-4">
          <RoleGate action="review.claim" fallback="disable">
            <Button size="sm" variant="outline" onClick={() => onSingleAction(item.id, "claim")}>
              클레임
            </Button>
          </RoleGate>
          <RoleGate action="review.approve" fallback="disable">
            <Button size="sm" onClick={() => onSingleAction(item.id, "approve")}>
              승인
            </Button>
          </RoleGate>
          <RoleGate action="review.reject" fallback="disable">
            <Button size="sm" variant="destructive" onClick={onSingleReject}>
              반려…
            </Button>
          </RoleGate>
          <RoleGate action="review.info_request" fallback="disable">
            <Button size="sm" variant="ghost" onClick={() => onSingleAction(item.id, "info-request")}>
              추가 정보 요청
            </Button>
          </RoleGate>
        </div>
      </CardContent>
    </Card>
    </div>
  );
}

function GuidelinePanel({
  kind,
  onClose,
}: {
  kind: ReviewType;
  onClose: () => void;
}) {
  const items = REVIEW_GUIDELINES[kind];
  return (
    <Card className="border-sky-200 bg-sky-50/40 dark:border-sky-500/30 dark:bg-sky-500/5">
      <CardHeader>
        <div className="flex items-center justify-between">
          <CardTitle className="text-sm">
            {REVIEW_TYPE_LABEL[kind]} 심사 가이드라인
          </CardTitle>
          <Button size="xs" variant="ghost" onClick={onClose}>
            닫기
          </Button>
        </div>
        <CardDescription>
          가이드라인 문서 링크 — 운영팀 위키와 동기화. 결정 시 함께 검토.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <ul className="ml-4 list-disc space-y-1.5 text-sm">
          {items.map((it, i) => (
            <li key={i}>{it}</li>
          ))}
        </ul>
      </CardContent>
    </Card>
  );
}

function DetailBlock({
  label,
  children,
}: {
  label: React.ReactNode;
  children: React.ReactNode;
}) {
  return (
    <div className="space-y-1.5">
      <div className="text-xs font-medium text-muted-foreground">{label}</div>
      {children}
    </div>
  );
}

function AutoCheckRow({ check }: { check: AutoCheckResult }) {
  const Icon =
    check.result === "pass"
      ? CheckCircle2
      : check.result === "warn"
        ? HelpCircle
        : check.result === "fail"
          ? XCircle
          : CircleDashed;
  const color =
    check.result === "pass"
      ? "text-emerald-600"
      : check.result === "warn"
        ? "text-amber-600"
        : "text-destructive";
  return (
    <li className="flex items-start gap-2 text-sm">
      <Icon className={cn("mt-0.5 size-4 shrink-0", color)} />
      <div>
        <span className="font-medium">{check.name}</span>
        {check.note ? (
          <span className="ml-1 text-xs text-muted-foreground">— {check.note}</span>
        ) : null}
      </div>
    </li>
  );
}
