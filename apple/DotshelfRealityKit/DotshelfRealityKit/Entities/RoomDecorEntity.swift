import RealityKit

/// 방 배치 꾸미기 엔티티 생성기 — 명세 §1.1.
/// 이름 prefix `roomdeco_`로 기존 책/책장 탭·드래그 경로와 분리.
enum RoomDecorEntity {
    @MainActor
    static func create(kind: RoomDecorKind) -> Entity {
        let name = "roomdeco_\(kind.rawValue)_\(UUID().uuidString.prefix(6))"
        switch kind {
        case .floorPot:     return potEntity(name: name)
        case .floorRug:     return rugEntity(name: name)
        case .floorSofa:    return sofaEntity(name: name)
        case .floorHamster: return hamsterEntity(name: name)
        case .wallFrame:    return frameEntity(name: name)
        case .wallClock:    return clockEntity(name: name)
        case .wallPoster:   return posterEntity(name: name)
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

    /// 낮은 2인용 소파 — 시트 + 등받이 + 두 개의 암레스트.
    private static func sofaEntity(name: String) -> Entity {
        let container = Entity()
        container.name = name

        let fabric = pbr(PlatformColor(red: 0.45, green: 0.35, blue: 0.55, alpha: 1), roughness: 0.75)
        let darkFabric = pbr(PlatformColor(red: 0.36, green: 0.28, blue: 0.44, alpha: 1), roughness: 0.8)

        // 시트
        let seat = ModelEntity(
            mesh: .generateBox(width: 0.70, height: 0.14, depth: 0.32, cornerRadius: 0.04),
            materials: [fabric]
        )
        seat.position = [0, 0.18, 0]
        container.addChild(seat)

        // 등받이
        let back = ModelEntity(
            mesh: .generateBox(width: 0.70, height: 0.28, depth: 0.10, cornerRadius: 0.04),
            materials: [fabric]
        )
        back.position = [0, 0.36, -0.11]
        container.addChild(back)

        // 양쪽 암레스트
        let armMesh = MeshResource.generateBox(width: 0.08, height: 0.22, depth: 0.32, cornerRadius: 0.03)
        for x: Float in [-0.31, 0.31] {
            let arm = ModelEntity(mesh: armMesh, materials: [darkFabric])
            arm.position = [x, 0.22, 0]
            container.addChild(arm)
        }

        attachInteraction(container, size: [0.74, 0.52, 0.34])
        return container
    }

    /// 작고 귀여운 햄스터 — 몸통(타원형 느낌의 납작한 박스) + 귀 두 개.
    private static func hamsterEntity(name: String) -> Entity {
        let container = Entity()
        container.name = name

        let body = pbr(PlatformColor(red: 0.82, green: 0.68, blue: 0.48, alpha: 1), roughness: 0.9)
        let belly = pbr(PlatformColor(red: 0.94, green: 0.86, blue: 0.72, alpha: 1), roughness: 0.95)
        let dark = pbr(PlatformColor(red: 0.18, green: 0.12, blue: 0.08, alpha: 1), roughness: 0.4)

        // 몸통
        let torso = ModelEntity(
            mesh: .generateBox(width: 0.16, height: 0.10, depth: 0.20, cornerRadius: 0.05),
            materials: [body]
        )
        torso.position = [0, 0.05, 0]
        container.addChild(torso)

        // 배 (앞쪽 밝은색)
        let bellyBox = ModelEntity(
            mesh: .generateBox(width: 0.12, height: 0.06, depth: 0.14, cornerRadius: 0.04),
            materials: [belly]
        )
        bellyBox.position = [0, 0.02, 0.04]
        container.addChild(bellyBox)

        // 머리
        let head = ModelEntity(
            mesh: .generateBox(width: 0.12, height: 0.10, depth: 0.10, cornerRadius: 0.05),
            materials: [body]
        )
        head.position = [0, 0.08, 0.12]
        container.addChild(head)

        // 귀 2개
        let earMesh = MeshResource.generateBox(width: 0.03, height: 0.04, depth: 0.015, cornerRadius: 0.01)
        for x: Float in [-0.035, 0.035] {
            let ear = ModelEntity(mesh: earMesh, materials: [body])
            ear.position = [x, 0.14, 0.11]
            container.addChild(ear)
        }

        // 눈 2개
        let eyeMesh = MeshResource.generateBox(width: 0.012, height: 0.012, depth: 0.005, cornerRadius: 0.005)
        for x: Float in [-0.025, 0.025] {
            let eye = ModelEntity(mesh: eyeMesh, materials: [dark])
            eye.position = [x, 0.09, 0.17]
            container.addChild(eye)
        }

        attachInteraction(container, size: [0.18, 0.18, 0.24])
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

    /// 세로형 포스터 — 얇은 프레임 + 밝은 색 바탕 (PBR 단색).
    private static func posterEntity(name: String) -> Entity {
        let container = Entity()
        container.name = name

        let frame = pbr(PlatformColor(red: 0.20, green: 0.14, blue: 0.10, alpha: 1), roughness: 0.7)
        let art = pbr(PlatformColor(red: 0.85, green: 0.40, blue: 0.38, alpha: 1), roughness: 0.6)

        // 바깥 프레임 (세로가 더 김)
        let outer = ModelEntity(
            mesh: .generateBox(width: 0.32, height: 0.46, depth: 0.015, cornerRadius: 0.004),
            materials: [frame]
        )
        container.addChild(outer)

        // 내부 아트워크
        let inner = ModelEntity(
            mesh: .generateBox(width: 0.28, height: 0.42, depth: 0.005, cornerRadius: 0.002),
            materials: [art]
        )
        inner.position = [0, 0, 0.010]
        container.addChild(inner)

        attachInteraction(container, size: [0.32, 0.46, 0.04])
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
