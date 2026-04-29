"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { useForm, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";
import {
  ArrowLeft,
  ArrowRight,
  Box,
  CheckCircle2,
  FileText,
  Truck,
  Upload,
} from "lucide-react";

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
  Form,
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from "@/components/ui/form";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { api, ApiError } from "@/lib/api";
import { cn } from "@/lib/utils";
import { formatPages, pagesToWon } from "@/lib/format";
import {
  AGE_RATING_LABEL,
  BOOK_CATEGORY_LABEL,
  type AgeRating,
  type Book,
  type BookCategory,
  type CoverMode,
  type IsbnLookupResult,
} from "@/lib/types";

const STEPS = [
  { id: 1, title: "ISBN 조회" },
  { id: 2, title: "권리 증빙" },
  { id: 3, title: "3D 표지" },
  { id: 4, title: "판매 정보" },
  { id: 5, title: "검수 신청" },
] as const;

interface BookDraft {
  isbn: string | null;
  manualEntry: boolean;
  meta: {
    title: string;
    author: string;
    publisher: string;
    synopsis: string;
  };
  rightsDocName: string | null;
  coverMode: CoverMode | null;
  consignmentAddress: {
    name: string;
    phone: string;
    address: string;
  } | null;
  saleInfo: {
    pricePages: number;
    category: BookCategory;
    ageRating: AgeRating;
    saleStartDate: string;
    marketingCopy: string;
  } | null;
  /** 청약철회 면책 1요건 — 미리보기 N페이지 PDF/EPUB. */
  previewSample: { name: string; pageCount: number } | null;
}

const emptyDraft: BookDraft = {
  isbn: null,
  manualEntry: false,
  meta: { title: "", author: "", publisher: "", synopsis: "" },
  rightsDocName: null,
  coverMode: null,
  consignmentAddress: null,
  saleInfo: null,
  previewSample: null,
};

export default function NewBookPage() {
  const router = useRouter();
  const [step, setStep] = useState(1);
  const [draft, setDraft] = useState<BookDraft>(emptyDraft);

  const goNext = () => setStep((s) => Math.min(STEPS.length, s + 1));
  const goPrev = () => setStep((s) => Math.max(1, s - 1));

  return (
    <div className="space-y-6">
      <div>
        <Link
          href="/creator/books"
          className="inline-flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground"
        >
          <ArrowLeft className="size-3" /> 책 목록으로
        </Link>
        <h1 className="mt-2 text-2xl font-semibold tracking-tight">책 신규 등록</h1>
        <p className="text-sm text-muted-foreground">
          단계별로 진행됩니다. 각 단계는 자동 임시 저장돼요.
        </p>
      </div>

      <Stepper step={step} />

      <div className="grid gap-6 lg:grid-cols-[1fr_320px]">
        <div>
          {step === 1 && (
            <IsbnStep
              draft={draft}
              onSubmit={(d) => {
                setDraft(d);
                goNext();
              }}
            />
          )}
          {step === 2 && (
            <RightsStep
              draft={draft}
              onSubmit={(d) => {
                setDraft(d);
                goNext();
              }}
              onBack={goPrev}
            />
          )}
          {step === 3 && (
            <CoverStep
              draft={draft}
              onSubmit={(d) => {
                setDraft(d);
                goNext();
              }}
              onBack={goPrev}
            />
          )}
          {step === 4 && (
            <SaleStep
              draft={draft}
              onSubmit={(d) => {
                setDraft(d);
                goNext();
              }}
              onBack={goPrev}
            />
          )}
          {step === 5 && (
            <ReviewStep
              draft={draft}
              onBack={goPrev}
              onSubmitted={(book) => {
                toast.success("검수 신청이 접수됐어요.");
                router.push(`/creator/books/${book.id}`);
              }}
            />
          )}
        </div>

        <DraftSidebar draft={draft} step={step} />
      </div>
    </div>
  );
}

