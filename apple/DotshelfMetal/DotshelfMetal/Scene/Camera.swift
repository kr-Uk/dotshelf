import simd

final class Camera {
    var eye: SIMD3<Float>
    var center: SIMD3<Float>
    var up: SIMD3<Float> = SIMD3<Float>(0, 1, 0)
    var fov: Float = Float.pi / 4
    var aspect: Float = 1.0
    var near: Float = 0.1
    var far: Float = 100.0

    private var targetZoom: Float
    private var currentZoom: Float
    private let minZoom: Float = 0.5
    private let maxZoom: Float = 3.0
    private let lerpSpeed: Float = 0.1

    init(eye: SIMD3<Float> = SIMD3<Float>(0, 1.0, 3.0),
         center: SIMD3<Float> = SIMD3<Float>(0, 1.0, 0)) {
        self.eye = eye
        self.center = center
        self.targetZoom = eye.z
        self.currentZoom = eye.z
    }

    var viewMatrix: float4x4 {
        float4x4.lookAt(eye: eye, center: center, up: up)
    }

    var projectionMatrix: float4x4 {
        float4x4.perspective(fov: fov, aspect: aspect, near: near, far: far)
    }

    func zoom(delta: Float) {
        targetZoom = min(max(targetZoom - delta, minZoom), maxZoom)
    }

    func update() {
        currentZoom += (targetZoom - currentZoom) * lerpSpeed
        eye.z = currentZoom
    }
}
