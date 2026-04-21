import RealityKit
import Observation
import simd

@Observable
class BookInteractionState {
    // 드래그 상태
    var draggedBook: Entity?
    var dragStartWorldPos: SIMD3<Float>?
    var originalSectionIdx: Int?
    var originalBookIndex: Int?
    var ghostPreview: Entity?
    var hoveredSectionIdx: Int?
    var hoveredInsertionIdx: Int?
    var isDragging: Bool = false

    // 책장 측면 경계 넘기기 (dwell 트리거)
    private var dragTranslationOffset: SIMD3<Float>?
    private var lastTranslation: SIMD3<Float>?
    private var edgePastDirection: SwitchDirection?
    private var edgeFlipTask: Task<Void, Never>?

    // 책 뷰잉 상태 (탭)
    var isViewingBook: Bool = false
    var viewedBook: Entity?
    var viewedBookOriginalTransform: Transform?
    var viewingLocalTransform: Transform?
    var viewBaseRotation: simd_quatf = simd_quatf(angle: 0, axis: SIMD3(0, 1, 0))
    var viewCurrentRotation: simd_quatf = simd_quatf(angle: 0, axis: SIMD3(0, 1, 0))

    // MARK: - 책 뷰잉 (탭)

    func viewBook(entity: Entity, camera: PerspectiveCamera?) {
        guard entity.name.hasPrefix("book_"), !isViewingBook, !isDragging else { return }
        guard let camera else { return }

        viewedBook = entity
        viewedBookOriginalTransform = entity.transform
        isViewingBook = true
        // 책 포커싱 중에는 그림자 캐스트 비활성 — 카메라 앞에 떠있는 책이 바닥/주변에
        // 어색한 그림자를 남기지 않도록. 해제 시 복원.
        Self.setCastsShadow(entity, false)
        // 책 우측이 표지라는 컨벤션과 일치: -π/2 회전으로 표지(앞면)가 카메라를 향함
        viewBaseRotation = simd_quatf(angle: -.pi / 2, axis: SIMD3(0, 1, 0))
        viewCurrentRotation = viewBaseRotation

        let bookComp = entity.components[BookComponent.self]
        let bookHeight = bookComp?.height ?? 0.24
        let bookWidth = bookComp?.width ?? 0.16

        let fovRad = camera.camera.fieldOfViewInDegrees * Float.pi / 180.0
        let maxDim = max(bookHeight, bookWidth)
        let distance = maxDim / (0.8 * 2.0 * tan(fovRad / 2.0))

        let camPos = camera.position(relativeTo: nil)
        let camForwardCol = camera.transform.matrix.columns.2
        let forward = -SIMD3<Float>(camForwardCol.x, camForwardCol.y, camForwardCol.z)
        let targetWorldPos = camPos + forward * distance

        let targetWorldTransform = Transform(
            scale: .one,
            rotation: viewCurrentRotation,
            translation: targetWorldPos
        )

        if let parent = entity.parent {
            let parentWorld = parent.transformMatrix(relativeTo: nil)
            let parentInv = parentWorld.inverse
            let targetLocal = Transform(matrix: parentInv * targetWorldTransform.matrix)
            viewingLocalTransform = targetLocal
            entity.move(to: targetLocal, relativeTo: entity.parent, duration: 0.5)
        } else {
            viewingLocalTransform = targetWorldTransform
            entity.move(to: targetWorldTransform, relativeTo: nil, duration: 0.5)
        }
    }

    func rotateViewedBook(totalDragX: Float, totalDragY: Float) {
        guard isViewingBook, let book = viewedBook, let baseTransform = viewingLocalTransform else { return }

        let sensitivity: Float = 0.008
        let yRot = simd_quatf(angle: totalDragX * sensitivity, axis: SIMD3(0, 1, 0))
        let xRot = simd_quatf(angle: totalDragY * sensitivity, axis: SIMD3(1, 0, 0))

        // 베이스 회전에 총 드래그 오프셋 적용
        viewCurrentRotation = yRot * viewBaseRotation * xRot

        var newTransform = baseTransform
        newTransform.rotation = viewCurrentRotation
        book.transform = newTransform
    }

