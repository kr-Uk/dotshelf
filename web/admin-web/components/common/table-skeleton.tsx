import { TableCell, TableRow } from "@/components/ui/table";
import { Skeleton } from "@/components/ui/skeleton";

/** 테이블 본문에 N행 × M열의 회색 블록 placeholder를 그린다. */
export function TableSkeletonRows({
  rows = 5,
  cols = 4,
}: {
  rows?: number;
  cols?: number;
}) {
  return (
    <>
      {Array.from({ length: rows }).map((_, i) => (
        <TableRow key={i}>
          {Array.from({ length: cols }).map((_, j) => (
            <TableCell key={j}>
              <Skeleton
                className="h-4"
                style={{ width: `${60 + ((i * 13 + j * 17) % 30)}%` }}
              />
            </TableCell>
          ))}
        </TableRow>
      ))}
    </>
  );
}

/** 카드 헤더 + 본문 1줄짜리 placeholder. */
export function CardSkeleton({ lines = 3 }: { lines?: number }) {
  return (
    <div className="space-y-2">
      {Array.from({ length: lines }).map((_, i) => (
        <Skeleton
          key={i}
          className="h-4"
          style={{ width: `${50 + ((i * 23) % 40)}%` }}
        />
      ))}
    </div>
  );
}

export { Skeleton };