function Stepper({ step }: { step: number }) {
  return (
    <ol className="flex flex-wrap items-center gap-x-2 gap-y-1 text-xs">
      {STEPS.map((s, i) => {
        const active = step === s.id;
        const done = step > s.id;
        return (
          <li key={s.id} className="flex items-center gap-2">
            <span
              className={cn(
                "inline-flex size-5 items-center justify-center rounded-full border text-[10px] font-medium",
                done
                  ? "border-primary bg-primary text-primary-foreground"
                  : active
                    ? "border-primary text-primary"
                    : "border-border text-muted-foreground",
              )}
            >
              {done ? <CheckCircle2 className="size-3" /> : s.id}
            </span>
            <span
              className={cn(
                active
                  ? "text-foreground font-medium"
                  : "text-muted-foreground",
              )}
            >
              {s.title}
            </span>
            {i < STEPS.length - 1 && (
              <span className="text-muted-foreground/50">›</span>
            )}
          </li>
        );
      })}
    </ol>
  );
}

function DraftSidebar({ draft, step }: { draft: BookDraft; step: number }) {
  return (
    <Card className="h-fit">
      <CardHeader>
        <CardTitle className="text-sm">진행 요약</CardTitle>
        <CardDescription className="text-xs">
          단계 {step}/{STEPS.length} · 자동 임시 저장
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-3 text-xs">
        <SidebarRow label="ISBN">
          {draft.isbn ?? (draft.manualEntry ? "자가출판 (수동 입력)" : "—")}
        </SidebarRow>
        <SidebarRow label="제목">{draft.meta.title || "—"}</SidebarRow>
        <SidebarRow label="저자">{draft.meta.author || "—"}</SidebarRow>
        <SidebarRow label="권리 증빙">
          {draft.rightsDocName ?? "—"}
        </SidebarRow>
        <SidebarRow label="3D 표지">
          {draft.coverMode === "self_upload"
            ? "직접 업로드"
            : draft.coverMode === "consignment"
              ? "위탁 제작"
              : "—"}
        </SidebarRow>
        <SidebarRow label="가격">
          {draft.saleInfo
            ? `${formatPages(draft.saleInfo.pricePages)} (${pagesToWon(draft.saleInfo.pricePages).toLocaleString()}원)`
            : "—"}
        </SidebarRow>
      </CardContent>
    </Card>
  );
}

function SidebarRow({
  label,
  children,
}: {
  label: string;
  children: React.ReactNode;
}) {
  return (
    <div className="flex items-start justify-between gap-3">
      <span className="shrink-0 text-muted-foreground">{label}</span>
      <span className="min-w-0 break-words text-right font-medium">
        {children}
      </span>
    </div>
  );
}

// ---------------------------------------------------------------------------
// Step 1: ISBN
// ---------------------------------------------------------------------------

