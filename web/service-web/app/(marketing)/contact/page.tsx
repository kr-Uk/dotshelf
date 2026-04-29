export default function ContactPage() {
  return (
    <section className="mx-auto flex w-full max-w-3xl flex-1 flex-col items-center justify-center px-6 py-24 text-center">
      <h1 className="text-3xl font-semibold tracking-tight md:text-4xl">
        문의하기
      </h1>
      <p className="mt-4 text-sm text-muted-foreground md:text-base">
        문의 양식은 준비 중입니다. 우선{" "}
        <a
          href="mailto:help@dotshelf.kr"
          className="font-medium text-foreground underline underline-offset-4 hover:no-underline"
        >
          help@dotshelf.kr
        </a>
        로 연락해주세요.
      </p>
    </section>
  );
}
