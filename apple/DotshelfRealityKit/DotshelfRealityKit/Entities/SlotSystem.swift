import RealityKit
// PlatformColor는 PlatformTypes.swift에서 정의 (PlatformColor / UIColor)
import Observation

enum SectionAlignment {
    case left
    case center
    case right
}

struct ShelfSection {
    let row: Int
    let column: Int
    let leftBound: Float
    let rightBound: Float
    let shelfTopY: Float
    let sectionHeight: Float
    var books: [Entity] = []
    var alignment: SectionAlignment = .left

    var usableWidth: Float { rightBound - leftBound }

    private static func orientation(of book: Entity) -> PlacementOrientation {
        book.components[BookComponent.self]?.orientation ?? .spine
    }

    /// i번째 책의 좌/우 half-footprint (이웃 컨텍스트 반영)
    private func halves(at i: Int) -> (left: Float, right: Float) {
        let book = books[i]
        let comp = book.components[BookComponent.self]
        let orient = comp?.orientation ?? .spine
        let t = comp?.thickness ?? 0.03
        let w = comp?.width ?? 0.16

        let leftNeighbor: PlacementOrientation? = i > 0 ? Self.orientation(of: books[i - 1]) : nil
        let rightNeighbor: PlacementOrientation? = i + 1 < books.count ? Self.orientation(of: books[i + 1]) : nil

        return (
            left: orient.halfFootprint(thickness: t, width: w, neighbor: leftNeighbor),
            right: orient.halfFootprint(thickness: t, width: w, neighbor: rightNeighbor)
        )
    }

    /// 정렬 기준으로 모든 책의 전체 x 차지 길이 + 갭 포함
    private func totalSpan(gapIndex: Int?, gapWidth: Float) -> Float {
        var span: Float = 0
        for i in 0..<books.count {
            if let gi = gapIndex, i == gi { span += gapWidth }
            let (left, right) = halves(at: i)
            span += left + right
        }
        return span
    }

    /// 정렬에 따라 책 묶음이 시작할 x 좌표
    private func startX(forSpan span: Float) -> Float {
        let margin: Float = 0.005
        let innerUsable = usableWidth - margin * 2
        switch alignment {
        case .left:   return leftBound + margin
        case .right:  return rightBound - margin - span
        case .center: return leftBound + margin + (innerUsable - span) / 2
        }
    }

    func computePositions(gapIndex: Int? = nil, gapWidth: Float = 0) -> [SIMD3<Float>] {
        let span = totalSpan(gapIndex: gapIndex, gapWidth: gapWidth)
        var x = startX(forSpan: span)
        var positions: [SIMD3<Float>] = []
        for i in 0..<books.count {
            if let gi = gapIndex, i == gi {
                x += gapWidth
            }
            let (left, right) = halves(at: i)
            let height = books[i].components[BookComponent.self]?.height ?? 0.24
            x += left
            let y = shelfTopY + height / 2
            positions.append(SIMD3(x, y, 0))
            x += right
        }
        return positions
    }

    func findInsertionIndex(worldX: Float) -> Int {
        let positions = computePositions()
        for (i, pos) in positions.enumerated() {
            if worldX < pos.x { return i }
        }
        return books.count
    }

    func totalBooksFootprint() -> Float {
        var total: Float = 0
        for i in 0..<books.count {
            let (left, right) = halves(at: i)
            total += left + right
        }
        return total
    }

    func canFit(extraFootprint: Float) -> Bool {
        totalBooksFootprint() + extraFootprint + 0.01 < usableWidth
    }
}

/// 책장 공개 범위 — 명세 §2.
enum ShelfVisibility: CaseIterable {
    case `public`
    case followers
    case `private`

    var label: String {
        switch self {
        case .public: return "전체 공개"
        case .followers: return "팔로워 공개"
        case .private: return "비공개"
        }
    }

    var systemImage: String {
        switch self {
        case .public: return "globe"
        case .followers: return "person.2"
        case .private: return "lock"
        }
    }
}

