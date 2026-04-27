import RealityKit

/// 책장 주변의 서재 방 환경을 프로시저럴하게 생성.
/// 바닥(나무) + 뒷벽 + 좌/우 측벽. 천장은 카메라 시야에 안 들어와 생략.
enum RoomEnvironment {
    static func create() -> Entity {
        let room = Entity()
        room.name = "room"

        // 치수
        let floorSize: Float = 5.0         // 바닥 한 변
        let wallHeight: Float = 3.1
        let backWallDistance: Float = 1.0  // 책장 뒤 벽까지의 거리 (책장 뒷면은 z ≈ -0.125)
        let sideWallDistance: Float = 2.0  // 중앙에서 측벽까지의 거리

        // 책장 구멍(alcove) 치수: 책장 회전이 벽 뒤로 파고들 수 있게 깊이 확보
        let holeWidth: Float = 1.7    // 책장 폭 1.6 + 여유
        let holeHeight: Float = 1.7   // 책장 높이 1.6 + 여유
        let alcoveDepth: Float = 0.9  // 회전 스윕 반경(~0.81) 수용

        // 실내 바닥: 벽 안쪽(x ∈ [-2, 2], z ∈ [-2.5, 2.5])
        room.addChild(makeFloor(width: 2 * sideWallDistance, depth: floorSize, centerX: 0))
        // 창 너머 실외 풀밭 — 실내 바닥보다 5cm 낮게 배치해 threshold 연출
        let outdoorY: Float = -0.05
        room.addChild(makeOutdoorGround(
            startX: sideWallDistance, endX: sideWallDistance + 10,
            depth: floorSize + 7, y: outdoorY
        ))
        // 실내외 경계의 수직 스텝 (안쪽 바닥 색 매치) — x=+2, 창 개구부 z 범위
        room.addChild(makeThresholdStep(
            x: sideWallDistance,
            zStart: -backWallDistance, zEnd: floorSize / 2,
            stepHeight: -outdoorY
        ))
        // 원거리 하늘 배경 (shadow frustum 밖)
        room.addChild(makeSkyBackdrop(x: sideWallDistance + 12))
        // 뒷벽: 실내 폭(x ∈ [-2, 2])에 맞게 크롭 — 야외 쪽으로 연장되지 않게
        addBackWallWithHole(
            to: room,
            wallWidth: 2 * sideWallDistance, wallHeight: wallHeight, wallZ: -backWallDistance,
            holeWidth: holeWidth, holeHeight: holeHeight
        )
        addAlcoveInteriorWalls(
            to: room,
            holeWidth: holeWidth, holeHeight: holeHeight,
            frontZ: -backWallDistance, depth: alcoveDepth
        )
        room.addChild(makeLeftWall(depth: floorSize, height: wallHeight, x: -sideWallDistance))
        // 앞벽: 방의 남쪽 면(z=+floorSize/2) 막음. 카메라 방향 뒤쪽이라 일반 뷰에는 안 보이지만
        // 디버그/외부 관점에서 방이 열려보이는 문제 해결.
        room.addChild(makeFrontWall(width: 2 * sideWallDistance, height: wallHeight, z: floorSize / 2))
        // 천장: IBL 하늘 빛이 창 위 벽에 직접 내리쏟는 걸 차단 — 물리적으로 맞는 그림자 연출.
        // 카메라는 모두 y ≤ 1.5에서 아래를 바라보므로 시야에 안 들어옴.
        room.addChild(makeCeiling(width: 2 * sideWallDistance, depth: floorSize, y: wallHeight))
        // 우측은 벽 없이 통창(WindowEntity)만 배치 — 외곽 프레임만 렌더링
        room.addChild(WindowEntity.createRightWall(wallX: sideWallDistance))
        room.addChild(makeWhiteboardOnLeftWall(wallX: -sideWallDistance))
        // 공간1 (좌측 벽 앞): 책상 + 노트북. 노트북을 탭하면 §5 노트북 포커싱 진입.
        room.addChild(DeskLaptopEntity.create(wallX: -sideWallDistance))

        return room
    }

    // MARK: - 3-stripe 벽 헬퍼 (Unity `wall_bottom / trim / top_check`)

    /// 바닥부터: 하단 이보리 패널 1.0m → 트림 몰딩 4cm → 상단 체크 베이지 (나머지).
    private static let stripeBottomHeight: Float = 1.0
    private static let stripeTrimHeight: Float = 0.04

