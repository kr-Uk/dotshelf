import SwiftUI
import RealityKit

enum CameraViewMode {
    case overview    // 코너 3/4 구도
    case frontal     // 책장 정면
    case whiteboard  // 화이트보드 정면
}

struct ContentView: View {
    /// 책장을 뒷벽에 붙인 위치 (뒷벽 z=-1.0, 책장 깊이 0.25, 뒷판 위치 -0.122, 5mm 간격)
    private let bookshelfZ: Float = -0.873

    @State private var cameraEntity: PerspectiveCamera?
    @State private var cameraMode: CameraViewMode = .overview
    @State private var bookshelfState = BookshelfState()
    @State private var interactionState = BookInteractionState()

    // 배치 변경 UI 상태
    @State private var isEditingOrientation = false
    @State private var stagedOrientation: PlacementOrientation = .spine
    @State private var editingBook: Entity?
    @State private var editingBookOriginalOrientation: PlacementOrientation = .spine

    // 섹션 정렬 UI 상태
    @State private var selectedSectionForAlignment: Int?

    // 화이트보드 방명록 상태
    @State private var whiteboardSurface: ModelEntity?
    @State private var likeCount: Int = 234
    @State private var isLiked: Bool = false

    // 정면 뷰에서 카메라 팬 드래그 시작점
    @State private var cameraPanStart: SIMD3<Float>?