/// 한 권의 책장 단위 (엔티티 + 섹션 상태)
struct Bookshelf {
    let entity: Entity
    var sections: [ShelfSection]
    var name: String = "책장"
    var visibility: ShelfVisibility = .public
}

enum SwitchDirection {
    case left   // 이전 책장으로 (현재가 오른쪽으로 나감)
    case right  // 다음 책장으로 (현재가 왼쪽으로 나감)
}

@Observable
class BookshelfState {
    // 활성 책장의 상태 미러 (기존 코드 호환)
    var sections: [ShelfSection] = []
    var shelfRoot: Entity?

    // 멀티 책장 관리
    var bookshelves: [Bookshelf] = []
    var currentIndex: Int = 0

    // 회전축(Turntable): 책장들이 이 주위에 원형 배치되어 벽 뒤에서 회전하며 등장
    var turntable: Entity?
    var turntableArmLength: Float = 0.127  // 피벗 ↔ 책장 중심 거리
    private var turntableAngle: Float = 0  // 누적 회전각 (라디안)

    // 2개의 물리적 슬롯 (앞/뒤). 책장 개수와 무관하게 항상 2개.
    // 슬롯에 파킹된 책장은 turntable이 회전하면서 앞↔뒤 위치가 바뀐다.
    var slots: [Entity] = []
    private var frontSlotIdx: Int = 0  // 0 또는 1 — 현재 앞면 슬롯

    // 드래그 중 책을 책장 밖에서도 들고 있을 수 있게 하는 월드 레이어
    // (turntable과 분리되어 있어 책장이 회전해도 책은 그대로 유지)
    var dragLayer: Entity?

    // 회전 중 연타 방지 — 진행 중이면 다음 클릭은 큐에 저장
    private var isFlipping: Bool = false
    private var queuedDirections: [SwitchDirection] = []

    // MARK: - 빌드/팝퓰레이트 (static — 각 책장마다 별도로 호출)

    static func makeDefaultSections(
        width: Float = 1.60,
        height: Float = 1.60,
        depth: Float = 0.25,
        rows: Int = 4,
        columns: Int = 4
    ) -> [ShelfSection] {
        let panelThickness: Float = 0.018
        let boardThickness: Float = 0.018
        let innerWidth = width - panelThickness * 2
        let sectionHeight = (height - boardThickness * Float(rows + 1)) / Float(rows)
        let colWidth = innerWidth / Float(columns)

        var result: [ShelfSection] = []
        for row in 0..<rows {
            let shelfTopY = boardThickness + Float(row) * (sectionHeight + boardThickness)
            for col in 0..<columns {
                let leftBound = -width / 2 + panelThickness + Float(col) * colWidth
                let rightBound = leftBound + colWidth
                result.append(ShelfSection(
                    row: row,
                    column: col,
                    leftBound: leftBound,
                    rightBound: rightBound,
                    shelfTopY: shelfTopY,
                    sectionHeight: sectionHeight
                ))
            }
        }
        return result
    }

    // MARK: - 다중 책장 관리

    func addBookshelf(entity: Entity, sections newSections: [ShelfSection], name: String = "책장") {
        let shelfName = name == "책장" ? "책장 \(bookshelves.count + 1)" : name
        bookshelves.append(Bookshelf(entity: entity, sections: newSections, name: shelfName))
        if bookshelves.count == 1 {
            self.sections = newSections
            self.shelfRoot = entity
            self.currentIndex = 0
        }
    }

    // MARK: - 페이즈 C — 책장 편집

    /// 현재 책장 이름.
    var currentBookshelfName: String {
        guard currentIndex < bookshelves.count else { return "책장" }
        return bookshelves[currentIndex].name
    }

    /// 현재 책장 공개 범위.
    var currentBookshelfVisibility: ShelfVisibility {
        guard currentIndex < bookshelves.count else { return .public }
        return bookshelves[currentIndex].visibility
    }

