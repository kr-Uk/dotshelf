import SwiftUI
import RealityKit
import BasicAssets

enum CameraViewMode {
    case overview    // 코너 3/4 구도
    case frontal     // 책장 정면
    case whiteboard  // 화이트보드 정면
    case debug       // 창 바깥에서 통창 통해 내부 확인
    case interior    // 방 안쪽 모서리에서 내부 전체 조감
}

struct ContentView: View {
    /// 루트에서 주입되는 세션. 로그아웃/탈퇴 시 Settings → Auth 연결.
    var auth: AuthStore = AuthStore()

    /// 책장을 뒷벽에 붙인 위치 (뒷벽 z=-1.0, 책장 깊이 0.25, 뒷판 위치 -0.122, 5mm 간격)
    private let bookshelfZ: Float = -0.873

    @State private var cameraEntity: PerspectiveCamera?
    @State private var cameraMode: CameraViewMode = .overview
    @State private var bookshelfState = BookshelfState()
    @State private var interactionState = BookInteractionState()
    @State private var recordStore = BookRecordStore()
    @State private var archiveStore = ArchiveStore()

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
    // 동일 내용 재렌더 방지용 캐시 키
    @State private var lastWhiteboardKey: String?

    // 페이즈 D — 방명록/프로필 편집 오버레이 상태
    @State private var guestbookStore = GuestbookStore()
    @State private var isGuestbookComposerOpen = false
    @State private var replyTargetEntry: GuestbookEntry?
    @State private var isBioEditorOpen = false
    @State private var isDashboardDetailOpen = false
    @State private var dashboardInitialTab: DashboardFocusOverlay.Tab = .summary

    // 페이즈 J — 설정 오버레이
    @State private var settingsStore = SettingsStore()
    @State private var isSettingsOpen = false

    // 페이즈 F — 노트북 포커싱
    @State private var marketStore = MarketStore()
    @State private var isLaptopOpen = false

    // 페이즈 D/F 공통 — 신고 모달
    @State private var reportTarget: ReportTarget?

    /// 현재 뷰어의 공간 권한. MVP는 "방문자 모드 토글" 기반 디버그.
    /// E 에이전트의 `isVisitorMode`/`isFollowingSpace`와 AuthStore를 조합.
    var permission: SpacePermission {
        SpacePermissionContext.debug(
            auth: auth,
            isVisitorMode: isVisitorMode,
            isFollowingSpace: isFollowingSpace
        ).permission
    }

    // 페이즈 B — 독서기록 2D 오버레이
    @State private var isReadingRecordOpen = false
    @State private var readingRecordBookName: String?

    // 페이즈 C — 책장 편집 오버레이
    @State private var isBookshelfEditOpen = false
    @State private var isBookSearchOpen = false
    @State private var isArchiveOpen = false
    @State private var isReorderOpen = false
    @State private var isDecorationGalleryOpen = false

    // 페이즈 E — 쿼터뷰 사회적 액션
    /// 디버그용 — 주인/방문자 시점 토글. 실제 서비스에선 로그인한 사용자와 공간 주인의 일치 여부로 판정.
    @State private var isVisitorMode: Bool = false
    @State private var isFollowingSpace: Bool = false
    @State private var hasLikedSpace: Bool = false
    @State private var spaceLikeCount: Int = 128

    // 페이즈 G — 공간 편집 서랍
    @State private var roomEditStore = RoomEditStore()
    @State private var isRoomEditDrawerOpen: Bool = false
    /// 벽 material tint 갱신용 엔티티 참조 (RealityView make에서 수집).
    @State private var wallEntities: [ModelEntity] = []
    /// 조명 프리셋 적용용 (configureLighting에서 반환).
    @State private var lightingHandles: LightingHandles?
    /// 방 배치 꾸미기 — 씬에 추가된 데코 오브젝트 저장소.
    @State private var roomDecorStore = RoomDecorStore()
    /// 모든 roomdeco_ 엔티티가 들어갈 월드-레벨 컨테이너.
    @State private var decorContainer: Entity?
    /// 드래그 시작 시 원점 — end 시 스냅 기준.
    @State private var decorDragStart: SIMD3<Float>?

    // 정면 뷰에서 카메라 팬 드래그 시작점
    @State private var cameraPanStart: SIMD3<Float>?

    var body: some View {
        sceneCore
            .overlay(alignment: .topLeading) { debugStatusBadge }
            .overlay { flipButtonsLayer }
            .overlay(alignment: .topTrailing) { topTrailingControls }
            .overlay(alignment: .bottom) { bookActionBarLayer }
            .overlay(alignment: .trailing) { bookReadingPanelLayer }
            .overlay(alignment: .topTrailing) { adBookHeaderLayer }
            .overlay { orientationEditorLayer }
            .overlay { sectionAlignmentLayer }
            .overlay(alignment: .bottom) { whiteboardActionBarBlock }
            .overlay { dotshelfModalStack }
            .overlay(alignment: .bottomLeading) { viewedBookBackButton }
            .overlay { readingRecordLayer }
            .overlay(alignment: .topLeading) { bookshelfEditMenuLayer }
            .overlay { bookSearchOverlayView }
            .overlay { archiveOverlayView }
            .overlay { reorderOverlayView }
            .overlay { decorationGalleryOverlayView }
            .overlay(alignment: .topLeading) { quarterViewSocialHUD }
            .overlay(alignment: .bottom) { roomEditDrawerLayer }
            .onChange(of: roomEditStore.wallpaper) { _, new in applyWallpaperTint(new) }
            .onChange(of: roomEditStore.indoorLight) { _, new in applyIndoorLight(new) }
            .onChange(of: roomEditStore.outdoorLight) { _, new in applyOutdoorLight(new) }
            .animation(.easeInOut(duration: 0.28), value: isRoomEditDrawerOpen)
            .onChange(of: guestbookStore.entries.count) { _, _ in
                renderWhiteboardTexture()
                syncDashboardEntryHits()
            }
            .onChange(of: whiteboardSurface) { _, _ in
                syncDashboardEntryHits()
                renderWhiteboardTexture()
            }
            .onChange(of: guestbookStore.bioText) { _, _ in renderWhiteboardTexture() }
            .onChange(of: guestbookStore.listVisibility) { _, _ in renderWhiteboardTexture() }
            .onChange(of: likeCount) { _, _ in renderWhiteboardTexture() }
            .onChange(of: isLiked) { _, _ in renderWhiteboardTexture() }
            .onChange(of: cameraMode) { _, new in
                if new == .whiteboard { renderWhiteboardTexture() }
                if isEditingOrientation { cancelEditing() }
            }
            .animation(.easeInOut(duration: 0.2), value: isGuestbookComposerOpen)
            .animation(.easeInOut(duration: 0.2), value: isBioEditorOpen)
            .animation(.easeInOut(duration: 0.2), value: replyTargetEntry?.id)
            .animation(.easeInOut(duration: 0.2), value: isSettingsOpen)
            .animation(.easeInOut(duration: 0.3), value: cameraMode)
            .animation(.easeInOut(duration: 0.25), value: isEditingOrientation)
            .animation(.easeInOut(duration: 0.2), value: selectedSectionForAlignment)
            .animation(.easeInOut(duration: 0.22), value: isReadingRecordOpen)
            .animation(.easeInOut(duration: 0.22), value: isBookshelfEditOpen)
            .animation(.easeInOut(duration: 0.22), value: isBookSearchOpen)
            .animation(.easeInOut(duration: 0.22), value: isArchiveOpen)
            .animation(.easeInOut(duration: 0.3), value: interactionState.isViewingBook)
            .background {
                LinearGradient(
                    colors: [
                        Color(red: 0.95, green: 0.91, blue: 0.85),
                        Color(red: 0.88, green: 0.83, blue: 0.76),
                        Color(red: 0.78, green: 0.72, blue: 0.65)
                    ],
                    startPoint: .top, endPoint: .bottom
                )
                .ignoresSafeArea()
            }
            #if os(macOS)
            .frame(minWidth: 800, minHeight: 600)
            .background {
                ScrollZoomCaptureView { delta in
                    guard let camera = cameraEntity else { return }
                    guard !interactionState.isViewingBook else { return }
                    guard cameraMode == .frontal else { return }
                    CameraController.zoom(camera: camera, delta: -delta, targetZ: bookshelfZ)
                }
            }
            #endif
    }

