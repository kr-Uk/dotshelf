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

    /// 화이트보드(좌측 벽) 정면 뷰로 카메라 애니메이션
    static func moveToWhiteboard(camera: PerspectiveCamera, wallX: Float = -2.0, duration: TimeInterval = 0.7) {
        camera.camera.fieldOfViewInDegrees = 45
        // 화이트보드 중심 월드좌표: (wallX + 0.005, 1.25, 0)
        let targetX = wallX + 0.005
        let camPos = SIMD3<Float>(targetX + 1.8, 1.25, 0)
        let lookAt = SIMD3<Float>(targetX, 1.25, 0)
        animateLook(camera: camera, from: camPos, at: lookAt, duration: duration)
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