    /// 주어진 너비·높이의 벽을 3 stripe으로 구성해 컨테이너 Entity로 반환.
    /// 컨테이너 원점이 "벽 중심"을 가리키도록 내부 plane y를 조정.
    /// 외부 사용자는 기존 단일 plane과 동일하게 `.position = (x, height/2, z)`로 붙이면 됨.
    private static func makeStripedWallPlane(
        width: Float,
        height: Float,
        named: String
    ) -> Entity {
        let root = Entity()
        root.name = named

        let bottomH: Float = min(stripeBottomHeight, height * 0.5)
        let trimH: Float = min(stripeTrimHeight, height - bottomH - 0.001)
        let topH: Float = max(0, height - bottomH - trimH)

        // 각 stripe 중심의 절대 y (바닥 0 기준)
        let bottomCenter = bottomH / 2
        let trimCenter = bottomH + trimH / 2
        let topCenter = bottomH + trimH + topH / 2

        // root는 벽 중심(height/2)에 위치 → 각 child y 오프셋 = stripe_center - height/2
        func addStripe(h: Float, centerY: Float, mat: PhysicallyBasedMaterial, suffix: String) {
            guard h > 0.001 else { return }
            let plane = ModelEntity(
                mesh: .generatePlane(width: width, height: h),
                materials: [mat]
            )
            plane.name = "\(named)_\(suffix)"
            plane.position = SIMD3<Float>(0, centerY - height / 2, 0)
            root.addChild(plane)
        }

        addStripe(h: bottomH, centerY: bottomCenter,
                  mat: MaterialFactory.wallBottomMaterial(), suffix: "bottom")
        addStripe(h: trimH, centerY: trimCenter,
                  mat: MaterialFactory.wallTrimMaterial(), suffix: "trim")
        addStripe(h: topH, centerY: topCenter,
                  mat: MaterialFactory.wallTopMaterial(), suffix: "top")

        return root
    }

    // MARK: - 뒷벽 구멍 (4개 스트립으로 구성)

