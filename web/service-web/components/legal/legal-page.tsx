import { ReactNode } from "react";

type LegalPageProps = {
  title: string;
  subtitle?: string;
  effectiveDate: string;
  status?: string;
  children: ReactNode;
};

export function LegalPage({
  title,
  subtitle,
  effectiveDate,
  status,
  children,
}: LegalPageProps) {
  return (
    <article className="max-w-3xl mx-auto px-5 sm:px-8 py-16 sm:py-24">
      <header className="mb-12 pb-8 border-b border-beige/60">
        <h1 className="text-3xl sm:text-4xl font-serif text-paper">{title}</h1>
        {subtitle && (
          <p className="mt-3 text-sm text-paper-muted">{subtitle}</p>
        )}
        <div className="mt-6 flex flex-wrap items-center gap-x-4 gap-y-1 mono text-[11px] text-paper-muted">
          <span>시행일: {effectiveDate}</span>
          {status && (
            <>
              <span>·</span>
              <span>상태: {status}</span>
            </>
          )}
        </div>
      </header>
      <div className="space-y-10 text-paper leading-relaxed">{children}</div>
    </article>
  );
}

type LegalSectionProps = {
  title: string;
  children: ReactNode;
};

export function LegalSection({ title, children }: LegalSectionProps) {
  return (
    <section className="space-y-3">
      <h2 className="text-lg sm:text-xl font-semibold text-paper">{title}</h2>
      <div className="space-y-3 text-[15px] text-paper">{children}</div>
    </section>
  );
}

type LegalSubsectionProps = {
  title: string;
  children: ReactNode;
};

export function LegalSubsection({ title, children }: LegalSubsectionProps) {
  return (
    <div className="space-y-2 pl-1">
      <h3 className="text-base font-semibold text-paper/90">{title}</h3>
      <div className="space-y-2">{children}</div>
    </div>
  );
}

export function LegalOrderedList({ items }: { items: ReactNode[] }) {
  return (
    <ol className="list-decimal list-outside ml-5 space-y-2 text-[15px]">
      {items.map((item, idx) => (
        <li key={idx}>{item}</li>
      ))}
    </ol>
  );
}

export function LegalUnorderedList({ items }: { items: ReactNode[] }) {
  return (
    <ul className="list-disc list-outside ml-5 space-y-2 text-[15px]">
      {items.map((item, idx) => (
        <li key={idx}>{item}</li>
      ))}
    </ul>
  );
}
