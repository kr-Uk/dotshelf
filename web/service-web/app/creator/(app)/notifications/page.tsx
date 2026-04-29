"use client";

import { Fragment, useMemo, useRef, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Bell, CheckCheck, MessageCircle, Paperclip, Plus } from "lucide-react";
import { toast } from "sonner";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Textarea } from "@/components/ui/textarea";
import { api, ApiError } from "@/lib/api";
import { formatDate } from "@/lib/format";
import {
  NOTIFICATION_CATEGORY_LABEL,
  SUPPORT_TICKET_CATEGORY_LABEL,
  SUPPORT_TICKET_STATUS_LABEL,
  type NotificationCategoryKey,
  type NotificationItem,
  type SupportTicket,
  type SupportTicketCategory,
} from "@/lib/types";

type NotificationFilter = NotificationCategoryKey | "all";

const NOTIFICATION_TABS: Array<{ value: NotificationFilter; label: string }> = [
  { value: "all", label: "전체" },
  { value: "review", label: NOTIFICATION_CATEGORY_LABEL.review },
  { value: "payout", label: NOTIFICATION_CATEGORY_LABEL.payout },
  { value: "ad_guardrail", label: NOTIFICATION_CATEGORY_LABEL.ad_guardrail },
  { value: "report", label: NOTIFICATION_CATEGORY_LABEL.report },
  { value: "system", label: NOTIFICATION_CATEGORY_LABEL.system },
];

const SUPPORT_CATEGORY_OPTIONS: Array<{
  value: SupportTicketCategory;
  label: string;
}> = [
  { value: "signup", label: SUPPORT_TICKET_CATEGORY_LABEL.signup },
  { value: "registration", label: SUPPORT_TICKET_CATEGORY_LABEL.registration },
  { value: "ad", label: SUPPORT_TICKET_CATEGORY_LABEL.ad },
  { value: "settlement", label: SUPPORT_TICKET_CATEGORY_LABEL.settlement },
  { value: "etc", label: SUPPORT_TICKET_CATEGORY_LABEL.etc },
];

export default function NotificationsPage() {
  const [filter, setFilter] = useState<NotificationFilter>("all");

  return (
    <div className="space-y-6">
      <div>
        <div className="text-xs font-medium text-muted-foreground">
          8. 알림 & 1:1 문의
        </div>
        <h1 className="mt-1 text-2xl font-semibold tracking-tight">알림</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          검수 결과 / 정산 / 광고 가드레일 / 신고 / 시스템 공지 알림과 1:1 문의를
          한 곳에서 확인해요.
        </p>
      </div>

      <NotificationsCard filter={filter} onFilterChange={setFilter} />

      <SupportTicketsSection />
    </div>
  );
}

