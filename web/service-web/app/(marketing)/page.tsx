export default function HomePage() {
  return (
    <section className="flex flex-1 flex-col items-center justify-center px-6 py-24 text-center">
      <span className="inline-flex items-center gap-2 rounded-full border border-border bg-muted/40 px-3 py-1 text-xs text-muted-foreground">
        <span className="size-1.5 rounded-full bg-emerald-500" />
        coming soon
      </span>
      <h1 className="mt-6 text-4xl font-semibold tracking-tight md:text-5xl">
        Dotshelf
      </h1>
      <p className="mt-4 max-w-xl text-base text-muted-foreground md:text-lg">
        좋아하는 책을 가상의 책장에 모으고, 친구의 책장도 들여다보세요.
      </p>
    </section>
  );
}
