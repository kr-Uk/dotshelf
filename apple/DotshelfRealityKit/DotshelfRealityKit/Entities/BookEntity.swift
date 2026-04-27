import RealityKit
// PlatformColor는 PlatformTypes.swift에서 정의 (PlatformColor / UIColor)

/// 책이 책장에 꽂히는 방향
enum PlacementOrientation: CaseIterable {
    case spine       // 측면: 책등이 정면을 향함 (기본)
    case front       // 정면: 표지가 정면을 향함
    case angled45    // 45도: 정면과 측면 사이

    /// 책의 Y축 회전 (책 우측이 표지임을 가정 → 반대 방향으로 회전)
    var rotation: simd_quatf {
        switch self {
        case .spine:    return simd_quatf(angle: 0, axis: SIMD3<Float>(0, 1, 0))
        case .front:    return simd_quatf(angle: -.pi / 2, axis: SIMD3<Float>(0, 1, 0))
        case .angled45: return simd_quatf(angle: -.pi / 4, axis: SIMD3<Float>(0, 1, 0))
        }
    }

    /// 최악(컨텍스트 무시) footprint — canFit 계산과 drag preview용
    func shelfFootprint(thickness: Float, width: Float) -> Float {
        switch self {
        case .spine:    return thickness
        case .angled45: return (thickness + width) * 0.7071
        case .front:    return width + 0.02
        }
    }

    /// 한쪽 방향의 half-footprint (이웃 방향 고려)
    ///
    /// 두 -45° 회전 책이 겹치지 않는 최소 중심 거리는 `t * √2` (직사각형 분리축 정리):
    ///   Region 조건 x+z ∈ [±t√2/2]가 서로 만나지 않으려면 d ≥ t·√2
    /// 따라서 45°-45° 인접의 한쪽 half = t·√2/2 ≈ 0.7071·t.
    ///
    /// - 45°: 바로 옆이 45° → `t·√2/2` (타이트 패킹, 겹침 없음)
    ///   그 외 (벽/다른 각도) → `(t+w)·√2/4` (full bounding box half)
    /// - spine / front: 컨텍스트 무관
    func halfFootprint(thickness: Float, width: Float, neighbor: PlacementOrientation?) -> Float {
        switch self {
        case .spine:
            return thickness / 2
        case .front:
            return width / 2 + 0.01
        case .angled45:
            if neighbor == .angled45 {
                return thickness * 0.7071  // = t·√2/2
            } else {
                return (thickness + width) * 0.7071 / 2  // = (t+w)·√2/4
            }
        }
    }
}

/// 책장 내 꾸미기 오브젝트 프리셋 — 페이즈 C 스텝 5.
enum DecorationPreset: String, CaseIterable, Identifiable {
    case biplane
    case greenPot
    case blueCube
    case goldClock
    case coralFrame

    var id: String { rawValue }

    var label: String {
        switch self {
        case .biplane:    return "복엽기"
        case .greenPot:   return "화분"
        case .blueCube:   return "파란 큐브"
        case .goldClock:  return "황동 시계"
        case .coralFrame: return "액자"
        }
    }

    var systemImage: String {
        switch self {
        case .biplane:    return "airplane"
        case .greenPot:   return "leaf.fill"
        case .blueCube:   return "cube.fill"
        case .goldClock:  return "clock.fill"
        case .coralFrame: return "photo"
        }
    }

    /// 갤러리 미리보기용 컬러 (SwiftUI)
    var previewRGB: (Double, Double, Double) {
        switch self {
        case .biplane:    return (0.60, 0.55, 0.50)
        case .greenPot:   return (0.38, 0.58, 0.32)
        case .blueCube:   return (0.24, 0.42, 0.72)
        case .goldClock:  return (0.86, 0.70, 0.24)
        case .coralFrame: return (0.86, 0.48, 0.40)
        }
    }
}

struct BookComponent: Component {
    var thickness: Float
    var height: Float
    var width: Float
    var orientation: PlacementOrientation = .spine
    /// 보관함에서 복구할 때 동일한 색으로 재생성하기 위한 팔레트 tint.
    /// 스캔/biplane 같이 프로시저럴이 아닌 책은 nil — 보관 불가로 취급.
    var tintColor: PlatformColor?
    /// 책장 내 꾸미기 오브젝트 여부. true면 탭 시 "책 포커싱 뷰"로 진입하지 않고
    /// 별도 꾸미기 액션(45° 회전/보관함)으로 처리 (§2 명세).
    var isDecoration: Bool = false
    /// 카탈로그 원본 id — 중복 추가 방지용. BookSearchOverlay에서 이 id로 "이미 있음" 판정.
    var catalogId: UUID?
    /// 책장 내 꾸미기 오브젝트 전용 — 45° 단위 Y축 회전 스텝 0…7. 명세 §2.
    var yawSteps: Int = 0
    /// 책장 내 꾸미기 프리셋 rawValue — 보관함 복구 시 동일 엔티티 재생성용.
    /// `isDecoration == true`일 때만 의미가 있음.
    var decorationKind: String?

