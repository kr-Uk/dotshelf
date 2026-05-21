"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";

import { Button } from "@/components/ui/button";
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from "@/components/ui/form";
import { Input } from "@/components/ui/input";
import { api, ApiError } from "@/lib/api";
import {
  clearPendingMfa,
  loadPendingMfa,
  saveAuth,
  type PendingMfa,
} from "@/lib/auth";
import type { AdminAccount } from "@/lib/types";

const schema = z.object({
  code: z
    .string()
    .regex(/^\d{6}$/u, "6자리 숫자를 입력해주세요."),
});

type FormValues = z.infer<typeof schema>;

const MAX_ATTEMPTS = 5;

export default function MfaPage() {
  const router = useRouter();
  const [pending, setPending] = useState<PendingMfa | null | undefined>(
    undefined,
  );
  const [attempts, setAttempts] = useState(0);
  const [locked, setLocked] = useState(false);

  useEffect(() => {
    // pending MFA는 페이지 진입 시점의 challenge를 고정해 submit 중 storage 정리와
    // redirect guard가 경쟁하지 않게 한다.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setPending(loadPendingMfa());
  }, []);

  useEffect(() => {
    if (pending === undefined) return;
    if (!pending) {
      router.replace("/login");
    }
  }, [pending, router]);

  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { code: "" },
  });

  async function onSubmit(values: FormValues) {
    if (!pending || locked) return;
    try {
      const res = await api<{ token: string; account: AdminAccount }>(
        "/auth/mfa/verify",
        {
          method: "POST",
          body: JSON.stringify({
            challengeId: pending.challengeId,
            code: values.code,
          }),
        },
      );
      saveAuth(res);
      clearPendingMfa();
      toast.success("로그인했어요.");
      router.replace("/dashboard");
    } catch (err) {
      const next = attempts + 1;
      setAttempts(next);
      if (next >= MAX_ATTEMPTS) {
        setLocked(true);
        toast.error("5회 실패로 계정이 잠겼습니다. 슈퍼 관리자에게 문의하세요.");
        return;
      }
      const message =
        err instanceof ApiError && err.status === 401
          ? `코드가 일치하지 않습니다. (${next}/${MAX_ATTEMPTS})`
          : "MFA 검증에 실패했어요.";
      toast.error(message);
      form.setValue("code", "");
    }
  }

  return (
    <div className="space-y-6">
      <div className="space-y-1.5">
        <h2 className="text-2xl font-semibold tracking-tight">2단계 인증</h2>
        <p className="text-sm text-muted-foreground">
          {pending?.email ?? "확인 중"} 계정에 등록된 OTP 앱의 6자리 코드를 입력하세요.
        </p>
      </div>

      <Form {...form}>
        <form
          className="space-y-4"
          onSubmit={form.handleSubmit(onSubmit)}
          noValidate
        >
          <FormField
            control={form.control}
            name="code"
            render={({ field }) => (
              <FormItem>
                <FormLabel>인증 코드</FormLabel>
                <FormControl>
                  <Input
                    inputMode="numeric"
                    maxLength={6}
                    autoComplete="one-time-code"
                    placeholder="6자리 숫자"
                    className="tracking-[0.6em] text-center text-lg"
                    {...field}
                  />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <Button
            type="submit"
            className="w-full"
            size="lg"
            disabled={form.formState.isSubmitting || locked}
          >
            {locked
              ? "계정 잠김 — 슈퍼 관리자에게 문의"
              : form.formState.isSubmitting
                ? "검증 중…"
                : "확인"}
          </Button>
        </form>
      </Form>

      <p className="rounded-md bg-muted/50 px-3 py-2 text-center text-xs text-muted-foreground">
        Mock 모드 — <code>000000</code>은 실패, 나머지 6자리는 통과합니다.
      </p>
    </div>
  );
}