function IsbnStep({
  draft,
  onSubmit,
}: {
  draft: BookDraft;
  onSubmit: (d: BookDraft) => void;
}) {
  const [isbn, setIsbn] = useState(draft.isbn ?? "");
  const [looking, setLooking] = useState(false);
  const [match, setMatch] = useState<IsbnLookupResult | null>(null);
  const [notFound, setNotFound] = useState(false);

  async function lookup() {
    setLooking(true);
    setMatch(null);
    setNotFound(false);
    try {
      const res = await api<IsbnLookupResult & { status: string }>(
        "/books/isbn-lookup",
        { method: "POST", body: JSON.stringify({ isbn }) },
      );
      setMatch(res);
    } catch (err) {
      if (err instanceof ApiError && err.status === 404) {
        setNotFound(true);
      } else {
        toast.error("ISBN 조회 중 오류. 잠시 후 다시 시도해주세요.");
      }
    } finally {
      setLooking(false);
    }
  }

  function proceedWithMatch() {
    if (!match) return;
    onSubmit({
      ...draft,
      isbn: match.isbn,
      manualEntry: false,
      meta: {
        title: match.title,
        author: match.author,
        publisher: match.publisher,
        synopsis: match.synopsis,
      },
    });
  }

  function proceedManually() {
    onSubmit({
      ...draft,
      isbn: null,
      manualEntry: true,
      meta: { title: "", author: "", publisher: "", synopsis: "" },
    });
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">ISBN 조회</CardTitle>
        <CardDescription>
          알라딘/카카오 도서 API와 연결돼 있어요. 자가출판이라면 수동 입력으로
          진행하세요.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="flex gap-2">
          <Input
            value={isbn}
            onChange={(e) => setIsbn(e.target.value)}
            placeholder="ISBN 13자리 (예: 9788954682152)"
          />
          <Button onClick={lookup} disabled={!isbn || looking}>
            {looking ? "조회 중…" : "조회"}
          </Button>
        </div>

        <p className="text-xs text-muted-foreground">
          Mock 매칭 ISBN: <code>9788954682152</code> · <code>9791168472334</code>{" "}
          · <code>9788937462931</code>
        </p>

        {match && (
          <div className="rounded-lg border border-emerald-200 bg-emerald-50/50 p-4">
            <div className="flex items-center gap-2 text-sm font-medium text-emerald-700">
              <CheckCircle2 className="size-4" /> 매칭 성공
            </div>
            <div className="mt-3 grid gap-2 text-sm">
              <div>
                <span className="text-muted-foreground">제목:</span>{" "}
                <strong>{match.title}</strong>
              </div>
              <div>
                <span className="text-muted-foreground">저자:</span>{" "}
                {match.author}
              </div>
              <div>
                <span className="text-muted-foreground">출판사:</span>{" "}
                {match.publisher}
              </div>
              <div className="text-muted-foreground">{match.synopsis}</div>
            </div>
            <Button className="mt-4 w-full" onClick={proceedWithMatch}>
              이 정보로 진행 <ArrowRight className="size-4" />
            </Button>
          </div>
        )}

        {notFound && (
          <div className="rounded-lg border border-amber-200 bg-amber-50/50 p-4">
            <div className="text-sm font-medium text-amber-800">
              매칭되는 ISBN이 없어요
            </div>
            <p className="mt-1 text-xs text-amber-800/80">
              자가출판이라면 수동 입력 모드로 메타데이터를 직접 작성해주세요.
              위탁 3D 제작은 ISBN 매칭 책에 한해서만 신청할 수 있습니다.
            </p>
            <Button
              variant="outline"
              className="mt-3"
              onClick={proceedManually}
            >
              수동 입력으로 진행 <ArrowRight className="size-4" />
            </Button>
          </div>
        )}

        <div className="border-t border-border pt-4 text-center text-xs text-muted-foreground">
          ISBN 없이 자가출판이라면{" "}
          <button
            type="button"
            onClick={proceedManually}
            className="font-medium text-foreground underline-offset-4 hover:underline"
          >
            수동 입력으로 바로 진행
          </button>
        </div>
      </CardContent>
    </Card>
  );
}

// ---------------------------------------------------------------------------
// Step 2: Rights document
// ---------------------------------------------------------------------------

const metaSchema = z.object({
  title: z.string().min(1, "제목을 입력해주세요."),
  author: z.string().min(1, "저자를 입력해주세요."),
  publisher: z.string().min(1, "출판사를 입력해주세요."),
  synopsis: z.string().min(1, "줄거리/소개를 작성해주세요."),
});