    var body: some View {
        RealityView { content in
            // 방 환경 (바닥 + 벽)
            let room = RoomEnvironment.create()
            content.add(room)

            // 화이트보드 표면 엔티티 참조 저장 (텍스처 업데이트용)
            if let surface = room.findEntity(named: "whiteboard_surface") as? ModelEntity {
                whiteboardSurface = surface
            }

            // 조명 전용 컨테이너 (책장이 전환될 때 조명은 고정)
            let lightContainer = Entity()
            lightContainer.position = SIMD3<Float>(0, 0, bookshelfZ)
            content.add(lightContainer)
            EnvironmentSetup.configureLighting(root: lightContainer)

            // 회전축(turntable): 벽 평면 자체를 피벗
            let armLength: Float = 0.127
            let wallZ: Float = -1.0
            let turntable = Entity()
            turntable.position = SIMD3<Float>(0, 0, wallZ)
            content.add(turntable)
            bookshelfState.turntable = turntable
            bookshelfState.turntableArmLength = armLength

            // 드래그 레이어: 책이 책장 경계를 넘어가도 turntable 회전과 분리되어 유지
            let dragLayer = Entity()
            content.add(dragLayer)
            bookshelfState.dragLayer = dragLayer

            // 고정된 2개의 슬롯 (책장 개수와 무관하게 turntable 자체 회전이 슬롯 위치를 바꿈)
            let slot0 = Entity()
            slot0.name = "slot0"
            slot0.position = SIMD3<Float>(0, 0, armLength)
            turntable.addChild(slot0)

            let slot1 = Entity()
            slot1.name = "slot1"
            slot1.position = SIMD3<Float>(0, 0, -armLength)
            slot1.orientation = simd_quatf(angle: .pi, axis: SIMD3<Float>(0, 1, 0))
            turntable.addChild(slot1)

            bookshelfState.slots = [slot0, slot1]

            // 회전 벽 패널 (숨겨진 슬롯을 가려주는 역할 — turntable과 함께 회전)
            let panelMesh = MeshResource.generateBox(width: 1.7, height: 1.7, depth: 0.005)
            let wallPanel = ModelEntity(mesh: panelMesh, materials: [MaterialFactory.wallMaterial()])
            wallPanel.name = "rotating_wall_panel"
            wallPanel.position = SIMD3<Float>(0, 0.85, 0)
            turntable.addChild(wallPanel)

            // 책장 3개 생성 (첫 2개는 슬롯에 장착, 3번째는 대기)
            for i in 0..<3 {
                let shelfEntity = BookshelfEntity.create()
                var sec = BookshelfState.makeDefaultSections()
                BookshelfState.populateBooks(
                    into: &sec, parent: shelfEntity,
                    includeScanned: i == 0, startIndex: i * 100
                )

                // 첫 번째 책장: 우측 상단에 광고 칸 (section 15)
                if i == 0 {
                    let adSecIdx = 15
                    if adSecIdx < sec.count {
                        for book in sec[adSecIdx].books { book.removeFromParent() }
                        sec[adSecIdx].books.removeAll()

                        // 광고 섹션 배경 (금색 발광 → 시각적으로 구분)
                        let adSec = sec[adSecIdx]
                        let secW = adSec.rightBound - adSec.leftBound
                        let secH = adSec.sectionHeight
                        let secCX = (adSec.leftBound + adSec.rightBound) / 2
                        let secCY = adSec.shelfTopY + secH / 2

                        let bgMesh = MeshResource.generatePlane(width: secW - 0.01, height: secH - 0.01)
                        var bgMat = PhysicallyBasedMaterial()
                        bgMat.baseColor = .init(tint: PlatformColor(red: 1.0, green: 0.88, blue: 0.35, alpha: 1.0))
                        bgMat.emissiveColor = .init(color: PlatformColor(red: 1.0, green: 0.88, blue: 0.35, alpha: 1.0))
                        bgMat.emissiveIntensity = 0.5
                        bgMat.roughness = .init(floatLiteral: 0.9)
                        bgMat.metallic = .init(floatLiteral: 0.0)
                        let adBg = ModelEntity(mesh: bgMesh, materials: [bgMat])
                        adBg.name = "ad_backdrop"
                        adBg.position = SIMD3<Float>(secCX, secCY, -0.11)
                        shelfEntity.addChild(adBg)

                        // 광고 책 배치
                        let ad = BookEntity.createAdBook()
                        ad.name = "book_ad_\(i)"
                        ad.position = SIMD3<Float>(secCX, adSec.shelfTopY + 0.13, 0)
                        shelfEntity.addChild(ad)
                        sec[adSecIdx].books.append(ad)
                    }
                }

                // 첫 번째 책장 상단-좌측 칸에 복엽기 배치
                if i == 0, let biplane = BookEntity.loadBiplaneDecoration(targetSize: 0.12) {
                    let decoSecIdx = 12  // 상단-좌측으로 이동 (기존 15 → 광고 칸으로 양보)
                    if decoSecIdx < sec.count {
                        // 해당 칸의 책들 제거
                        for book in sec[decoSecIdx].books {
                            book.removeFromParent()
                        }
                        sec[decoSecIdx].books.removeAll()

                        biplane.name = "book_\(i * 100 + 99)_biplane"  // 고유 이름
                        let bpHeight = biplane.components[BookComponent.self]?.height ?? 0.12
                        let section = sec[decoSecIdx]
                        let centerX = (section.leftBound + section.rightBound) / 2
                        biplane.position = SIMD3<Float>(
                            centerX,
                            section.shelfTopY + bpHeight / 2,
                            0
                        )
                        shelfEntity.addChild(biplane)
                        // SlotSystem의 books 배열에 등록 → drag/drop/layoutSection 대상
                    }
                }

                BookshelfState.populateSectionClickAreas(
                    in: sec, parent: shelfEntity, bookshelfIndex: i
                )
                bookshelfState.addBookshelf(entity: shelfEntity, sections: sec)
            }
            bookshelfState.installInitialShelves()

            // HDRI 환경맵: IBL 조명 + 반사 (보이는 배경은 SwiftUI에서 처리)
            if let envResource = EnvironmentSetup.loadEnvironmentResource() {
                let iblEntity = Entity()
                let iblComponent = ImageBasedLightComponent(source: .single(envResource), intensityExponent: 1.0)
                iblEntity.components.set(iblComponent)
                iblEntity.components.set(ImageBasedLightReceiverComponent(imageBasedLight: iblEntity))
                content.add(iblEntity)
            }

            let camera = CameraController.createOverviewCamera(bookshelfZ: bookshelfZ)
            content.add(camera)
            cameraEntity = camera
        }
        .simultaneousGesture(
            TapGesture()
                .targetedToAnyEntity()
                .onEnded { value in
                    let name = value.entity.name
                    // 책 탭 → 책 보기
                    if name.hasPrefix("book_") {
                        guard !interactionState.isDragging else { return }
                        selectedSectionForAlignment = nil
                        interactionState.viewBook(entity: value.entity, camera: cameraEntity)
                        return
                    }
                    guard !interactionState.isViewingBook, !interactionState.isDragging else { return }
                    // 섹션 영역 탭 → 정렬 UI 표시. 이름 형식: "section_<bookshelfIdx>_<secIdx>"
                    if name.hasPrefix("section_"), !isEditingOrientation {
                        let parts = name.dropFirst("section_".count).split(separator: "_")
                        if parts.count == 2,
                           let bsIdx = Int(parts[0]),
                           let secIdx = Int(parts[1]),
                           bsIdx == bookshelfState.currentIndex {
                            selectedSectionForAlignment = (selectedSectionForAlignment == secIdx) ? nil : secIdx
                        }
                        return
                    }
                    // 뒷벽 탭 → 책장 정면 뷰
                    if name == "room_wall_back" {
                        if let camera = cameraEntity {
                            CameraController.moveToFrontal(camera: camera, bookshelfZ: bookshelfZ)
                            cameraMode = .frontal
                        }
                    }
                    // 화이트보드 탭 → 화이트보드 정면 뷰
                    if name == "whiteboard_frame" {
                        if let camera = cameraEntity {
                            CameraController.moveToWhiteboard(camera: camera)
                            cameraMode = .whiteboard
                        }
                    }
                }
        )
        .simultaneousGesture(
            DragGesture()
                .targetedToAnyEntity()
                .onChanged { value in
                    let entity = value.entity
                    guard entity.name.hasPrefix("book_") else { return }

                    if interactionState.isViewingBook {
                        let t = value.gestureValue.translation
                        interactionState.rotateViewedBook(
                            totalDragX: Float(t.width),
                            totalDragY: Float(t.height)
                        )
                        return
                    }

                    if !interactionState.isDragging {
                        interactionState.beginDrag(entity: entity, state: bookshelfState)
                    }

                    let t = value.gestureValue.translation
                    let scaleFactor: Float = 0.002
                    let translation = SIMD3<Float>(
                        Float(t.width) * scaleFactor,
                        Float(-t.height) * scaleFactor,
                        0
                    )
                    interactionState.updateDrag(entity: entity, translation: translation, state: bookshelfState)
                }
                .onEnded { value in
                    let entity = value.entity
                    guard entity.name.hasPrefix("book_") else { return }
                    if interactionState.isViewingBook {
                        interactionState.finishRotation()
                        return
                    }
                    interactionState.endDrag(entity: entity, state: bookshelfState)
                }
        )
        // 정면 뷰에서 빈 공간 드래그 시 카메라 팬 (책 드래그와 동시 매칭되면 책 우선)
        .simultaneousGesture(
            DragGesture(minimumDistance: 6)
                .onChanged { value in
                    guard cameraMode == .frontal else { return }
                    guard !interactionState.isDragging else { return }  // 책 드래그 중이면 무시
                    guard !interactionState.isViewingBook, !isEditingOrientation else { return }
                    guard let camera = cameraEntity else { return }

                    if cameraPanStart == nil {
                        cameraPanStart = camera.position
                    }
                    guard let start = cameraPanStart else { return }

                    let t = value.translation
                    let sensitivity: Float = 0.002
                    let dx = -Float(t.width) * sensitivity   // drag right → content follows (camera left)
                    let dy = Float(t.height) * sensitivity   // drag down → content follows (camera up)

                    let newX = max(-0.9, min(0.9, start.x + dx))
                    let newY = max(0.3, min(1.8, start.y + dy))
                    camera.position = SIMD3<Float>(newX, newY, start.z)
                }
                .onEnded { _ in
                    cameraPanStart = nil
                }
        )
        #if os(iOS)
        .simultaneousGesture(
            MagnifyGesture()
                .onChanged { value in
                    guard let camera = cameraEntity else { return }
                    guard !interactionState.isViewingBook else { return }
                    guard cameraMode == .frontal else { return }
                    let delta = Float(value.magnification - 1.0) * 0.05
                    CameraController.zoom(camera: camera, delta: delta, targetZ: bookshelfZ)
                }
        )
        #endif
        .overlay(alignment: .topLeading) {
            // 디버그: glTF 책 로드 상태 표시
            Text("glTF: \(BookEntity.scannedBookLoadStatus)")
                .font(.system(size: 12, design: .monospaced))
                .padding(8)
                .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 6))
                .padding(12)
        }
        .overlay {
            // 책장 양 옆에 플립 버튼 (책장 좌/우 가장자리의 월드좌표를 스크린에 투영)
            if bookshelfState.bookshelves.count > 1,
               !interactionState.isViewingBook, !isEditingOrientation,
               cameraMode != .whiteboard,
               let camera = cameraEntity {
                GeometryReader { geo in
                    TimelineView(.animation) { _ in
                        // 책장 좌/우 엣지 바깥쪽 (책장 폭 1.6m, 높이 중심 0.8m)
                        let leftWorld = SIMD3<Float>(-0.95, 0.8, bookshelfZ)
                        let rightWorld = SIMD3<Float>(0.95, 0.8, bookshelfZ)
                        let leftScreen = Self.projectWorldToScreen(worldPos: leftWorld, camera: camera, viewSize: geo.size)
                        let rightScreen = Self.projectWorldToScreen(worldPos: rightWorld, camera: camera, viewSize: geo.size)

                        if let p = leftScreen {
                            flipButton(direction: .left)
                                .position(x: p.x, y: p.y)
                        }
                        if let p = rightScreen {
                            flipButton(direction: .right)
                                .position(x: p.x, y: p.y)
                        }
                    }
                }
            }
        }
        .overlay(alignment: .topTrailing) {
            // 오버뷰가 아닌 뷰에서 오버뷰로 돌아가는 버튼
            if cameraMode != .overview && !interactionState.isViewingBook {
                Button(action: {
                    if let camera = cameraEntity {
                        CameraController.moveToOverview(camera: camera, bookshelfZ: bookshelfZ)
                        cameraMode = .overview
                    }
                }) {
                    HStack(spacing: 6) {
                        Image(systemName: "arrow.up.left.and.arrow.down.right")
                            .font(.system(size: 13, weight: .semibold))
                        Text("방 전체")
                            .font(.system(size: 13, weight: .medium))
                    }
                    .padding(.horizontal, 14)
                    .padding(.vertical, 8)
                    .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 10))
                }
                .buttonStyle(.plain)
                .padding(12)
                .transition(.opacity)
            }
        }
        .overlay(alignment: .bottom) {
            // 책 뷰잉 중: "배치 변경" 버튼
            if interactionState.isViewingBook && !isEditingOrientation {
                changePlacementButton
                    .padding(.bottom, 24)
                    .transition(.opacity.combined(with: .move(edge: .bottom)))
            }
        }
        .overlay(alignment: .trailing) {
            // 책 확대 시 우측에 독서기록 + 댓글 패널 (광고 책은 제외)
            if interactionState.isViewingBook && !isEditingOrientation,
               let viewedBook = interactionState.viewedBook,
               !viewedBook.name.contains("_ad") {
                BookReadingPanel(bookName: viewedBook.name)
                    .padding(.trailing, 24)
                    .transition(.opacity.combined(with: .move(edge: .trailing)))
            }
        }
        .overlay(alignment: .topTrailing) {
            // 광고 책 확대 시 AD 배지 + X 닫기
            if interactionState.isViewingBook,
               let viewedBook = interactionState.viewedBook,
               viewedBook.name.contains("_ad") {
                HStack(spacing: 12) {
                    Text("AD")
                        .font(.system(size: 12, weight: .black))
                        .foregroundStyle(.white)
                        .padding(.horizontal, 10)
                        .padding(.vertical, 5)
                        .background(Color.orange, in: Capsule())
                    Text("광고")
                        .font(.system(size: 13, weight: .medium))
                        .foregroundStyle(.secondary)
                    Spacer()
                    Button(action: { dismissAdBook(viewedBook) }) {
                        Image(systemName: "xmark.circle.fill")
                            .font(.system(size: 28))
                            .foregroundStyle(.white, .black.opacity(0.5))
                            .shadow(color: .black.opacity(0.3), radius: 4, y: 2)
                    }
                    .buttonStyle(.plain)
                }
                .padding(20)
                .transition(.opacity)
            }
        }
        .overlay {
            // 배치 편집 중: 책 근처에 옵션 + 확인/취소
            if isEditingOrientation, let editingBook, let camera = cameraEntity {
                GeometryReader { geo in
                    TimelineView(.animation) { _ in
                        let worldPos = editingBook.position(relativeTo: nil)
                        let projected = Self.projectWorldToScreen(
                            worldPos: worldPos,
                            camera: camera,
                            viewSize: geo.size
                        )
                        let fallback = CGPoint(x: geo.size.width / 2, y: geo.size.height * 0.7)
                        let screenPos = projected ?? fallback
                        // 책 아래쪽에 UI 배치, 화면 경계 내로 클램프
                        let uiY = min(screenPos.y + 140, geo.size.height - 70)
                        let uiX = min(max(screenPos.x, 140), geo.size.width - 140)
                        orientationEditor
                            .position(x: uiX, y: uiY)
                    }
                }
                .transition(.opacity)
            }
        }
        .overlay {
            // 섹션 정렬 선택 UI (섹션 탭 시)
            if let secIdx = selectedSectionForAlignment, let camera = cameraEntity,
               secIdx < bookshelfState.sections.count,
               let shelfRoot = bookshelfState.shelfRoot {
                GeometryReader { geo in
                    TimelineView(.animation) { _ in
                        let sec = bookshelfState.sections[secIdx]
                        let localCenter = SIMD3<Float>(
                            (sec.leftBound + sec.rightBound) / 2,
                            sec.shelfTopY + sec.sectionHeight / 2,
                            0
                        )
                        let worldPos = shelfRoot.position(relativeTo: nil) + localCenter
                        let projected = Self.projectWorldToScreen(
                            worldPos: worldPos,
                            camera: camera,
                            viewSize: geo.size
                        )
                        let fallback = CGPoint(x: geo.size.width / 2, y: geo.size.height / 2)
                        let screenPos = projected ?? fallback
                        let uiY = max(min(screenPos.y + 80, geo.size.height - 50), 50)
                        let uiX = min(max(screenPos.x, 100), geo.size.width - 100)
                        alignmentPicker(for: secIdx)
                            .position(x: uiX, y: uiY)
                    }
                }
                .transition(.opacity)
            }
        }
        .overlay(alignment: .bottom) {
            // 화이트보드 뷰에서 좋아요 인터랙션 버튼 (대시보드 자체는 화이트보드 텍스처로 렌더링)
            if cameraMode == .whiteboard {
                Button(action: toggleLike) {
                    HStack(spacing: 8) {
                        Image(systemName: isLiked ? "heart.fill" : "heart")
                            .font(.system(size: 16, weight: .semibold))
                            .foregroundStyle(isLiked ? .red : .primary)
                        Text(isLiked ? "좋아요 취소" : "좋아요")
                            .font(.system(size: 14, weight: .medium))
                    }
                    .padding(.horizontal, 20)
                    .padding(.vertical, 12)
                    .background(.ultraThinMaterial, in: Capsule())
                    .shadow(color: .black.opacity(0.15), radius: 6, y: 2)
                }
                .buttonStyle(.plain)
                .padding(.bottom, 24)
                .transition(.opacity.combined(with: .move(edge: .bottom)))
            }
        }
        .animation(.easeInOut(duration: 0.3), value: cameraMode)
        .onChange(of: whiteboardSurface) { _, _ in renderWhiteboardTexture() }
        .onChange(of: likeCount) { _, _ in renderWhiteboardTexture() }
        .onChange(of: isLiked) { _, _ in renderWhiteboardTexture() }
        .onChange(of: cameraMode) { _, new in
            if new == .whiteboard { renderWhiteboardTexture() }  // 최신 책 개수 반영
        }
        .animation(.easeInOut(duration: 0.25), value: isEditingOrientation)
        .animation(.easeInOut(duration: 0.2), value: selectedSectionForAlignment)
        .overlay(alignment: .bottomLeading) {
            if interactionState.isViewingBook {
                Button(action: {
                    interactionState.dismissBook(state: bookshelfState)
                }) {
                    HStack(spacing: 6) {
                        Image(systemName: "chevron.left")
                            .font(.system(size: 14, weight: .semibold))
                        Text("뒤로")
                            .font(.system(size: 14, weight: .medium))
                    }
                    .padding(.horizontal, 16)
                    .padding(.vertical, 10)
                    .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 10))
                }
                .buttonStyle(.plain)
                .padding(20)
                .transition(.opacity.combined(with: .move(edge: .bottom)))
            }
        }
        .animation(.easeInOut(duration: 0.3), value: interactionState.isViewingBook)
        #if os(macOS)
        .frame(minWidth: 800, minHeight: 600)
        #endif
        .background {
            // 따뜻한 스튜디오 느낌 그라데이션 배경
            LinearGradient(
                colors: [
                    Color(red: 0.95, green: 0.91, blue: 0.85),
                    Color(red: 0.88, green: 0.83, blue: 0.76),
                    Color(red: 0.78, green: 0.72, blue: 0.65)
                ],
                startPoint: .top,
                endPoint: .bottom
            )
            .ignoresSafeArea()
        }
        #if os(macOS)
        .background {
            ScrollZoomCaptureView { delta in
                guard let camera = cameraEntity else { return }
                guard !interactionState.isViewingBook else { return }
                guard cameraMode == .frontal else { return }  // 오버뷰에서는 줌 비활성
                CameraController.zoom(camera: camera, delta: -delta, targetZ: bookshelfZ)
            }
        }
        #endif
    }

    // MARK: - "배치 변경" 버튼 (책 확대 상태에서 표시)
    private var changePlacementButton: some View {
        Button(action: {
            guard let book = interactionState.viewedBook else { return }
            let orient = book.components[BookComponent.self]?.orientation ?? .spine
            editingBook = book
            editingBookOriginalOrientation = orient
            stagedOrientation = orient
            interactionState.dismissBook(state: bookshelfState)
            isEditingOrientation = true
        }) {
            HStack(spacing: 6) {
                Image(systemName: "rotate.3d")
                    .font(.system(size: 13, weight: .semibold))
                Text("배치 변경")
                    .font(.system(size: 13, weight: .medium))
            }
            .padding(.horizontal, 16)
            .padding(.vertical, 10)
            .background(.ultraThinMaterial, in: Capsule())
        }
        .buttonStyle(.plain)
    }

    // MARK: - 배치 편집 UI (옵션 + 확인/취소) — 책 근처에 플로팅
    private var orientationEditor: some View {
        VStack(spacing: 10) {
            HStack(spacing: 8) {
                orientationButton(label: "측면", systemImage: "rectangle.portrait", orientation: .spine)
                orientationButton(label: "45°", systemImage: "rectangle.portrait.righthalf.inset.filled", orientation: .angled45)
                orientationButton(label: "정면", systemImage: "rectangle.portrait.fill", orientation: .front)
            }
            .padding(.horizontal, 12)
            .padding(.vertical, 8)
            .background(.ultraThinMaterial, in: Capsule())

            HStack(spacing: 10) {
                Button(action: cancelEditing) {
                    Text("취소")
                        .font(.system(size: 13, weight: .medium))
                        .padding(.horizontal, 18)
                        .padding(.vertical, 8)
                        .background(.ultraThinMaterial, in: Capsule())
                }
                .buttonStyle(.plain)

                Button(action: confirmEditing) {
                    Text("확인")
                        .font(.system(size: 13, weight: .semibold))
                        .foregroundStyle(.white)
                        .padding(.horizontal, 18)
                        .padding(.vertical, 8)
                        .background(Color.accentColor, in: Capsule())
                }
                .buttonStyle(.plain)
            }
        }
    }

    private func orientationButton(label: String, systemImage: String, orientation: PlacementOrientation) -> some View {
        let isSelected = stagedOrientation == orientation
        return Button(action: {
            stagedOrientation = orientation
            previewOrientation(orientation)
        }) {
            HStack(spacing: 4) {
                Image(systemName: systemImage)
                    .font(.system(size: 12))
                Text(label)
                    .font(.system(size: 13, weight: .medium))
            }
            .padding(.horizontal, 10)
            .padding(.vertical, 6)
            .background(
                Capsule().fill(isSelected ? Color.accentColor.opacity(0.4) : Color.clear)
            )
        }
        .buttonStyle(.plain)
    }

    /// 프리뷰: BookComponent 업데이트 + 해당 섹션만 re-layout (푸시 없음, 오버플로우는 시각적으로 표시)
    private func previewOrientation(_ orientation: PlacementOrientation) {
        guard let book = editingBook else { return }
        guard var comp = book.components[BookComponent.self] else { return }
        comp.orientation = orientation
        book.components.set(comp)

        if let secIdx = bookshelfState.sections.firstIndex(where: { $0.books.contains(where: { $0 === book }) }) {
            bookshelfState.layoutSection(secIdx, duration: 0.25)
        }
    }

    private func confirmEditing() {
        guard let book = editingBook else { return }
        // stagedOrientation이 이미 프리뷰로 적용되어 있음 — changeOrientation은 푸시 + 최종 레이아웃
        bookshelfState.changeOrientation(of: book, to: stagedOrientation)
        clearEditing()
    }

    private func cancelEditing() {
        // BookComponent를 원래 방향으로 되돌리고 레이아웃 원상복구
        previewOrientation(editingBookOriginalOrientation)
        clearEditing()
    }

    private func clearEditing() {
        editingBook = nil
        isEditingOrientation = false
    }

    // MARK: - 광고 책 닫기
    private func dismissAdBook(_ book: Entity) {
        // 현재 활성 책장의 섹션에서 제거
        for i in 0..<bookshelfState.sections.count {
            if let idx = bookshelfState.sections[i].books.firstIndex(where: { $0 === book }) {
                bookshelfState.sections[i].books.remove(at: idx)
                bookshelfState.layoutSection(i, duration: 0.2)
                break
            }
        }
        // 뷰잉 종료 → 씬에서 제거
        interactionState.dismissBook(state: bookshelfState)
        book.removeFromParent()
    }

    // 모든 책장 통틀어 꽂혀있는 책 총 개수 (방명록 대시보드용)
    private var totalBookCount: Int {
        bookshelfState.bookshelves.reduce(0) { total, shelf in
            total + shelf.sections.reduce(0) { $0 + $1.books.count }
        }
    }

    // MARK: - 좋아요
    private func toggleLike() {
        if isLiked {
            isLiked = false
            likeCount = max(0, likeCount - 1)
        } else {
            isLiked = true
            likeCount += 1
        }
    }

    // MARK: - 화이트보드 대시보드를 SwiftUI → 이미지 → 텍스처로 변환해 표면에 적용
    private func renderWhiteboardTexture() {
        guard let surface = whiteboardSurface else { return }

        let dashboard = WhiteboardDashboard(
            bookCount: totalBookCount,
            likeCount: likeCount,
            isLiked: isLiked
        )
        let renderer = ImageRenderer(content: dashboard)
        renderer.scale = 3.0   // 해상도 업 (3600×2250) → 텍스트 선명도 대폭 향상
        renderer.isOpaque = true

        guard let cgImage = renderer.cgImage else { return }

        Task { @MainActor in
            do {
                let texture = try await TextureResource(
                    image: cgImage,
                    options: TextureResource.CreateOptions(semantic: .color)
                )
                // 밝기 유지 — emissive 2.5, baseColor texture(디테일 기여)
                var material = PhysicallyBasedMaterial()
                material.baseColor = .init(tint: .white, texture: .init(texture))
                material.emissiveColor = .init(texture: .init(texture))
                material.emissiveIntensity = 2.5
                material.roughness = .init(floatLiteral: 1.0)
                material.metallic = .init(floatLiteral: 0.0)
                material.specular = .init(floatLiteral: 0.0)
                material.blending = .opaque
                surface.model?.materials = [material]
            } catch {
                print("⚠️ 화이트보드 텍스처 생성 실패: \(error)")
            }
        }
    }

    // MARK: - 책장 넘기기
    private func flipButton(direction: SwitchDirection) -> some View {
        Button(action: { flipBookshelf(direction: direction) }) {
            Image(systemName: direction == .left ? "chevron.left.circle.fill" : "chevron.right.circle.fill")
                .font(.system(size: 32))
                .foregroundStyle(.white.opacity(0.95), .black.opacity(0.4))
                .shadow(color: .black.opacity(0.3), radius: 4, y: 2)
        }
        .buttonStyle(.plain)
    }

    private func flipBookshelf(direction: SwitchDirection) {
        let count = bookshelfState.bookshelves.count
        guard count > 1 else { return }
        // 진행 중인 UI 상태 모두 해제
        selectedSectionForAlignment = nil
        if isEditingOrientation { cancelEditing() }

        bookshelfState.switchTo(direction: direction)
    }

    // MARK: - 섹션 정렬 UI
    @ViewBuilder
    private func alignmentPicker(for secIdx: Int) -> some View {
        let current = bookshelfState.sections[secIdx].alignment
        HStack(spacing: 6) {
            alignmentButton(.left, systemImage: "align.horizontal.left", label: "좌", current: current, secIdx: secIdx)
            alignmentButton(.center, systemImage: "align.horizontal.center", label: "중", current: current, secIdx: secIdx)
            alignmentButton(.right, systemImage: "align.horizontal.right", label: "우", current: current, secIdx: secIdx)
        }
        .padding(.horizontal, 10)
        .padding(.vertical, 6)
        .background(.ultraThinMaterial, in: Capsule())
    }

    private func alignmentButton(_ alignment: SectionAlignment, systemImage: String, label: String, current: SectionAlignment, secIdx: Int) -> some View {
        let isSelected = current == alignment
        return Button(action: {
            bookshelfState.setAlignment(alignment, for: secIdx)
            selectedSectionForAlignment = nil
        }) {
            HStack(spacing: 4) {
                Image(systemName: systemImage).font(.system(size: 12))
                Text(label).font(.system(size: 12, weight: .medium))
            }
            .padding(.horizontal, 8)
            .padding(.vertical, 4)
            .background(Capsule().fill(isSelected ? Color.accentColor.opacity(0.4) : Color.clear))
        }
        .buttonStyle(.plain)
    }

    // MARK: - 3D → 2D 스크린 좌표 투영
    /// 월드 좌표를 화면 좌표(포인트)로 투영. 카메라 뒤쪽이면 nil.
    static func projectWorldToScreen(
        worldPos: SIMD3<Float>,
        camera: PerspectiveCamera,
        viewSize: CGSize
    ) -> CGPoint? {
        guard viewSize.width > 0 && viewSize.height > 0 else { return nil }

        let viewMatrix = camera.transformMatrix(relativeTo: nil).inverse

        let fovRad = camera.camera.fieldOfViewInDegrees * Float.pi / 180
        let aspect = Float(viewSize.width / viewSize.height)
        let near: Float = 0.05
        let far: Float = 100
        let f = 1.0 / tan(fovRad / 2)

        let proj = float4x4(
            SIMD4<Float>(f / aspect, 0, 0, 0),
            SIMD4<Float>(0, f, 0, 0),
            SIMD4<Float>(0, 0, (far + near) / (near - far), -1),
            SIMD4<Float>(0, 0, (2 * far * near) / (near - far), 0)
        )

        let world4 = SIMD4<Float>(worldPos.x, worldPos.y, worldPos.z, 1)
        let view4 = viewMatrix * world4
        let clip4 = proj * view4

        guard clip4.w > 0.001 else { return nil }  // 뒤쪽이면 무시

        let ndcX = clip4.x / clip4.w
        let ndcY = clip4.y / clip4.w

        let screenX = CGFloat((ndcX + 1) / 2) * viewSize.width
        let screenY = CGFloat((1 - ndcY) / 2) * viewSize.height
        return CGPoint(x: screenX, y: screenY)
    }
}

