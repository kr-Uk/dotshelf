import simd

struct Vertex {
    var position: SIMD3<Float>
    var normal: SIMD3<Float>
    var uv: SIMD2<Float>
    var tangent: SIMD3<Float>
}
