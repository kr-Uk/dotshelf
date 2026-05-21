import simd

extension float4x4 {
    static func perspective(fov: Float, aspect: Float, near: Float, far: Float) -> float4x4 {
        let y = 1.0 / tanf(fov * 0.5)
        let x = y / aspect
        let z = far / (near - far)
        return float4x4(columns: (
            SIMD4<Float>(x, 0, 0,  0),
            SIMD4<Float>(0, y, 0,  0),
            SIMD4<Float>(0, 0, z, -1),
            SIMD4<Float>(0, 0, z * near, 0)
        ))
    }

    static func lookAt(eye: SIMD3<Float>, center: SIMD3<Float>, up: SIMD3<Float>) -> float4x4 {
        let f = normalize(center - eye)
        let s = normalize(cross(f, up))
        let u = cross(s, f)
        return float4x4(columns: (
            SIMD4<Float>(s.x, u.x, -f.x, 0),
            SIMD4<Float>(s.y, u.y, -f.y, 0),
            SIMD4<Float>(s.z, u.z, -f.z, 0),
            SIMD4<Float>(-dot(s, eye), -dot(u, eye), dot(f, eye), 1)
        ))
    }

    static func translation(_ t: SIMD3<Float>) -> float4x4 {
        return float4x4(columns: (
            SIMD4<Float>(1, 0, 0, 0),
            SIMD4<Float>(0, 1, 0, 0),
            SIMD4<Float>(0, 0, 1, 0),
            SIMD4<Float>(t.x, t.y, t.z, 1)
        ))
    }

    static func rotation(angle: Float, axis: SIMD3<Float>) -> float4x4 {
        let a = normalize(axis)
        let c = cosf(angle)
        let s = sinf(angle)
        let t = 1 - c
        return float4x4(columns: (
            SIMD4<Float>(t * a.x * a.x + c,       t * a.x * a.y + s * a.z, t * a.x * a.z - s * a.y, 0),
            SIMD4<Float>(t * a.x * a.y - s * a.z, t * a.y * a.y + c,       t * a.y * a.z + s * a.x, 0),
            SIMD4<Float>(t * a.x * a.z + s * a.y, t * a.y * a.z - s * a.x, t * a.z * a.z + c,       0),
            SIMD4<Float>(0, 0, 0, 1)
        ))
    }

    static func scale(_ s: SIMD3<Float>) -> float4x4 {
        return float4x4(columns: (
            SIMD4<Float>(s.x, 0, 0, 0),
            SIMD4<Float>(0, s.y, 0, 0),
            SIMD4<Float>(0, 0, s.z, 0),
            SIMD4<Float>(0, 0, 0, 1)
        ))
    }

    static func orthographic(left: Float, right: Float, bottom: Float, top: Float, near: Float, far: Float) -> float4x4 {
        let sx = 2.0 / (right - left)
        let sy = 2.0 / (top - bottom)
        let sz = 1.0 / (near - far)
        let tx = -(right + left) / (right - left)
        let ty = -(top + bottom) / (top - bottom)
        let tz = near / (near - far)
        return float4x4(columns: (
            SIMD4<Float>(sx, 0,  0,  0),
            SIMD4<Float>(0,  sy, 0,  0),
            SIMD4<Float>(0,  0,  sz, 0),
            SIMD4<Float>(tx, ty, tz, 1)
        ))
    }

    static let identity = matrix_identity_float4x4
}