    /// RealityView + gesture chain만 포함. type-check 부담 축소용 추출.
    private var sceneCore: some View {
        RealityView { content in
            // 방 환경 (바닥 + 벽)
            let room = RoomEnvironment.create()
            content.add(room)

            // 페이즈 G — 벽 엔티티 수집 (벽지 tint 실시간 적용용)
            collectWallEntities(from: room)

            // 페이즈 G — 배치 꾸미기 오브젝트가 들어갈 월드 컨테이너 (room 좌표와 동일)
            let decor = Entity()
            decor.name = "roomdeco_container"
            content.add(decor)
            decorContainer = decor

            // 화이트보드 표면 엔티티 참조 저장 (텍스처 업데이트용)
            if let surface = room.findEntity(named: "whiteboard_surface") as? ModelEntity {
                whiteboardSurface = surface
            }

            // 조명 전용 컨테이너 (책장이 전환될 때 조명은 고정)
            let lightContainer = Entity()
            lightContainer.position = SIMD3<Float>(0, 0, bookshelfZ)
            content.add(lightContainer)
            lightingHandles = EnvironmentSetup.configureLighting(root: lightContainer)

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

            // HDRI 환경맵: 야외 sky IBL. 0.8 ≈ 1.74× — 그림자 진 영역의 ambient fill 추가 강화.
            if let envResource = EnvironmentSetup.loadEnvironmentResource() {
                let iblEntity = Entity()
                let iblComponent = ImageBasedLightComponent(source: .single(envResource), intensityExponent: 0.8)
                iblEntity.components.set(iblComponent)
                iblEntity.components.set(ImageBasedLightReceiverComponent(imageBasedLight: iblEntity))
                content.add(iblEntity)
            }

            // 창문 밖에서 날아다니는 새 (그림자가 책장에 드리움)
            let bird = BirdEntity.create()
            BirdEntity.initializeFlight(on: bird)
            content.add(bird)

            let camera = CameraController.createOverviewCamera(bookshelfZ: bookshelfZ)
            content.add(camera)
            cameraEntity = camera

            // Reality Composer Pro(ShaderGraphMaterial) 유리 비동기 로드 후 교체.
            // 실패 시 폴백: 기존 PhysicallyBasedMaterial 유리 유지 (WindowEntity.createRightWall에서 이미 적용).
            Task { @MainActor in
                do {
                    let glassMat = try await ShaderGraphMaterial(
                        named: "/Root/GlassMaterial",
                        from: "Scene",
                        in: basicAssetsBundle
                    )
                    // 우측 통창 유리 + 앞벽 유리 모두 교체
                    // 유리가 햇빛을 차단하지 않도록 dynamic light shadow pass에서 제외.
                    for name in ["window_glass", "room_wall_front"] {
                        if let glass = room.findEntity(named: name) as? ModelEntity {
                            glass.model?.materials = [glassMat]
                            glass.components.set(DynamicLightShadowComponent(castsShadow: false))
                        }
                    }
                } catch {
                    print("⚠️ ShaderGraphMaterial 로드 실패: \(error)")
                }
            }
        }
        .simultaneousGesture(
            TapGesture()
                .targetedToAnyEntity()
                .onEnded { value in
                    let name = value.entity.name
                    // 방 배치 꾸미기 탭 → 선택 토글
                    if name.hasPrefix("roomdeco_") {
                        tapRoomDecor(value.entity)
                        return
                    }
                    // 책 탭 → 책 보기 (배치 편집 중이면 자동 취소 후 진입)
                    if name.hasPrefix("book_") {
                        guard !interactionState.isDragging else { return }
                        if isEditingOrientation { cancelEditing() }
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
                    // 노트북 탭 → §5 노트북 포커싱 2D 오버레이
                    if name == "laptop_hit" {
                        isLaptopOpen = true
                        return
                    }
                    // 화이트보드 프레임/표면 탭 → 화이트보드 뷰 전환 (하위 element hit은 아래에서 처리)
                    if name == "whiteboard_frame" || name == "whiteboard_surface" {
                        if let camera = cameraEntity {
                            CameraController.moveToWhiteboard(camera: camera)
                            cameraMode = .whiteboard
                        }
                    }
                    // 페이즈 D — 대시보드 element별 hit 박스 탭 (whiteboard 모드에서만 반응)
                    if cameraMode == .whiteboard {
                        handleDashboardHit(name: name)
                    }
                }
        )
        .simultaneousGesture(
            DragGesture()
                .targetedToAnyEntity()
                .onChanged { value in
                    let entity = value.entity
                    // 방 배치 꾸미기 드래그 분기
                    if entity.name.hasPrefix("roomdeco_") {
                        if decorDragStart == nil { beginDecorDrag(entity) }
                        updateDecorDrag(entity, screenTranslation: value.gestureValue.translation)
                        return
                    }
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
                    if entity.name.hasPrefix("roomdeco_") {
                        endDecorDrag(entity)
                        return
                    }
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
    }

    // MARK: - 책 확대 시 액션 (페이즈 A — §3 명세)

    private func startPlacementEditing() {
        guard let book = interactionState.viewedBook else { return }
        let orient = book.components[BookComponent.self]?.orientation ?? .spine
        editingBook = book
        editingBookOriginalOrientation = orient
        stagedOrientation = orient
        interactionState.dismissBook(state: bookshelfState)
        isEditingOrientation = true
    }

    /// 책장 변경: 책을 화면 앞(world-fixed dragLayer)에 유지한 채 target 책장으로 턴테이블 회전,
    /// 회전 완료 후 target 섹션 자리로 꽂히는 애니메이션 + 포커싱 해제.
    private func moveViewedBookToBookshelf(_ targetIdx: Int) {
        guard let book = interactionState.viewedBook else { return }
        let count = bookshelfState.bookshelves.count
        guard targetIdx >= 0, targetIdx < count, targetIdx != bookshelfState.currentIndex else { return }

        let bookComp = book.components[BookComponent.self]
        let footprint = bookComp?.footprint ?? 0.03

        // 1) 현재 책장 섹션에서 제거 (데이터만)
        var removedFromCurrent = false
        for i in 0..<bookshelfState.sections.count {
            if let idx = bookshelfState.sections[i].books.firstIndex(where: { $0 === book }) {
                bookshelfState.sections[i].books.remove(at: idx)
                bookshelfState.layoutSection(i, duration: 0.25)
                removedFromCurrent = true
                break
            }
        }
        guard removedFromCurrent else { return }

        // 2) target 책장의 fit 가능한 섹션 찾기 + 데이터 삽입
        var targetSections = bookshelfState.bookshelves[targetIdx].sections
        guard let targetSecIdx = targetSections.firstIndex(where: { $0.canFit(extraFootprint: footprint) }) else {
            print("⚠️ 타겟 책장 \(targetIdx + 1)에 공간 없음 — 원래 책장으로 복구")
            // 원래 책장의 fit 가능한 섹션에 다시 삽입
            for i in 0..<bookshelfState.sections.count where bookshelfState.sections[i].canFit(extraFootprint: footprint) {
                bookshelfState.sections[i].books.append(book)
                bookshelfState.layoutSection(i, duration: 0.3)
                break
            }
            return
        }
        targetSections[targetSecIdx].books.append(book)
        bookshelfState.bookshelves[targetIdx].sections = targetSections

        // 3) 회전 동안 책이 카메라 앞에 고정되도록 world-identity dragLayer로 재부모화
        if let dragLayer = bookshelfState.dragLayer {
            book.setParent(dragLayer, preservingWorldTransform: true)
            // viewingLocalTransform을 새 parent 기준으로 업데이트 (혹시 회전 입력이 들어올 때 안전)
            interactionState.viewingLocalTransform = book.transform
        }

        // 4) 최단 방향으로 회전 (다단계면 FIFO 큐잉)
        let forward = (targetIdx - bookshelfState.currentIndex + count) % count
        let backward = (bookshelfState.currentIndex - targetIdx + count) % count
        let direction: SwitchDirection = forward <= backward ? .right : .left
        let steps = min(forward, backward)
        for _ in 0..<steps {
            bookshelfState.switchTo(direction: direction)
        }

        // 5) 회전 완료 후 target 책장으로 재부모화 + 섹션 자리로 꽂히는 애니메이션 + 포커싱 해제
        let rotationSeconds: Double = Double(steps) * 1.0 + 0.05  // 각 flip 1초 + 소량 버퍼
        Task { @MainActor in
            try? await Task.sleep(for: .seconds(rotationSeconds))
            let targetEntity = bookshelfState.bookshelves[targetIdx].entity
            book.setParent(targetEntity, preservingWorldTransform: true)
            // dismissBook은 책이 속한 섹션을 찾아 layoutSection으로 제자리로 애니메이션 (0.4초)
            interactionState.dismissBook(state: bookshelfState)
        }
    }

    private func archiveViewedBook() {
        guard let book = interactionState.viewedBook else { return }
        // 엔티티 제거 전에 메타 데이터 사진 찍기
        _ = archiveStore.archiveBookEntity(book, using: recordStore)
        recordStore.update(book.name) { $0.isArchived = true }
        removeBookFromShelf(book)
        dismissViewingDirectly()
    }

    private func deleteViewedBook() {
        guard let book = interactionState.viewedBook else { return }
        recordStore.remove(book.name)
        removeBookFromShelf(book)
        dismissViewingDirectly()
    }

    private func openReadingRecordForViewedBook() {
        guard let name = interactionState.viewedBook?.name else { return }
        readingRecordBookName = name
        isReadingRecordOpen = true
    }

    private func removeBookFromShelf(_ book: Entity) {
        for bi in 0..<bookshelfState.bookshelves.count {
            for si in 0..<bookshelfState.bookshelves[bi].sections.count {
                if let idx = bookshelfState.bookshelves[bi].sections[si].books.firstIndex(where: { $0 === book }) {
                    bookshelfState.bookshelves[bi].sections[si].books.remove(at: idx)
                    if bi == bookshelfState.currentIndex, si < bookshelfState.sections.count {
                        bookshelfState.sections[si].books.removeAll { $0 === book }
                        bookshelfState.layoutSection(si, duration: 0.25)
                    }
                    break
                }
            }
        }
        book.removeFromParent()
    }

    private func dismissViewingDirectly() {
        interactionState.restoreShadowForViewedBook()
        interactionState.isViewingBook = false
        interactionState.viewedBook = nil
        interactionState.viewedBookOriginalTransform = nil
        interactionState.viewingLocalTransform = nil
    }

    // MARK: - 페이즈 C — 책장 편집 액션

    private func addBookshelfAction() {
        bookshelfState.addEmptyBookshelfAndSwitch()
    }

    private func removeBookshelfAction() {
        _ = bookshelfState.removeCurrentBookshelf { book in
            // tintColor가 있는 기본 박스 책만 보관함으로. 나머지는 그냥 제거.
            if archiveStore.archiveBookEntity(book, using: recordStore) {
                recordStore.update(book.name) { $0.isArchived = true }
            }
            book.removeFromParent()
        }
        isBookshelfEditOpen = false
    }

    /// 꾸미기 프리셋을 현재 책장의 첫 fit 섹션에 삽입.
    private func addDecoration(_ preset: DecorationPreset) -> Bool {
        let entity = BookEntity.createDecoration(kind: preset)
        return insertBookIntoCurrentShelf(entity, seedRecord: nil, bookName: entity.name)
    }

    // MARK: - 오버레이 뷰 (컴파일러 타입 추론 부담 완화를 위해 분리)

    @ViewBuilder
    private var bookSearchOverlayView: some View {
        if isBookSearchOpen {
            BookSearchOverlay(
                onAdd: { cb in addBookFromCatalog(cb) },
                onClose: { isBookSearchOpen = false }
            )
            .transition(.opacity.combined(with: .scale(scale: 0.97)))
        }
    }

    @ViewBuilder
    private var archiveOverlayView: some View {
        if isArchiveOpen {
            ArchiveView(
                store: archiveStore,
                onRestore: { item in restoreBookFromArchive(item) },
                onDeletePermanently: { item in archiveStore.remove(id: item.id) },
                onClose: { isArchiveOpen = false }
            )
            .transition(.opacity.combined(with: .scale(scale: 0.97)))
        }
    }

    @ViewBuilder
    private var reorderOverlayView: some View {
        if isReorderOpen {
            BookshelfReorderOverlay(
                state: bookshelfState,
                onMove: { from, to in bookshelfState.moveBookshelf(from: from, to: to) },
                onClose: { isReorderOpen = false }
            )
            .transition(.opacity.combined(with: .scale(scale: 0.97)))
        }
    }

    @ViewBuilder
    private var decorationGalleryOverlayView: some View {
        if isDecorationGalleryOpen {
            DecorationGalleryOverlay(
                onAdd: { preset in addDecoration(preset) },
                onClose: { isDecorationGalleryOpen = false }
            )
            .transition(.opacity.combined(with: .scale(scale: 0.97)))
        }
    }

    // MARK: - 페이즈 E — 쿼터뷰 사회적 액션 (팔로우/공간 좋아요/편집 진입)

    @ViewBuilder
    private var quarterViewSocialHUD: some View {
        if cameraMode == .overview
            && !interactionState.isViewingBook
            && !isSettingsOpen
            && !isLaptopOpen
            && !isDashboardDetailOpen
        {
            VStack(alignment: .leading, spacing: 10) {
                viewerModeTogglePill
                if isVisitorMode {
                    HStack(spacing: 8) {
                        followSpaceButton
                        spaceLikeButton
                    }
                } else {
                    editRoomButton
                }
            }
            .padding(.top, 54)   // 좌상단 debug 배지 아래
            .padding(.leading, 12)
            .transition(.opacity)
        }
    }

    private var viewerModeTogglePill: some View {
        Button {
            isVisitorMode.toggle()
        } label: {
            HStack(spacing: 5) {
                Image(systemName: isVisitorMode ? "person" : "crown.fill")
                    .font(.system(size: 11, weight: .bold))
                Text(isVisitorMode ? "방문자 시점" : "주인 시점")
                    .font(.system(size: 11, weight: .semibold))
            }
            .foregroundStyle(isVisitorMode ? Color.primary : Color.orange)
            .padding(.horizontal, 10)
            .padding(.vertical, 6)
            .background(.ultraThinMaterial, in: Capsule())
        }
        .buttonStyle(.plain)
        .help(isVisitorMode ? "내 공간을 내가 방문한 모습으로 미리보기" : "주인 시점 — 편집 가능")
    }

    private var followSpaceButton: some View {
        Button {
            isFollowingSpace.toggle()
        } label: {
            HStack(spacing: 5) {
                Image(systemName: isFollowingSpace ? "checkmark" : "plus")
                    .font(.system(size: 11, weight: .bold))
                Text(isFollowingSpace ? "팔로잉" : "팔로우")
                    .font(.system(size: 12, weight: .semibold))
            }
            .foregroundStyle(isFollowingSpace ? Color.primary : Color.white)
            .padding(.horizontal, 14)
            .padding(.vertical, 7)
            .background(
                Capsule().fill(isFollowingSpace ? Color.gray.opacity(0.2) : Color.accentColor)
            )
            .overlay(
                Capsule().strokeBorder(isFollowingSpace ? Color.primary.opacity(0.2) : .clear, lineWidth: 1)
            )
        }
        .buttonStyle(.plain)
    }

    private var spaceLikeButton: some View {
        Button {
            if hasLikedSpace {
                hasLikedSpace = false
                spaceLikeCount = max(0, spaceLikeCount - 1)
            } else {
                hasLikedSpace = true
                spaceLikeCount += 1
            }
        } label: {
            HStack(spacing: 5) {
                Image(systemName: hasLikedSpace ? "heart.fill" : "heart")
                    .font(.system(size: 12, weight: .bold))
                    .foregroundStyle(hasLikedSpace ? Color.red : Color.primary)
                Text("\(spaceLikeCount)")
                    .font(.system(size: 12, weight: .semibold))
            }
            .padding(.horizontal, 12)
            .padding(.vertical, 7)
            .background(.ultraThinMaterial, in: Capsule())
        }
        .buttonStyle(.plain)
        .help("공간 좋아요 — 대시보드에만 카운트 표시")
    }

    /// 주인 시점 — 공간 편집(페이즈 G) 서랍 열기.
    private var editRoomButton: some View {
        Button {
            isRoomEditDrawerOpen = true
        } label: {
            HStack(spacing: 5) {
                Image(systemName: "wand.and.stars")
                    .font(.system(size: 11, weight: .bold))
                Text("공간 편집")
                    .font(.system(size: 12, weight: .semibold))
            }
            .padding(.horizontal, 14)
            .padding(.vertical, 7)
            .background(.ultraThinMaterial, in: Capsule())
        }
        .buttonStyle(.plain)
        .help("공간 편집 서랍 (§1.1)")
    }

    @ViewBuilder
    private var roomEditDrawerLayer: some View {
        if isRoomEditDrawerOpen, cameraMode == .overview {
            RoomEditDrawer(
                store: roomEditStore,
                decorStore: roomDecorStore,
                onAddDecor: { kind in addRoomDecor(kind) },
                onRemoveDecor: { id in removeRoomDecor(id: id) },
                onClose: { isRoomEditDrawerOpen = false }
            )
            .transition(.move(edge: .bottom).combined(with: .opacity))
        }
    }

    // MARK: - 페이즈 G — 벽지 실시간 적용

    /// RealityView make 시점에 벽 엔티티를 수집해 `wallEntities`에 저장.
    func collectWallEntities(from root: Entity) {
        var found: [ModelEntity] = []
        collectWalls(from: root, into: &found)
        wallEntities = found
    }

    private func collectWalls(from entity: Entity, into out: inout [ModelEntity]) {
        let name = entity.name
        if name.hasPrefix("room_wall_") && !name.contains("front"),
           let me = entity as? ModelEntity {
            // 유리인 front 벽은 제외 — shader graph 처리 중
            out.append(me)
        }
        for child in entity.children {
            collectWalls(from: child, into: &out)
        }
    }

    // MARK: - 페이즈 G — 배치 꾸미기 추가/삭제/드래그

    /// 프리셋을 방 안 기본 위치에 추가. 같은 종류가 겹치지 않도록 카운트 기반 오프셋.
    private func addRoomDecor(_ kind: RoomDecorKind) {
        guard let container = decorContainer else { return }
        let entity = RoomDecorEntity.create(kind: kind)

        // 기본 배치 위치
        let base: SIMD3<Float>
        if kind.isWallMounted {
            // 좌측 벽(x=-1.98) 앞, 눈높이
            base = [-1.98, 1.40, 0.3]
            // 벽걸이는 좌측 벽을 향하도록 Y축 +90° 회전
            entity.orientation = simd_quatf(angle: .pi / 2, axis: [0, 1, 0])
        } else {
            // 공간1 바닥 (x=-1.2 근방, z=0.6)
            base = [-1.2, 0, 0.6]
        }

        // 기존 같은 종류 개수로 살짝 오프셋 (0.3m 간격)
        let sameKindCount = roomDecorStore.items.filter { $0.kind == kind }.count
        let offset: SIMD3<Float> = kind.isWallMounted
            ? [0, 0, Float(sameKindCount) * 0.35]
            : [Float(sameKindCount) * 0.3, 0, 0]
        let pos = base + offset

        entity.setPosition(pos, relativeTo: nil)
        container.addChild(entity)
        roomDecorStore.add(kind: kind, entity: entity, at: pos)
    }

    private func removeRoomDecor(id: UUID) {
        if let entity = roomDecorStore.remove(id: id) {
            entity.removeFromParent()
        }
    }

    /// 데코 탭 → 선택 상태 토글. 선택된 항목은 서랍에서 강조.
    private func tapRoomDecor(_ entity: Entity) {
        if let item = roomDecorStore.item(for: entity) {
            roomDecorStore.selectedId = (roomDecorStore.selectedId == item.id) ? nil : item.id
        }
    }

    /// 드래그: 바닥 오브젝트는 X/Z, 벽걸이는 Y/Z. 드래그 종료 시 0.1m 스냅.
    private func beginDecorDrag(_ entity: Entity) {
        guard roomDecorStore.item(for: entity) != nil else { return }
        decorDragStart = entity.position(relativeTo: nil)
    }

    private func updateDecorDrag(_ entity: Entity, screenTranslation: CGSize) {
        guard let start = decorDragStart,
              let item = roomDecorStore.item(for: entity) else { return }
        let s: Float = 0.005  // 스크린 px → 월드 m
        let dx = Float(screenTranslation.width) * s
        let dy = Float(-screenTranslation.height) * s

        var newPos = start
        if item.kind.isWallMounted {
            // 벽걸이 — 벽 평면(Z축 드래그 + Y축 올리기)
            newPos.z = start.z + dx
            newPos.y = max(0.2, start.y + dy)
        } else {
            // 바닥 — X/Z 평면
            newPos.x = clamp(start.x + dx, min: -1.9, max: 1.9)
            newPos.z = clamp(start.z + dy, min: -0.9, max: 1.9)
        }
        entity.setPosition(newPos, relativeTo: nil)
    }

    private func endDecorDrag(_ entity: Entity) {
        guard let item = roomDecorStore.item(for: entity) else {
            decorDragStart = nil; return
        }
        // 0.1m 스냅
        var p = entity.position(relativeTo: nil)
        p.x = snapTo(p.x, step: 0.1)
        p.z = snapTo(p.z, step: 0.1)
        if item.kind.isWallMounted {
            p.y = snapTo(p.y, step: 0.1)
        }
        entity.setPosition(p, relativeTo: nil)
        roomDecorStore.updatePosition(id: item.id, to: p)
        decorDragStart = nil
    }

    private func snapTo(_ v: Float, step: Float) -> Float {
        (v / step).rounded() * step
    }

    private func clamp(_ v: Float, min lo: Float, max hi: Float) -> Float {
        Swift.max(lo, Swift.min(hi, v))
    }

    /// 내부 조명(램프) 프리셋 실시간 적용.
    func applyIndoorLight(_ preset: IndoorLightPreset) {
        guard let lh = lightingHandles else { return }
        let c = preset.lampColor
        let color = PlatformColor(
            red: CGFloat(c.r), green: CGFloat(c.g), blue: CGFloat(c.b), alpha: 1
        )
        let scale = Float(preset.intensityScale)
        lh.lamp1.light.color = color
        lh.lamp2.light.color = color
        lh.lamp1.light.intensity = LightingHandles.baseLamp1Intensity * scale
        lh.lamp2.light.intensity = LightingHandles.baseLamp2Intensity * scale
    }

    /// 외부 조명(태양광) 프리셋 실시간 적용.
    func applyOutdoorLight(_ preset: OutdoorLightPreset) {
        guard let lh = lightingHandles else { return }
        let c = preset.sunColor
        let color = PlatformColor(
            red: CGFloat(c.r), green: CGFloat(c.g), blue: CGFloat(c.b), alpha: 1
        )
        let scale = Float(preset.intensityScale)
        lh.sunLight.light.color = color
        lh.sunRay.light.color = color
        lh.sunLight.light.intensity = LightingHandles.baseSunIntensity * scale
        lh.sunRay.light.intensity = LightingHandles.baseSunRayIntensity * scale
    }

    /// 벽지 변경 시 수집된 벽 엔티티들의 material tint를 즉시 갱신.
    func applyWallpaperTint(_ preset: WallpaperPreset) {
        let t = preset.tint
        let color = PlatformColor(
            red: CGFloat(t.r), green: CGFloat(t.g), blue: CGFloat(t.b), alpha: 1
        )
        for wall in wallEntities {
            guard var mats = wall.model?.materials else { continue }
            for i in 0..<mats.count {
                if var pbr = mats[i] as? PhysicallyBasedMaterial {
                    // 기존 textureCoordinateTransform 등은 유지하고 tint만 교체
                    if let existing = pbr.baseColor.texture {
                        pbr.baseColor = .init(tint: color, texture: existing)
                    } else {
                        pbr.baseColor = .init(tint: color)
                    }
                    mats[i] = pbr
                }
            }
            wall.model?.materials = mats
        }
    }

    // MARK: - 책 추가/복구 공통 (스텝 4)

    /// 카탈로그의 책을 현재 책장의 첫 fit 섹션에 추가.
    private func addBookFromCatalog(_ cb: CatalogBook) -> Bool {
        let tint = bookPaletteColor(at: cb.paletteIndex)
        let bookName = "book_custom_\(UUID().uuidString.prefix(8))"
        let book = BookEntity.createWithFaces(
            thickness: cb.thickness,
            height: cb.height,
            width: 0.16,
            color: tint,
            record: cb.record
        )
        book.name = bookName
        return insertBookIntoCurrentShelf(book, seedRecord: cb.record, bookName: bookName)
    }

    /// 보관함 아이템을 메타데이터로 재생성해 현재 책장에 복구.
    private func restoreBookFromArchive(_ item: ArchiveStore.Item) -> Bool {
        let tint = PlatformColor(
            red: CGFloat(item.tintR),
            green: CGFloat(item.tintG),
            blue: CGFloat(item.tintB),
            alpha: CGFloat(item.tintA)
        )
        let rec = recordStore.record(for: item.bookName)
        let book = BookEntity.createWithFaces(
            thickness: item.thickness,
            height: item.height,
            width: item.width,
            color: tint,
            record: rec
        )
        book.name = item.bookName
        let ok = insertBookIntoCurrentShelf(book, seedRecord: nil, bookName: item.bookName)
        if ok {
            recordStore.update(item.bookName) { $0.isArchived = false }
            archiveStore.remove(id: item.id)
        }
        return ok
    }

    /// 생성된 책 엔티티를 현재 책장의 첫 fit 섹션에 삽입. 성공 여부 반환.
    private func insertBookIntoCurrentShelf(_ book: Entity, seedRecord: BookRecord?, bookName: String) -> Bool {
        let comp = book.components[BookComponent.self]
        let footprint = comp?.footprint ?? 0.03
        guard let secIdx = bookshelfState.sections.firstIndex(where: { $0.canFit(extraFootprint: footprint) }) else {
            return false
        }
        if let rec = seedRecord {
            recordStore.update(bookName) { $0 = rec }
        }
        bookshelfState.sections[secIdx].books.append(book)
        bookshelfState.shelfRoot?.addChild(book)
        bookshelfState.layoutSection(secIdx, duration: 0.3)
        return true
    }

    /// populateBooks와 동일한 팔레트 — 카탈로그 paletteIndex로 결정적 색 선택.
    private func bookPaletteColor(at idx: Int) -> PlatformColor {
        let palette: [PlatformColor] = [
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
        return palette[idx % palette.count]
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

    // MARK: - body 경량화용 분리 overlay 블록

    private var debugStatusBadge: some View {
        Text("glTF: \(BookEntity.scannedBookLoadStatus)")
            .font(.system(size: 12, design: .monospaced))
            .padding(8)
            .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 6))
            .padding(12)
    }

    @ViewBuilder
    private var viewedBookBackButton: some View {
        if interactionState.isViewingBook {
            Button(action: { interactionState.dismissBook(state: bookshelfState) }) {
                HStack(spacing: 6) {
                    Image(systemName: "chevron.left")
                        .font(.system(size: 14, weight: .semibold))
                    Text("뒤로")
                        .font(.system(size: 14, weight: .medium))
                }
                .padding(.horizontal, 16).padding(.vertical, 10)
                .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 10))
            }
            .buttonStyle(.plain)
            .padding(20)
            .transition(.opacity.combined(with: .move(edge: .bottom)))
        }
    }

    @ViewBuilder
    private var readingRecordLayer: some View {
        if isReadingRecordOpen, let name = readingRecordBookName {
            ReadingRecordView(
                bookName: name,
                store: recordStore,
                onDismiss: { isReadingRecordOpen = false }
            )
            .transition(.opacity.combined(with: .scale(scale: 0.97)))
        }
    }

    @ViewBuilder
    private var bookshelfEditMenuLayer: some View {
        if isBookshelfEditOpen, cameraMode == .frontal {
            BookshelfEditMenu(
                state: bookshelfState,
                archiveCount: archiveStore.items.count,
                onAddBookshelf: { addBookshelfAction() },
                onRemoveBookshelf: { removeBookshelfAction() },
                onAddBook: { isBookSearchOpen = true },
                onOpenArchive: { isArchiveOpen = true },
                onAddDecoration: { isDecorationGalleryOpen = true },
                onReorder: { isReorderOpen = true },
                onClose: { isBookshelfEditOpen = false }
            )
            .padding(.top, 20).padding(.leading, 20)
            .transition(.opacity.combined(with: .move(edge: .leading)))
        }
    }


    @ViewBuilder
    private var flipButtonsLayer: some View {
        if bookshelfState.bookshelves.count > 1,
           !interactionState.isViewingBook, !isEditingOrientation,
           cameraMode != .whiteboard,
           let camera = cameraEntity {
            GeometryReader { geo in
                TimelineView(.animation) { _ in
                    let leftWorld = SIMD3<Float>(-0.95, 0.8, bookshelfZ)
                    let rightWorld = SIMD3<Float>(0.95, 0.8, bookshelfZ)
                    let leftScreen = Self.projectWorldToScreen(worldPos: leftWorld, camera: camera, viewSize: geo.size)
                    let rightScreen = Self.projectWorldToScreen(worldPos: rightWorld, camera: camera, viewSize: geo.size)
                    if let p = leftScreen {
                        flipButton(direction: .left).position(x: p.x, y: p.y)
                    }
                    if let p = rightScreen {
                        flipButton(direction: .right).position(x: p.x, y: p.y)
                    }
                }
            }
        }
    }

    @ViewBuilder
    private var bookActionBarLayer: some View {
        if interactionState.isViewingBook && !isEditingOrientation,
           let viewedBook = interactionState.viewedBook,
           !viewedBook.name.contains("_ad") {
            BookActionBar(
                bookName: viewedBook.name,
                store: recordStore,
                bookshelfNames: (0..<bookshelfState.bookshelves.count).map { "책장 \($0 + 1)" },
                currentBookshelfIndex: bookshelfState.currentIndex,
                onChangePlacement: { startPlacementEditing() },
                onChangeBookshelf: { targetIdx in moveViewedBookToBookshelf(targetIdx) },
                onArchive: { archiveViewedBook() },
                onDelete: { deleteViewedBook() },
                onOpenReadingRecord: { openReadingRecordForViewedBook() }
            )
            .padding(.bottom, 24)
            .transition(.opacity.combined(with: .move(edge: .bottom)))
        }
    }

    @ViewBuilder
    private var bookReadingPanelLayer: some View {
        if interactionState.isViewingBook && !isEditingOrientation,
           let viewedBook = interactionState.viewedBook,
           !viewedBook.name.contains("_ad") {
            BookReadingPanel(
                bookName: viewedBook.name,
                store: recordStore,
                onOpenReadingRecord: { openReadingRecordForViewedBook() }
            )
            .padding(.trailing, 24)
            .transition(.opacity.combined(with: .move(edge: .trailing)))
        }
    }

    @ViewBuilder
    private var adBookHeaderLayer: some View {
        if interactionState.isViewingBook,
           let viewedBook = interactionState.viewedBook,
           viewedBook.name.contains("_ad") {
            HStack(spacing: 12) {
                Text("AD")
                    .font(.system(size: 12, weight: .black))
                    .foregroundStyle(.white)
                    .padding(.horizontal, 10).padding(.vertical, 5)
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

    @ViewBuilder
    private var orientationEditorLayer: some View {
        if isEditingOrientation, let editingBook, let camera = cameraEntity {
            GeometryReader { geo in
                TimelineView(.animation) { _ in
                    let worldPos = editingBook.position(relativeTo: nil)
                    let projected = Self.projectWorldToScreen(worldPos: worldPos, camera: camera, viewSize: geo.size)
                    let fallback = CGPoint(x: geo.size.width / 2, y: geo.size.height * 0.7)
                    let screenPos = projected ?? fallback
                    let uiY = min(screenPos.y + 140, geo.size.height - 70)
                    let uiX = min(max(screenPos.x, 140), geo.size.width - 140)
                    orientationEditor.position(x: uiX, y: uiY)
                }
            }
            .transition(.opacity)
        }
    }

    @ViewBuilder
    private var sectionAlignmentLayer: some View {
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
                    let projected = Self.projectWorldToScreen(worldPos: worldPos, camera: camera, viewSize: geo.size)
                    let fallback = CGPoint(x: geo.size.width / 2, y: geo.size.height / 2)
                    let screenPos = projected ?? fallback
                    let uiY = max(min(screenPos.y + 80, geo.size.height - 50), 50)
                    let uiX = min(max(screenPos.x, 100), geo.size.width - 100)
                    alignmentPicker(for: secIdx).position(x: uiX, y: uiY)
                }
            }
            .transition(.opacity)
        }
    }

    @ViewBuilder
    private var dashboardDetailLayer: some View {
        if cameraMode == .whiteboard, isDashboardDetailOpen {
            DashboardFocusOverlay(
                store: guestbookStore,
                totalBookCount: totalBookCount,
                likeCount: likeCount,
                isLiked: isLiked,
                initialTab: dashboardInitialTab,
                onClose: { isDashboardDetailOpen = false },
                onOpenComposer: { isGuestbookComposerOpen = true },
                onOpenBioEditor: { isBioEditorOpen = true },
                onReply: { entry in replyTargetEntry = entry },
                onToggleLike: toggleLike,
                onVisitUser: { _ in },
                onReport: { target in reportTarget = target }
            )
            .transition(.opacity.combined(with: .scale(scale: 0.98)))
        }
    }

    @ViewBuilder
    private var reportComposerLayer: some View {
        if let target = reportTarget {
            ReportComposer(
                settings: settingsStore,
                target: target,
                onClose: { reportTarget = nil }
            )
            .transition(.opacity.combined(with: .scale(scale: 0.96)))
        }
    }

    // MARK: - 상단 우측 컨트롤 (body 경량화용)
    @ViewBuilder
    private var topTrailingControls: some View {
        if cameraMode != .overview && !interactionState.isViewingBook {
            nonOverviewControls
        } else if cameraMode == .overview && !interactionState.isViewingBook {
            overviewControls
        }
    }

    private var nonOverviewControls: some View {
        HStack(spacing: 8) {
            if cameraMode == .frontal, !isEditingOrientation, !isBookshelfEditOpen {
                Button { isBookshelfEditOpen = true } label: {
                    HStack(spacing: 6) {
                        Image(systemName: "square.stack.3d.up")
                            .font(.system(size: 13, weight: .semibold))
                        Text("편집")
                            .font(.system(size: 13, weight: .medium))
                    }
                    .padding(.horizontal, 14).padding(.vertical, 8)
                    .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 10))
                }
                .buttonStyle(.plain)
            }
            Button(action: {
                if let camera = cameraEntity {
                    CameraController.moveToOverview(camera: camera, bookshelfZ: bookshelfZ)
                    cameraMode = .overview
                    isBookshelfEditOpen = false
                }
            }) {
                HStack(spacing: 6) {
                    Image(systemName: "arrow.up.left.and.arrow.down.right")
                        .font(.system(size: 13, weight: .semibold))
                    Text("방 전체").font(.system(size: 13, weight: .medium))
                }
                .padding(.horizontal, 14).padding(.vertical, 8)
                .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 10))
            }
            .buttonStyle(.plain)
        }
        .padding(12)
        .transition(.opacity)
    }

    private var overviewControls: some View {
        VStack(spacing: 8) {
            Button(action: { isSettingsOpen = true }) {
                HStack(spacing: 6) {
                    Image(systemName: "gearshape.fill").font(.system(size: 14, weight: .semibold))
                    Text("설정").font(.system(size: 13, weight: .medium))
                }
                .padding(.horizontal, 14).padding(.vertical, 8)
                .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 10))
            }
            .buttonStyle(.plain)

            Button(action: {
                if let camera = cameraEntity {
                    CameraController.moveToDebug(camera: camera)
                    cameraMode = .debug
                }
            }) {
                HStack(spacing: 6) {
                    Image(systemName: "cube.transparent").font(.system(size: 13, weight: .semibold))
                    Text("디버그 뷰").font(.system(size: 13, weight: .medium))
                }
                .padding(.horizontal, 14).padding(.vertical, 8)
                .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 10))
            }
            .buttonStyle(.plain)

            Button(action: {
                if let camera = cameraEntity {
                    CameraController.moveToInteriorOverview(camera: camera)
                    cameraMode = .interior
                }
            }) {
                HStack(spacing: 6) {
                    Image(systemName: "house").font(.system(size: 13, weight: .semibold))
                    Text("실내 조감").font(.system(size: 13, weight: .medium))
                }
                .padding(.horizontal, 14).padding(.vertical, 8)
                .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 10))
            }
            .buttonStyle(.plain)
        }
        .padding(12)
        .transition(.opacity)
    }

    // MARK: - 페이즈 D: whiteboard 전용 overlay 블록 (body 경량화용)
    @ViewBuilder
    private var whiteboardActionBarBlock: some View {
        if cameraMode == .whiteboard {
            whiteboardActionBar
                .padding(.bottom, 24)
                .transition(.opacity.combined(with: .move(edge: .bottom)))
        }
    }

    @ViewBuilder
    private var whiteboardComposerBlock: some View {
        if cameraMode == .whiteboard, isGuestbookComposerOpen {
            GuestbookComposerOverlay(
                store: guestbookStore,
                onClose: { isGuestbookComposerOpen = false }
            )
            .transition(.opacity.combined(with: .scale(scale: 0.96)))
        }
    }

    @ViewBuilder
    private var whiteboardReplyBlock: some View {
        if cameraMode == .whiteboard, let entry = replyTargetEntry {
            GuestbookReplySheet(
                store: guestbookStore,
                entry: entry,
                onClose: { replyTargetEntry = nil }
            )
            .transition(.opacity.combined(with: .scale(scale: 0.96)))
        }
    }

    @ViewBuilder
    private var whiteboardBioEditorBlock: some View {
        if cameraMode == .whiteboard, isBioEditorOpen {
            ProfileBioEditor(
                store: guestbookStore,
                onClose: { isBioEditorOpen = false }
            )
            .transition(.opacity.combined(with: .scale(scale: 0.96)))
        }
    }

    // MARK: - 페이즈 D/F/J/신고/이전 공용 모달 스택
    /// 내가 담당한 모든 2D 풀 오버레이·시트를 한 자리에 모은다.
    /// body chain에 `.overlay { dotshelfModalStack }` 하나로만 걸어 type-check 부담 감소.
    /// 조건부 분기라 비활성 modal은 EmptyView가 되어 hit-test/렌더 비용 없음.
    @ViewBuilder
    private var dotshelfModalStack: some View {
        ZStack {
            settingsOverlayBlock
            laptopFocusLayer
            dashboardDetailLayer
            whiteboardComposerBlock
            whiteboardReplyBlock
            whiteboardBioEditorBlock
            reportComposerLayer
        }
    }

    // MARK: - 페이즈 F: 노트북 포커싱 오버레이 블록
    @ViewBuilder
    private var laptopFocusLayer: some View {
        if isLaptopOpen {
            LaptopFocusOverlay(
                market: marketStore,
                settings: settingsStore,
                onClose: { isLaptopOpen = false },
                onReport: { target in reportTarget = target }
            )
            .transition(.opacity.combined(with: .scale(scale: 0.98)))
        }
    }

    // MARK: - 페이즈 J: 설정 오버레이 블록
    @ViewBuilder
    private var settingsOverlayBlock: some View {
        if isSettingsOpen {
            SettingsOverlay(
                settings: settingsStore,
                guestbook: guestbookStore,
                onClose: { isSettingsOpen = false },
                onLogout: {
                    isSettingsOpen = false
                    auth.logout()
                },
                onLogin: {
                    isSettingsOpen = false
                    auth.logout()
                },
                onWithdraw: {
                    isSettingsOpen = false
                    auth.withdraw()
                }
            )
            .transition(.opacity.combined(with: .scale(scale: 0.98)))
        }
    }

    // MARK: - 페이즈 D: 대시보드 element 탭 핸들러
    private func handleDashboardHit(name: String) {
        switch name {
        case DashboardHitRegions.bioName:
            isBioEditorOpen = true
        case DashboardHitRegions.likeName:
            toggleLike()
        case DashboardHitRegions.booksName:
            dashboardInitialTab = .summary
            isDashboardDetailOpen = true
        case DashboardHitRegions.followersName:
            dashboardInitialTab = .followers
            isDashboardDetailOpen = true
        case DashboardHitRegions.followingName:
            dashboardInitialTab = .following
            isDashboardDetailOpen = true
        case DashboardHitRegions.guestbookHeaderName:
            dashboardInitialTab = .guestbook
            isDashboardDetailOpen = true
        default:
            // 개별 방명록 카드 탭 → 바로 답장 시트
            if let entryId = DashboardHitRegions.entryId(fromHitName: name),
               let entry = guestbookStore.entries.first(where: { $0.id == entryId }) {
                replyTargetEntry = entry
            }
        }
    }

    private func syncDashboardEntryHits() {
        guard let surface = whiteboardSurface else { return }
        DashboardHitRegions.syncEntryHits(
            on: surface,
            surfaceWidth: DashboardLayout.surfaceWidth,
            surfaceHeight: DashboardLayout.surfaceHeight,
            entryIds: guestbookStore.entries.map { $0.id }
        )
    }

    // MARK: - 페이즈 D: 화이트보드 뷰 액션바 + 최근 방명록 패널
    @ViewBuilder
    private var whiteboardActionBar: some View {
        HStack(spacing: 10) {
            Button(action: toggleLike) {
                HStack(spacing: 6) {
                    Image(systemName: isLiked ? "heart.fill" : "heart")
                        .font(.system(size: 14, weight: .semibold))
                        .foregroundStyle(isLiked ? .red : .primary)
                    Text(isLiked ? "좋아요 취소" : "좋아요")
                        .font(.system(size: 13, weight: .medium))
                }
                .padding(.horizontal, 14)
                .padding(.vertical, 10)
                .background(.ultraThinMaterial, in: Capsule())
            }
            .buttonStyle(.plain)

            Button(action: { isGuestbookComposerOpen = true }) {
                HStack(spacing: 6) {
                    Image(systemName: "square.and.pencil")
                        .font(.system(size: 13, weight: .semibold))
                    Text("방명록 남기기")
                        .font(.system(size: 13, weight: .medium))
                }
                .padding(.horizontal, 14)
                .padding(.vertical, 10)
                .background(.ultraThinMaterial, in: Capsule())
            }
            .buttonStyle(.plain)

            Button(action: { isBioEditorOpen = true }) {
                HStack(spacing: 6) {
                    Image(systemName: "person.text.rectangle")
                        .font(.system(size: 13, weight: .semibold))
                    Text("프로필 편집")
                        .font(.system(size: 13, weight: .medium))
                }
                .padding(.horizontal, 14)
                .padding(.vertical, 10)
                .background(.ultraThinMaterial, in: Capsule())
            }
            .buttonStyle(.plain)
        }
        .shadow(color: .black.opacity(0.15), radius: 6, y: 2)
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

        // onChange가 여러 소스에서 연속 트리거될 수 있음 — 내용 동일하면 재렌더 생략
        let entriesHash = guestbookStore.entries.reduce(into: 0) { acc, e in
            acc &+= e.id.hashValue &+ e.message.hashValue &+ e.replies.count
        }
        let key = "\(totalBookCount)-\(likeCount)-\(isLiked)-\(guestbookStore.bioText.hashValue)-\(guestbookStore.listVisibility.rawValue)-\(entriesHash)"
        if key == lastWhiteboardKey { return }
        lastWhiteboardKey = key

        let dashboard = WhiteboardDashboard(
            bookCount: totalBookCount,
            likeCount: likeCount,
            isLiked: isLiked,
            bioText: guestbookStore.bioText,
            entries: guestbookStore.entries,
            visibility: guestbookStore.listVisibility,
            followerCount: guestbookStore.visibleFollowers.count,
            followingCount: guestbookStore.visibleFollowing.count
        )
        let renderer = ImageRenderer(content: dashboard)
        renderer.scale = 2.0   // 2400×1500. 3.0은 32MB RGBA라 VRAM 부담 커서 낮춤.
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
