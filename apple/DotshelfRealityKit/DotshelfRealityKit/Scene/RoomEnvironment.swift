import RealityKit

/// 책장 주변의 서재 방 환경을 프로시저럴하게 생성.
/// 바닥(나무) + 뒷벽 + 좌/우 측벽. 천장은 카메라 시야에 안 들어와 생략.
enum RoomEnvironment {
    static func create() -> Entity {
        let room = Entity()
        room.name = "room"

        // 치수
        let floorSize: Float = 5.0         // 바닥 한 변
        let wallHeight: Float = 2.8
        let backWallDistance: Float = 1.0  // 책장 뒤 벽까지의 거리 (책장 뒷면은 z ≈ -0.125)
        let sideWallDistance: Float = 2.0  // 중앙에서 측벽까지의 거리

        // 책장 구멍(alcove) 치수: 책장 회전이 벽 뒤로 파고들 수 있게 깊이 확보
        let holeWidth: Float = 1.7    // 책장 폭 1.6 + 여유
        let holeHeight: Float = 1.7   // 책장 높이 1.6 + 여유
        let alcoveDepth: Float = 0.9  // 회전 스윕 반경(~0.81) 수용

        room.addChild(makeFloor(size: floorSize))
        addBackWallWithHole(
            to: room,
            wallWidth: floorSize, wallHeight: wallHeight, wallZ: -backWallDistance,
            holeWidth: holeWidth, holeHeight: holeHeight
        )
        addAlcoveInteriorWalls(
            to: room,
            holeWidth: holeWidth, holeHeight: holeHeight,
            frontZ: -backWallDistance, depth: alcoveDepth
        )
        room.addChild(makeLeftWall(depth: floorSize, height: wallHeight, x: -sideWallDistance))
        room.addChild(makeRightWall(depth: floorSize, height: wallHeight, x: sideWallDistance))
        room.addChild(makeWhiteboardOnLeftWall(wallX: -sideWallDistance))

        return room
    }

    // MARK: - 뒷벽 구멍 (4개 스트립으로 구성)

    private static func addBackWallWithHole(
        to parent: Entity,
        wallWidth: Float, wallHeight: Float, wallZ: Float,
        holeWidth: Float, holeHeight: Float
    ) {
        let material = MaterialFactory.wallMaterial()
        let sideStripWidth = (wallWidth - holeWidth) / 2
        let topStripHeight = wallHeight - holeHeight

        // 좌측 스트립
        let leftStrip = ModelEntity(
            mesh: .generateBox(width: sideStripWidth, height: wallHeight, depth: 0.01),
            materials: [material]
        )
        leftStrip.name = "room_wall_back"
        leftStrip.position = SIMD3<Float>(-(holeWidth + sideStripWidth) / 2, wallHeight / 2, wallZ)
        leftStrip.collision = CollisionComponent(shapes: [.generateBox(width: sideStripWidth, height: wallHeight, depth: 0.01)])
        leftStrip.components.set(InputTargetComponent())
        parent.addChild(leftStrip)

        // 우측 스트립
        let rightStrip = ModelEntity(
            mesh: .generateBox(width: sideStripWidth, height: wallHeight, depth: 0.01),
            materials: [material]
        )
        rightStrip.name = "room_wall_back"
        rightStrip.position = SIMD3<Float>((holeWidth + sideStripWidth) / 2, wallHeight / 2, wallZ)
        rightStrip.collision = CollisionComponent(shapes: [.generateBox(width: sideStripWidth, height: wallHeight, depth: 0.01)])
        rightStrip.components.set(InputTargetComponent())
        parent.addChild(rightStrip)

        // 상단 스트립 (구멍 위)
        let topStrip = ModelEntity(
            mesh: .generateBox(width: holeWidth, height: topStripHeight, depth: 0.01),
            materials: [material]
        )
        topStrip.name = "room_wall_back"
        topStrip.position = SIMD3<Float>(0, holeHeight + topStripHeight / 2, wallZ)
        topStrip.collision = CollisionComponent(shapes: [.generateBox(width: holeWidth, height: topStripHeight, depth: 0.01)])
        topStrip.components.set(InputTargetComponent())
        parent.addChild(topStrip)
        // 바닥 스트립 없음 (구멍이 바닥까지 닿음)
    }

    // MARK: - Alcove 내벽 (좌/우/천장/뒤)

    private static func addAlcoveInteriorWalls(
        to parent: Entity,
        holeWidth: Float, holeHeight: Float,
        frontZ: Float, depth: Float
    ) {
        let material = MaterialFactory.wallMaterial()
        let backZ = frontZ - depth
        let midZ = (frontZ + backZ) / 2

        // 뒤쪽 내벽
        let back = ModelEntity(
            mesh: .generateBox(width: holeWidth, height: holeHeight, depth: 0.01),
            materials: [material]
        )
        back.name = "alcove_back"
        back.position = SIMD3<Float>(0, holeHeight / 2, backZ)
        parent.addChild(back)

        // 좌측 내벽
        let leftInner = ModelEntity(
            mesh: .generateBox(width: 0.01, height: holeHeight, depth: depth),
            materials: [material]
        )
        leftInner.name = "alcove_left"
        leftInner.position = SIMD3<Float>(-holeWidth / 2, holeHeight / 2, midZ)
        parent.addChild(leftInner)

        // 우측 내벽
        let rightInner = ModelEntity(
            mesh: .generateBox(width: 0.01, height: holeHeight, depth: depth),
            materials: [material]
        )
        rightInner.name = "alcove_right"
        rightInner.position = SIMD3<Float>(holeWidth / 2, holeHeight / 2, midZ)
        parent.addChild(rightInner)

        // 천장 내벽
        let ceiling = ModelEntity(
            mesh: .generateBox(width: holeWidth, height: 0.01, depth: depth),
            materials: [material]
        )
        ceiling.name = "alcove_ceiling"
        ceiling.position = SIMD3<Float>(0, holeHeight, midZ)
        parent.addChild(ceiling)
        // 바닥은 기존 floor가 계속 이어짐
    }

