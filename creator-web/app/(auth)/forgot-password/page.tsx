"use client";

import Link from "next/link";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";

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
import { api } from "@/lib/api";

const schema = z.object({
  email: z.string().email("이메일 형식을 확인해주세요."),
});

type FormValues = z.infer<typeof schema>;

export default function ForgotPasswordPage() {
  const [sent, setSent] = useState(false);
  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { email: "" },
  });

  async function onSubmit(values: FormValues) {
    await api("/auth/forgot-password", {
      method: "POST",
      body: JSON.stringify(values),
    }).catch(() => null);
    setSent(true);
  }

  return (
    <div className="space-y-6">
      <div className="space-y-1.5">
        <h2 className="text-2xl font-semibold tracking-tight">비밀번호 재설정</h2>
        <p className="text-sm text-muted-foreground">
          가입한 이메일로 재설정 링크를 보내드려요.
        </p>
      </div>

      {sent ? (
        <div className="rounded-md border border-border bg-muted/30 px-4 py-6 text-sm">
          입력한 이메일로 재설정 링크를 보냈어요. 메일이 보이지 않는다면 스팸함도
          확인해주세요.
        </div>
      ) : (
        <Form {...form}>
          <form
            onSubmit={form.handleSubmit(onSubmit)}
            className="space-y-4"
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
            <Button
              type="submit"
              className="w-full"
              size="lg"
              disabled={form.formState.isSubmitting}
            >
              {form.formState.isSubmitting ? "보내는 중…" : "재설정 링크 받기"}
            </Button>
          </form>
        </Form>
      )}

      <div className="text-center text-sm text-muted-foreground">
        <Link
          href="/login"
          className="font-medium text-foreground underline-offset-4 hover:underline"
        >
          로그인으로 돌아가기
        </Link>
      </div>
    </div>
  );
}
