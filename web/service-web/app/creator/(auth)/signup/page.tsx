"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";
import { ArrowLeft, ArrowRight, Check } from "lucide-react";

import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
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
import { api } from "@/lib/api";
import { cn } from "@/lib/utils";
import { SUPPLIER_TYPE_LABEL, type SupplierType } from "@/lib/types";

const STEPS = [
  { id: 1, title: "공급자 유형" },
  { id: 2, title: "사업자 정보" },
  { id: 3, title: "계정 정보" },
  { id: 4, title: "약관 동의" },
  { id: 5, title: "정산 계좌" },
  { id: 6, title: "승인 대기" },
] as const;

const businessSchema = z.object({
  businessNumber: z
    .string()
    .regex(/^\d{3}-?\d{2}-?\d{5}$/, "사업자 번호 10자리를 정확히 입력해주세요."),
  businessName: z.string().min(1, "상호를 입력해주세요."),
  representativeName: z.string().min(1, "대표자명을 입력해주세요."),
  address: z.string().min(1, "사업장 주소를 입력해주세요."),
  industry: z.string().min(1, "업태·종목을 입력해주세요."),
});

/** 생년월일(YYYY-MM-DD) 기준 나이가 14세 이상인지 — 만 나이. */
function isAtLeast14(dob: string): boolean {
  if (!dob) return false;
  const today = new Date();
  const d = new Date(dob);
  if (Number.isNaN(d.getTime())) return false;
  let age = today.getFullYear() - d.getFullYear();
  const m = today.getMonth() - d.getMonth();
  if (m < 0 || (m === 0 && today.getDate() < d.getDate())) age--;
  return age >= 14;
}

const accountSchema = z
  .object({
    email: z.string().email("이메일 형식을 확인해주세요."),
    password: z.string().min(8, "비밀번호는 최소 8자."),
    passwordConfirm: z.string(),
    phone: z
      .string()
      .regex(/^010-?\d{3,4}-?\d{4}$/, "휴대폰 번호 형식을 확인해주세요."),
    dateOfBirth: z
      .string()
      .min(1, "생년월일을 입력해주세요.")
      .refine(isAtLeast14, {
        message: "만 14세 미만은 가입할 수 없어요.",
      }),
  })
  .refine((v) => v.password === v.passwordConfirm, {
    path: ["passwordConfirm"],
    message: "비밀번호가 일치하지 않아요.",
  });

const termsSchema = z.object({
  // 4종 약관 + 개인정보 + 만19세 확인 (필수)
  termsService: z.literal(true, { message: "필수 동의 항목입니다." }),
  termsSeller: z.literal(true, { message: "필수 동의 항목입니다." }),
  termsAd: z.literal(true, { message: "필수 동의 항목입니다." }),
  termsUgc: z.literal(true, { message: "필수 동의 항목입니다." }),
  termsPrivacy: z.literal(true, { message: "필수 동의 항목입니다." }),
  termsAdult: z.literal(true, { message: "필수 동의 항목입니다." }),
  // 선택
  termsMarketing: z.boolean().optional(),
});

const payoutSchema = z.object({
  bankName: z.string().min(1, "은행을 선택해주세요."),
  accountHolder: z.string().min(1, "예금주명을 입력해주세요."),
  accountNumber: z.string().min(6, "계좌번호를 정확히 입력해주세요."),
});

type SignupState = {
  supplierTypes: SupplierType[];
  business: z.infer<typeof businessSchema> | null;
  account: z.infer<typeof accountSchema> | null;
  terms: z.infer<typeof termsSchema> | null;
  payout: z.infer<typeof payoutSchema> | null;
};

const initialState: SignupState = {
  supplierTypes: [],
  business: null,
  account: null,
  terms: null,
  payout: null,
};

