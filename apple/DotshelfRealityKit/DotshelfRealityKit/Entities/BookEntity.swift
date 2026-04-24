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

struct BookComponent: Component {
    var thickness: Float
    var height: Float
    var width: Float
    var orientation: PlacementOrientation = .spine

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
        entity.components.set(BookComponent(thickness: thickness, height: height, width: width))

        return entity
    }

    /// 광고용 특별 책. 금색 글로우 + 이름에 "_ad" 포함. 확대 후 X로 닫을 수 있음.
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
            width: scaledExtents.z
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
