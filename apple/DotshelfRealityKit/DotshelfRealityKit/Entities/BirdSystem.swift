import RealityKit

/// 매 프레임 새의 날갯짓 + 비행 보간을 업데이트하는 시스템.
struct BirdSystem: System {
    static let query = EntityQuery(where: .has(BirdComponent.self))

    init(scene: Scene) {}

    func update(context: SceneUpdateContext) {
        let dt = Float(context.deltaTime)

        context.scene.performQuery(Self.query).forEach { entity in
            guard var comp = entity.components[BirdComponent.self] else { return }

            comp.timeElapsed += dt

            // 날갯짓: 좌/우 피벗을 반대 방향 Z축 회전
            let flap = sin(comp.timeElapsed * comp.flapSpeed) * comp.flapAmplitude
            entity.findEntity(named: "bird_wing_left")?.orientation =
                simd_quatf(angle: flap, axis: SIMD3<Float>(0, 0, 1))
            entity.findEntity(named: "bird_wing_right")?.orientation =
                simd_quatf(angle: -flap, axis: SIMD3<Float>(0, 0, 1))

            // 비행 보간 (smoothstep)
            if comp.flightDuration > 0 {
                comp.flightProgress += dt / comp.flightDuration
                if comp.flightProgress >= 1.0 {
                    entity.components.set(comp)
                    BirdEntity.pickNextPath(for: entity)
                    return
                }
                let t = smoothStep(comp.flightProgress)
                entity.position = mix(comp.flightStart, comp.flightEnd, t: t)
            }

            entity.components.set(comp)
        }
    }

    private func smoothStep(_ x: Float) -> Float {
        let c = max(0, min(1, x))
        return c * c * (3 - 2 * c)
    }

    private func mix(_ a: SIMD3<Float>, _ b: SIMD3<Float>, t: Float) -> SIMD3<Float> {
        a + (b - a) * t
    }
}
