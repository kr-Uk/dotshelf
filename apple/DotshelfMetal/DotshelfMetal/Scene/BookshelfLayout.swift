import simd

/// 책장 슬롯 레이아웃 정보. 슬롯 중심 위치 계산과 nearest-slot 탐색에 사용.
struct BookshelfLayout {
    let rows: Int
    let columns: Int
    let shelfWidth: Float
    let shelfHeight: Float
    let boardThickness: Float

    var columnWidth: Float {
        (shelfWidth - Float(columns + 1) * boardThickness) / Float(columns)
    }
    var rowHeight: Float {
        (shelfHeight - Float(rows + 1) * boardThickness) / Float(rows)
    }

    static let `default` = BookshelfLayout(
        rows: 3, columns: 2,
        shelfWidth: 1.8, shelfHeight: 2.0,
        boardThickness: 0.03
    )

    /// 슬롯의 바닥 중심 위치 (책이 놓이는 지점).
    func slotCenter(row: Int, col: Int) -> SIMD3<Float> {
        let x = -shelfWidth * 0.5 + boardThickness + Float(col) * (columnWidth + boardThickness) + columnWidth * 0.5
        let y = Float(row) * (rowHeight + boardThickness) + boardThickness + rowHeight * 0.5
        return SIMD3<Float>(x, y, 0)
    }

    /// 월드 좌표에 가장 가까운 슬롯의 (row, col)과 중심 위치 반환.
    func nearestSlot(to worldPos: SIMD3<Float>) -> (row: Int, col: Int, center: SIMD3<Float>)? {
        var best: (Int, Int, SIMD3<Float>, Float)?
        for r in 0..<rows {
            for c in 0..<columns {
                let center = slotCenter(row: r, col: c)
                let d = distance(SIMD2<Float>(worldPos.x, worldPos.y),
                                 SIMD2<Float>(center.x, center.y))
                if best == nil || d < best!.3 {
                    best = (r, c, center, d)
                }
            }
        }
        guard let b = best else { return nil }
        return (b.0, b.1, b.2)
    }
}
