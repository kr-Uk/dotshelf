import AppKit
import Observation
import simd

@Observable
final class InputHandler {
    weak var camera: Camera?
    var sceneRoot: SceneNode?
    var bookshelfState: BookshelfState?
    var viewportSize: SIMD2<Float> = SIMD2<Float>(800, 600)

    // View mode state
    var isViewingBook: Bool = false
    private var viewedBook: SceneNode?
    private var viewedBookOriginalLocal: float4x4?
    private var viewBaseRotation: simd_quatf = simd_quatf(angle: 0, axis: SIMD3(0, 1, 0))
    private var viewCurrentRotation: simd_quatf = simd_quatf(angle: 0, axis: SIMD3(0, 1, 0))

    // Drag state
    var isDraggingBook: Bool = false
    private var draggedBook: SceneNode?
    private var originalSectionIdx: Int?
    private var originalBookIdx: Int?
    private var ghostNode: SceneNode?
    private var hoveredSectionIdx: Int?
    private var hoveredInsertionIdx: Int?
    private var mouseDownFlippedPoint: NSPoint = .zero

    private let rotBook = float4x4.rotation(angle: Float.pi / 2, axis: SIMD3<Float>(0, 1, 0))

    // MARK: - Scroll

    func handleScrollWheel(deltaY: CGFloat) {
        guard !isViewingBook && !isDraggingBook else { return }
        camera?.zoom(delta: Float(deltaY) * 0.1)
    }

    // MARK: - Mouse events

    func mouseDown(atFlipped point: NSPoint) {
        mouseDownFlippedPoint = point
    }

    func beginDrag() {
        if isViewingBook { return }

        // Pick book at initial click position
        guard let book = pickBook(at: mouseDownFlippedPoint),
              let state = bookshelfState else { return }

        draggedBook = book
        isDraggingBook = true

        // Remove book from its section, then re-layout that section
        if let loc = state.find(book: book) {
            originalSectionIdx = loc.sectionIdx
            originalBookIdx = loc.bookIdx
            state.sections[loc.sectionIdx].books.remove(at: loc.bookIdx)
            state.layoutSection(loc.sectionIdx)
        }
    }

    func updateDrag(currentFlippedPoint: NSPoint, totalDx: CGFloat, totalDy: CGFloat) {
        if isViewingBook, let book = viewedBook {
            let sensitivity: Float = 0.008
            let yRot = simd_quatf(angle: Float(totalDx) * sensitivity, axis: SIMD3(0, 1, 0))
            let xRot = simd_quatf(angle: Float(-totalDy) * sensitivity, axis: SIMD3(1, 0, 0))
            viewCurrentRotation = yRot * viewBaseRotation * xRot
            rebuildViewTarget(for: book)
            return
        }

        if isDraggingBook, let book = draggedBook,
           let state = bookshelfState,
           let camera = camera,
           let meta = book.bookMetadata {

            // Project mouse to z=0 plane for the drag world position
            let sp = SIMD2<Float>(Float(currentFlippedPoint.x), Float(currentFlippedPoint.y))
            let ray = Raycaster.screenToWorldRay(screenPoint: sp, viewportSize: viewportSize, camera: camera)
            guard let hit = Raycaster.intersectZPlane(ray: ray, planeZ: 0) else { return }

            // Book floats in air while dragging
            let bookWorldPos = SIMD3<Float>(hit.x, hit.y, 0.12)
            book.localTransform = float4x4.translation(bookWorldPos) * rotBook

            // Find nearest section and decide hover state
            guard let nearestIdx = state.findNearestSection(worldPos: bookWorldPos) else {
                clearHover()
                return
            }

            let sec = state.sections[nearestIdx]

            // Check if book fits in this section's X range and Y is within section height
            let yInRange = abs(bookWorldPos.y - (sec.shelfTopY + sec.sectionHeight / 2)) < sec.sectionHeight * 0.8
            let xInRange = bookWorldPos.x > sec.leftBound - 0.1 && bookWorldPos.x < sec.rightBound + 0.1
            let canFit = sec.canFit(extraThickness: meta.thickness)

            guard yInRange && xInRange && canFit else {
                clearHover()
                return
            }

            let insertIdx = sec.findInsertionIndex(worldX: bookWorldPos.x)

            if hoveredSectionIdx != nearestIdx || hoveredInsertionIdx != insertIdx {
                // Clear previous section's gap
                if let prev = hoveredSectionIdx, prev != nearestIdx {
                    state.layoutSection(prev)
                }

                hoveredSectionIdx = nearestIdx
                hoveredInsertionIdx = insertIdx

                // Expand a gap in the current section
                state.layoutSection(nearestIdx, gapIndex: insertIdx, gapWidth: meta.thickness + 0.008)

                // Update ghost at gap center (with correct Y = shelfTop + height/2 for gravity)
                updateGhost(secIdx: nearestIdx, insertIdx: insertIdx, book: book, meta: meta)
            }
        }
    }