    /// 현재 방향에서 책장에 차지하는 가로 공간
    var footprint: Float {
        orientation.shelfFootprint(thickness: thickness, width: width)
    }
}

// TODO: 프로덕션에서는 generateBox() 대신 glTF/GLB 모델을 로드.
// Entity.load(named: "book.usdz") 또는 glTF 직접 로드(macOS 15+)로 교체 예정.
// glTF 모델에는 UV 전개된 6면 텍스처가 포함되어 크로스플랫폼(웹/Android/iOS) 공용으로 사용.
// 파이프라인: Object Capture/Blender → glTF/GLB → CDN → 각 플랫폼 로드
enum BookEntity {
    static func create(
        thickness: Float = 0.03,
        height: Float = 0.24,
        width: Float = 0.16,
        color: PlatformColor = .systemRed
    ) -> ModelEntity {
        let mesh = MeshResource.generateBox(
            width: thickness, height: height, depth: width,
            cornerRadius: 0.002
        )
        let material = MaterialFactory.bookMaterial(color: color)
        let entity = ModelEntity(mesh: mesh, materials: [material])
        entity.name = "book"

        entity.collision = CollisionComponent(
            shapes: [.generateBox(width: thickness, height: height, depth: width)]
        )
        entity.components.set(InputTargetComponent())
        entity.components.set(BookComponent(
            thickness: thickness,
            height: height,
            width: width,
            tintColor: color
        ))

        return entity
    }

    /// 광고용 특별 책. 금색 글로우 + 이름에 "_ad" 포함. 확대 후 X로 닫을 수 있음.
    /// - Note: 텍스트가 없는 단순 금색 박스. 호출 측에서 `BookFactory.make(from:)`로 대체 권장 —
    ///   팩토리 경로가 기본 책과 동일하게 표지·책등·후면 텍스트를 자동으로 입혀 주고,
    ///   `isAd: true` 플래그로 금색 emissive만 덧씌움.
    @available(*, deprecated, message: "Use BookFactory.make(from: BookSpec(isAd: true, ...))")
    static func createAdBook(
        thickness: Float = 0.045,
        height: Float = 0.26,
        width: Float = 0.17
    ) -> ModelEntity {
        let mesh = MeshResource.generateBox(
            width: thickness, height: height, depth: width,
            cornerRadius: 0.003
        )
        var material = PhysicallyBasedMaterial()
        material.baseColor = .init(tint: PlatformColor(red: 0.95, green: 0.78, blue: 0.20, alpha: 1.0))
        material.roughness = .init(floatLiteral: 0.2)
        material.metallic = .init(floatLiteral: 0.4)
        material.emissiveColor = .init(color: PlatformColor(red: 0.95, green: 0.78, blue: 0.20, alpha: 1.0))
        material.emissiveIntensity = 0.4
        let entity = ModelEntity(mesh: mesh, materials: [material])
        entity.name = "book_ad"
        entity.collision = CollisionComponent(
            shapes: [.generateBox(width: thickness, height: height, depth: width)]
        )
        entity.components.set(InputTargetComponent())
        entity.components.set(BookComponent(thickness: thickness, height: height, width: width))
        return entity
    }

    /// 기본 박스 책 + 표지/책등/후면 텍스트 plane 부착.
    /// `.spine` orientation 기준: +X=표지, -X=후면, +Z=책등(카메라를 향함).
    /// - Parameter isAd: true일 때 표지 좌측 상단에 "AD" 스티커 렌더.
    @MainActor
    static func createWithFaces(
        thickness: Float = 0.03,
        height: Float = 0.24,
        width: Float = 0.16,
        color: PlatformColor,
        record: BookRecord,
        isAd: Bool = false
    ) -> ModelEntity {
        let entity = create(thickness: thickness, height: height, width: width, color: color)
        attachFaces(
            to: entity, thickness: thickness, height: height, width: width,
            color: color, record: record, isAd: isAd
        )
        return entity
    }

