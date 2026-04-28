"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
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
import { saveAuth } from "@/lib/auth";
import { api, ApiError } from "@/lib/api";
import { useNow } from "@/lib/use-now";
import type { Account } from "@/lib/types";

const schema = z.object({
  email: z.string().email("이메일 형식을 확인해주세요."),
  password: z.string().min(1, "비밀번호를 입력해주세요."),
});

type FormValues = z.infer<typeof schema>;

const MAX_ATTEMPTS = 5;

export default function LoginPage() {
  const router = useRouter();
  const [attempts, setAttempts] = useState(0);
  const [lockedUntil, setLockedUntil] = useState<number | null>(null);
  // 잠금 카운트다운을 render-pure하게 받기 위해 useNow 사용.
  const now = useNow(1_000);

  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { email: "", password: "" },
  });

  const locked = lockedUntil !== null && now < lockedUntil;

  async function onSubmit(values: FormValues) {
    if (locked) return;
    try {
      const res = await api<{ token: string; account: Account }>(
        "/auth/login",
        {
          method: "POST",
          body: JSON.stringify(values),
        },
      );
      saveAuth(res);
      toast.success("로그인했어요.");
      router.replace("/dashboard");
    } catch (err) {
      const next = attempts + 1;
      setAttempts(next);
      if (next >= MAX_ATTEMPTS) {
        // useNow의 now 사용 — 최대 1초 오차지만 5분 잠금엔 무시 가능.
        const until = now + 5 * 60 * 1000;
        setLockedUntil(until);
        toast.error("5회 실패로 5분간 잠겼습니다.");
      } else {
        const message =
          err instanceof ApiError && err.status === 401
            ? `이메일 또는 비밀번호를 확인해주세요. (${next}/${MAX_ATTEMPTS})`
            : "로그인에 실패했어요. 잠시 후 다시 시도해주세요.";
        toast.error(message);
      }
    }
  }

  return (
    <div className="space-y-6">
      <div className="space-y-1.5">
        <h2 className="text-2xl font-semibold tracking-tight">로그인</h2>
        <p className="text-sm text-muted-foreground">
          입점 승인된 계정으로 로그인하세요.
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
            name="email"
            render={({ field }) => (
              <FormItem>
                <FormLabel>이메일</FormLabel>
                <FormControl>
                  <Input
                    type="email"
                    autoComplete="email"
                    placeholder="you@example.com"
                    {...field}
                  />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />
          <FormField
            control={form.control}
            name="password"
            render={({ field }) => (
              <FormItem>
                <div className="flex items-center justify-between">
                  <FormLabel>비밀번호</FormLabel>
                  <Link
                    href="/forgot-password"
                    className="text-xs text-muted-foreground underline-offset-4 hover:underline"
                  >
                    비밀번호를 잊으셨나요?
                  </Link>
                </div>
                <FormControl>
                  <Input
                    type="password"
                    autoComplete="current-password"
                    placeholder="••••••••"
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
            disabled={form.formState.isSubmitting || !!locked}
          >
            {locked
              ? "잠시 후 다시 시도해주세요"
              : form.formState.isSubmitting
                ? "로그인 중…"
                : "로그인"}
          </Button>
        </form>
      </Form>

      <div className="text-center text-sm text-muted-foreground">
        아직 계정이 없으신가요?{" "}
        <Link
          href="/signup"
          className="font-medium text-foreground underline-offset-4 hover:underline"
        >
          입점 신청
        </Link>
      </div>

      {process.env.NEXT_PUBLIC_USE_MOCKS === "true" && (
        <p className="rounded-md bg-muted/50 px-3 py-2 text-center text-xs text-muted-foreground">
          Mock 모드 — 임의 이메일 + 비밀번호로 로그인 가능.{" "}
          <code>wrong</code>을 비밀번호로 입력하면 실패 흐름 테스트.
        </p>
      )}
    </div>
  );
}