    func endDrag(at flippedPoint: NSPoint, dragDistance: CGFloat) {
        if isViewingBook {
            viewBaseRotation = viewCurrentRotation
            return
        }

        if isDraggingBook, let book = draggedBook, let state = bookshelfState {
            removeGhost()

            if let secIdx = hoveredSectionIdx, let insertIdx = hoveredInsertionIdx {
                // Insert at hovered position
                state.sections[secIdx].books.insert(book, at: insertIdx)
                state.layoutSection(secIdx)
            } else {
                // Try drop at current position, or fall back to original
                let pos = SIMD3<Float>(book.localTransform.columns.3.x,
                                       book.localTransform.columns.3.y,
                                       book.localTransform.columns.3.z)
                if let meta = book.bookMetadata,
                   let nearestIdx = state.findNearestSection(worldPos: pos) {
                    let sec = state.sections[nearestIdx]
                    let insertIdx = sec.findInsertionIndex(worldX: pos.x)
                    if sec.canFit(extraThickness: meta.thickness) {
                        state.sections[nearestIdx].books.insert(book, at: insertIdx)
                        state.layoutSection(nearestIdx)
                    } else if let origSec = originalSectionIdx, let origIdx = originalBookIdx {
                        let idx = min(origIdx, state.sections[origSec].books.count)
                        state.sections[origSec].books.insert(book, at: idx)
                        state.layoutSection(origSec)
                    }
                } else if let origSec = originalSectionIdx, let origIdx = originalBookIdx {
                    let idx = min(origIdx, state.sections[origSec].books.count)
                    state.sections[origSec].books.insert(book, at: idx)
                    state.layoutSection(origSec)
                }
            }

            resetDragState()
            return
        }

        // Click (no drag)
        if dragDistance < 3 {
            handleClick(atFlipped: flippedPoint)
        }
    }

    // MARK: - Ghost preview

    private func updateGhost(secIdx: Int, insertIdx: Int, book: SceneNode, meta: BookMetadata) {
        guard let state = bookshelfState, let root = sceneRoot else { return }
        removeGhost()

        let sec = state.sections[secIdx]
        let x = sec.gapCenterX(insertIdx: insertIdx, newBookThickness: meta.thickness)
        let y = sec.shelfTopY + meta.height / 2   // 중력: 책 바닥이 선반 상단
        let pos = SIMD3<Float>(x, y, 0)

        let ghost = SceneNode()
        ghost.name = "ghost"
        ghost.mesh = book.mesh
        ghost.bookMetadata = meta
        if var mat = book.material {
            mat.alpha = 0.35
            ghost.material = mat
        }
        ghost.localTransform = float4x4.translation(pos) * rotBook
        root.addChild(ghost)
        ghostNode = ghost
    }

    private func removeGhost() {
        if let g = ghostNode {
            sceneRoot?.removeChild(g)
        }
        ghostNode = nil
    }

    private func clearHover() {
        if let prev = hoveredSectionIdx, let state = bookshelfState {
            state.layoutSection(prev)
        }
        removeGhost()
        hoveredSectionIdx = nil
        hoveredInsertionIdx = nil
    }

    private func resetDragState() {
        isDraggingBook = false
        draggedBook = nil
        originalSectionIdx = nil
        originalBookIdx = nil
        hoveredSectionIdx = nil
        hoveredInsertionIdx = nil
    }

    // MARK: - Click (view mode toggle)

    private func handleClick(atFlipped point: NSPoint) {
        if isViewingBook {
            dismissBook()
            return
        }
        guard let book = pickBook(at: point) else { return }
        viewBook(book)
    }

    private func pickBook(at flippedPoint: NSPoint) -> SceneNode? {
        guard let camera = camera, let root = sceneRoot else { return nil }
        let sp = SIMD2<Float>(Float(flippedPoint.x), Float(flippedPoint.y))
        let ray = Raycaster.screenToWorldRay(screenPoint: sp, viewportSize: viewportSize, camera: camera)

        var closest: SceneNode?
        var closestT: Float = .infinity
        for node in root.allRenderableNodes() where node.name.hasPrefix("book_") {
            guard let aabb = Raycaster.computeAABB(for: node) else { continue }
            if let t = aabb.intersects(ray: ray), t < closestT {
                closestT = t
                closest = node
            }
        }
        return closest
    }

    // MARK: - View mode

    private func viewBook(_ node: SceneNode) {
        viewedBook = node
        viewedBookOriginalLocal = node.localTransform
        isViewingBook = true
        viewBaseRotation = simd_quatf(angle: .pi / 2, axis: SIMD3(0, 1, 0))
        viewCurrentRotation = viewBaseRotation
        rebuildViewTarget(for: node)
    }

    private func rebuildViewTarget(for node: SceneNode) {
        guard let camera = camera else { return }
        let forward = normalize(camera.center - camera.eye)
        let targetWorldPos = camera.eye + forward * 0.6

        let rotMat = matrix_float4x4(viewCurrentRotation)
        var target = rotMat
        target.columns.3 = SIMD4<Float>(targetWorldPos.x, targetWorldPos.y, targetWorldPos.z, 1)
        node.viewModeTarget = target
        if node.viewModeOverride == nil {
            node.viewModeOverride = node.worldTransform
        }
    }

    func dismissBook() {
        guard let node = viewedBook, let orig = viewedBookOriginalLocal else { return }
        node.viewModeTarget = orig
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.35) { [weak node] in
            node?.viewModeOverride = nil
            node?.viewModeTarget = nil
        }
        isViewingBook = false
        viewedBook = nil
        viewedBookOriginalLocal = nil
    }
}
