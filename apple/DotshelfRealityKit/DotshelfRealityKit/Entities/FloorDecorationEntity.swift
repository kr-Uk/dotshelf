import Foundation
import RealityKit

/// 바닥에 놓는 꾸미기 오브젝트(§1.1 "배치 꾸미기 — 바닥 놓기").
/// USDZ 에셋을 normalize scale로 맞춰 올리고 탭/드래그 가능한 collision + InputTarget 장착.
///
/// 엔티티 이름 규칙: `decoration_<UUID>` — ContentView tap/drag 핸들러에서 `decoration_` prefix로 분기.
enum FloorDecorationEntity {

    /// 바닥에 놓는 오브젝트의 기본 타겟 크기 (가장 긴 변 기준, m).
    /// 햄햄 인형 기준 0.35m ≈ 35cm 봉제 인형 느낌.
    static let defaultTargetSize: Float = 0.35

    /// assetKey: 번들에 있는 USDZ 리소스 이름 (확장자 제외).
    static func load(assetKey: String, id: UUID, targetSize: Float = defaultTargetSize) -> Entity? {
        guard let url = Bundle.main.url(forResource: assetKey, withExtension: "usdz"),
              let loaded = try? Entity.load(contentsOf: url) else {
            print("⚠️ FloorDecoration 로드 실패: \(assetKey).usdz")
            return nil
        }

        let bounds = loaded.visualBounds(recursive: true, relativeTo: nil)
        let maxDim = max(bounds.extents.x, bounds.extents.y, bounds.extents.z)
        let scale: Float = targetSize / max(maxDim, 0.001)

        // scaler: loaded만 스케일, loaded의 원 transform은 건드리지 않음 (USDZ 원본 보존)
        let scaler = Entity()
        scaler.scale = SIMD3<Float>(repeating: scale)
        scaler.addChild(loaded)
        // loaded의 mesh center를 scaler 원점으로 정렬 (바닥 접지는 container가 처리)
        loaded.position = SIMD3<Float>(
            -bounds.center.x,
            -bounds.center.y,
            -bounds.center.z
        )

        // container: 월드 타겟 사이즈 기준으로 collision/input 부착
        let container = Entity()
        container.name = "decoration_\(id.uuidString)"
        container.addChild(scaler)

        // container.position.y = 바닥(0)에 접지하도록 scaled height / 2 오프셋은 FloorDecorationStore가 세팅.
        // 여기서는 hit box만 세팅. 가로세로 모두 조금 여유 있게 해서 탭 편의성 확보.
        let scaledExtents = bounds.extents * scale
        container.components.set(CollisionComponent(shapes: [
            .generateBox(
                width: scaledExtents.x * 1.05,
                height: scaledExtents.y * 1.05,
                depth: scaledExtents.z * 1.05
            )
        ]))
        container.components.set(InputTargetComponent())
        container.components.set(FloorDecorationComponent(
            id: id,
            assetKey: assetKey,
            baseHeight: scaledExtents.y
        ))

        return container
    }

    /// 엔티티 이름 → UUID 복원 (tap/drag 핸들러에서).
    static func id(fromEntityName name: String) -> UUID? {
        let prefix = "decoration_"
        guard name.hasPrefix(prefix) else { return nil }
        return UUID(uuidString: String(name.dropFirst(prefix.count)))
    }

    /// 방 안쪽 바닥 경계. 방 치수(-2..+2 × -bookshelfZ..+floorSize/2)와 오브젝트 반경을 고려해 보수적 제한.
    static let floorBoundsX: ClosedRange<Float> = -1.80...1.80
    /// z 범위: 뒤쪽 alcove(책장) 앞 + 앞벽까지. 책장 alcove 안은 피하게.
    static let floorBoundsZ: ClosedRange<Float> = -0.60...1.40

    /// 0.1m 스냅 (명세 §1.1 "배치 꾸미기 드래그 + 0.1m 스냅 그리드").
    static let snapStep: Float = 0.1

    static func snap(_ v: Float) -> Float {
        (v / snapStep).rounded() * snapStep
    }

    /// 드래그 최종 좌표를 방 범위 + 0.1m 스냅으로 클램프.
    static func clampedSnap(_ world: SIMD3<Float>, baseHeight: Float) -> SIMD3<Float> {
        let x = min(max(world.x, floorBoundsX.lowerBound), floorBoundsX.upperBound)
        let z = min(max(world.z, floorBoundsZ.lowerBound), floorBoundsZ.upperBound)
        return SIMD3<Float>(snap(x), baseHeight / 2, snap(z))
    }
}

/// FloorDecoration 엔티티의 메타.
/// RealityKit의 ECS를 따르되 System은 없음 — 상호작용은 ContentView gesture에서 직접 처리.
struct FloorDecorationComponent: Component {
    let id: UUID
    let assetKey: String
    /// 접지용 — 바닥 y=0에 놓을 때 container.position.y = baseHeight / 2.
    let baseHeight: Float
}
