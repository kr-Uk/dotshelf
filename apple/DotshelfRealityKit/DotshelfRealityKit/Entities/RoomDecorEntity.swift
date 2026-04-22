import RealityKit

/// 방 배치 꾸미기 엔티티 생성기 — 명세 §1.1.
/// 이름 prefix `roomdeco_`로 기존 책/책장 탭·드래그 경로와 분리.
enum RoomDecorEntity {
    @MainActor
    static func create(kind: RoomDecorKind) -> Entity {
        let name = "roomdeco_\(kind.rawValue)_\(UUID().uuidString.prefix(6))"
        switch kind {
        case .floorPot:   return potEntity(name: name)
        case .floorRug:   return rugEntity(name: name)
        case .floorStool: return stoolEntity(name: name)
        case .wallFrame:  return frameEntity(name: name)
        case .wallClock:  return clockEntity(name: name)
        }
    }

    // MARK: - 바닥 오브젝트

    private static func potEntity(name: String) -> Entity {
        let container = Entity()
        container.name = name

        let brown = pbr(PlatformColor(red: 0.48, green: 0.32, blue: 0.22, alpha: 1), roughness: 0.7)
        let green = pbr(PlatformColor(red: 0.35, green: 0.55, blue: 0.30, alpha: 1), roughness: 0.7)

        // 화분 몸통
        let pot = ModelEntity(
            mesh: .generateBox(width: 0.18, height: 0.14, depth: 0.18, cornerRadius: 0.02),
            materials: [brown]
        )
        pot.position = [0, 0.07, 0]
        container.addChild(pot)

        // 잎사귀
        let leaves = ModelEntity(
            mesh: .generateBox(width: 0.16, height: 0.28, depth: 0.16, cornerRadius: 0.04),
            materials: [green]
        )
        leaves.position = [0, 0.28, 0]
        container.addChild(leaves)

        attachInteraction(container, size: [0.2, 0.5, 0.2])
        return container
    }

    private static func rugEntity(name: String) -> Entity {
        let container = Entity()
        container.name = name

        let mat = pbr(PlatformColor(red: 0.72, green: 0.48, blue: 0.36, alpha: 1), roughness: 0.9)
        let rug = ModelEntity(
            mesh: .generateBox(width: 0.9, height: 0.02, depth: 0.6, cornerRadius: 0.03),
            materials: [mat]
        )
        rug.position = [0, 0.01, 0]
        container.addChild(rug)

        attachInteraction(container, size: [0.9, 0.06, 0.6])
        return container
    }

    private static func stoolEntity(name: String) -> Entity {
        let container = Entity()
        container.name = name

        let wood = pbr(PlatformColor(red: 0.55, green: 0.42, blue: 0.32, alpha: 1), roughness: 0.55)

        // 상판
        let top = ModelEntity(
            mesh: .generateBox(width: 0.26, height: 0.04, depth: 0.26, cornerRadius: 0.02),
            materials: [wood]
        )
        top.position = [0, 0.30, 0]
        container.addChild(top)

        // 다리 4개
        let legMesh = MeshResource.generateBox(
            width: 0.025, height: 0.28, depth: 0.025, cornerRadius: 0.003
        )
        let legPositions: [SIMD3<Float>] = [
            [0.10, 0.14, 0.10],  [-0.10, 0.14, 0.10],
            [0.10, 0.14, -0.10], [-0.10, 0.14, -0.10],
        ]
        for pos in legPositions {
            let leg = ModelEntity(mesh: legMesh, materials: [wood])
            leg.position = pos
            container.addChild(leg)
        }

        attachInteraction(container, size: [0.28, 0.34, 0.28])
        return container
    }

    // MARK: - 벽걸이 오브젝트

    private static func frameEntity(name: String) -> Entity {
        let container = Entity()
        container.name = name

        let frame = pbr(PlatformColor(red: 0.88, green: 0.82, blue: 0.72, alpha: 1), roughness: 0.5)
        let glass = pbr(PlatformColor(red: 0.50, green: 0.62, blue: 0.72, alpha: 1), roughness: 0.2)

        // 액자 틀 (얇은 박스 가로 넓음)
        let outer = ModelEntity(
            mesh: .generateBox(width: 0.40, height: 0.30, depth: 0.02, cornerRadius: 0.01),
            materials: [frame]
        )
        container.addChild(outer)

        // 액자 안쪽 (사진)
        let inner = ModelEntity(
            mesh: .generateBox(width: 0.34, height: 0.24, depth: 0.005, cornerRadius: 0.005),
            materials: [glass]
        )
        inner.position = [0, 0, 0.012]
        container.addChild(inner)

        attachInteraction(container, size: [0.40, 0.30, 0.05])
        return container
    }

    private static func clockEntity(name: String) -> Entity {
        let container = Entity()
        container.name = name

        let dark = pbr(PlatformColor(red: 0.20, green: 0.22, blue: 0.26, alpha: 1), roughness: 0.4)
        let cream = pbr(PlatformColor(red: 0.94, green: 0.90, blue: 0.82, alpha: 1), roughness: 0.7)

        // 시계 뒷판
        let back = ModelEntity(
            mesh: .generateBox(width: 0.22, height: 0.22, depth: 0.025, cornerRadius: 0.10),
            materials: [dark]
        )
        container.addChild(back)

        // 문자판
        let dial = ModelEntity(
            mesh: .generateBox(width: 0.18, height: 0.18, depth: 0.005, cornerRadius: 0.09),
            materials: [cream]
        )
        dial.position = [0, 0, 0.015]
        container.addChild(dial)

        attachInteraction(container, size: [0.22, 0.22, 0.05])
        return container
    }

    // MARK: - 공통

    private static func pbr(_ color: PlatformColor, roughness: Float) -> PhysicallyBasedMaterial {
        var mat = PhysicallyBasedMaterial()
        mat.baseColor = .init(tint: color)
        mat.roughness = .init(floatLiteral: roughness)
        mat.metallic = .init(floatLiteral: 0.0)
        return mat
    }

    private static func attachInteraction(_ e: Entity, size: SIMD3<Float>) {
        e.components.set(CollisionComponent(
            shapes: [.generateBox(width: size.x, height: size.y, depth: size.z)]
        ))
        e.components.set(InputTargetComponent())
    }
}