// MARK: - macOS 스크롤 줌
#if os(macOS)
struct ScrollZoomCaptureView: NSViewRepresentable {
    let onScroll: (Float) -> Void

    func makeNSView(context: Context) -> PassthroughScrollView {
        let view = PassthroughScrollView()
        view.onScroll = onScroll
        return view
    }

    func updateNSView(_ nsView: PassthroughScrollView, context: Context) {
        nsView.onScroll = onScroll
    }
}

class PassthroughScrollView: NSView {
    var onScroll: ((Float) -> Void)?
    private var scrollMonitor: Any?

    override func viewDidMoveToWindow() {
        super.viewDidMoveToWindow()
        if let window, scrollMonitor == nil {
            scrollMonitor = NSEvent.addLocalMonitorForEvents(matching: .scrollWheel) { [weak self] event in
                guard let self else { return event }
                if window === event.window {
                    let delta = Float(event.deltaY) * 0.05
                    self.onScroll?(delta)
                }
                return event
            }
        }
    }

    override func removeFromSuperview() {
        if let monitor = scrollMonitor {
            NSEvent.removeMonitor(monitor)
            scrollMonitor = nil
        }
        super.removeFromSuperview()
    }

    override func hitTest(_ point: NSPoint) -> NSView? { nil }
}
#endif

#Preview {
    ContentView()
}
