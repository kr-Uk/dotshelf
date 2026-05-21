import RealityKit
import Foundation

enum BookAnimation {
    static func liftBook(entity: Entity) {
        var transform = entity.transform
        transform.translation.z += 0.12
        entity.move(to: transform, relativeTo: entity.parent, duration: 0.15)
    }

    static func snapToPosition(entity: Entity, position: SIMD3<Float>, duration: TimeInterval = 0.25) {
        var transform = entity.transform
        transform.translation = position
        entity.move(to: transform, relativeTo: entity.parent, duration: duration)
    }

    static func simulateGravityDrop(
        entity: Entity,
        from startY: Float,
        targetY: Float,
        duration: TimeInterval = 0.4
    ) {
        var transform = entity.transform
        transform.translation.y = targetY
        transform.translation.z = 0
        entity.move(to: transform, relativeTo: entity.parent, duration: duration)
    }
}