    private static func addBackWallWithHole(
        to parent: Entity,
        wallWidth: Float, wallHeight: Float, wallZ: Float,
        holeWidth: Float, holeHeight: Float
    ) {
        let sideStripWidth = (wallWidth - holeWidth) / 2
        let topStripHeight = wallHeight - holeHeight

        // 좌측 스트립 (3-stripe 구성)
        let leftStripe = makeStripedWallPlane(
            width: sideStripWidth, height: wallHeight, named: "room_wall_back"
        )
        leftStripe.position = SIMD3<Float>(-(holeWidth + sideStripWidth) / 2, wallHeight / 2, wallZ)
        // 탭 hit용 collision은 컨테이너에 박스로
        let leftHit = ModelEntity()
        leftHit.name = "room_wall_back"
        leftHit.collision = CollisionComponent(shapes: [
            .generateBox(width: sideStripWidth, height: wallHeight, depth: 0.01)
        ])
        leftHit.components.set(InputTargetComponent())
        leftStripe.addChild(leftHit)
        parent.addChild(leftStripe)

        // 우측 스트립 (3-stripe 구성)
        let rightStripe = makeStripedWallPlane(
            width: sideStripWidth, height: wallHeight, named: "room_wall_back"
        )
        rightStripe.position = SIMD3<Float>((holeWidth + sideStripWidth) / 2, wallHeight / 2, wallZ)
        let rightHit = ModelEntity()
        rightHit.name = "room_wall_back"
        rightHit.collision = CollisionComponent(shapes: [
            .generateBox(width: sideStripWidth, height: wallHeight, depth: 0.01)
        ])
        rightHit.components.set(InputTargetComponent())
        rightStripe.addChild(rightHit)
        parent.addChild(rightStripe)

        // 상단 스트립 (구멍 위) — 전부 체크 베이지 영역
        let topStrip = ModelEntity(
            mesh: .generateBox(width: holeWidth, height: topStripHeight, depth: 0.01),
            materials: [MaterialFactory.wallTopMaterial()]
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

    private static func makeFloor(width: Float, depth: Float, centerX: Float) -> ModelEntity {
        let mesh = MeshResource.generatePlane(width: width, depth: depth)
        let material = MaterialFactory.floorMaterial()
        let entity = ModelEntity(mesh: mesh, materials: [material])
        entity.name = "room_floor"
        entity.position = SIMD3<Float>(centerX, 0, 0)

        entity.components.set(CollisionComponent(
            shapes: [.generateBox(width: width, height: 0.01, depth: depth)]
        ))
        entity.components.set(PhysicsBodyComponent(
            massProperties: .default,
            material: .generate(friction: 0.6, restitution: 0.1),
            mode: .static
        ))
        return entity
    }

    /// 창 너머 실외 풀밭 (x 방향 연장). y는 실내 바닥(0)보다 살짝 낮게 주면 threshold 감이 생김.
    private static func makeOutdoorGround(startX: Float, endX: Float, depth: Float, y: Float = 0) -> ModelEntity {
        let width = endX - startX
        let mesh = MeshResource.generatePlane(width: width, depth: depth)
        let entity = ModelEntity(mesh: mesh, materials: [MaterialFactory.grassMaterial()])
        entity.name = "outdoor_ground"
        entity.position = SIMD3<Float>((startX + endX) / 2, y, 0)
        return entity
    }

    /// 실내외 바닥 높이차를 시각화하는 수직 stepper (어두운 원목 톤)
    private static func makeThresholdStep(x: Float, zStart: Float, zEnd: Float, stepHeight: Float) -> ModelEntity {
        let depth = zEnd - zStart
        let mesh = MeshResource.generateBox(width: 0.02, height: stepHeight, depth: depth)
        let entity = ModelEntity(mesh: mesh, materials: [MaterialFactory.floorMaterial()])
        entity.name = "threshold_step"
        entity.position = SIMD3<Float>(x, -stepHeight / 2, (zStart + zEnd) / 2)
        return entity
    }

    /// 창문 상단 헤더 — 플라스터 벽 밴드로 창이 벽에 박혀있다는 감각 제공
    private static func makeWindowHeader(x: Float, zStart: Float, zEnd: Float, yBottom: Float, yTop: Float) -> ModelEntity {
        let depth = zEnd - zStart
        let height = yTop - yBottom
        let mesh = MeshResource.generateBox(width: 0.01, height: height, depth: depth)
        let entity = ModelEntity(mesh: mesh, materials: [MaterialFactory.wallMaterial()])
        entity.name = "window_header"
        entity.position = SIMD3<Float>(x, (yBottom + yTop) / 2, (zStart + zEnd) / 2)
        return entity
    }

    /// 원거리 하늘 배경 — 창 바깥에 서 있는 큰 vertical plane, unlit.
    /// shadow 맵 최대 거리 바깥에 배치해 그림자 계산에서 제외된다.
    private static func makeSkyBackdrop(x: Float) -> ModelEntity {
        let mesh = MeshResource.generatePlane(width: 40, height: 25)
        let entity = ModelEntity(mesh: mesh, materials: [MaterialFactory.skyMaterial()])
        entity.name = "sky_backdrop"
        entity.position = SIMD3<Float>(x, 12.5, 0)
        // 기본 plane 노멀 +Z → 회전해 -X 방향으로 향하게 (방 쪽을 바라봄)
        entity.orientation = simd_quatf(angle: -.pi / 2, axis: SIMD3<Float>(0, 1, 0))
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

    /// 방 앞벽 (남쪽 면) — 유리 통면. 외부에서 방 내부가 투시되면서도 geometry가 IBL/공간을 정의.
    private static func makeFrontWall(width: Float, height: Float, z: Float) -> ModelEntity {
        let mesh = MeshResource.generateBox(width: width, height: height, depth: 0.015)
        let entity = ModelEntity(mesh: mesh, materials: [MaterialFactory.windowGlassMaterial()])
        entity.name = "room_wall_front"
        entity.position = SIMD3<Float>(0, height / 2, z)
        return entity
    }

    /// 방 천장. 얇은 box로 위·아래 모두 렌더 — 디버그 오버헤드 뷰에서도 보이도록.
    /// IBL 간접광이 창 위 벽에 수직으로 내리쏟는 경로를 차단하는 역할.
    private static func makeCeiling(width: Float, depth: Float, y: Float) -> ModelEntity {
        let thickness: Float = 0.02
        let mesh = MeshResource.generateBox(width: width, height: thickness, depth: depth)
        let entity = ModelEntity(mesh: mesh, materials: [MaterialFactory.wallMaterial()])
        entity.name = "room_ceiling"
        // 천장 아랫면이 y=wallHeight에 맞도록 중심은 그 위 thickness/2만큼.
        entity.position = SIMD3<Float>(0, y + thickness / 2, 0)
        return entity
    }

    private static func makeLeftWall(depth: Float, height: Float, x: Float) -> Entity {
        // 3 stripe 컨테이너 — 내부 plane은 +Z normal (XY plane).
        // Ry(+90°) 회전으로 벽면이 +x 방향을 향하게 됨 (화이트보드가 붙는 방향).
        let root = makeStripedWallPlane(width: depth, height: height, named: "room_wall_left")
        root.position = SIMD3<Float>(x, height / 2, 0)
        root.orientation = simd_quatf(angle: .pi / 2, axis: SIMD3<Float>(0, 1, 0))

        // Collision + Physics는 root에 부착 (자식 plane의 시각과 독립)
        root.components.set(CollisionComponent(
            shapes: [.generateBox(width: depth, height: height, depth: 0.1)]
        ))
        root.components.set(PhysicsBodyComponent(
            massProperties: .default,
            material: .generate(friction: 0.5, restitution: 0.1),
            mode: .static
        ))
        return root
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

        // 표면(대시보드 내용물)은 element별 hit 박스(DashboardHitRegions)가 탭을 대신 받는다.
        // surface 자체 InputTarget은 달지 않음 — hit region miss 시 frame으로 전파되도록.
        // element별 hit 박스 설치 (고정 element: bio/like/stats/guestbook header)
        let surfaceWidth = width - frameThickness * 2
        let surfaceHeight = height - frameThickness * 2
        DashboardHitRegions.install(on: surface,
                                    surfaceWidth: surfaceWidth,
                                    surfaceHeight: surfaceHeight)

        return whiteboard
    }
}