    // MARK: - 파트 생성

    private static func makeFloor(size: Float) -> ModelEntity {
        let mesh = MeshResource.generatePlane(width: size, depth: size)
        let material = MaterialFactory.floorMaterial()
        let entity = ModelEntity(mesh: mesh, materials: [material])
        entity.name = "room_floor"
        entity.position = SIMD3<Float>(0, 0, 0)

        // 물리 시뮬레이션용 — 책이 떨어질 때 부딪힐 수 있도록 static 콜리전
        // 얇은 박스(두께 1cm)로 바닥 덮음
        entity.components.set(CollisionComponent(
            shapes: [.generateBox(width: size, height: 0.01, depth: size)]
        ))
        entity.components.set(PhysicsBodyComponent(
            massProperties: .default,
            material: .generate(friction: 0.6, restitution: 0.1),
            mode: .static
        ))
        return entity
    }

    private static func makeBackWall(width: Float, height: Float, z: Float) -> ModelEntity {
        let mesh = MeshResource.generatePlane(width: width, height: height)
        let material = MaterialFactory.wallMaterial()
        let entity = ModelEntity(mesh: mesh, materials: [material])
        entity.name = "room_wall_back"
        entity.position = SIMD3<Float>(0, height / 2, z)
        // 벽 탭으로 정면 뷰 전환 — CollisionComponent + InputTargetComponent 필요
        entity.collision = CollisionComponent(shapes: [.generateBox(width: width, height: height, depth: 0.01)])
        entity.components.set(InputTargetComponent())
        return entity
    }

    private static func makeLeftWall(depth: Float, height: Float, x: Float) -> ModelEntity {
        let mesh = MeshResource.generatePlane(width: depth, height: height)
        let material = MaterialFactory.wallMaterial()
        let entity = ModelEntity(mesh: mesh, materials: [material])
        entity.name = "room_wall_left"
        entity.position = SIMD3<Float>(x, height / 2, 0)
        entity.orientation = simd_quatf(angle: .pi / 2, axis: SIMD3<Float>(0, 1, 0))
        // Collision: entity-local에서 x=길이(평면방향), z=두께(벽 수직) 순서. Ry(π/2) 회전 반영.
        entity.components.set(CollisionComponent(
            shapes: [.generateBox(width: depth, height: height, depth: 0.1)]
        ))
        entity.components.set(PhysicsBodyComponent(
            massProperties: .default,
            material: .generate(friction: 0.5, restitution: 0.1),
            mode: .static
        ))
        return entity
    }

    private static func makeRightWall(depth: Float, height: Float, x: Float) -> ModelEntity {
        let mesh = MeshResource.generatePlane(width: depth, height: height)
        let material = MaterialFactory.wallMaterial()
        let entity = ModelEntity(mesh: mesh, materials: [material])
        entity.name = "room_wall_right"
        entity.position = SIMD3<Float>(x, height / 2, 0)
        entity.orientation = simd_quatf(angle: -.pi / 2, axis: SIMD3<Float>(0, 1, 0))
        entity.components.set(CollisionComponent(
            shapes: [.generateBox(width: depth, height: height, depth: 0.1)]
        ))
        entity.components.set(PhysicsBodyComponent(
            massProperties: .default,
            material: .generate(friction: 0.5, restitution: 0.1),
            mode: .static
        ))
        return entity
    }

    /// 화이트보드: 프레임(알루미늄) + 흰색 표면. 좌측 벽에 부착.
    private static func makeWhiteboardOnLeftWall(wallX: Float) -> Entity {
        let whiteboard = Entity()
        whiteboard.name = "whiteboard"

        let width: Float = 1.2
        let height: Float = 0.75
        let frameThickness: Float = 0.03

        // 프레임 (뒤)
        let frameMesh = MeshResource.generatePlane(width: width, height: height)
        let frame = ModelEntity(mesh: frameMesh, materials: [MaterialFactory.whiteboardFrameMaterial()])
        frame.name = "whiteboard_frame"
        whiteboard.addChild(frame)

        // 흰색 표면 (프레임 안쪽, 2mm 앞)
        let surfaceMesh = MeshResource.generatePlane(
            width: width - frameThickness * 2,
            height: height - frameThickness * 2
        )
        let surface = ModelEntity(mesh: surfaceMesh, materials: [MaterialFactory.whiteboardSurfaceMaterial()])
        surface.name = "whiteboard_surface"
        surface.position = SIMD3<Float>(0, 0, 0.002)
        whiteboard.addChild(surface)

        // 좌측 벽에 부착: 벽면(x = wallX, 바깥쪽 normal = +x)보다 5mm 앞 (+x 방향)
        whiteboard.position = SIMD3<Float>(wallX + 0.005, 1.25, 0)
        // +z 방향을 향하던 plane을 +x 방향으로 돌림 (좌측 벽 정면이 +x)
        whiteboard.orientation = simd_quatf(angle: .pi / 2, axis: SIMD3<Float>(0, 1, 0))

        // 프레임에 Collision + InputTarget (화이트보드 탭 감지)
        frame.collision = CollisionComponent(shapes: [.generateBox(width: width, height: height, depth: 0.01)])
        frame.components.set(InputTargetComponent())

        return whiteboard
    }
}