function NotificationsCard({
  filter,
  onFilterChange,
}: {
  filter: NotificationFilter;
  onFilterChange: (next: NotificationFilter) => void;
}) {
  const queryClient = useQueryClient();

  const notifications = useQuery({
    queryKey: ["notifications"],
    queryFn: () => api<NotificationItem[]>("/notifications"),
  });

  const filtered = useMemo(() => {
    if (!notifications.data) return undefined;
    if (filter === "all") return notifications.data;
    return notifications.data.filter((n) => n.category === filter);
  }, [notifications.data, filter]);

  const markAllRead = useMutation({
    mutationFn: () =>
      api<{ ok: true }>("/notifications/mark-all-read", { method: "POST" }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["notifications"] });
      toast.success("모든 알림을 읽음 처리했어요.");
    },
    onError: (err) =>
      toast.error(
        err instanceof ApiError ? err.message : "읽음 처리 중 오류가 났어요.",
      ),
  });

  const markOneRead = useMutation({
    mutationFn: (id: string) =>
      api<{ ok: true }>(`/notifications/${id}/read`, { method: "POST" }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["notifications"] });
    },
    onError: (err) =>
      toast.error(
        err instanceof ApiError ? err.message : "읽음 처리 중 오류가 났어요.",
      ),
  });

  const unreadCount = notifications.data?.filter((n) => !n.read).length ?? 0;

  return (
    <Card>
      <CardHeader className="gap-3">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <CardTitle className="flex items-center gap-2 text-base">
              <Bell className="size-4" /> 알림함
              {unreadCount > 0 && (
                <Badge variant="secondary" className="text-[10px]">
                  안 읽음 {unreadCount}
                </Badge>
              )}
            </CardTitle>
            <CardDescription>
              카테고리 탭으로 필터링하고, 항목을 누르면 읽음 처리돼요.
            </CardDescription>
          </div>
          <Button
            variant="outline"
            size="sm"
            onClick={() => markAllRead.mutate()}
            disabled={markAllRead.isPending || unreadCount === 0}
          >
            <CheckCheck className="size-3.5" />
            {markAllRead.isPending ? "처리 중…" : "모두 읽음 처리"}
          </Button>
        </div>
        <Tabs
          value={filter}
          onValueChange={(v) => onFilterChange(v as NotificationFilter)}
        >
          <TabsList>
            {NOTIFICATION_TABS.map((t) => (
              <TabsTrigger key={t.value} value={t.value}>
                {t.label}
              </TabsTrigger>
            ))}
          </TabsList>
        </Tabs>
      </CardHeader>
      <CardContent className="grid gap-2 p-3">
        {!filtered && (
          <>
            <Skeleton className="h-14 w-full" />
            <Skeleton className="h-14 w-full" />
            <Skeleton className="h-14 w-full" />
          </>
        )}
        {filtered?.length === 0 && (
          <div className="rounded-md border border-dashed border-border px-3 py-12 text-center text-sm text-muted-foreground">
            조건에 맞는 알림이 없어요.
          </div>
        )}
        {filtered?.map((n) => (
          <button
            key={n.id}
            type="button"
            onClick={() => {
              if (!n.read) markOneRead.mutate(n.id);
            }}
            className="flex w-full items-start gap-3 rounded-md border border-border bg-card px-3 py-3 text-left transition-colors hover:bg-muted/40 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring/50"
          >
            <Badge variant="outline" className="text-[10px]">
              {NOTIFICATION_CATEGORY_LABEL[n.category]}
            </Badge>
            <div className="min-w-0 flex-1">
              <div className="flex items-center gap-2 text-sm font-medium">
                {!n.read && (
                  <span className="size-1.5 rounded-full bg-primary" />
                )}
                <span className="truncate">{n.title}</span>
              </div>
              <p className="mt-1 text-xs text-muted-foreground">{n.body}</p>
            </div>
            <span className="shrink-0 text-xs text-muted-foreground">
              {formatDate(n.createdAt)}
            </span>
          </button>
        ))}
      </CardContent>
    </Card>
  );
}

function SupportTicketsSection() {
  const [expandedId, setExpandedId] = useState<string | null>(null);

  const tickets = useQuery({
    queryKey: ["support-tickets"],
    queryFn: () => api<SupportTicket[]>("/support/tickets"),
  });

  return (
    <Card>
      <CardHeader>
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <CardTitle className="flex items-center gap-2 text-base">
              <MessageCircle className="size-4" /> 1:1 문의
            </CardTitle>
            <CardDescription>
              가입·등록·광고·정산 등 운영팀에 직접 묻고 답변을 받아요.
            </CardDescription>
          </div>
          <NewTicketDialog />
        </div>
      </CardHeader>
      <CardContent className="px-0">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead className="pl-6">카테고리</TableHead>
              <TableHead>제목</TableHead>
              <TableHead>상태</TableHead>
              <TableHead className="pr-6 text-right">마지막 업데이트</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {tickets.isLoading &&
              [1, 2, 3].map((i) => (
                <TableRow key={i}>
                  <TableCell colSpan={4} className="px-6">
                    <Skeleton className="h-7 w-full" />
                  </TableCell>
                </TableRow>
              ))}
            {tickets.data?.length === 0 && (
              <TableRow>
                <TableCell
                  colSpan={4}
                  className="px-6 py-12 text-center text-sm text-muted-foreground"
                >
                  아직 보낸 문의가 없어요. 우측 상단 &quot;새 문의&quot;로 시작해요.
                </TableCell>
              </TableRow>
            )}
            {tickets.data?.map((t) => {
              const expanded = expandedId === t.id;
              return (
                <Fragment key={t.id}>
                  <TableRow
                    aria-expanded={expanded}
                    onClick={() =>
                      setExpandedId((cur) => (cur === t.id ? null : t.id))
                    }
                    className="cursor-pointer"
                  >
                    <TableCell className="pl-6">
                      <Badge variant="secondary" className="text-[10px]">
                        {SUPPORT_TICKET_CATEGORY_LABEL[t.category]}
                      </Badge>
                    </TableCell>
                    <TableCell className="font-medium">
                      <div className="flex items-center gap-2">
                        <span className="truncate">{t.subject}</span>
                        {t.attachmentNames.length > 0 && (
                          <span className="inline-flex items-center gap-1 text-xs text-muted-foreground">
                            <Paperclip className="size-3" />
                            {t.attachmentNames.length}
                          </span>
                        )}
                      </div>
                    </TableCell>
                    <TableCell>
                      <TicketStatusBadge status={t.status} />
                    </TableCell>
                    <TableCell className="pr-6 text-right text-sm text-muted-foreground">
                      {formatDate(t.updatedAt)}
                    </TableCell>
                  </TableRow>
                  {expanded && (
                    <TableRow>
                      <TableCell colSpan={4} className="bg-muted/30 px-6 py-4">
                        <TicketDetail ticket={t} />
                      </TableCell>
                    </TableRow>
                  )}
                </Fragment>
              );
            })}
          </TableBody>
        </Table>
      </CardContent>
    </Card>
  );
}