    func finishRotation() {
        // 현재 회전을 베이스로 저장 (다음 드래그의 시작점)
        viewBaseRotation = viewCurrentRotation
    }

    func dismissBook(state: BookshelfState? = nil) {
        guard isViewingBook, let book = viewedBook else { return }

        // 그림자 캐스트 복원
        Self.setCastsShadow(book, true)

        // 해당 책이 속한 섹션을 재레이아웃하여 현재 방향으로 배치
        if let state = state,
           let secIdx = state.sections.firstIndex(where: { $0.books.contains(where: { $0 === book }) }) {
            state.layoutSection(secIdx, duration: 0.4)
        } else if let original = viewedBookOriginalTransform {
            book.move(to: original, relativeTo: book.parent, duration: 0.4)
        }

        isViewingBook = false
        viewedBook = nil
        viewedBookOriginalTransform = nil
        viewingLocalTransform = nil
        viewBaseRotation = simd_quatf(angle: 0, axis: SIMD3(0, 1, 0))
        viewCurrentRotation = viewBaseRotation
    }

    /// dismissBook 을 우회하고 포커싱 상태만 즉시 해제할 때 ContentView에서 호출.
    /// 그림자 오버라이드만 복원.
    func restoreShadowForViewedBook() {
        guard let book = viewedBook else { return }
        Self.setCastsShadow(book, true)
    }

    /// 엔티티 트리에 재귀적으로 DynamicLightShadowComponent를 토글.
    /// 책은 보통 container + 자식 plane/mesh들의 복합체라 재귀가 필요.
    static func setCastsShadow(_ entity: Entity, _ casts: Bool) {
        if casts {
            entity.components.remove(DynamicLightShadowComponent.self)
        } else {
            entity.components.set(DynamicLightShadowComponent(castsShadow: false))
        }
        for child in entity.children {
            setCastsShadow(child, casts)
        }
    }

    /// 뷰잉 중인 책의 책장 꽂기 방향 변경. 섹션 오버플로우 시 다른 책들이 근처로 밀려남.
    func setViewedBookOrientation(_ orientation: PlacementOrientation, state: BookshelfState) {
        guard let book = viewedBook else { return }
        state.changeOrientation(of: book, to: orientation)
    }

    /// 현재 뷰잉 중인 책의 방향을 반환 (UI 표시용)
    func currentViewedBookOrientation() -> PlacementOrientation {
        viewedBook?.components[BookComponent.self]?.orientation ?? .spine
    }

    // MARK: - 드래그

    func beginDrag(entity: Entity, state: BookshelfState) {
        guard entity.name.hasPrefix("book_"), !isViewingBook else { return }
        draggedBook = entity
        dragStartWorldPos = entity.position
        isDragging = true

        // 바닥에 떨어져 있던 책이라면 물리 바디 제거 (드래그와 물리 충돌 방지)
        if entity.components[PhysicsBodyComponent.self] != nil {
            entity.components.remove(PhysicsBodyComponent.self)
            entity.components.remove(PhysicsMotionComponent.self)
        }

        // 원래 섹션에서 책 제거
        for (secIdx, sec) in state.sections.enumerated() {
            if let bookIdx = sec.books.firstIndex(where: { $0 === entity }) {
                originalSectionIdx = secIdx
                originalBookIndex = bookIdx
                state.sections[secIdx].books.remove(at: bookIdx)
                state.layoutSection(secIdx, duration: 0.2)
                break
            }
        }

        // 들어올리기
        BookAnimation.liftBook(entity: entity)
    }