export default function SignupPage() {
  const router = useRouter();
  const [step, setStep] = useState(1);
  const [state, setState] = useState<SignupState>(initialState);

  const goNext = () => setStep((s) => Math.min(STEPS.length, s + 1));
  const goPrev = () => setStep((s) => Math.max(1, s - 1));

  return (
    <div className="space-y-6">
      <div>
        <Link
          href="/creator/login"
          className="inline-flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground"
        >
          <ArrowLeft className="size-3" /> 로그인으로
        </Link>
        <h2 className="mt-2 text-2xl font-semibold tracking-tight">입점 신청</h2>
        <p className="text-sm text-muted-foreground">
          단계별로 진행하며, 각 단계는 자동 임시 저장됩니다.
        </p>
      </div>

      <Stepper step={step} />

      {step === 1 && (
        <SupplierTypeStep
          value={state.supplierTypes}
          onSubmit={(types) => {
            setState((s) => ({ ...s, supplierTypes: types }));
            goNext();
          }}
        />
      )}
      {step === 2 && (
        <BusinessStep
          value={state.business}
          onSubmit={(b) => {
            setState((s) => ({ ...s, business: b }));
            goNext();
          }}
          onBack={goPrev}
        />
      )}
      {step === 3 && (
        <AccountStep
          value={state.account}
          onSubmit={(a) => {
            setState((s) => ({ ...s, account: a }));
            goNext();
          }}
          onBack={goPrev}
        />
      )}
      {step === 4 && (
        <TermsStep
          value={state.terms}
          onSubmit={(t) => {
            setState((s) => ({ ...s, terms: t }));
            goNext();
          }}
          onBack={goPrev}
        />
      )}
      {step === 5 && (
        <PayoutStep
          value={state.payout}
          onSubmit={async (p) => {
            const payload = { ...state, payout: p };
            setState(payload);
            await api("/auth/signup", {
              method: "POST",
              body: JSON.stringify(payload),
            }).catch(() => null);
            toast.success("신청이 접수됐어요.");
            goNext();
          }}
          onBack={goPrev}
        />
      )}
      {step === 6 && (
        <PendingStep
          email={state.account?.email ?? ""}
          onClose={() => router.replace("/creator/login")}
        />
      )}
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
              {done ? <Check className="size-3" /> : s.id}
            </span>
            <span
              className={cn(
                active ? "text-foreground font-medium" : "text-muted-foreground",
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

const SUPPLIER_TYPES: SupplierType[] = ["publisher", "author", "designer"];

function SupplierTypeStep({
  value,
  onSubmit,
}: {
  value: SupplierType[];
  onSubmit: (v: SupplierType[]) => void;
}) {
  const [selected, setSelected] = useState<SupplierType[]>(value);
  const [touched, setTouched] = useState(false);
  const toggle = (t: SupplierType) => {
    setTouched(true);
    setSelected((prev) =>
      prev.includes(t) ? prev.filter((x) => x !== t) : [...prev, t],
    );
  };

  const error =
    touched && selected.length === 0
      ? "최소 한 가지 유형을 선택해주세요."
      : null;

  return (
    <div className="space-y-4">
      <p className="text-sm text-muted-foreground">
        복수 선택이 가능합니다. 가입 후 변경은 운영 문의가 필요해요.
      </p>
      <div className="grid gap-2">
        {SUPPLIER_TYPES.map((t) => {
          const checked = selected.includes(t);
          return (
            <button
              key={t}
              type="button"
              onClick={() => toggle(t)}
              className={cn(
                "flex items-center justify-between rounded-lg border p-4 text-left transition",
                checked
                  ? "border-primary bg-primary/5"
                  : "border-border hover:bg-muted/40",
              )}
            >
              <div>
                <div className="text-sm font-medium">
                  {SUPPLIER_TYPE_LABEL[t]}
                </div>
                <div className="text-xs text-muted-foreground">
                  {t === "publisher" && "라이선스 보유 출판사"}
                  {t === "author" && "자가 출판·기존 출판 작가"}
                  {t === "designer" && "3D 꾸미기 에셋 제작자"}
                </div>
              </div>
              <span
                className={cn(
                  "inline-flex size-5 items-center justify-center rounded-full border",
                  checked
                    ? "border-primary bg-primary text-primary-foreground"
                    : "border-border",
                )}
              >
                {checked && <Check className="size-3" />}
              </span>
            </button>
          );
        })}
      </div>
      {error && <p className="text-sm text-destructive">{error}</p>}
      <Button
        size="lg"
        className="w-full"
        disabled={selected.length === 0}
        onClick={() => onSubmit(selected)}
      >
        다음 <ArrowRight className="size-4" />
      </Button>
    </div>
  );
}

function BusinessStep({
  value,
  onSubmit,
  onBack,
}: {
  value: z.infer<typeof businessSchema> | null;
  onSubmit: (v: z.infer<typeof businessSchema>) => void;
  onBack: () => void;
}) {
  const [verifying, setVerifying] = useState(false);
  const [verified, setVerified] = useState<null | "active" | "closed">(null);
  const form = useForm<z.infer<typeof businessSchema>>({
    resolver: zodResolver(businessSchema),
    defaultValues: value ?? {
      businessNumber: "",
      businessName: "",
      representativeName: "",
      address: "",
      industry: "",
    },
  });

  async function verify() {
    const num = form.getValues("businessNumber");
    setVerifying(true);
    try {
      const res = await api<{
        status: "active" | "closed";
        businessName?: string;
        representativeName?: string;
      }>("/auth/business-status", {
        method: "POST",
        body: JSON.stringify({ businessNumber: num }),
      });
      setVerified(res.status);
      if (res.status === "active") {
        if (res.businessName) form.setValue("businessName", res.businessName);
        if (res.representativeName)
          form.setValue("representativeName", res.representativeName);
        toast.success("사업자 활성 상태입니다.");
      } else {
        toast.error("휴업 또는 폐업 상태로 가입이 제한됩니다.");
      }
    } catch {
      toast.error("사업자 번호를 다시 확인해주세요.");
      setVerified(null);
    } finally {
      setVerifying(false);
    }
  }

  return (
    <Form {...form}>
      <form
        className="space-y-4"
        onSubmit={form.handleSubmit(onSubmit)}
        noValidate
      >
        <FormField
          control={form.control}
          name="businessNumber"
          render={({ field }) => (
            <FormItem>
              <FormLabel>사업자 번호</FormLabel>
              <div className="flex gap-2">
                <FormControl>
                  <Input placeholder="123-45-67890" {...field} />
                </FormControl>
                <Button
                  type="button"
                  variant="outline"
                  size="lg"
                  onClick={verify}
                  disabled={verifying}
                >
                  {verifying ? "확인 중…" : "검증"}
                </Button>
              </div>
              <FormMessage />
              {verified === "active" && (
                <p className="text-xs text-emerald-600">
                  ✓ 활성 상태 — 가입을 진행할 수 있어요.
                </p>
              )}
              {verified === "closed" && (
                <p className="text-xs text-destructive">
                  휴업·폐업 상태로 가입이 차단됩니다.
                </p>
              )}
            </FormItem>
          )}
        />
        <div className="grid grid-cols-2 gap-3">
          <FormField
            control={form.control}
            name="businessName"
            render={({ field }) => (
              <FormItem>
                <FormLabel>상호</FormLabel>
                <FormControl>
                  <Input {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />
          <FormField
            control={form.control}
            name="representativeName"
            render={({ field }) => (
              <FormItem>
                <FormLabel>대표자명</FormLabel>
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
          name="address"
          render={({ field }) => (
            <FormItem>
              <FormLabel>사업장 주소</FormLabel>
              <FormControl>
                <Input {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
        <FormField
          control={form.control}
          name="industry"
          render={({ field }) => (
            <FormItem>
              <FormLabel>업태·종목</FormLabel>
              <FormControl>
                <Input placeholder="예: 정보통신업 / 출판" {...field} />
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
          <Button
            type="submit"
            size="lg"
            className="flex-1"
            disabled={verified !== "active"}
          >
            다음 <ArrowRight className="size-4" />
          </Button>
        </div>
      </form>
    </Form>
  );
}

function AccountStep({
  value,
  onSubmit,
  onBack,
}: {
  value: z.infer<typeof accountSchema> | null;
  onSubmit: (v: z.infer<typeof accountSchema>) => void;
  onBack: () => void;
}) {
  const form = useForm<z.infer<typeof accountSchema>>({
    resolver: zodResolver(accountSchema),
    defaultValues:
      value ?? {
        email: "",
        password: "",
        passwordConfirm: "",
        phone: "",
        dateOfBirth: "",
      },
  });

  return (
    <Form {...form}>
      <form
        className="space-y-4"
        onSubmit={form.handleSubmit(onSubmit)}
        noValidate
      >
        <FormField
          control={form.control}
          name="email"
          render={({ field }) => (
            <FormItem>
              <FormLabel>이메일</FormLabel>
              <FormControl>
                <Input type="email" placeholder="you@example.com" {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
        <div className="grid grid-cols-2 gap-3">
          <FormField
            control={form.control}
            name="password"
            render={({ field }) => (
              <FormItem>
                <FormLabel>비밀번호</FormLabel>
                <FormControl>
                  <Input type="password" {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />
          <FormField
            control={form.control}
            name="passwordConfirm"
            render={({ field }) => (
              <FormItem>
                <FormLabel>비밀번호 확인</FormLabel>
                <FormControl>
                  <Input type="password" {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />
        </div>
        <FormField
          control={form.control}
          name="phone"
          render={({ field }) => (
            <FormItem>
              <FormLabel>담당자 휴대폰 (SMS 인증)</FormLabel>
              <FormControl>
                <Input placeholder="010-1234-5678" {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
        <FormField
          control={form.control}
          name="dateOfBirth"
          render={({ field }) => (
            <FormItem>
              <FormLabel>담당자 생년월일</FormLabel>
              <FormControl>
                <Input
                  type="date"
                  max={new Date().toISOString().slice(0, 10)}
                  {...field}
                />
              </FormControl>
              <FormDescription>
                만 14세 미만은 가입할 수 없어요 (개인정보보호법 §22의2).
              </FormDescription>
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
  );
}

const TERMS = [
  {
    key: "termsService",
    label: "[필수] 서비스 이용약관",
    required: true,
    href: "/legal/terms-service",
  },
  {
    key: "termsSeller",
    label: "[필수] 판매자(입점) 약관",
    required: true,
    href: "/legal/terms-seller",
  },
  {
    key: "termsAd",
    label: "[필수] 광고 서비스 약관",
    required: true,
    href: "/legal/terms-ad",
  },
  {
    key: "termsUgc",
    label: "[필수] UGC(콘텐츠) 약관",
    required: true,
    href: "/legal/terms-ugc",
  },
  {
    key: "termsPrivacy",
    label: "[필수] 개인정보 처리방침",
    required: true,
    href: "/legal/privacy",
  },
  {
    key: "termsAdult",
    label: "[필수] 만 19세 이상이며, 가입 정보가 정확함",
    required: true,
    href: null,
  },
  {
    key: "termsMarketing",
    label: "[선택] 마케팅 정보 수신",
    required: false,
    href: null,
  },
] as const;

function TermsStep({
  value,
  onSubmit,
  onBack,
}: {
  value: z.infer<typeof termsSchema> | null;
  onSubmit: (v: z.infer<typeof termsSchema>) => void;
  onBack: () => void;
}) {
  const [terms, setTerms] = useState<Record<string, boolean>>(
    () =>
      value ?? {
        termsService: false,
        termsSeller: false,
        termsAd: false,
        termsUgc: false,
        termsPrivacy: false,
        termsAdult: false,
        termsMarketing: false,
      },
  );

  const allRequiredOk = TERMS.filter((t) => t.required).every(
    (t) => terms[t.key],
  );

  return (
    <div className="space-y-4">
      <button
        type="button"
        onClick={() => {
          const next: Record<string, boolean> = {};
          TERMS.forEach((t) => (next[t.key] = true));
          setTerms(next);
        }}
        className="w-full rounded-lg border border-border p-3 text-left text-sm font-medium hover:bg-muted/40"
      >
        모두 동의
      </button>
      <div className="space-y-2">
        {TERMS.map((t) => (
          <label
            key={t.key}
            className="flex cursor-pointer items-center gap-3 rounded-md px-2 py-2 hover:bg-muted/40"
          >
            <Checkbox
              checked={!!terms[t.key]}
              onCheckedChange={(v) =>
                setTerms((prev) => ({ ...prev, [t.key]: !!v }))
              }
            />
            <span className="flex-1 text-sm">{t.label}</span>
            {t.href ? (
              <Link
                href={t.href}
                target="_blank"
                onClick={(e) => e.stopPropagation()}
                className="text-xs text-muted-foreground underline-offset-4 hover:text-foreground hover:underline"
              >
                내용 보기
              </Link>
            ) : null}
          </label>
        ))}
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
        <Button
          size="lg"
          className="flex-1"
          disabled={!allRequiredOk}
          onClick={() =>
            onSubmit({
              termsService: true,
              termsSeller: true,
              termsAd: true,
              termsUgc: true,
              termsPrivacy: true,
              termsAdult: true,
              termsMarketing: !!terms.termsMarketing,
            })
          }
        >
          다음 <ArrowRight className="size-4" />
        </Button>
      </div>
    </div>
  );
}

function PayoutStep({
  value,
  onSubmit,
  onBack,
}: {
  value: z.infer<typeof payoutSchema> | null;
  onSubmit: (v: z.infer<typeof payoutSchema>) => Promise<void> | void;
  onBack: () => void;
}) {
  const form = useForm<z.infer<typeof payoutSchema>>({
    resolver: zodResolver(payoutSchema),
    defaultValues:
      value ?? { bankName: "", accountHolder: "", accountNumber: "" },
  });

  return (
    <Form {...form}>
      <form
        className="space-y-4"
        onSubmit={form.handleSubmit(async (v) => {
          await onSubmit(v);
        })}
        noValidate
      >
        <p className="text-xs text-muted-foreground">
          예금주는 사업자명과 일치해야 하고, 통장 사본은 v1.1에서 업로드할
          예정입니다 (v1.0 mock).
        </p>
        <FormField
          control={form.control}
          name="bankName"
          render={({ field }) => (
            <FormItem>
              <FormLabel>은행</FormLabel>
              <FormControl>
                <Input placeholder="예: KB국민은행" {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
        <FormField
          control={form.control}
          name="accountHolder"
          render={({ field }) => (
            <FormItem>
              <FormLabel>예금주</FormLabel>
              <FormControl>
                <Input {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
        <FormField
          control={form.control}
          name="accountNumber"
          render={({ field }) => (
            <FormItem>
              <FormLabel>계좌번호</FormLabel>
              <FormControl>
                <Input placeholder="-없이 숫자만" {...field} />
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
          <Button
            type="submit"
            size="lg"
            className="flex-1"
            disabled={form.formState.isSubmitting}
          >
            {form.formState.isSubmitting ? "신청 중…" : "신청 제출"}
          </Button>
        </div>
      </form>
    </Form>
  );
}

function PendingStep({
  email,
  onClose,
}: {
  email: string;
  onClose: () => void;
}) {
  return (
    <div className="space-y-5">
      <div className="rounded-lg border border-border bg-muted/30 px-4 py-6 text-sm">
        <div className="text-base font-semibold">접수가 완료됐어요.</div>
        <p className="mt-1 text-muted-foreground">
          관리자 검수 후 <strong>1영업일 이내</strong>에 결과를 알려드립니다.
          접수 메일은 {email || "입력하신 이메일"}로 발송돼요.
        </p>
        <ul className="mt-4 grid gap-2 text-xs text-muted-foreground">
          <li>• 승인 전에는 등록·판매 기능이 비활성화돼요.</li>
          <li>• 사업자 정보 변경은 승인 후 [설정 → 계정]에서 가능합니다.</li>
        </ul>
      </div>
      <Button size="lg" className="w-full" onClick={onClose}>
        로그인 화면으로
      </Button>
    </div>
  );
}
