import RealityKit
import Foundation

enum CameraController {
    /// 카메라가 타겟 대상(책장)으로부터 떨어진 초기 거리 (정면 뷰)
    static let initialDistanceFromTarget: Float = 2.5
    static let minDistanceFromTarget: Float = 0.8
    static let maxDistanceFromTarget: Float = 4.5

    /// 룸 오버뷰(코너 3/4 구도) 카메라 생성
    static func createOverviewCamera(bookshelfZ: Float) -> PerspectiveCamera {
        let camera = PerspectiveCamera()
        camera.camera.fieldOfViewInDegrees = 55
        applyOverview(to: camera, bookshelfZ: bookshelfZ, animated: false)
        return camera
    }

    /// 책장 정면 뷰로 카메라 애니메이션
    static func moveToFrontal(camera: PerspectiveCamera, bookshelfZ: Float, duration: TimeInterval = 0.7) {
        camera.camera.fieldOfViewInDegrees = 45
        let camPos = SIMD3<Float>(0, 0.8, bookshelfZ + initialDistanceFromTarget)
        let lookAt = SIMD3<Float>(0, 0.8, bookshelfZ)
        animateLook(camera: camera, from: camPos, at: lookAt, duration: duration)
    }

    /// 룸 오버뷰로 카메라 애니메이션
    static func moveToOverview(camera: PerspectiveCamera, bookshelfZ: Float, duration: TimeInterval = 0.7) {
        camera.camera.fieldOfViewInDegrees = 55
        applyOverview(to: camera, bookshelfZ: bookshelfZ, animated: true, duration: duration)
    }

    /// 디버그용 외부 뷰: 창 바깥에서 통창을 통해 방 내부를 비스듬히 들여다봄.
    /// 천장 아래(y<3.1), 앞벽 앞(z<2.5)에 위치해 박스에 막히지 않고 통창 개구부로 내부 확인.
    static func moveToDebug(camera: PerspectiveCamera, duration: TimeInterval = 0.7) {
        camera.camera.fieldOfViewInDegrees = 55
        let camPos = SIMD3<Float>(5.5, 2.0, 1.5)
        let lookAt = SIMD3<Float>(-1.0, 1.2, -0.8)
        animateLook(camera: camera, from: camPos, at: lookAt, duration: duration)
    }

    /// 실내 조감 뷰: 방 안쪽 모서리(천장 아래, 앞벽 앞)에서 책장 반대편으로 향함.
    /// 방 내부 전체 + 통창 + 화이트보드 + 천장 내벽을 한 번에 확인.
    static func moveToInteriorOverview(camera: PerspectiveCamera, duration: TimeInterval = 0.7) {
        camera.camera.fieldOfViewInDegrees = 65
        let camPos = SIMD3<Float>(1.8, 2.5, 2.2)
        let lookAt = SIMD3<Float>(-0.5, 1.0, -1.0)
        animateLook(camera: camera, from: camPos, at: lookAt, duration: duration)
    }

    /// 화이트보드(좌측 벽) 정면 뷰로 카메라 애니메이션
    /// 화이트보드 크기 1.2×0.75m. 거리 0.95m + FOV 45°에서 세로 방향 0.787m 시야 →
    /// 화이트보드 세로 0.75m가 화면의 ~95%를 차지하여 화면을 거의 꽉 채운다.
    static func moveToWhiteboard(camera: PerspectiveCamera, wallX: Float = -2.0, duration: TimeInterval = 0.7) {
        camera.camera.fieldOfViewInDegrees = 45
        // 화이트보드 중심 월드좌표: (wallX + 0.005, 1.25, 0)
        let targetX = wallX + 0.005
        let camPos = SIMD3<Float>(targetX + 0.95, 1.25, 0)
        let lookAt = SIMD3<Float>(targetX, 1.25, 0)
        animateLook(camera: camera, from: camPos, at: lookAt, duration: duration)
    }