    func renameCurrentBookshelf(to name: String) {
        guard currentIndex < bookshelves.count else { return }
        let trimmed = name.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return }
        bookshelves[currentIndex].name = String(trimmed.prefix(30))
    }

    func setCurrentBookshelfVisibility(_ v: ShelfVisibility) {
        guard currentIndex < bookshelves.count else { return }
        bookshelves[currentIndex].visibility = v
    }

    /// 빈 책장 생성 + 턴테이블에 장착하고 자동으로 해당 책장으로 전환.
    /// 호출 측: UI 편집 메뉴의 "책장 추가".
    /// - Returns: 새 책장의 index
    @discardableResult
    func addEmptyBookshelfAndSwitch() -> Int? {
        guard let turntable else { return nil }
        _ = turntable  // 사용 경고 방지

        let shelfEntity = BookshelfEntity.create()
        var sec = BookshelfState.makeDefaultSections()
        BookshelfState.populateSectionClickAreas(
            in: sec, parent: shelfEntity, bookshelfIndex: bookshelves.count
        )

        // 저장 — 이 시점에 bookshelves.count가 이전 값이므로 이름 "책장 N+1"
        let newIndex = bookshelves.count
        addBookshelf(entity: shelfEntity, sections: sec)

        // 현재가 유일한 책장이면 슬롯0이 비어있을 수도 — 안전하게 처리
        if bookshelves.count == 1, let slot0 = slots.first {
            slot0.addChild(shelfEntity)
            return 0
        }

        // 전환 애니메이션으로 새 책장으로 이동 (최단 방향)
        let count = bookshelves.count
        let forward = (newIndex - currentIndex + count) % count
        let backward = (currentIndex - newIndex + count) % count
        let direction: SwitchDirection = forward <= backward ? .right : .left
        let steps = min(forward, backward)
        for _ in 0..<steps {
            switchTo(direction: direction)
        }
        return newIndex
    }

    /// 현재 책장을 삭제. 2개 이상일 때만 가능. 책/오브젝트는 다른 책장의 빈 공간으로 자동 이전,
    /// 공간이 부족하면 archive 클로저로 넘김(보관함). 삭제 완료 후 인접 책장으로 이동.
    /// - Parameter archiveFallback: 메타데이터를 받아 보관 처리(또는 포기). 어떤 책이든 호출됨.
    /// - Returns: 삭제 성공 여부
    @discardableResult
    func removeCurrentBookshelf(archiveFallback: (Entity) -> Void) -> Bool {
        guard bookshelves.count > 1 else { return false }

        let removeIdx = currentIndex
        let removedShelf = bookshelves[removeIdx]

        // 삭제될 책장의 섹션별 책들을 모두 수집
        var bookQueue: [Entity] = []
        for sec in removedShelf.sections {
            bookQueue.append(contentsOf: sec.books)
        }

        // 다른 책장들의 섹션에 빈 공간 있으면 이전, 없으면 archiveFallback
        for book in bookQueue {
            guard let comp = book.components[BookComponent.self] else {
                archiveFallback(book); continue
            }
            let footprint = comp.footprint

            var placed = false
            for bi in 0..<bookshelves.count where bi != removeIdx {
                if let targetSecIdx = bookshelves[bi].sections.firstIndex(where: { $0.canFit(extraFootprint: footprint) }) {
                    // 재부모화 + 데이터 이전
                    book.setParent(bookshelves[bi].entity, preservingWorldTransform: false)
                    bookshelves[bi].sections[targetSecIdx].books.append(book)
                    placed = true
                    break
                }
            }
            if !placed {
                archiveFallback(book)
            }
        }

        // 책장 엔티티 제거
        removedShelf.entity.removeFromParent()
        bookshelves.remove(at: removeIdx)

        // 인덱스/포인터 조정: 우선 인접(removeIdx) 쪽으로 current 이동
        let newCount = bookshelves.count
        let newCurrent = min(removeIdx, newCount - 1)
        currentIndex = newCurrent
        sections = bookshelves[newCurrent].sections
        shelfRoot = bookshelves[newCurrent].entity

        remountSlotsToCurrent()
        return true
    }

    /// 현재 책장의 순서를 `to` 인덱스로 이동. 현 책장의 sections를 먼저 저장하고
    /// currentIndex를 재계산한 뒤 슬롯 재장착까지 자동.
    func moveBookshelf(from: Int, to: Int) {
        guard from >= 0, from < bookshelves.count,
              to >= 0, to < bookshelves.count,
              from != to else { return }

        // 1. 현재 활성 섹션을 bookshelves[currentIndex]에 반영
        if currentIndex < bookshelves.count {
            bookshelves[currentIndex].sections = sections
        }

        // 2. 배열 재배치
        let moved = bookshelves.remove(at: from)
        bookshelves.insert(moved, at: to)

        // 3. currentIndex 재계산
        if currentIndex == from {
            currentIndex = to
        } else if from < currentIndex && to >= currentIndex {
            currentIndex -= 1
        } else if from > currentIndex && to <= currentIndex {
            currentIndex += 1
        }

        // 4. 상태 동기화 + 슬롯 재장착
        sections = bookshelves[currentIndex].sections
        shelfRoot = bookshelves[currentIndex].entity
        remountSlotsToCurrent()
    }

    /// 현재 `currentIndex` 기준으로 슬롯을 재세팅. 턴테이블 각도도 0으로 복귀.
    /// remove/move 후 공통으로 호출.
    private func remountSlotsToCurrent() {
        guard !bookshelves.isEmpty, let frontSlot = slots.first else { return }
        frontSlot.children.forEach { $0.removeFromParent() }
        frontSlot.addChild(bookshelves[currentIndex].entity)
        frontSlotIdx = 0
        turntableAngle = 0
        if let turntable {
            var t = turntable.transform
            t.rotation = simd_quatf(angle: 0, axis: SIMD3<Float>(0, 1, 0))
            turntable.move(to: t, relativeTo: turntable.parent, duration: 0.3)
        }
        if bookshelves.count >= 2, slots.count == 2 {
            slots[1].children.forEach { $0.removeFromParent() }
            let nextIdx = (currentIndex + 1) % bookshelves.count
            slots[1].addChild(bookshelves[nextIdx].entity)
        }
    }

    /// 턴테이블 설치 후 첫 2개 책장을 슬롯에 장착
    func installInitialShelves() {
        guard slots.count == 2, !bookshelves.isEmpty else { return }
        slots[0].addChild(bookshelves[0].entity)
        if bookshelves.count >= 2 {
            slots[1].addChild(bookshelves[1].entity)
        }
    }

    /// 턴테이블 180° 회전으로 전환. 연속 호출 시 순차 실행(FIFO 큐).
    func switchTo(direction: SwitchDirection) {
        guard bookshelves.count > 1, let turntable, slots.count == 2 else { return }

        if isFlipping {
            queuedDirections.append(direction)
            return
        }

        isFlipping = true
        performFlip(direction: direction, turntable: turntable)
    }

    private func performFlip(direction: SwitchDirection, turntable: Entity) {
        // 현재 활성 섹션을 저장
        bookshelves[currentIndex].sections = sections

        let count = bookshelves.count
        let targetIdx = (currentIndex + (direction == .right ? 1 : -1) + count) % count

        // 숨겨진 슬롯에 타겟 책장 세팅 (회전 벽 패널 뒤, 보이지 않음)
        let hiddenSlotIdx = 1 - frontSlotIdx
        let hiddenSlot = slots[hiddenSlotIdx]
        let targetEntity = bookshelves[targetIdx].entity
        if targetEntity.parent !== hiddenSlot {
            hiddenSlot.children.forEach { $0.removeFromParent() }
            hiddenSlot.addChild(targetEntity)
        }

        // 턴테이블 180° 회전 (항상 고정, 큐잉으로 연타 시 방향 보존)
        let delta: Float = direction == .right ? -.pi : +.pi
        turntableAngle += delta
        var target = turntable.transform
        target.rotation = simd_quatf(angle: turntableAngle, axis: SIMD3<Float>(0, 1, 0))
        let duration: TimeInterval = 1.0
        turntable.move(to: target, relativeTo: turntable.parent, duration: duration)

        // 상태 업데이트
        frontSlotIdx = hiddenSlotIdx
        currentIndex = targetIdx
        sections = bookshelves[targetIdx].sections
        shelfRoot = targetEntity

        // 애니메이션 완료 후 큐잉된 다음 플립 실행 (FIFO)
        Task { @MainActor in
            try? await Task.sleep(for: .seconds(duration))
            self.isFlipping = false
            if !self.queuedDirections.isEmpty {
                let next = self.queuedDirections.removeFirst()
                self.switchTo(direction: next)
            }
        }
    }

    func sectionIndex(row: Int, column: Int) -> Int? {
        sections.firstIndex { $0.row == row && $0.column == column }
    }

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

    /// 섹션 배열에 책을 채움 (colors 팔레트에서 순환)
    @MainActor
    static func populateBooks(
        into sections: inout [ShelfSection],
        parent: Entity,
        includeScanned: Bool,
        startIndex: Int = 0,
        booksPerSection: Int = 3
    ) {
        let colors: [PlatformColor] = [
            PlatformColor(red: 0.75, green: 0.22, blue: 0.17, alpha: 1),
            PlatformColor(red: 0.18, green: 0.35, blue: 0.65, alpha: 1),
            PlatformColor(red: 0.22, green: 0.55, blue: 0.30, alpha: 1),
            PlatformColor(red: 0.55, green: 0.35, blue: 0.18, alpha: 1),
            PlatformColor(red: 0.30, green: 0.25, blue: 0.55, alpha: 1),
            PlatformColor(red: 0.90, green: 0.88, blue: 0.82, alpha: 1),
            PlatformColor(red: 0.15, green: 0.15, blue: 0.18, alpha: 1),
            PlatformColor(red: 0.85, green: 0.68, blue: 0.20, alpha: 1),
            PlatformColor(red: 0.80, green: 0.40, blue: 0.15, alpha: 1),
            PlatformColor(red: 0.60, green: 0.20, blue: 0.45, alpha: 1),
            PlatformColor(red: 0.35, green: 0.55, blue: 0.65, alpha: 1),
            PlatformColor(red: 0.70, green: 0.30, blue: 0.30, alpha: 1),
        ]

        var bookIndex = startIndex
        var scannedBookInserted = !includeScanned

        for secIdx in 0..<sections.count {
            let sec = sections[secIdx]
            var x = sec.leftBound + 0.005

            if secIdx == 0, !scannedBookInserted, let scanned = BookEntity.loadScannedBook(targetHeight: 0.24) {
                let comp = scanned.components[BookComponent.self]
                let thickness = comp?.thickness ?? 0.04
                let height = comp?.height ?? 0.24

                scanned.name = "book_\(bookIndex)_scanned"
                let posY = sec.shelfTopY + height / 2
                scanned.position = SIMD3(x + thickness / 2, posY, 0)
                parent.addChild(scanned)

                sections[secIdx].books.append(scanned)
                x += thickness
                bookIndex += 1
                scannedBookInserted = true
            }

            let existing = sections[secIdx].books.count
            let toAdd = max(0, booksPerSection - existing)
            for _ in 0..<toAdd {
                let thickness = Float.random(in: 0.025...0.045)
                let height = Float.random(in: 0.20...0.27)
                let color = colors[bookIndex % colors.count]

                let name = "book_\(bookIndex)"
                let record = BookRecord.sample(for: name)
                let book = BookEntity.createWithFaces(
                    thickness: thickness,
                    height: height,
                    width: 0.16,
                    color: color,
                    record: record
                )
                book.name = name

                let posY = sec.shelfTopY + height / 2
                book.position = SIMD3(x + thickness / 2, posY, 0)
                parent.addChild(book)

                sections[secIdx].books.append(book)
                x += thickness
                bookIndex += 1
            }
        }
    }

    func setAlignment(_ alignment: SectionAlignment, for secIdx: Int) {
        guard secIdx < sections.count else { return }
        sections[secIdx].alignment = alignment
        layoutSection(secIdx, duration: 0.35)
    }

    /// 각 섹션의 빈 영역에 대한 탭 감지용 투명 엔티티 생성
    /// - Parameter bookshelfIndex: 같은 index 라인에 여러 책장이 있을 때 이름 충돌 방지용
    static func populateSectionClickAreas(in sections: [ShelfSection], parent: Entity, bookshelfIndex: Int = 0) {
        for (i, sec) in sections.enumerated() {
            let w = sec.rightBound - sec.leftBound
            let h = sec.sectionHeight
            let clickArea = Entity()
            clickArea.name = "section_\(bookshelfIndex)_\(i)"
            clickArea.position = SIMD3(
                (sec.leftBound + sec.rightBound) / 2,
                sec.shelfTopY + sec.sectionHeight / 2,
                -0.1
            )
            clickArea.components.set(CollisionComponent(shapes: [.generateBox(width: w, height: h, depth: 0.005)]))
            clickArea.components.set(InputTargetComponent())
            parent.addChild(clickArea)
        }
    }

    func layoutSection(_ secIdx: Int, gapIndex: Int? = nil, gapWidth: Float = 0, duration: TimeInterval = 0.2) {
        let positions = sections[secIdx].computePositions(gapIndex: gapIndex, gapWidth: gapWidth)
        for (i, pos) in positions.enumerated() {
            let book = sections[secIdx].books[i]
            let orientation = book.components[BookComponent.self]?.orientation ?? .spine
            var transform = book.transform
            transform.translation = pos
            transform.rotation = orientation.rotation
            book.move(to: transform, relativeTo: book.parent, duration: duration)
        }
    }

    /// 책의 방향을 바꾸고, 필요 시 다른 책들을 가장 가까운 빈 섹션으로 밀어냄
    func changeOrientation(of bookEntity: Entity, to newOrientation: PlacementOrientation) {
        guard var comp = bookEntity.components[BookComponent.self] else { return }
        comp.orientation = newOrientation
        bookEntity.components.set(comp)

        // 해당 책의 섹션 찾기
        guard let secIdx = sections.firstIndex(where: { $0.books.contains(where: { $0 === bookEntity }) }) else {
            return
        }

        // 오버플로우 시 끝 쪽부터 (대상 책 제외) 다른 섹션으로 이동
        while sections[secIdx].totalBooksFootprint() + 0.01 > sections[secIdx].usableWidth {
            guard let removeIdx = sections[secIdx].books.lastIndex(where: { $0 !== bookEntity }) else { break }
            let bumped = sections[secIdx].books.remove(at: removeIdx)

            if let targetIdx = findNearestSectionWithSpace(for: bumped, excluding: secIdx) {
                sections[targetIdx].books.append(bumped)
                layoutSection(targetIdx, duration: 0.35)
            } else {
                // 공간이 있는 섹션이 없으면 원래 섹션에 되돌려두고 탈출
                sections[secIdx].books.insert(bumped, at: removeIdx)
                break
            }
        }

        layoutSection(secIdx, duration: 0.35)
    }

    private func findNearestSectionWithSpace(for book: Entity, excluding excludeIdx: Int) -> Int? {
        guard let comp = book.components[BookComponent.self] else { return nil }
        let footprint = comp.footprint
        let sourcePos = book.position

        var candidates: [(Int, Float)] = []
        for (i, sec) in sections.enumerated() where i != excludeIdx {
            if sec.canFit(extraFootprint: footprint) {
                let centerX = (sec.leftBound + sec.rightBound) / 2
                let centerY = sec.shelfTopY + sec.sectionHeight / 2
                let dist = abs(sourcePos.x - centerX) + abs(sourcePos.y - centerY)
                candidates.append((i, dist))
            }
        }
        return candidates.min(by: { $0.1 < $1.1 })?.0
    }
}
