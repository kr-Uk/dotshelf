import simd

// SIMD3<Float> is backed by SIMD4Storage (16 bytes).
// Its w component (= 0) can overwrite the next field in a struct.
// Use packed (Float, Float, Float) tuples for uniform structs to avoid this.

struct PackedFloat3 {
    var x: Float
    var y: Float
    var z: Float

    init(_ v: SIMD3<Float>) {
        x = v.x; y = v.y; z = v.z
    }
    init(_ x: Float, _ y: Float, _ z: Float) {
        self.x = x; self.y = y; self.z = z
    }
    static let zero = PackedFloat3(0, 0, 0)
}

struct DirectionalLightSwift {
    var direction: PackedFloat3
    var _pad0: Float = 0
    var color: PackedFloat3
    var intensity: Float
}

struct PointLightSwift {
    var position: PackedFloat3
    var _pad0: Float = 0
    var color: PackedFloat3
    var intensity: Float
    var attenuationRadius: Float
    var _pad1: Float = 0
    var _pad2: Float = 0
    var _pad3: Float = 0
}

struct LightDataSwift {
    var directional: DirectionalLightSwift
    var pointLights: (PointLightSwift, PointLightSwift, PointLightSwift, PointLightSwift)
    var pointLightCount: Int32
    var _pad0: Float = 0
    var _pad1: Float = 0
    var _pad2: Float = 0
    var ambientColor: PackedFloat3
    var ambientIntensity: Float

    static func defaultLighting() -> LightDataSwift {
        // Directional light from upper-front, angled so it reaches into the shelves
        let dir = normalize(SIMD3<Float>(-0.3, -0.6, -1.0))
        let directional = DirectionalLightSwift(
            direction: PackedFloat3(dir),
            color: PackedFloat3(1.0, 0.96, 0.9),
            intensity: 2.5
        )

        // Point lights positioned in front of the bookshelf for fill lighting
        let p1 = PointLightSwift(
            position: PackedFloat3(0.8, 1.8, 1.5),
            color: PackedFloat3(1.0, 0.9, 0.7),
            intensity: 2.0,
            attenuationRadius: 6.0
        )
        let p2 = PointLightSwift(
            position: PackedFloat3(-0.8, 1.2, 1.5),
            color: PackedFloat3(1.0, 0.9, 0.7),
            intensity: 2.0,
            attenuationRadius: 6.0
        )
        let empty = PointLightSwift(
            position: .zero,
            color: .zero, intensity: 0, attenuationRadius: 0
        )

        return LightDataSwift(
            directional: directional,
            pointLights: (p1, p2, empty, empty),
            pointLightCount: 2,
            ambientColor: PackedFloat3(0.5, 0.48, 0.45),
            ambientIntensity: 1.0
        )
    }
}
