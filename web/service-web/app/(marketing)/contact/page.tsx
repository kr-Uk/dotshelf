"use client";

import { useState } from "react";

import { Icon } from "@/components/marketing/icon";

const FAQS = [
  {
    q: "친구의 서재를 보려면 친구도 가입해야 하나요?",
    a: "네, 양쪽 모두 DotShelf 계정이 있어야 해요. 다만 ‘공개 서재’ 링크를 통해 친구가 회원이 아니더라도 웹에서 둘러볼 수 있는 기능은 곧 출시될 예정입니다.",
  },
  {
    q: "책 데이터는 어디서 가져오나요?",
    a: "ISBN 기반의 글로벌 도서 데이터베이스(국립중앙도서관, OpenLibrary, Google Books)를 사용해요. 표지가 없는 책은 직접 사진을 찍어 등록할 수도 있습니다.",
  },
  {
    q: "결제는 어떻게 이루어지나요? 무료인가요?",
    a: "기본 기능은 모두 무료입니다. 책장 테마, 추가 컬렉션 슬롯 등은 DotShelf+ (월 4,900원) 구독으로 이용할 수 있어요.",
  },
  {
    q: "계정을 삭제하면 데이터는 어떻게 되나요?",
    a: "30일 동안은 복구가 가능하도록 보관되며, 그 이후에는 모든 개인 데이터가 영구 삭제됩니다. 자세한 내용은 개인정보 처리방침을 참고해주세요.",
  },
];

const TOPICS = ["일반 문의", "버그 신고", "제휴/파트너십", "미디어"] as const;
type Topic = (typeof TOPICS)[number];

export default function ContactPage() {
  return (
    <div className="page-fade max-w-6xl mx-auto px-5 sm:px-8 pt-14 pb-16">
      <div className="grid lg:grid-cols-12 gap-10">
        <header className="lg:col-span-12 mb-2">
          <div className="mono text-[11px] tracking-widest uppercase text-wood mb-4">
            ── 문의
          </div>
          <h1 className="serif text-[40px] sm:text-[52px] leading-[1.05] text-charcoal max-w-2xl">
            궁금한 점이 있다면, <span className="italic">편하게</span>{" "}
            적어주세요.
          </h1>
          <p className="mt-4 text-[16px] text-charcoal/75 max-w-xl leading-[1.7]">
            대부분의 답은 아래 FAQ에서 찾으실 수 있어요. 그래도 답이 없다면 폼을
            채워 보내주세요.
          </p>
        </header>

        <div className="lg:col-span-7">
          <ContactForm />
          <div className="mt-12">
            <div className="flex items-baseline justify-between mb-2">
              <h2 className="serif text-[26px] text-charcoal">
                자주 묻는 질문
              </h2>
              <span className="mono text-[11px] text-paper-muted">
                {FAQS.length} ITEMS
              </span>
            </div>
            <div>
              {FAQS.map((f) => (
                <FAQItem key={f.q} {...f} />
              ))}
            </div>
          </div>
        </div>
        <aside className="lg:col-span-5">
          <ContactSidebar />
        </aside>
      </div>
    </div>
  );
}

function FAQItem({ q, a }: { q: string; a: string }) {
  return (
    <details className="border-b border-beige/70 py-5 group">
      <summary className="flex items-start justify-between gap-4">
        <span className="serif text-[18px] sm:text-[19px] text-charcoal leading-snug">
          {q}
        </span>
        <span className="faq-caret text-wood mt-1 flex-shrink-0">
          <Icon name="plus" size={18} strokeWidth={1.8} />
        </span>
      </summary>
      <p className="mt-3 text-[14px] sm:text-[15px] text-charcoal/75 leading-[1.75] max-w-2xl">
        {a}
      </p>
    </details>
  );
}

type FormState = {
  name: string;
  email: string;
  topic: Topic;
  message: string;
};

type FormErrors = Partial<Record<keyof FormState, string>>;

const EMPTY_FORM: FormState = {
  name: "",
  email: "",
  topic: "일반 문의",
  message: "",
};