    @MainActor
    private static func attachFaces(
        to entity: ModelEntity,
        thickness: Float,
        height: Float,
        width: Float,
        color: PlatformColor,
        record: BookRecord,
        isAd: Bool
    ) {
        let offset: Float = 0.0005  // z-fighting 방지

        // 표지 (+X) — plane normal +Z → Y축 +90° 회전으로 +X 향함
        if let mat = BookFaceRenderer.coverMaterial(record: record, bgColor: color, isAd: isAd) {
            let mesh = MeshResource.generatePlane(width: width, height: height)
            let plane = ModelEntity(mesh: mesh, materials: [mat])
            plane.position = SIMD3(thickness / 2 + offset, 0, 0)
            plane.orientation = simd_quatf(angle: .pi / 2, axis: SIMD3(0, 1, 0))
            entity.addChild(plane)
        }
        // 후면 (-X) — Y축 -90° 회전
        if let mat = BookFaceRenderer.backMaterial(record: record, bgColor: color) {
            let mesh = MeshResource.generatePlane(width: width, height: height)
            let plane = ModelEntity(mesh: mesh, materials: [mat])
            plane.position = SIMD3(-(thickness / 2 + offset), 0, 0)
            plane.orientation = simd_quatf(angle: -.pi / 2, axis: SIMD3(0, 1, 0))
            entity.addChild(plane)
        }
        // 책등 (+Z) — identity rotation, 크기 thickness × height
        if let mat = BookFaceRenderer.spineMaterial(record: record, bgColor: color) {
            let mesh = MeshResource.generatePlane(width: thickness, height: height)
            let plane = ModelEntity(mesh: mesh, materials: [mat])
            plane.position = SIMD3(0, 0, width / 2 + offset)
            entity.addChild(plane)
        }
    }

    // MARK: - 책장 내 꾸미기 오브젝트 (페이즈 C 스텝 5)

    /// 이름 prefix "deco_"로 탭 대상에서 제외되는 꾸미기 오브젝트.
    /// BookComponent를 가져 기존 섹션 시스템(정렬·이동 등)과 호환.
    @MainActor
    static func createDecoration(kind: DecorationPreset) -> Entity {
        let entity: Entity
        switch kind {
        case .biplane:
            if let b = loadBiplaneDecoration(targetSize: 0.12) {
                b.name = "deco_biplane_\(UUID().uuidString.prefix(6))"
                entity = b
            } else {
                entity = createDecorationBox(
                    color: PlatformColor.gray,
                    thickness: 0.05, height: 0.10, width: 0.10,
                    name: "deco_fallback_\(UUID().uuidString.prefix(6))"
                )
            }
        case .greenPot:
            entity = createDecorationBox(
                color: PlatformColor(red: 0.38, green: 0.58, blue: 0.32, alpha: 1),
                thickness: 0.08, height: 0.14, width: 0.08,
                name: "deco_pot_\(UUID().uuidString.prefix(6))"
            )
        case .blueCube:
            entity = createDecorationBox(
                color: PlatformColor(red: 0.24, green: 0.42, blue: 0.72, alpha: 1),
                thickness: 0.08, height: 0.08, width: 0.08,
                name: "deco_cube_\(UUID().uuidString.prefix(6))"
            )
        case .goldClock:
            entity = createDecorationBox(
                color: PlatformColor(red: 0.86, green: 0.70, blue: 0.24, alpha: 1),
                thickness: 0.04, height: 0.10, width: 0.10,
                name: "deco_clock_\(UUID().uuidString.prefix(6))"
            )
        case .coralFrame:
            entity = createDecorationBox(
                color: PlatformColor(red: 0.86, green: 0.48, blue: 0.40, alpha: 1),
                thickness: 0.02, height: 0.12, width: 0.10,
                name: "deco_frame_\(UUID().uuidString.prefix(6))"
            )
        }
        // 모든 경로에서 isDecoration + decorationKind 주입. 보관함 복구 시 재생성 키로 사용.
        if var comp = entity.components[BookComponent.self] {
            comp.isDecoration = true
            comp.decorationKind = kind.rawValue
            entity.components.set(comp)
        }
        return entity
    }

    private static func createDecorationBox(
        color: PlatformColor,
        thickness: Float,
        height: Float,
        width: Float,
        name: String
    ) -> ModelEntity {
        let mesh = MeshResource.generateBox(
            width: thickness, height: height, depth: width,
            cornerRadius: 0.005
        )
        var mat = PhysicallyBasedMaterial()
        mat.baseColor = .init(tint: color)
        mat.roughness = .init(floatLiteral: 0.5)
        mat.metallic = .init(floatLiteral: 0.0)
        let entity = ModelEntity(mesh: mesh, materials: [mat])
        entity.name = name
        entity.collision = CollisionComponent(
            shapes: [.generateBox(width: thickness, height: height, depth: width)]
        )
        entity.components.set(InputTargetComponent())
        entity.components.set(BookComponent(
            thickness: thickness, height: height, width: width,
            tintColor: color, isDecoration: true
            // decorationKind는 createDecoration 공통 분기에서 주입됨.
        ))
        return entity
    }

