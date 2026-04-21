import simd

struct Ray {
    var origin: SIMD3<Float>
    var direction: SIMD3<Float>
}

struct AABB {
    var min: SIMD3<Float>
    var max: SIMD3<Float>

    func intersects(ray: Ray) -> Float? {
        var tmin: Float = -Float.infinity
        var tmax: Float = Float.infinity

        for i in 0..<3 {
            if abs(ray.direction[i]) < 1e-8 {
                if ray.origin[i] < min[i] || ray.origin[i] > max[i] {
                    return nil
                }
            } else {
                let invD = 1.0 / ray.direction[i]
                var t0 = (min[i] - ray.origin[i]) * invD
                var t1 = (max[i] - ray.origin[i]) * invD
                if invD < 0 { swap(&t0, &t1) }
                tmin = Swift.max(tmin, t0)
                tmax = Swift.min(tmax, t1)
                if tmax < tmin { return nil }
            }
        }

        return tmin > 0 ? tmin : (tmax > 0 ? tmax : nil)
    }
}

struct Raycaster {
    static func screenToWorldRay(screenPoint: SIMD2<Float>, viewportSize: SIMD2<Float>, camera: Camera) -> Ray {
        // NDC
        let ndcX = (2.0 * screenPoint.x / viewportSize.x) - 1.0
        let ndcY = 1.0 - (2.0 * screenPoint.y / viewportSize.y)

        let invProj = camera.projectionMatrix.inverse
        let invView = camera.viewMatrix.inverse

        let clipNear = SIMD4<Float>(ndcX, ndcY, 0, 1)
        let clipFar = SIMD4<Float>(ndcX, ndcY, 1, 1)

        var eyeNear = invProj * clipNear
        eyeNear /= eyeNear.w
        var eyeFar = invProj * clipFar
        eyeFar /= eyeFar.w

        let worldNear = invView * eyeNear
        let worldFar = invView * eyeFar

        let origin = SIMD3<Float>(worldNear.x, worldNear.y, worldNear.z)
        let dir = normalize(SIMD3<Float>(worldFar.x - worldNear.x, worldFar.y - worldNear.y, worldFar.z - worldNear.z))

        return Ray(origin: origin, direction: dir)
    }

    /// Ray와 z=planeZ 평면의 교점 (x, y 평면은 아니고 XY평면 at z=planeZ).
    static func intersectZPlane(ray: Ray, planeZ: Float) -> SIMD3<Float>? {
        let dz = ray.direction.z
        if abs(dz) < 1e-6 { return nil }
        let t = (planeZ - ray.origin.z) / dz
        if t < 0 { return nil }
        return ray.origin + ray.direction * t
    }

    static func computeAABB(for node: SceneNode) -> AABB? {
        guard node.mesh != nil else { return nil }
        // Approximate AABB from world transform + unit box assumption
        let t = node.worldTransform
        let center = SIMD3<Float>(t.columns.3.x, t.columns.3.y, t.columns.3.z)
        // Rough half-extents based on book dimensions
        let halfExtent = SIMD3<Float>(0.12, 0.16, 0.04)
        return AABB(min: center - halfExtent, max: center + halfExtent)
    }
}