    /// 노트북 디스플레이 정면 뷰 (LaptopFocusOverlay 진입 직전 전환 애니메이션용).
    /// 디스플레이 plane(`laptop_screen_glow`)의 월드 transform에서 위치·법선을 뽑아
    /// 화면 정면 `distance`(m) 앞에 카메라를 정렬한다. 이후 2D 오버레이가 화면을 덮으므로
    /// 카메라 자체는 보조적이지만, 오버레이 ultraThin material을 통해 살짝 비치는 배경이
    /// 노트북 디스플레이와 일치하는 느낌을 준다.
    /// 노트북 디스플레이 앞 포커싱.
    /// 베젤을 뷰포트 밖으로 밀어내기 위해 **glow plane 실제 크기(베젤 제외)** 기준으로
    /// 거리를 자동 계산한다. LaptopFocusOverlay는 뷰포트 전체를 덮으므로,
    /// 결과적으로 오버레이가 디스플레이 내부(베젤 안쪽)에만 렌더된다.
    ///
    /// - screenHeight: glow plane의 실제 세로(m). `DeskLaptopEntity`에서 0.22 - 0.02 = 0.20.
    /// - fillRatio: 뷰포트 높이 대비 glow 높이 비율 (1.0 이상이면 살짝 잘려 베젤이 완전히 사라짐).
    static func moveToLaptop(
        camera: PerspectiveCamera,
        screenPos: SIMD3<Float>,
        screenNormal: SIMD3<Float>,
        screenHeight: Float = 0.20,
        fillRatio: Float = 1.02,
        fovDeg: Float = 25,
        duration: TimeInterval = 0.6
    ) {
        camera.camera.fieldOfViewInDegrees = fovDeg
        let fovRad = fovDeg * .pi / 180
        // 수직 시야(m) = 2·d·tan(FOV/2). 이걸 screenHeight/fillRatio 와 같게 해서 d 역산.
        let viewportHeightAtD = screenHeight / fillRatio
        let distance = viewportHeightAtD / (2 * tan(fovRad / 2))
        let n = simd_normalize(screenNormal)
        let camPos = screenPos + n * distance
        animateLook(camera: camera, from: camPos, at: screenPos, duration: duration)
    }

    /// 타겟을 기준으로 +z 방향 거리 클램프
    static func zoom(camera: PerspectiveCamera, delta: Float, targetZ: Float = 0) {
        let currentZ = camera.position.z
        let candidateZ = currentZ - delta
        let newZ = max(targetZ + minDistanceFromTarget, min(targetZ + maxDistanceFromTarget, candidateZ))
        var transform = camera.transform
        transform.translation.z = newZ
        camera.move(to: transform, relativeTo: nil, duration: 0.15)
    }

    // MARK: - Private

    private static func applyOverview(
        to camera: PerspectiveCamera,
        bookshelfZ: Float,
        animated: Bool,
        duration: TimeInterval = 0.7
    ) {
        // 코너 3/4 구도: 오른쪽-앞-위에서 책장이 있는 뒷벽을 바라봄 (조금 더 뒤에서)
        let camPos = SIMD3<Float>(1.6, 1.5, bookshelfZ + 3.2)
        let lookAt = SIMD3<Float>(-0.3, 0.9, bookshelfZ + 0.2)

        if animated {
            animateLook(camera: camera, from: camPos, at: lookAt, duration: duration)
        } else {
            camera.position = camPos
            camera.look(at: lookAt, from: camPos, relativeTo: nil)
        }
    }

    private static func animateLook(
        camera: PerspectiveCamera,
        from camPos: SIMD3<Float>,
        at lookAt: SIMD3<Float>,
        duration: TimeInterval
    ) {
        // 타겟 transform을 만들기 위해 임시 엔티티 사용
        let temp = Entity()
        temp.position = camPos
        temp.look(at: lookAt, from: camPos, relativeTo: nil)
        camera.move(to: temp.transform, relativeTo: nil, duration: duration)
    }
}
