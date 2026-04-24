import RealityKit

/// 우측 벽을 덮는 통창 프레임(멀리언 없음) + 유리.
/// 건축적 통합을 위해:
/// - 창 폭·높이는 실제 "열린 벽 면적"(back wall~room front, floor~header 하단)에 맞춤
/// - 프레임 depth는 10cm — 벽 두께처럼 느껴지게
/// - 프레임은 `UnlitMaterial`로 lighting 무시 → 역광 상황에서도 실내쪽 면이 어두워지지 않음
enum WindowEntity {
    /// 창문 폭 (z 방향) — 뒷벽(z=-1)부터 방 전면(z=+2.5)까지 3.5m
    static let width: Float = 3.5
    /// 창문 높이 (y 방향) — 바닥부터 천장(y=3.1)까지 풀 통창. header 밴드 제거.
    static let height: Float = 3.1
    /// 창 중심 y
    static let centerY: Float = 1.55
    /// 창 중심 z — (-1 + 2.5) / 2 = 0.75
    static let centerZ: Float = 0.75

    private static let memberThickness: Float = 0.04
    /// 프레임 깊이 (x 방향) — 4cm. 매입감은 약하지만 sunRay가 만드는 줄무늬 그림자가 과도해지는 걸 완화.
    static let frameDepth: Float = 0.04

    static func createRightWall(wallX: Float) -> Entity {
        let window = Entity()
        window.name = "window_right"
        window.position = SIMD3<Float>(wallX, centerY, centerZ)

        let frameMat = MaterialFactory.windowFrameUnlitMaterial()
        let t = memberThickness
        let halfW = width / 2
        let halfH = height / 2
        // 프레임은 창 평면 기준 실내 쪽(-X)으로 10cm 매입된 형태:
        // 프레임 박스의 중심 local x = -frameDepth/2 → 박스 스팬 local x ∈ [-frameDepth, 0],
        // 즉 월드 x ∈ [wallX - frameDepth, wallX].
        let frameLocalX: Float = -frameDepth / 2

        // 유리 — 얇은 박스(두께 1.5cm)로 양면 렌더 + 약간의 두께감.
        // 박스 축: width=X(두께), height=Y, depth=Z. 창 평면 local x=0에 중심 정렬.
        let glass = ModelEntity(
            mesh: .generateBox(
                width: 0.015,
                height: height - t * 2,
                depth: width - t * 2
            ),
            materials: [MaterialFactory.windowGlassMaterial()]
        )
        glass.name = "window_glass"
        window.addChild(glass)

        // 박스 축: width=X, height=Y, depth=Z
        let topFrame = ModelEntity(
            mesh: .generateBox(width: frameDepth, height: t, depth: width),
            materials: [frameMat]
        )
        topFrame.position = SIMD3<Float>(frameLocalX, halfH - t / 2, 0)
        topFrame.name = "window_frame_top"
        window.addChild(topFrame)

        let bottomFrame = ModelEntity(
            mesh: .generateBox(width: frameDepth, height: t, depth: width),
            materials: [frameMat]
        )
        bottomFrame.position = SIMD3<Float>(frameLocalX, -halfH + t / 2, 0)
        bottomFrame.name = "window_frame_bottom"
        window.addChild(bottomFrame)

        let backFrame = ModelEntity(
            mesh: .generateBox(width: frameDepth, height: height - t * 2, depth: t),
            materials: [frameMat]
        )
        backFrame.position = SIMD3<Float>(frameLocalX, 0, -halfW + t / 2)
        backFrame.name = "window_frame_back"
        window.addChild(backFrame)

        let frontFrame = ModelEntity(
            mesh: .generateBox(width: frameDepth, height: height - t * 2, depth: t),
            materials: [frameMat]
        )
        frontFrame.position = SIMD3<Float>(frameLocalX, 0, halfW - t / 2)
        frontFrame.name = "window_frame_front"
        window.addChild(frontFrame)

        return window
    }
}