function ContactForm() {
  const [form, setForm] = useState<FormState>(EMPTY_FORM);
  const [sent, setSent] = useState(false);
  const [errors, setErrors] = useState<FormErrors>({});

  const submit = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const errs: FormErrors = {};
    if (!form.name.trim()) errs.name = "이름을 입력해주세요";
    if (!form.email.trim() || !/^[^@]+@[^@]+\.[^@]+$/.test(form.email))
      errs.email = "올바른 이메일을 입력해주세요";
    if (form.message.trim().length < 10) errs.message = "10자 이상 입력해주세요";
    setErrors(errs);
    if (Object.keys(errs).length === 0) setSent(true);
  };

  if (sent) {
    return (
      <div className="rounded-3xl border border-beige paper-dark p-10 text-center">
        <div className="w-14 h-14 mx-auto rounded-full bg-cream border border-beige flex items-center justify-center text-wood mb-5">
          <Icon name="check" size={26} strokeWidth={2} />
        </div>
        <h3 className="serif text-[26px] text-charcoal">
          메시지가 전송되었어요.
        </h3>
        <p className="mt-2 text-[14px] text-charcoal/75 max-w-sm mx-auto leading-[1.7]">
          평일 기준 1~2일 이내에{" "}
          <span className="mono text-charcoal">{form.email}</span>로
          답장드릴게요.
        </p>
        <button
          type="button"
          onClick={() => {
            setSent(false);
            setForm(EMPTY_FORM);
          }}
          className="mt-6 btn-secondary inline-flex items-center gap-1.5 px-5 py-2.5 rounded-full text-sm"
        >
          새 문의 보내기
        </button>
      </div>
    );
  }

  const change =
    <K extends "name" | "email" | "message">(k: K) =>
    (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
      const value = e.target.value;
      setForm((f) => ({ ...f, [k]: value }));
      if (errors[k]) setErrors((er) => ({ ...er, [k]: undefined }));
    };

  return (
    <form
      onSubmit={submit}
      className="rounded-3xl border border-beige bg-cream p-6 sm:p-8 shadow-warm"
      noValidate
    >
      <div className="grid sm:grid-cols-2 gap-4">
        <div>
          <label className="field-label" htmlFor="contact-name">
            이름
          </label>
          <input
            id="contact-name"
            className="field"
            placeholder="홍길동"
            value={form.name}
            onChange={change("name")}
          />
          {errors.name && (
            <p className="mt-1.5 text-[12px] text-wood">{errors.name}</p>
          )}
        </div>
        <div>
          <label className="field-label" htmlFor="contact-email">
            이메일
          </label>
          <input
            id="contact-email"
            type="email"
            className="field"
            placeholder="you@example.com"
            value={form.email}
            onChange={change("email")}
          />
          {errors.email && (
            <p className="mt-1.5 text-[12px] text-wood">{errors.email}</p>
          )}
        </div>
      </div>
      <div className="mt-4">
        <span className="field-label">문의 유형</span>
        <div className="flex flex-wrap gap-2">
          {TOPICS.map((t) => (
            <button
              key={t}
              type="button"
              onClick={() => setForm((f) => ({ ...f, topic: t }))}
              className={`px-4 py-2 rounded-full text-[13px] border transition ${
                form.topic === t
                  ? "bg-charcoal text-cream border-charcoal"
                  : "bg-cream text-charcoal border-beige hover:border-wood/60"
              }`}
            >
              {t}
            </button>
          ))}
        </div>
      </div>
      <div className="mt-4">
        <label className="field-label" htmlFor="contact-message">
          메시지
        </label>
        <textarea
          id="contact-message"
          className="field"
          rows={6}
          placeholder="자세히 알려주세요. 관련 화면이 있다면 캡처도 함께 첨부해주시면 좋아요."
          value={form.message}
          onChange={change("message")}
        />
        {errors.message && (
          <p className="mt-1.5 text-[12px] text-wood">{errors.message}</p>
        )}
      </div>
      <div className="mt-6 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
        <p className="text-[12px] text-paper-muted">
          개인정보 처리방침에 동의하며, 응답을 위해 이메일을 사용합니다.
        </p>
        <button
          type="submit"
          className="btn-primary inline-flex items-center justify-center gap-2 px-6 py-3 rounded-full text-sm font-medium"
        >
          <Icon name="mail" size={15} />
          문의 보내기
        </button>
      </div>
    </form>
  );
}

function ContactSidebar() {
  return (
    <div className="space-y-4">
      <div className="rounded-2xl border border-beige bg-beige-soft/50 p-5">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-cream border border-beige flex items-center justify-center text-wood">
            <Icon name="mail" size={18} />
          </div>
          <div>
            <div className="mono text-[10px] uppercase tracking-widest text-paper-muted">
              일반 문의
            </div>
            <a
              href="mailto:contact@dotshelf.digital"
              className="serif text-[16px] text-charcoal ink-link"
            >
              contact@dotshelf.digital
            </a>
          </div>
        </div>
      </div>
      <div className="rounded-2xl border border-beige bg-beige-soft/50 p-5">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-cream border border-beige flex items-center justify-center text-wood">
            <Icon name="users" size={18} />
          </div>
          <div>
            <div className="mono text-[10px] uppercase tracking-widest text-paper-muted">
              제휴 / 파트너십
            </div>
            <a
              href="mailto:hello@dotshelf.digital"
              className="serif text-[16px] text-charcoal ink-link"
            >
              hello@dotshelf.digital
            </a>
          </div>
        </div>
      </div>
      <div className="rounded-2xl border border-beige bg-cream p-5">
        <div className="mono text-[10px] uppercase tracking-widest text-paper-muted mb-3">
          응답 시간
        </div>
        <div className="flex items-baseline gap-2">
          <span className="serif text-[28px] text-charcoal leading-none">
            평균 18시간
          </span>
        </div>
        <p className="text-[12px] text-paper-muted mt-3 leading-[1.6]">
          평일(KST 09:00–19:00) 기준이에요. 주말 문의는 월요일에 답장드려요.
        </p>
      </div>
      <div className="rounded-2xl border border-beige bg-cream p-5">
        <div className="mono text-[10px] uppercase tracking-widest text-paper-muted mb-2">
          위치
        </div>
        <p className="text-[14px] text-charcoal leading-[1.7]">
          서울특별시 성동구
          <br />
          DotShelf, Inc.
        </p>
      </div>
    </div>
  );
}