    static func createGhost(thickness: Float, height: Float, width: Float) -> ModelEntity {
        let mesh = MeshResource.generateBox(
            width: thickness, height: height, depth: width,
            cornerRadius: 0.002
        )
        let material = MaterialFactory.ghostMaterial()
        let entity = ModelEntity(mesh: mesh, materials: [material])
        entity.name = "ghost_preview"
        return entity
    }

    /// 스캔된 glTF 책 로드 상태 (디버깅용)
    static var scannedBookLoadStatus: String = "대기 중"

    /// 스캔된 USDZ 책을 로드.
    /// 프로덕션: 백엔드가 glTF/USDZ 둘 다 생성 → Apple 플랫폼은 USDZ, 웹/Android는 glTF 다운로드
    /// - Parameter targetHeight: 기존 책들과 어울리도록 스케일할 기준 높이 (기본 0.24m)
    /// - Returns: BookComponent가 붙은 ModelEntity (로드 실패 시 nil)
    static func loadScannedBook(targetHeight: Float = 0.24) -> Entity? {
        guard let url = Bundle.main.url(forResource: "testbook", withExtension: "usdz") else {
            scannedBookLoadStatus = "❌ 번들에서 testbook.usdz 못 찾음"
            return nil
        }

        let loaded: Entity
        do {
            loaded = try Entity.load(contentsOf: url)
            scannedBookLoadStatus = "✅ 로드 성공"
        } catch {
            scannedBookLoadStatus = "❌ 로드 실패: \(error.localizedDescription)"
            return nil
        }

        // 모델의 원래 바운딩 박스 측정
        let bounds = loaded.visualBounds(relativeTo: nil)
        let originalSize = bounds.extents  // (x, y, z)

        // y축(세로) 기준으로 타겟 높이에 맞춰 스케일 계산
        let scale = targetHeight / max(originalSize.y, 0.001)
        loaded.scale = SIMD3<Float>(repeating: scale)

        // 스케일 적용 후의 실제 크기
        let scaledSize = originalSize * scale
        // glTF 모델의 축 매핑: x=thickness, y=height, z=width(depth)으로 간주
        let thickness = scaledSize.x
        let height = scaledSize.y
        let width = scaledSize.z

        // 컨테이너 엔티티 생성 (BookComponent, Collision, Input 부착용)
        let container = Entity()
        container.name = "book_scanned"
        container.addChild(loaded)

        // 로드된 모델의 중심을 원점에 맞추기 위해 오프셋 보정
        let center = bounds.center * scale
        loaded.position = -center

        container.components.set(BookComponent(thickness: thickness, height: height, width: width))
        container.components.set(CollisionComponent(
            shapes: [.generateBox(width: thickness, height: height, depth: width)]
        ))
        container.components.set(InputTargetComponent())

        return container
    }

    /// 장식용 복엽기(toy biplane) 로드.
    /// 계층: container(scale=1, components) → scaler(scale=S) → loaded(원본 transform 그대로)
    /// - loaded의 transform을 건드리지 않아 USDZ 원본 transform 간섭 없음
    /// - 컨테이너에 scale=1 유지 → Collision/BookComponent가 월드 타겟 사이즈 그대로 반영
    static func loadBiplaneDecoration(targetSize: Float = 0.12) -> Entity? {
        guard let url = Bundle.main.url(forResource: "toy_biplane", withExtension: "usdz"),
              let loaded = try? Entity.load(contentsOf: url) else {
            return nil
        }

        let bounds = loaded.visualBounds(recursive: true, relativeTo: nil)
        let maxDim: Float = max(bounds.extents.x, bounds.extents.y, bounds.extents.z)
        let scale: Float = targetSize / max(maxDim, 0.001)

        // scaler: loaded의 scale만 전담
        let scaler = Entity()
        scaler.scale = SIMD3<Float>(repeating: scale)
        scaler.addChild(loaded)
        // loaded의 mesh 중심을 scaler 원점에 오도록 오프셋
        loaded.position = SIMD3<Float>(
            -bounds.center.x,
            -bounds.center.y,
            -bounds.center.z
        )

        // container: 논리적 크기 기준(scale=1) 컴포넌트 장착
        let container = Entity()
        container.name = "book_biplane"
        container.addChild(scaler)

        let scaledExtents = bounds.extents * scale
        container.components.set(BookComponent(
            thickness: scaledExtents.x,
            height: scaledExtents.y,
            width: scaledExtents.z,
            isDecoration: true
        ))
        container.components.set(CollisionComponent(
            shapes: [.generateBox(
                width: scaledExtents.x,
                height: scaledExtents.y,
                depth: scaledExtents.z
            )]
        ))
        container.components.set(InputTargetComponent())

        return container
    }
}

