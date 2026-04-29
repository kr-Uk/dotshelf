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

function Books({ count = 14, base = 0 }: { count?: number; base?: number }) {
  return (
    <>
      {Array.from({ length: count }).map((_, i) => {
        const c = SPINE_COLORS[(i + base) % SPINE_COLORS.length];
        const h = 70 + ((i * 13) % 28);
        const w = 14 + ((i * 7) % 10);
        const tilt = i === count - 2 ? -6 : 0;
        const left = i * (w + 1.5);
        return (
          <div
            key={i}
            className="book-spine"
            style={{
              left: `${left}px`,
              width: `${w}px`,
              height: `${h}%`,
              background: `linear-gradient(180deg, ${c[0]} 0%, ${c[1]} 100%)`,
              transform: `rotate(${tilt}deg)`,
              transformOrigin: "bottom left",
            }}
          />
        );
      })}
    </>
  );
}

function Shelf({
  width = 360,
  height = 110,
  books = 14,
  base = 0,
}: {
  width?: number;
  height?: number;
  books?: number;
  base?: number;
}) {
  return (
    <div className="relative" style={{ width, height }}>
      <div
        className="wood-grain absolute left-0 right-0"
        style={{ bottom: 0, height: 14, borderRadius: 2 }}
      />
      <div
        className="absolute"
        style={{ left: 12, right: 12, bottom: 14, top: 4 }}
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
        className="shelf-stage relative mx-auto"
        style={{ width: 460, maxWidth: "100%" }}
      >
        <div
          className="absolute inset-0 rounded-md"
          style={{
            background: "linear-gradient(180deg, #EFE3CF 0%, #DCC8A8 100%)",
            transform: "translateZ(-40px)",
            boxShadow: "inset 0 0 60px rgba(58,46,38,0.18)",
          }}
        />
        <div
          className="wood-grain absolute"
          style={{
            left: -10,
            top: -10,
            bottom: -10,
            width: 16,
            borderRadius: 4,
          }}
        />
        <div
          className="wood-grain absolute"
          style={{
            right: -10,
            top: -10,
            bottom: -10,
            width: 16,
            borderRadius: 4,
          }}
        />
        <div
          className="wood-grain absolute"
          style={{
            left: -14,
            right: -14,
            top: -16,
            height: 18,
            borderRadius: 4,
          }}
        />

        <div className="flex flex-col gap-6 p-4">
          <Shelf books={13} base={0} />
          <Shelf books={15} base={3} />
          <Shelf books={12} base={6} />
          <Shelf books={14} base={9} />
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
