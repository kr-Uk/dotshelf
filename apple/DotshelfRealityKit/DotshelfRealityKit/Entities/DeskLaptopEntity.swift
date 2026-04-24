import Foundation
import RealityKit

/// 좌측 벽(공간1)에 배치하는 책상 + 노트북 조합.
/// - 책상: 1.2×0.6×0.75m 원목 톤 박스 + 다리 4개
/// - 노트북: clamshell (베이스 + 비스듬히 세운 디스플레이). 디스플레이에 광고/화면보호기 emissive.
/// - 노트북을 탭하면 §5 노트북 포커싱 2D 오버레이로 진입. (이름 "laptop_hit")
enum DeskLaptopEntity {
    /// 책상 치수
    static let deskWidth: Float = 1.2
    static let deskDepth: Float = 0.55
    static let deskHeight: Float = 0.75
    static let deskThickness: Float = 0.04

    /// 좌측 벽 x에서 책상 중심까지의 거리 (벽을 등지고 배치).
    static let deskOffsetFromWall: Float = deskDepth / 2 + 0.02

    /// 책상 + 노트북을 방에 배치. wallX는 좌측 벽 x (음수, e.g. -2.0).
    static func create(wallX: Float) -> Entity {
        let root = Entity()
        root.name = "desk_laptop_group"

        let desk = makeDesk()
        root.addChild(desk)

        let laptop = makeLaptop()
        // 책상 윗면 중앙에 배치 (책상 local origin은 책상 중심에서 바닥 기준이 아닌 중심 기준)
        laptop.position = SIMD3<Float>(0, deskHeight + 0.005, 0)
        root.addChild(laptop)

        // 책상은 좌측 벽을 "등지고" 배치 → +x 방향을 바라봄.
        // 좌측 벽 x = wallX (음수). 책상 중심 x = wallX + deskOffsetFromWall.
        root.position = SIMD3<Float>(wallX + deskOffsetFromWall, 0, 0)
        // 책상의 긴 변을 z축으로 맞추기 위해 y축 +90° 회전.
        root.orientation = simd_quatf(angle: .pi / 2, axis: SIMD3<Float>(0, 1, 0))
        return root
    }

    // MARK: - Desk

    private static func makeDesk() -> Entity {
        let desk = Entity()
        desk.name = "desk"

        var topMat = PhysicallyBasedMaterial()
        topMat.baseColor = .init(tint: PlatformColor(red: 0.55, green: 0.40, blue: 0.28, alpha: 1.0))
        topMat.roughness = .init(floatLiteral: 0.55)
        topMat.metallic = .init(floatLiteral: 0.0)

        let top = ModelEntity(
            mesh: .generateBox(width: deskWidth, height: deskThickness, depth: deskDepth, cornerRadius: 0.008),
            materials: [topMat]
        )
        top.name = "desk_top"
        top.position = SIMD3<Float>(0, deskHeight - deskThickness / 2, 0)
        desk.addChild(top)

        var legMat = PhysicallyBasedMaterial()
        legMat.baseColor = .init(tint: PlatformColor(red: 0.28, green: 0.22, blue: 0.18, alpha: 1.0))
        legMat.roughness = .init(floatLiteral: 0.7)

        let legHeight = deskHeight - deskThickness
        let legSize: Float = 0.04
        let dx = deskWidth / 2 - 0.08
        let dz = deskDepth / 2 - 0.06
        let legY = legHeight / 2
        for (sx, sz) in [(-dx, -dz), (dx, -dz), (-dx, dz), (dx, dz)] {
            let leg = ModelEntity(
                mesh: .generateBox(width: legSize, height: legHeight, depth: legSize),
                materials: [legMat]
            )
            leg.position = SIMD3<Float>(sx, legY, sz)
            desk.addChild(leg)
        }
        return desk
    }

    // MARK: - Laptop (clamshell)

    private static func makeLaptop() -> Entity {
        let laptop = Entity()
        laptop.name = "laptop"

        let baseW: Float = 0.34
        let baseD: Float = 0.24
        let baseH: Float = 0.014

        var bodyMat = PhysicallyBasedMaterial()
        bodyMat.baseColor = .init(tint: PlatformColor(red: 0.70, green: 0.72, blue: 0.74, alpha: 1.0))
        bodyMat.roughness = .init(floatLiteral: 0.35)
        bodyMat.metallic = .init(floatLiteral: 0.7)

        // Base (키보드 부분)
        let base = ModelEntity(
            mesh: .generateBox(width: baseW, height: baseH, depth: baseD, cornerRadius: 0.005),
            materials: [bodyMat]
        )
        base.name = "laptop_base"
        base.position = SIMD3<Float>(0, baseH / 2, 0)
        laptop.addChild(base)

        // Screen (디스플레이 덮개) — 0°가 수직 열림, 뒤로 20° 젖혀 110° 열림 상태 연출
        let screenW = baseW
        let screenH: Float = 0.22
        let screenThickness: Float = 0.01
        let screenAngle: Float = -.pi * 20 / 180  // -20° (수직에서 뒤로 20° 젖힘 = 실제 100~110° 열림)

        // Hinge 그룹 — 베이스 뒤쪽 가장자리 기준 회전
        let hinge = Entity()
        hinge.position = SIMD3<Float>(0, baseH, -baseD / 2)
        hinge.orientation = simd_quatf(angle: screenAngle, axis: SIMD3<Float>(1, 0, 0))
        laptop.addChild(hinge)

        let screenBack = ModelEntity(
            mesh: .generateBox(width: screenW, height: screenH, depth: screenThickness, cornerRadius: 0.005),
            materials: [bodyMat]
        )
        screenBack.name = "laptop_screen"
        // hinge 기준 위로 screenH/2, 앞쪽으로 살짝
        screenBack.position = SIMD3<Float>(0, screenH / 2, -screenThickness / 2)
        hinge.addChild(screenBack)

        // 화면보호기 (emissive plane) — 디스플레이 앞면
        var screenGlowMat = PhysicallyBasedMaterial()
        screenGlowMat.baseColor = .init(tint: PlatformColor(red: 0.10, green: 0.12, blue: 0.20, alpha: 1.0))
        screenGlowMat.emissiveColor = .init(color: PlatformColor(red: 0.35, green: 0.55, blue: 0.95, alpha: 1.0))
        screenGlowMat.emissiveIntensity = 1.0
        screenGlowMat.roughness = .init(floatLiteral: 0.3)
        screenGlowMat.metallic = .init(floatLiteral: 0.0)
        let glow = ModelEntity(
            mesh: .generatePlane(width: screenW - 0.02, height: screenH - 0.02),
            materials: [screenGlowMat]
        )
        glow.name = "laptop_screen_glow"
        glow.position = SIMD3<Float>(0, screenH / 2, screenThickness / 2 + 0.001)
        hinge.addChild(glow)

        // 탭 감지 — 별도 투명 hit 엔티티 (베이스 + 화면 덮는 넉넉한 범위)
        let hit = Entity()
        hit.name = "laptop_hit"
        hit.position = SIMD3<Float>(0, 0.11, -0.04)
        hit.components.set(CollisionComponent(shapes: [
            .generateBox(width: baseW * 1.15, height: 0.26, depth: baseD * 1.25)
        ]))
        hit.components.set(InputTargetComponent())
        laptop.addChild(hit)

        return laptop
    }
}