// MARK: - 책 엔티티 팩토리 (API 연동 대비 단일 진입점)

/// 스캔된 3D 책 모델 에셋의 출처.
/// 서버 응답에서 URL이 있으면 `.remoteURL`, 앱 번들 리소스라면 `.bundled`.
enum ScannedBookAsset {
    case bundled(resourceName: String, ext: String)
    case remoteURL(URL)
}

/// 서버 API 응답(또는 mock)에서 조립되는 책 생성 spec.
/// 호출 측은 DTO → BookSpec 매핑만 하면 되고, 3D vs 텍스트 분기는 `BookFactory.make`가 담당.
struct BookSpec {
    /// 엔티티 이름 (도메인 ID 매핑 키). 예: "book_\(serverId)", 광고는 "book_ad_\(slot)".
    var name: String
    /// 표지 텍스트·독서기록 도메인 데이터. BookRecordStore는 엔티티 이름으로 조회.
    var record: BookRecord
    /// 기본 박스 책일 때의 표지 색 (스캔 모델이면 무시됨).
    var tint: PlatformColor
    var thickness: Float = 0.035
    var height: Float = 0.24
    var width: Float = 0.16
    /// 3D 스캔 모델 에셋. nil이면 기본 박스 + 표지 텍스트 경로.
    /// - Note: API 연동 시 서버의 `scanned_model_url`이 있을 때만 set.
    var scannedAsset: ScannedBookAsset? = nil
    /// 광고 책 여부 — 표지에 "AD" 스티커 렌더. 이름 prefix는 `book_ad_*` 권장.
    var isAd: Bool = false
}

/// 모든 책(기본 박스 / 스캔 / 광고) 생성의 **단일 진입점**.
/// - 스캔 모델 로드 성공 → 그대로 반환 (현재 `isAd`일 때 스캔 모델 위 스티커는 미지원, 추후).
/// - 실패하거나 nil → 텍스트 표지·책등·후면이 붙은 기본 박스 경로 (`isAd` 시 표지에 AD 스티커).
///
/// API 연동 시 호출 측: `BookRepository.list() → [BookSpec] → BookFactory.make` 루프면 됩니다.
enum BookFactory {
    @MainActor
    static func make(from spec: BookSpec) -> Entity {
        // 1) 스캔 모델 우선 시도.
        if let asset = spec.scannedAsset,
           let scanned = loadScanned(asset: asset, targetHeight: spec.height) {
            scanned.name = spec.name
            // TODO: 스캔 모델용 AD 스티커 오버레이는 추후 (UV 위치 추정 필요).
            return scanned
        }

        // 2) Fallback: 텍스트 면(+표지/책등/후면) 있는 기본 박스.
        let entity = BookEntity.createWithFaces(
            thickness: spec.thickness,
            height: spec.height,
            width: spec.width,
            color: spec.tint,
            record: spec.record,
            isAd: spec.isAd
        )
        entity.name = spec.name
        return entity
    }

    @MainActor
    private static func loadScanned(asset: ScannedBookAsset, targetHeight: Float) -> Entity? {
        let url: URL?
        switch asset {
        case .bundled(let name, let ext):
            url = Bundle.main.url(forResource: name, withExtension: ext)
        case .remoteURL(let u):
            // MVP: `Entity.load(contentsOf:)`로 sync 로드 시도.
            // 프로덕션은 네트워크 레이어에서 사전 다운로드 → 번들 캐시 경로로 변환 권장.
            url = u
        }
        guard let resolved = url, let loaded = try? Entity.load(contentsOf: resolved) else {
            return nil
        }
        let bounds = loaded.visualBounds(relativeTo: nil)
        let scale = targetHeight / max(bounds.extents.y, 0.001)
        loaded.scale = SIMD3<Float>(repeating: scale)
        let scaledSize = bounds.extents * scale

        let container = Entity()
        container.addChild(loaded)
        loaded.position = -bounds.center * scale

        container.components.set(BookComponent(
            thickness: scaledSize.x,
            height: scaledSize.y,
            width: scaledSize.z
        ))
        container.components.set(CollisionComponent(
            shapes: [.generateBox(width: scaledSize.x, height: scaledSize.y, depth: scaledSize.z)]
        ))
        container.components.set(InputTargetComponent())

        return container
    }
}