    func updateDrag(entity: Entity, translation: SIMD3<Float>, state: BookshelfState) {
        guard isDragging, entity === draggedBook else { return }
        guard let startPos = dragStartWorldPos else { return }

        lastTranslation = translation

        // edge flip 이후 translation 리베이스 (offset 빼기)
        let offset = dragTranslationOffset ?? SIMD3<Float>.zero
        let effTranslation = SIMD3<Float>(
            translation.x - offset.x,
            translation.y - offset.y,
            translation.z - offset.z
        )
        // 플립 전: shelf-local (lift +0.12 필요), 플립 후: dragLayer(world, 이미 lifted 상태) → 0
        let zLift: Float = dragTranslationOffset == nil ? 0.12 : 0.0

        // 방 경계 클램프 — 책 크기 고려 (측벽 x=±2, 천장 y=2.8, 바닥 y=0)
        let comp = entity.components[BookComponent.self]
        let dragBookH = comp?.height ?? 0.24
        // 수평 최대 반폭 (정면/측면 orientation 상관없이 벽에 안 부딪히는 안전값)
        let bookHalfExtent = max(comp?.thickness ?? 0.03, comp?.width ?? 0.16) / 2
        let wallMargin: Float = 0.05
        let maxX: Float = 2.0 - bookHalfExtent - wallMargin
        let minY: Float = dragBookH / 2
        let maxY: Float = 2.75 - dragBookH / 2

        entity.position = SIMD3(
            max(-maxX, min(maxX, startPos.x + effTranslation.x)),
            max(minY, min(maxY, startPos.y + effTranslation.y)),
            startPos.z + zLift
        )

        // 책장 측면 경계 dwell 체크 (1초 이상 유지 시 flip)
        handleEdgeDwell(entity: entity, state: state)

        // 가장 가까운 섹션 찾기
        let bookWorldPos = entity.position
        guard let nearestSecIdx = state.findNearestSection(worldPos: bookWorldPos) else {
            clearHover(state: state)
            return
        }

        let sec = state.sections[nearestSecIdx]
        let bookComp = entity.components[BookComponent.self]
        let bookFootprint = bookComp?.footprint ?? 0.03
        let bookHeight = bookComp?.height ?? 0.24

        // 섹션 범위 확인
        let yInRange = abs(bookWorldPos.y - (sec.shelfTopY + sec.sectionHeight / 2)) < sec.sectionHeight * 0.8
        let xInRange = bookWorldPos.x > sec.leftBound - 0.1 && bookWorldPos.x < sec.rightBound + 0.1
        let canFit = sec.canFit(extraFootprint: bookFootprint)

        guard yInRange && xInRange && canFit else {
            clearHover(state: state)
            return
        }

        let insertIdx = sec.findInsertionIndex(worldX: bookWorldPos.x)

        // 이전 호버와 다르면 업데이트
        if hoveredSectionIdx != nearestSecIdx || hoveredInsertionIdx != insertIdx {
            // 이전 호버 정리
            if let prevSecIdx = hoveredSectionIdx, prevSecIdx != nearestSecIdx {
                state.layoutSection(prevSecIdx, duration: 0.2)
            }

            hoveredSectionIdx = nearestSecIdx
            hoveredInsertionIdx = insertIdx

            // 갭 벌리기
            state.layoutSection(nearestSecIdx, gapIndex: insertIdx, gapWidth: bookFootprint + 0.008, duration: 0.2)

            // 고스트 프리뷰 업데이트
            updateGhostPreview(
                state: state,
                secIdx: nearestSecIdx,
                insertIdx: insertIdx,
                footprint: bookFootprint,
                thickness: bookComp?.thickness ?? 0.03,
                height: bookHeight,
                width: bookComp?.width ?? 0.16,
                orientation: bookComp?.orientation ?? .spine
            )
        }
    }