function TicketStatusBadge({
  status,
}: {
  status: SupportTicket["status"];
}) {
  if (status === "open") {
    return (
      <Badge className="bg-amber-100 text-amber-800 hover:bg-amber-100/80">
        {SUPPORT_TICKET_STATUS_LABEL.open}
      </Badge>
    );
  }
  if (status === "answered") {
    return (
      <Badge className="bg-emerald-100 text-emerald-700 hover:bg-emerald-100/80">
        {SUPPORT_TICKET_STATUS_LABEL.answered}
      </Badge>
    );
  }
  return (
    <Badge variant="outline" className="text-muted-foreground">
      {SUPPORT_TICKET_STATUS_LABEL.closed}
    </Badge>
  );
}

function TicketDetail({ ticket }: { ticket: SupportTicket }) {
  return (
    <div className="space-y-3">
      <div className="text-xs text-muted-foreground">
        접수일 {formatDate(ticket.createdAt)}
      </div>
      <div className="rounded-md border border-border bg-card px-3 py-3 text-sm whitespace-pre-wrap">
        {ticket.body}
      </div>
      {ticket.attachmentNames.length > 0 && (
        <div className="flex flex-wrap gap-1.5">
          {ticket.attachmentNames.map((name) => (
            <Badge key={name} variant="outline" className="text-[10px]">
              <Paperclip className="size-3" /> {name}
            </Badge>
          ))}
        </div>
      )}
      {ticket.replies.length === 0 ? (
        <p className="text-xs text-muted-foreground">
          아직 답변이 없어요. 운영팀이 확인 중이에요.
        </p>
      ) : (
        <div className="space-y-2">
          <div className="text-xs font-medium text-muted-foreground">답변</div>
          {ticket.replies.map((r) => {
            const isAdmin = r.author === "admin";
            return (
              <div
                key={r.id}
                className={
                  isAdmin
                    ? "rounded-md border-l-2 border-primary bg-muted/60 px-3 py-2"
                    : "rounded-md border border-border bg-card px-3 py-2"
                }
              >
                <div className="flex items-center justify-between text-xs text-muted-foreground">
                  <span className="font-medium">
                    {isAdmin ? "운영팀" : "나"}
                  </span>
                  <span>{formatDate(r.createdAt)}</span>
                </div>
                <p className="mt-1 text-sm whitespace-pre-wrap">{r.body}</p>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}

function NewTicketDialog() {
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);
  const [category, setCategory] = useState<SupportTicketCategory>("registration");
  const [subject, setSubject] = useState("");
  const [body, setBody] = useState("");
  const [attachmentNames, setAttachmentNames] = useState<string[]>([]);
  const fileInputRef = useRef<HTMLInputElement | null>(null);

  const create = useMutation({
    mutationFn: () =>
      api<SupportTicket>("/support/tickets", {
        method: "POST",
        body: JSON.stringify({ category, subject, body, attachmentNames }),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["support-tickets"] });
      toast.success("문의를 보냈어요. 운영팀이 곧 답변드려요.");
      setOpen(false);
      setCategory("registration");
      setSubject("");
      setBody("");
      setAttachmentNames([]);
    },
    onError: (err) =>
      toast.error(
        err instanceof ApiError ? err.message : "문의 전송 중 오류가 났어요.",
      ),
  });

  const canSubmit =
    subject.trim().length > 0 && body.trim().length > 0 && !create.isPending;

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger
        render={
          <Button size="sm">
            <Plus className="size-3.5" /> 새 문의
          </Button>
        }
      />
      <DialogContent>
        <DialogHeader>
          <DialogTitle>1:1 문의 보내기</DialogTitle>
          <DialogDescription>
            카테고리와 제목·본문을 적어주세요. 첨부 파일은 파일명만 함께 전달돼요.
          </DialogDescription>
        </DialogHeader>
        <div className="grid gap-3">
          <div className="grid gap-1.5">
            <Label htmlFor="ticket-category">카테고리</Label>
            <Select
              value={category}
              onValueChange={(v) => setCategory(v as SupportTicketCategory)}
            >
              <SelectTrigger id="ticket-category" className="w-full">
                <SelectValue>
                  {(v: SupportTicketCategory) =>
                    SUPPORT_CATEGORY_OPTIONS.find((o) => o.value === v)?.label ??
                    "카테고리"
                  }
                </SelectValue>
              </SelectTrigger>
              <SelectContent>
                {SUPPORT_CATEGORY_OPTIONS.map((opt) => (
                  <SelectItem key={opt.value} value={opt.value}>
                    {opt.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="grid gap-1.5">
            <Label htmlFor="ticket-subject">제목</Label>
            <Input
              id="ticket-subject"
              value={subject}
              onChange={(e) => setSubject(e.target.value)}
              placeholder="간단히 한 줄로 적어주세요"
              maxLength={120}
            />
          </div>
          <div className="grid gap-1.5">
            <Label htmlFor="ticket-body">본문</Label>
            <Textarea
              id="ticket-body"
              value={body}
              onChange={(e) => setBody(e.target.value)}
              placeholder="문의 내용을 자세히 적어주세요"
              className="min-h-32"
            />
          </div>
          <div className="grid gap-1.5">
            <Label>첨부 파일</Label>
            <div className="flex flex-wrap items-center gap-2">
              <input
                ref={fileInputRef}
                type="file"
                multiple
                className="hidden"
                onChange={(e) => {
                  const files = Array.from(e.target.files ?? []);
                  if (files.length === 0) return;
                  setAttachmentNames((cur) => [
                    ...cur,
                    ...files.map((f) => f.name),
                  ]);
                  e.target.value = "";
                }}
              />
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => fileInputRef.current?.click()}
              >
                <Paperclip className="size-3.5" /> 파일 선택
              </Button>
              {attachmentNames.length === 0 ? (
                <span className="text-xs text-muted-foreground">
                  파일을 선택하면 파일명만 전달돼요.
                </span>
              ) : (
                attachmentNames.map((name, idx) => (
                  <Badge
                    key={`${name}-${idx}`}
                    variant="outline"
                    className="text-[10px]"
                  >
                    <Paperclip className="size-3" /> {name}
                    <button
                      type="button"
                      onClick={() =>
                        setAttachmentNames((cur) =>
                          cur.filter((_, i) => i !== idx),
                        )
                      }
                      className="ml-1 text-muted-foreground hover:text-foreground"
                      aria-label={`${name} 제거`}
                    >
                      ×
                    </button>
                  </Badge>
                ))
              )}
            </div>
          </div>
        </div>
        <DialogFooter>
          <DialogClose render={<Button variant="outline" />}>취소</DialogClose>
          <Button
            onClick={() => create.mutate()}
            disabled={!canSubmit}
          >
            {create.isPending ? "보내는 중…" : "문의 보내기"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
