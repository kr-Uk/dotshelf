const SPINE_COLORS: ReadonlyArray<readonly [string, string]> = [
  ["#7A4A36", "#3A2118"],
  ["#A38069", "#5C4332"],
  ["#3F4A3A", "#1F2A1B"],
  ["#C2A07A", "#7A5A40"],
  ["#5A3A4A", "#2E1A26"],
  ["#8E6E5A", "#4A3526"],
  ["#B89A78", "#6E5238"],
  ["#3F3328", "#1B1410"],
  ["#9E5A3A", "#5A2D1B"],
  ["#6F5C46", "#3B2E22"],
  ["#A88A6A", "#604832"],
  ["#3A2E26", "#1B1410"],
];

function Books({ count = 11, base = 0 }: { count?: number; base?: number }) {
  let cursor = 0;
  const books = Array.from({ length: count }).map((_, i) => {
    const seed = i + base;
    const c = SPINE_COLORS[seed % SPINE_COLORS.length];
    const h = 82 + ((seed * 13) % 17);
    const w = 14 + ((seed * 7) % 11);
    const leanIdx = (seed * 5) % 13;
    const tilt = leanIdx === 3 ? -5 : leanIdx === 9 ? 4 : 0;
    const gapType = (seed * 3) % 11;
    const extraGap =
      gapType === 2 ? 7 : gapType === 5 ? 16 : gapType === 8 ? 24 : 0;
    const left = cursor;
    cursor += w + 1.5 + extraGap;
    return { c, h, w, tilt, left };
  });
  return (
    <>
      {books.map((b, i) => (
        <div
          key={i}
          className="book-spine"
          style={{
            left: `${b.left}px`,
            width: `${b.w}px`,
            height: `${b.h}%`,
            background: `linear-gradient(180deg, ${b.c[0]} 0%, ${b.c[1]} 100%)`,
            transform: `rotate(${b.tilt}deg)`,
            transformOrigin: "bottom left",
          }}
        />
      ))}
    </>
  );
}

function Shelf({
  width = 360,
  height = 110,
  books = 14,
  base = 0,
  topPlank = true,
}: {
  width?: number;
  height?: number;
  books?: number;
  base?: number;
  topPlank?: boolean;
}) {
  return (
    <div className="relative" style={{ width, height }}>
      {topPlank && (
        <div
          className="wood-grain absolute left-0 right-0"
          style={{ top: 0, height: 14, borderRadius: 2 }}
        />
      )}
      <div
        className="absolute"
        style={{
          left: 12,
          right: 12,
          top: topPlank ? 14 : 0,
          bottom: 0,
        }}
      >
        <Books count={books} base={base} />
      </div>
    </div>
  );
}

export function Bookshelf3D() {
  return (
    <div className="scene w-full" style={{ minHeight: 460 }}>
      <div
        className="shelf-stage relative mx-auto rounded-md overflow-hidden"
        style={{
          width: 460,
          maxWidth: "100%",
          background:
            "linear-gradient(108deg, #F2E7D2 0%, #E5D2B6 55%, #C9B393 100%)",
          boxShadow:
            "inset 14px 0 32px -10px rgba(58, 46, 38, 0.3), inset -10px 0 28px -8px rgba(255, 245, 220, 0.28), inset 0 0 60px rgba(58, 46, 38, 0.1)",
        }}
      >
        <div className="flex flex-col p-4">
          <Shelf books={12} base={0} topPlank={false} />
          <Shelf books={11} base={4} />
          <Shelf books={13} base={7} />
          <Shelf books={10} base={11} />
          <div
            className="wood-grain"
            style={{ height: 14, borderRadius: 2 }}
          />
        </div>

        <div
          className="pointer-events-none absolute -top-8 -left-8 w-40 h-40 rounded-full"
          style={{
            background:
              "radial-gradient(circle, rgba(255,240,210,0.7), transparent 70%)",
          }}
        />
      </div>
    </div>
  );
}

export function BookRow() {
  const items = Array.from({ length: 32 });
  return (
    <div className="overflow-hidden" style={{ height: 130 }}>
      <div
        className="drift flex items-end gap-1"
        style={{ width: "max-content" }}
      >
        {items.concat(items).map((_, i) => {
          const c = SPINE_COLORS[i % SPINE_COLORS.length];
          const h = 80 + ((i * 11) % 40);
          const w = 16 + ((i * 5) % 10);
          return (
            <div
              key={i}
              className="book-spine relative"
              style={{
                position: "relative",
                width: w,
                height: h,
                background: `linear-gradient(180deg, ${c[0]} 0%, ${c[1]} 100%)`,
              }}
            />
          );
        })}
      </div>
      <div
        className="wood-grain"
        style={{ height: 10, marginTop: -2, borderRadius: 2 }}
      />
    </div>
  );
}