    func endDrag(entity: Entity, state: BookshelfState) {
        guard isDragging, entity === draggedBook else { return }

        // 드래그 중에 edge flip으로 dragLayer에 얹혔다면 현재 활성 shelf로 재부모화
        // (world transform 보존 → 이후 layoutSection이 shelf-local target으로 안내)
        if let dragLayer = state.dragLayer, entity.parent === dragLayer,
           let shelfRoot = state.shelfRoot {
            entity.setParent(shelfRoot, preservingWorldTransform: true)
        }

        removeGhostPreview()

        if let secIdx = hoveredSectionIdx, let insertIdx = hoveredInsertionIdx {
            // 유효한 위치에 놓기
            state.sections[secIdx].books.insert(entity, at: insertIdx)
            let positions = state.sections[secIdx].computePositions()
            let targetPos = positions[insertIdx]

            // 중력처럼 떨어지면서 위치로 이동
            BookAnimation.simulateGravityDrop(
                entity: entity,
                from: entity.position.y,
                targetY: targetPos.y,
                duration: 0.3
            )

            var transform = entity.transform
            transform.translation = targetPos
            entity.move(to: transform, relativeTo: entity.parent, duration: 0.3)

            state.layoutSection(secIdx, duration: 0.25)
        } else {
            // 책장 범위 내인지 판별 (shelf-local 기준)
            let pos = entity.position
            let inShelfX = abs(pos.x) < 0.85
            let inShelfY = pos.y > -0.1 && pos.y < 1.7

            if inShelfX && inShelfY {
                // 책장 범위 안 → 가장 가까운 섹션에 넣기
                let dropPos = entity.position
                if let nearestSecIdx = state.findNearestSection(worldPos: dropPos) {
                    let sec = state.sections[nearestSecIdx]
                    let bookComp = entity.components[BookComponent.self]
                    let bookFootprint = bookComp?.footprint ?? 0.03
                    let insertIdx = sec.findInsertionIndex(worldX: dropPos.x)

                    if sec.canFit(extraFootprint: bookFootprint) {
                        state.sections[nearestSecIdx].books.insert(entity, at: insertIdx)
                        let positions = state.sections[nearestSecIdx].computePositions()
                        let targetPos = positions[insertIdx]

                        var transform = entity.transform
                        transform.translation = targetPos
                        entity.move(to: transform, relativeTo: entity.parent, duration: 0.4)

                        state.layoutSection(nearestSecIdx, duration: 0.3)
                    } else if let origSec = originalSectionIdx, let origIdx = originalBookIndex {
                        let idx = min(origIdx, state.sections[origSec].books.count)
                        state.sections[origSec].books.insert(entity, at: idx)
                        state.layoutSection(origSec, duration: 0.3)

                        let positions = state.sections[origSec].computePositions()
                        if idx < positions.count {
                            BookAnimation.snapToPosition(entity: entity, position: positions[idx], duration: 0.3)
                        }
                    }
                }
            } else {
                // 책장 범위 밖 → 책장에서 분리 후 바닥에 떨어뜨리기
                dropToFloor(entity: entity, state: state)
            }
        }

        resetDragState()
    }

    private func updateGhostPreview(
        state: BookshelfState,
        secIdx: Int,
        insertIdx: Int,
        footprint: Float,
        thickness: Float,
        height: Float,
        width: Float,
        orientation: PlacementOrientation
    ) {
        removeGhostPreview()

        let ghost = BookEntity.createGhost(thickness: thickness, height: height, width: width)
        ghost.orientation = orientation.rotation
        let sec = state.sections[secIdx]

        // 갭 중심 위치 계산 (기존 책들은 footprint 사용)
        var x = sec.leftBound + 0.005
        for i in 0..<insertIdx {
            if i < sec.books.count {
                x += sec.books[i].components[BookComponent.self]?.footprint ?? 0.03
            }
        }
        x += footprint / 2

        let y = sec.shelfTopY + height / 2
        ghost.position = SIMD3(x, y, 0)

        state.shelfRoot?.addChild(ghost)
        ghostPreview = ghost
    }

    private func removeGhostPreview() {
        ghostPreview?.removeFromParent()
        ghostPreview = nil
    }

    private func clearHover(state: BookshelfState) {
        if let prevSecIdx = hoveredSectionIdx {
            state.layoutSection(prevSecIdx, duration: 0.2)
        }
        removeGhostPreview()
        hoveredSectionIdx = nil
        hoveredInsertionIdx = nil
    }

