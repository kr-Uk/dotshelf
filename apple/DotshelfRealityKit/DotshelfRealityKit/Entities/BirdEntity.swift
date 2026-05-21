import RealityKit
import Foundation

/// 새 1마리의 날갯짓 위상과 비행 보간 상태.
struct BirdComponent: Component {
    var timeElapsed: Float = 0
    var flapSpeed: Float = 11         // rad/s
    var flapAmplitude: Float = 0.6    // rad (~34°)

    var flightStart: SIMD3<Float> = .zero
    var flightEnd: SIMD3<Float> = .zero
    var flightDuration: Float = 5.0
    var flightProgress: Float = 0     // 0..1
}

/// 간단한 3D 새: 몸통 + 머리 + 꼬리 + 좌/우 날개.
/// - 로컬 +Z가 비행 방향(정면), +X가 오른쪽.
/// - 날개는 pivot 앵커 엔티티에 달려 Z축 회전으로 플래핑.
enum BirdEntity {
    static func create() -> Entity {
        let bird = Entity()
        bird.name = "bird"

        let material = MaterialFactory.birdMaterial()

        // 몸통 (Z축으로 길쭉)
        let body = ModelEntity(
            mesh: .generateBox(width: 0.04, height: 0.04, depth: 0.11, cornerRadius: 0.018),
            materials: [material]
        )
        body.name = "bird_body"
        bird.addChild(body)

        // 머리 (앞쪽 +Z, 약간 위)
        let head = ModelEntity(
            mesh: .generateBox(width: 0.035, height: 0.035, depth: 0.035, cornerRadius: 0.015),
            materials: [material]
        )
        head.position = SIMD3<Float>(0, 0.012, 0.065)
        head.name = "bird_head"
        bird.addChild(head)

        // 꼬리 (뒤쪽 -Z, 약간 위)
        let tail = ModelEntity(
            mesh: .generateBox(width: 0.02, height: 0.028, depth: 0.04, cornerRadius: 0.005),
            materials: [material]
        )
        tail.position = SIMD3<Float>(0, 0.014, -0.075)
        tail.name = "bird_tail"
        bird.addChild(tail)

        // 날개: 앵커(피벗) + 실제 윙 메시
        let wingSpanHalf: Float = 0.14
        let wingChord: Float = 0.08
        let wingThickness: Float = 0.006

        let leftAnchor = Entity()
        leftAnchor.name = "bird_wing_left"
        leftAnchor.position = SIMD3<Float>(-0.02, 0, 0)
        let leftWing = ModelEntity(
            mesh: .generateBox(width: wingSpanHalf, height: wingThickness, depth: wingChord),
            materials: [material]
        )
        leftWing.position = SIMD3<Float>(-wingSpanHalf / 2, 0, 0)
        leftAnchor.addChild(leftWing)
        bird.addChild(leftAnchor)

        let rightAnchor = Entity()
        rightAnchor.name = "bird_wing_right"
        rightAnchor.position = SIMD3<Float>(0.02, 0, 0)
        let rightWing = ModelEntity(
            mesh: .generateBox(width: wingSpanHalf, height: wingThickness, depth: wingChord),
            materials: [material]
        )
        rightWing.position = SIMD3<Float>(wingSpanHalf / 2, 0, 0)
        rightAnchor.addChild(rightWing)
        bird.addChild(rightAnchor)

        bird.components.set(BirdComponent())
        return bird
    }

    /// 초기 비행 경로 설정 (창문 앞을 가로지름)
    static func initializeFlight(on bird: Entity) {
        guard var comp = bird.components[BirdComponent.self] else { return }
        let path = randomCrossingPath()
        comp.flightStart = path.start
        comp.flightEnd = path.end
        comp.flightDuration = path.duration
        comp.flightProgress = 0
        bird.components.set(comp)

        bird.position = path.start
        applyFacing(bird: bird, from: path.start, to: path.end)
    }

    /// 현재 위치에서 다음 경로로 이어감. 주로 반대쪽으로 가로지름.
    static func pickNextPath(for bird: Entity) {
        guard var comp = bird.components[BirdComponent.self] else { return }
        let currentPos = bird.position

        let preferCrossing = Float.random(in: 0...1) < 0.75
        let targetZ: Float
        if preferCrossing {
            targetZ = currentPos.z > 0
                ? Float.random(in: -2.0 ... -1.0)
                : Float.random(in: 1.0 ... 2.0)
        } else {
            targetZ = Float.random(in: -2.0 ... 2.0)
        }
        let targetY = Float.random(in: 1.15 ... 2.15)
        let targetX = Float.random(in: 2.4 ... 3.2)
        let targetPos = SIMD3<Float>(targetX, targetY, targetZ)

        let distance = simd_length(targetPos - currentPos)
        let duration = max(2.8, min(6.0, distance / 0.75))

        comp.flightStart = currentPos
        comp.flightEnd = targetPos
        comp.flightDuration = duration
        comp.flightProgress = 0
        bird.components.set(comp)

        applyFacing(bird: bird, from: currentPos, to: targetPos)
    }

    /// 이동 방향(수평 yaw)으로 머리를 돌림.
    static func applyFacing(bird: Entity, from: SIMD3<Float>, to: SIMD3<Float>) {
        let dir = to - from
        let yaw = atan2(dir.x, dir.z)
        bird.orientation = simd_quatf(angle: yaw, axis: SIMD3<Float>(0, 1, 0))
    }

    private static func randomCrossingPath() -> (start: SIMD3<Float>, end: SIMD3<Float>, duration: Float) {
        let startAtNegZ = Bool.random()
        let startZ: Float = startAtNegZ ? Float.random(in: -2.0 ... -1.4) : Float.random(in: 1.4 ... 2.0)
        let endZ: Float = startAtNegZ ? Float.random(in: 1.4 ... 2.0) : Float.random(in: -2.0 ... -1.4)
        let startY = Float.random(in: 1.3 ... 2.0)
        let endY = Float.random(in: 1.3 ... 2.0)
        let startX = Float.random(in: 2.4 ... 3.0)
        let endX = Float.random(in: 2.4 ... 3.0)
        return (
            SIMD3<Float>(startX, startY, startZ),
            SIMD3<Float>(endX, endY, endZ),
            Float.random(in: 3.8 ... 5.5)
        )
    }
}