function RightsStep({
  draft,
  onSubmit,
  onBack,
}: {
  draft: BookDraft;
  onSubmit: (d: BookDraft) => void;
  onBack: () => void;
}) {
  const form = useForm<z.infer<typeof metaSchema>>({
    resolver: zodResolver(metaSchema),
    defaultValues: draft.meta,
  });
  const [fileName, setFileName] = useState<string | null>(draft.rightsDocName);

  function handlePick(e: React.ChangeEvent<HTMLInputElement>) {
    const f = e.target.files?.[0];
    if (f) setFileName(f.name);
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">권리 증빙 업로드</CardTitle>
        <CardDescription>
          출판사는 라이선스 계약서·자체 발행 증빙. 작가는 저작권 증명서·계약서를
          올려주세요. 관리자 검수에서 사용합니다.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <Form {...form}>
          <form
            className="space-y-4"
            onSubmit={form.handleSubmit((meta) => {
              if (!fileName) {
                toast.error("권리 증빙 파일을 첨부해주세요.");
                return;
              }
              onSubmit({ ...draft, meta, rightsDocName: fileName });
            })}
            noValidate
          >
            {draft.manualEntry && (
              <p className="rounded-md bg-muted/50 px-3 py-2 text-xs text-muted-foreground">
                자가출판으로 진행 중이에요. 메타데이터를 직접 입력해주세요.
              </p>
            )}
            <div className="grid grid-cols-2 gap-3">
              <FormField
                control={form.control}
                name="title"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>제목</FormLabel>
                    <FormControl>
                      <Input {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name="author"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>저자</FormLabel>
                    <FormControl>
                      <Input {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>
            <FormField
              control={form.control}
              name="publisher"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>출판사</FormLabel>
                  <FormControl>
                    <Input {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
            <FormField
              control={form.control}
              name="synopsis"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>줄거리·소개</FormLabel>
                  <FormControl>
                    <Textarea rows={4} {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <div className="rounded-lg border border-dashed border-border p-4">
              <label className="flex cursor-pointer items-center gap-3">
                <span className="inline-flex size-9 items-center justify-center rounded-md bg-muted">
                  <FileText className="size-4" />
                </span>
                <div className="flex-1 text-sm">
                  <div className="font-medium">
                    {fileName ?? "권리 증빙 파일 첨부 (PDF / JPG)"}
                  </div>
                  <div className="text-xs text-muted-foreground">
                    Mock — 실제 업로드는 v1.1에서.
                  </div>
                </div>
                <span className="rounded-md border border-border px-2 py-1 text-xs">
                  파일 선택
                </span>
                <input
                  type="file"
                  className="hidden"
                  accept=".pdf,.jpg,.jpeg,.png"
                  onChange={handlePick}
                />
              </label>
            </div>

            <div className="flex gap-2 pt-2">
              <Button
                type="button"
                variant="outline"
                size="lg"
                className="flex-1"
                onClick={onBack}
              >
                <ArrowLeft className="size-4" /> 이전
              </Button>
              <Button type="submit" size="lg" className="flex-1">
                다음 <ArrowRight className="size-4" />
              </Button>
            </div>
          </form>
        </Form>
      </CardContent>
    </Card>
  );
}

// ---------------------------------------------------------------------------
// Step 3: 3D Cover
// ---------------------------------------------------------------------------

const consignmentSchema = z.object({
  name: z.string().min(1, "수령인을 입력해주세요."),
  phone: z
    .string()
    .regex(/^010-?\d{3,4}-?\d{4}$/, "휴대폰 번호 형식을 확인해주세요."),
  address: z.string().min(1, "주소를 입력해주세요."),
});

function CoverStep({
  draft,
  onSubmit,
  onBack,
}: {
  draft: BookDraft;
  onSubmit: (d: BookDraft) => void;
  onBack: () => void;
}) {
  const [mode, setMode] = useState<CoverMode | null>(draft.coverMode);
  const [modelFileName, setModelFileName] = useState<string | null>(null);
  const consignmentAvailable = !!draft.isbn; // 위탁은 ISBN 매칭 책에 한해서만

  const consignmentForm = useForm<z.infer<typeof consignmentSchema>>({
    resolver: zodResolver(consignmentSchema),
    defaultValues:
      draft.consignmentAddress ?? { name: "", phone: "", address: "" },
  });

  function handleSelfUploadSubmit() {
    if (!modelFileName) {
      toast.error("3D 모델 파일을 업로드해주세요.");
      return;
    }
    onSubmit({
      ...draft,
      coverMode: "self_upload",
      consignmentAddress: null,
    });
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">3D 표지 처리</CardTitle>
        <CardDescription>
          (A) 직접 업로드 또는 (B) Dotshelf 위탁 제작 중 선택. 위탁은 ISBN 매칭
          책에 한해서만 가능합니다.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="grid gap-3 md:grid-cols-2">
          <ModeCard
            active={mode === "self_upload"}
            onClick={() => setMode("self_upload")}
            icon={<Box className="size-4" />}
            title="(A) 직접 업로드"
            desc="USDZ / glTF 파일 업로드. 폴리곤·텍스처 해상도·물리 단위 가이드라인 적용."
          />
          <ModeCard
            active={mode === "consignment"}
            disabled={!consignmentAvailable}
            onClick={() => consignmentAvailable && setMode("consignment")}
            icon={<Truck className="size-4" />}
            title="(B) Dotshelf 위탁 제작"
            desc={
              consignmentAvailable
                ? "실물 책 1부 발송 → 스캐닝 → 검수 → 적용. 수수료 안내·예상 일정 별도."
                : "ISBN 매칭 책에 한해 신청 가능합니다."
            }
          />
        </div>

        {mode === "self_upload" && (
          <div className="rounded-lg border border-dashed border-border p-4">
            <label className="flex cursor-pointer items-center gap-3">
              <span className="inline-flex size-9 items-center justify-center rounded-md bg-muted">
                <Upload className="size-4" />
              </span>
              <div className="flex-1 text-sm">
                <div className="font-medium">
                  {modelFileName ?? "USDZ / glTF 파일 업로드"}
                </div>
                <div className="text-xs text-muted-foreground">
                  업로드 후 서버 측 폴리곤/사이즈 검증 (mock에서는 즉시 통과)
                </div>
              </div>
              <span className="rounded-md border border-border px-2 py-1 text-xs">
                파일 선택
              </span>
              <input
                type="file"
                className="hidden"
                accept=".usdz,.glb,.gltf,.zip"
                onChange={(e) => {
                  const f = e.target.files?.[0];
                  if (f) setModelFileName(f.name);
                }}
              />
            </label>
          </div>
        )}

        {mode === "consignment" && (
          <Form {...consignmentForm}>
            <form
              className="grid gap-3"
              onSubmit={consignmentForm.handleSubmit((addr) =>
                onSubmit({
                  ...draft,
                  coverMode: "consignment",
                  consignmentAddress: addr,
                }),
              )}
              noValidate
            >
              <p className="rounded-md bg-amber-50 px-3 py-2 text-xs text-amber-800">
                실물 책 1부를 수령 후 반환하지 않으니 신중히 결정해주세요. 수수료
                안내는 검수 단계에서 함께 발송됩니다.
              </p>
              <FormField
                control={consignmentForm.control}
                name="name"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>수령인 / 발송인</FormLabel>
                    <FormControl>
                      <Input {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={consignmentForm.control}
                name="phone"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>연락처</FormLabel>
                    <FormControl>
                      <Input placeholder="010-1234-5678" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={consignmentForm.control}
                name="address"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>발송 주소</FormLabel>
                    <FormControl>
                      <Input {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <div className="flex gap-2 pt-2">
                <Button
                  type="button"
                  variant="outline"
                  size="lg"
                  className="flex-1"
                  onClick={onBack}
                >
                  <ArrowLeft className="size-4" /> 이전
                </Button>
                <Button type="submit" size="lg" className="flex-1">
                  다음 <ArrowRight className="size-4" />
                </Button>
              </div>
            </form>
          </Form>
        )}

        {mode === "self_upload" && (
          <div className="flex gap-2 pt-2">
            <Button
              type="button"
              variant="outline"
              size="lg"
              className="flex-1"
              onClick={onBack}
            >
              <ArrowLeft className="size-4" /> 이전
            </Button>
            <Button
              size="lg"
              className="flex-1"
              onClick={handleSelfUploadSubmit}
            >
              다음 <ArrowRight className="size-4" />
            </Button>
          </div>
        )}

        {!mode && (
          <div className="flex gap-2 pt-2">
            <Button
              type="button"
              variant="outline"
              size="lg"
              className="flex-1"
              onClick={onBack}
            >
              <ArrowLeft className="size-4" /> 이전
            </Button>
            <Button size="lg" className="flex-1" disabled>
              방식 선택 후 진행
            </Button>
          </div>
        )}
      </CardContent>
    </Card>
  );
}

function ModeCard({
  active,
  disabled,
  onClick,
  icon,
  title,
  desc,
}: {
  active: boolean;
  disabled?: boolean;
  onClick: () => void;
  icon: React.ReactNode;
  title: string;
  desc: string;
}) {
  return (
    <button
      type="button"
      disabled={disabled}
      onClick={onClick}
      className={cn(
        "flex flex-col items-start gap-2 rounded-lg border p-4 text-left transition",
        active
          ? "border-primary bg-primary/5"
          : "border-border hover:bg-muted/40",
        disabled && "cursor-not-allowed opacity-50 hover:bg-transparent",
      )}
    >
      <span className="inline-flex size-8 items-center justify-center rounded-md bg-muted">
        {icon}
      </span>
      <div className="text-sm font-semibold">{title}</div>
      <p className="text-xs text-muted-foreground">{desc}</p>
    </button>
  );
}

// ---------------------------------------------------------------------------
// Step 4: Sale info
// ---------------------------------------------------------------------------

const saleSchema = z.object({
  pricePages: z
    .number()
    .int("정수로 입력")
    .min(100, "최소 100페이지 (= 1,000원)부터 가능합니다."),
  category: z.enum([
    "novel",
    "essay",
    "self_dev",
    "comics",
    "humanities",
    "kids",
    "etc",
  ]),
  ageRating: z.enum(["all", "12+", "15+", "19+"]),
  saleStartDate: z.string().min(1, "판매 시작일을 선택해주세요."),
  marketingCopy: z.string().max(40, "40자 이내로 작성해주세요.").optional(),
});

function SaleStep({
  draft,
  onSubmit,
  onBack,
}: {
  draft: BookDraft;
  onSubmit: (d: BookDraft) => void;
  onBack: () => void;
}) {
  const form = useForm<z.infer<typeof saleSchema>>({
    resolver: zodResolver(saleSchema),
    defaultValues: draft.saleInfo
      ? draft.saleInfo
      : {
          pricePages: 1000,
          category: "novel",
          ageRating: "all",
          saleStartDate: "",
          marketingCopy: "",
        },
  });

  // useWatch는 React Compiler 호환.
  const pricePages = useWatch({ control: form.control, name: "pricePages" });

  const [previewName, setPreviewName] = useState<string | null>(
    draft.previewSample?.name ?? null,
  );
  const [previewPages, setPreviewPages] = useState<number>(
    draft.previewSample?.pageCount ?? 20,
  );

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">판매 정보</CardTitle>
        <CardDescription>
          가격은 페이지(p) 단위로 입력하세요. 1p = 10원으로 환산됩니다.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <Form {...form}>
          <form
            className="space-y-4"
            onSubmit={form.handleSubmit((v) => {
              if (!previewName) {
                toast.error(
                  "미리보기 파일을 첨부해주세요. 청약철회 면책 1요건입니다.",
                );
                return;
              }
              onSubmit({
                ...draft,
                saleInfo: {
                  pricePages: v.pricePages,
                  category: v.category,
                  ageRating: v.ageRating,
                  saleStartDate: v.saleStartDate,
                  marketingCopy: v.marketingCopy ?? "",
                },
                previewSample: {
                  name: previewName,
                  pageCount: previewPages,
                },
              });
            })}
            noValidate
          >
            <FormField
              control={form.control}
              name="pricePages"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>가격 (페이지)</FormLabel>
                  <FormControl>
                    <Input
                      type="number"
                      min={100}
                      step={50}
                      value={Number.isFinite(field.value) ? field.value : ""}
                      onChange={(e) => {
                        const v = e.target.value;
                        field.onChange(v === "" ? 0 : Number(v));
                      }}
                      onBlur={field.onBlur}
                      name={field.name}
                      ref={field.ref}
                    />
                  </FormControl>
                  <FormDescription>
                    {pricePages
                      ? `약 ${pagesToWon(Number(pricePages)).toLocaleString()}원`
                      : "1p = 10원"}
                  </FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />

            <div className="grid grid-cols-2 gap-3">
              <FormField
                control={form.control}
                name="category"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>카테고리</FormLabel>
                    <Select
                      value={field.value}
                      onValueChange={field.onChange}
                    >
                      <SelectTrigger>
                        <SelectValue>
                          {(v: BookCategory) => BOOK_CATEGORY_LABEL[v]}
                        </SelectValue>
                      </SelectTrigger>
                      <SelectContent>
                        {Object.entries(BOOK_CATEGORY_LABEL).map(([v, l]) => (
                          <SelectItem key={v} value={v}>
                            {l}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name="ageRating"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>연령 등급</FormLabel>
                    <Select
                      value={field.value}
                      onValueChange={field.onChange}
                    >
                      <SelectTrigger>
                        <SelectValue>
                          {(v: AgeRating) => AGE_RATING_LABEL[v]}
                        </SelectValue>
                      </SelectTrigger>
                      <SelectContent>
                        {Object.entries(AGE_RATING_LABEL).map(([v, l]) => (
                          <SelectItem key={v} value={v}>
                            {l}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>

            <FormField
              control={form.control}
              name="saleStartDate"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>판매 시작일</FormLabel>
                  <FormControl>
                    <Input type="date" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="marketingCopy"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>마케팅 카피 (선택)</FormLabel>
                  <FormControl>
                    <Input
                      placeholder="40자 이내, 검색·광고 노출 시 활용"
                      {...field}
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            {/* 청약철회 면책 1요건 — 미리보기 N페이지 첨부 */}
            <div className="space-y-2 rounded-lg border border-amber-200 bg-amber-50/40 p-3">
              <div className="flex items-start gap-2">
                <FileText className="mt-0.5 size-4 shrink-0 text-amber-700" />
                <div className="text-xs leading-relaxed text-amber-900">
                  <strong>미리보기 첨부는 필수</strong>예요. 디지털 콘텐츠
                  청약철회 면책의 3요건 중 하나(시험 제공)를 충족하려면 일부
                  페이지를 미리보기로 노출해야 합니다.
                </div>
              </div>
              <label className="flex cursor-pointer items-center gap-3 rounded-md border border-dashed border-amber-300 bg-background p-3 hover:bg-amber-50">
                <span className="inline-flex size-9 items-center justify-center rounded-md bg-amber-100">
                  <Upload className="size-4 text-amber-700" />
                </span>
                <div className="flex-1 text-sm">
                  <div className="font-medium">
                    {previewName ?? "미리보기 PDF / EPUB"}
                  </div>
                  <div className="text-xs text-muted-foreground">
                    권장 분량: 챕터 1 또는 약 10~30페이지
                  </div>
                </div>
                <span className="rounded-md border border-border px-2 py-1 text-xs">
                  파일 선택
                </span>
                <input
                  type="file"
                  className="hidden"
                  accept=".pdf,.epub"
                  onChange={(e) => {
                    const f = e.target.files?.[0];
                    if (f) setPreviewName(f.name);
                  }}
                />
              </label>
              {previewName && (
                <div className="grid gap-1.5">
                  <Label className="text-xs">미리보기 페이지 수</Label>
                  <Input
                    type="number"
                    min={1}
                    max={200}
                    value={previewPages}
                    onChange={(e) =>
                      setPreviewPages(Number(e.target.value))
                    }
                  />
                </div>
              )}
            </div>

            <div className="flex gap-2 pt-2">
              <Button
                type="button"
                variant="outline"
                size="lg"
                className="flex-1"
                onClick={onBack}
              >
                <ArrowLeft className="size-4" /> 이전
              </Button>
              <Button type="submit" size="lg" className="flex-1">
                다음 <ArrowRight className="size-4" />
              </Button>
            </div>
          </form>
        </Form>
      </CardContent>
    </Card>
  );
}

// ---------------------------------------------------------------------------
// Step 5: Review submission
// ---------------------------------------------------------------------------

function ReviewStep({
  draft,
  onBack,
  onSubmitted,
}: {
  draft: BookDraft;
  onBack: () => void;
  onSubmitted: (book: Book) => void;
}) {
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit() {
    if (!draft.saleInfo) {
      toast.error("판매 정보 단계가 비어 있어요.");
      return;
    }
    setSubmitting(true);
    try {
      const created = await api<Book>("/books", {
        method: "POST",
        body: JSON.stringify({
          isbn: draft.isbn,
          title: draft.meta.title,
          author: draft.meta.author,
          publisher: draft.meta.publisher,
          synopsis: draft.meta.synopsis,
          coverMode: draft.coverMode,
          category: draft.saleInfo.category,
          ageRating: draft.saleInfo.ageRating,
          pricePages: draft.saleInfo.pricePages,
          saleStartDate: draft.saleInfo.saleStartDate,
          marketingCopy: draft.saleInfo.marketingCopy,
          previewSampleName: draft.previewSample?.name ?? null,
          previewSamplePageCount: draft.previewSample?.pageCount ?? null,
          status: "draft",
        }),
      });
      const submitted = await api<Book>(`/books/${created.id}/submit`, {
        method: "POST",
      });
      onSubmitted(submitted);
    } catch {
      toast.error("검수 신청 중 오류. 잠시 후 다시 시도해주세요.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">최종 확인 후 검수 신청</CardTitle>
        <CardDescription>
          신청 후 관리자 큐에 진입합니다. SLA: 영업일 1.5일 이내 회신.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <ReviewRow label="ISBN">
          {draft.isbn ?? "자가출판 (수동 입력)"}
        </ReviewRow>
        <ReviewRow label="제목 / 저자">
          <strong>{draft.meta.title}</strong> · {draft.meta.author}
        </ReviewRow>
        <ReviewRow label="출판사">{draft.meta.publisher}</ReviewRow>
        <ReviewRow label="줄거리">
          <span className="line-clamp-2 text-muted-foreground">
            {draft.meta.synopsis}
          </span>
        </ReviewRow>
        <ReviewRow label="권리 증빙">{draft.rightsDocName}</ReviewRow>
        <ReviewRow label="3D 표지">
          {draft.coverMode === "self_upload" ? (
            "직접 업로드"
          ) : (
            <Badge>위탁 제작</Badge>
          )}
        </ReviewRow>
        {draft.saleInfo && (
          <>
            <ReviewRow label="가격">
              {formatPages(draft.saleInfo.pricePages)} (
              {pagesToWon(draft.saleInfo.pricePages).toLocaleString()}원)
            </ReviewRow>
            <ReviewRow label="카테고리·연령">
              {BOOK_CATEGORY_LABEL[draft.saleInfo.category]} ·{" "}
              {AGE_RATING_LABEL[draft.saleInfo.ageRating]}
            </ReviewRow>
            <ReviewRow label="판매 시작일">
              {draft.saleInfo.saleStartDate}
            </ReviewRow>
            {draft.saleInfo.marketingCopy && (
              <ReviewRow label="마케팅 카피">
                {draft.saleInfo.marketingCopy}
              </ReviewRow>
            )}
          </>
        )}
        <ReviewRow label="미리보기">
          {draft.previewSample ? (
            <div className="flex items-center gap-2">
              <Badge className="bg-emerald-100 text-emerald-700 hover:bg-emerald-100/80">
                첨부됨
              </Badge>
              <span className="text-xs text-muted-foreground">
                {draft.previewSample.name} · {draft.previewSample.pageCount}p
              </span>
            </div>
          ) : (
            <Badge variant="destructive">미첨부 (검수 차단 가능)</Badge>
          )}
        </ReviewRow>

        <div className="flex gap-2 pt-2">
          <Button
            type="button"
            variant="outline"
            size="lg"
            className="flex-1"
            onClick={onBack}
            disabled={submitting}
          >
            <ArrowLeft className="size-4" /> 이전
          </Button>
          <Button
            size="lg"
            className="flex-1"
            onClick={handleSubmit}
            disabled={submitting}
          >
            {submitting ? "신청 중…" : "검수 신청"}
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}

function ReviewRow({
  label,
  children,
}: {
  label: string;
  children: React.ReactNode;
}) {
  return (
    <div className="grid grid-cols-[120px_1fr] gap-3 border-b border-border/60 pb-3 text-sm last:border-0">
      <span className="text-muted-foreground">{label}</span>
      <div>{children}</div>
    </div>
  );
}