    /// 책이 책장 밖에서 놓이면 책장에서 분리 후 물리 엔진으로 자유낙하.
    /// - 책장(회전/이동)에 종속되어 있으면 물리 시뮬레이션과 parent transform이 충돌 →
    ///   월드 고정 레이어(dragLayer)로 재부모화해서 독립된 물리 객체로 만듦.
    private func dropToFloor(entity: Entity, state: BookshelfState) {
        // 책장 하위에 남아있다면 dragLayer(월드 고정)로 분리 — world transform 보존
        if let dragLayer = state.dragLayer, entity.parent !== dragLayer {
            entity.setParent(dragLayer, preservingWorldTransform: true)
        }

        var body = PhysicsBodyComponent(
            massProperties: .default,
            material: .generate(friction: 0.4, restitution: 0.05),
            mode: .dynamic
        )
        body.isAffectedByGravity = true
        entity.components.set(body)

        // 초기 회전/선속도로 쓰러짐 유도 (손에서 놓는 느낌)
        var motion = PhysicsMotionComponent()
        motion.angularVelocity = SIMD3<Float>(
            Float.random(in: -1.5...1.5),
            Float.random(in: -0.8...0.8),
            Float.random(in: -1.5...1.5)
        )
        motion.linearVelocity = SIMD3<Float>(
            Float.random(in: -0.25...0.25),
            0,
            Float.random(in: -0.25...0.25)
        )
        entity.components.set(motion)
    }

    private func resetDragState() {
        draggedBook = nil
        dragStartWorldPos = nil
        originalSectionIdx = nil
        originalBookIndex = nil
        hoveredSectionIdx = nil
        hoveredInsertionIdx = nil
        isDragging = false
        dragTranslationOffset = nil
        lastTranslation = nil
        cancelEdgeFlipTimer()
    }

    // MARK: - 측면 경계 dwell → 책장 전환

    private let edgeDwellDuration: TimeInterval = 1.0
    private let edgeThresholdX: Float = 0.95  // 책장 반폭 0.8 + 여유 0.15

    private func handleEdgeDwell(entity: Entity, state: BookshelfState) {
        guard state.bookshelves.count > 1 else { return }

        let worldX = entity.position(relativeTo: nil).x
        let direction: SwitchDirection?
        if worldX > edgeThresholdX {
            direction = .right
        } else if worldX < -edgeThresholdX {
            direction = .left
        } else {
            direction = nil
        }

        if direction != edgePastDirection {
            // 방향이 바뀌었거나 경계 내로 돌아옴 → 타이머 취소 후 새로 시작
            cancelEdgeFlipTimer()
            edgePastDirection = direction
            if let dir = direction {
                scheduleEdgeFlip(direction: dir, state: state)
            }
        }
    }

    private func scheduleEdgeFlip(direction: SwitchDirection, state: BookshelfState) {
        edgeFlipTask = Task { @MainActor [weak self] in
            try? await Task.sleep(for: .seconds(self?.edgeDwellDuration ?? 1.0))
            guard let self, !Task.isCancelled else { return }
            self.triggerEdgeFlip(direction: direction, state: state)
        }
    }

    private func cancelEdgeFlipTimer() {
        edgeFlipTask?.cancel()
        edgeFlipTask = nil
        edgePastDirection = nil
    }

    private func triggerEdgeFlip(direction: SwitchDirection, state: BookshelfState) {
        guard let entity = draggedBook, let dragLayer = state.dragLayer else { return }

        // 1) 책을 world-space dragLayer로 재부모화 (책장 회전과 무관하게 유지)
        entity.setParent(dragLayer, preservingWorldTransform: true)

        // 2) 원래 섹션 참조 제거 — 이 책은 더 이상 옛 책장 소속이 아님
        originalSectionIdx = nil
        originalBookIndex = nil

        // 3) hover/ghost 정리
        clearHover(state: state)

        // 4) 드래그 좌표 리베이스 — 현재 world 위치를 새 startPos로,
        //    지금까지의 누적 translation을 offset으로 저장해
        //    이후 updateDrag에서 (translation - offset) 만큼만 이동
        dragStartWorldPos = entity.position(relativeTo: nil)
        dragTranslationOffset = lastTranslation ?? SIMD3<Float>.zero

        // 5) 책장 전환 시작 (1초 회전)
        state.switchTo(direction: direction)

        edgeFlipTask = nil
        edgePastDirection = nil  // 여전히 경계 밖이면 다음 updateDrag에서 새 타이머 등록 가능
    }
}
