import simd

struct ShelfSection {
    let row: Int
    let column: Int
    let leftBound: Float
    let rightBound: Float
    let shelfTopY: Float
    let sectionHeight: Float
    var books: [SceneNode] = []

    var usableWidth: Float { rightBound - leftBound }

    /// 섹션에 들어있는 책들의 타겟 위치를 계산. 선택적으로 gapIndex 위치에 공간을 벌림.
    func computePositions(gapIndex: Int? = nil, gapWidth: Float = 0) -> [SIMD3<Float>] {
        var positions: [SIMD3<Float>] = []
        var x = leftBound + 0.005
        for (i, book) in books.enumerated() {
            if let gi = gapIndex, i == gi {
                x += gapWidth
            }
            let meta = book.bookMetadata
            let thickness = meta?.thickness ?? 0.03
            let height = meta?.height ?? 0.24
            x += thickness / 2
            let y = shelfTopY + height / 2    // ← 중력: 책 바닥이 선반 상단에 닿음
            positions.append(SIMD3<Float>(x, y, 0))
            x += thickness / 2
        }
        return positions
    }

    /// 주어진 월드 X에 대해 "이 위치에 삽입하면 몇 번째 인덱스일지" 찾기.
    func findInsertionIndex(worldX: Float) -> Int {
        var x = leftBound + 0.005
        for (i, book) in books.enumerated() {
            let thickness = book.bookMetadata?.thickness ?? 0.03
            let center = x + thickness / 2
            if worldX < center { return i }
            x += thickness
        }
        return books.count
    }

    /// 갭(insertIdx 위치)의 중심 X 좌표 (고스트 배치에 사용).
    func gapCenterX(insertIdx: Int, newBookThickness: Float) -> Float {
        var x = leftBound + 0.005
        for i in 0..<insertIdx {
            if i < books.count {
                x += books[i].bookMetadata?.thickness ?? 0.03
            }
        }
        return x + newBookThickness / 2
    }

    func totalBooksWidth() -> Float {
        books.reduce(0) { $0 + ($1.bookMetadata?.thickness ?? 0.03) }
    }

    func canFit(extraThickness: Float) -> Bool {
        totalBooksWidth() + extraThickness + 0.01 < usableWidth
    }
}

final class BookshelfState {
    var sections: [ShelfSection] = []
    weak var root: SceneNode?

    func findNearestSection(worldPos: SIMD3<Float>) -> Int? {
        var bestIdx: Int?
        var bestDist: Float = .infinity
        for (i, sec) in sections.enumerated() {
            let centerX = (sec.leftBound + sec.rightBound) / 2
            let centerY = sec.shelfTopY + sec.sectionHeight / 2
            let dist = abs(worldPos.x - centerX) + abs(worldPos.y - centerY)
            if dist < bestDist {
                bestDist = dist
                bestIdx = i
            }
        }
        return bestIdx
    }

    /// 해당 책이 속한 섹션 인덱스와 섹션 내 책 인덱스 반환.
    func find(book: SceneNode) -> (sectionIdx: Int, bookIdx: Int)? {
        for (si, sec) in sections.enumerated() {
            if let bi = sec.books.firstIndex(where: { $0 === book }) {
                return (si, bi)
            }
        }
        return nil
    }

    /// 섹션 내 모든 책을 올바른 위치로 이동 (gapIndex 있으면 갭을 벌림).
    func layoutSection(_ secIdx: Int, gapIndex: Int? = nil, gapWidth: Float = 0) {
        let positions = sections[secIdx].computePositions(gapIndex: gapIndex, gapWidth: gapWidth)
        let rot = float4x4.rotation(angle: Float.pi / 2, axis: SIMD3<Float>(0, 1, 0))
        for (i, pos) in positions.enumerated() {
            let book = sections[secIdx].books[i]
            book.localTransform = float4x4.translation(pos) * rot
        }
    }

    /// 모든 섹션을 즉시 레이아웃 (초기 배치 또는 리셋).
    func layoutAll() {
        for i in 0..<sections.count {
            layoutSection(i)
        }
    }
}
