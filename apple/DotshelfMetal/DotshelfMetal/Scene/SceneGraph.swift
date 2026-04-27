import Metal
import simd

struct BookMetadata {
    var thickness: Float
    var height: Float
    var width: Float
}

final class SceneNode {
    var localTransform: float4x4 = .identity
    var worldTransform: float4x4 = .identity
    var children: [SceneNode] = []
    var mesh: MeshRef?
    var material: MaterialUniformsSwift?
    var name: String = ""
    var bookMetadata: BookMetadata?

    // Book interaction state
    var isPulledOut: Bool = false
    var pullOffset: Float = 0.0
    var targetPullOffset: Float = 0.0

    // View mode: book floats in front of camera, overrides localTransform
    var viewModeOverride: float4x4? = nil    // if set, replaces localTransform
    var viewModeTarget: float4x4? = nil      // target for smooth lerp
    var viewModeRotation: simd_quatf = simd_quatf(angle: 0, axis: SIMD3(0, 1, 0))

    var albedoTexture: MTLTexture?
    var normalTexture: MTLTexture?
    var roughnessTexture: MTLTexture?

    struct MeshRef {
        var vertexBuffer: MTLBuffer
        var indexBuffer: MTLBuffer
        var indexCount: Int
    }

    func addChild(_ child: SceneNode) {
        children.append(child)
    }

    func removeChild(_ child: SceneNode) {
        children.removeAll { $0 === child }
    }

    func updateWorldTransforms(parentTransform: float4x4 = .identity) {
        let base = viewModeOverride ?? localTransform
        worldTransform = parentTransform * base

        // Apply pull animation offset only if not in view mode
        if viewModeOverride == nil && abs(pullOffset) > 0.001 {
            worldTransform = worldTransform * float4x4.translation(SIMD3<Float>(0, 0, pullOffset))
        }

        for child in children {
            child.updateWorldTransforms(parentTransform: worldTransform)
        }
    }

    func updateAnimations() {
        // Pull animation
        pullOffset += (targetPullOffset - pullOffset) * 0.15

        // View mode transform lerp
        if let target = viewModeTarget {
            if var current = viewModeOverride {
                // Lerp translation
                let currentTrans = SIMD3<Float>(current.columns.3.x, current.columns.3.y, current.columns.3.z)
                let targetTrans = SIMD3<Float>(target.columns.3.x, target.columns.3.y, target.columns.3.z)
                let lerpTrans = currentTrans + (targetTrans - currentTrans) * 0.18

                // Smoothly approach the target matrix (including rotation)
                let t: Float = 0.18
                current.columns.0 = current.columns.0 + (target.columns.0 - current.columns.0) * t
                current.columns.1 = current.columns.1 + (target.columns.1 - current.columns.1) * t
                current.columns.2 = current.columns.2 + (target.columns.2 - current.columns.2) * t
                current.columns.3 = SIMD4<Float>(lerpTrans.x, lerpTrans.y, lerpTrans.z, 1)
                viewModeOverride = current
            } else {
                viewModeOverride = target
            }
        }

        for child in children {
            child.updateAnimations()
        }
    }

    func allRenderableNodes() -> [SceneNode] {
        var result: [SceneNode] = []
        if mesh != nil {
            result.append(self)
        }
        for child in children {
            result.append(contentsOf: child.allRenderableNodes())
        }
        return result
    }
}
