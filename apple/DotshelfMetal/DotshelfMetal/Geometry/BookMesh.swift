import Metal
import simd

struct BookMesh {
    static func create(device: MTLDevice, thickness: Float = 0.04, height: Float = 0.28, width: Float = 0.20) -> (vertexBuffer: MTLBuffer, indexBuffer: MTLBuffer, indexCount: Int) {
        let hw = width * 0.5
        let hh = height * 0.5
        let ht = thickness * 0.5

        let vertices: [Vertex] = [
            // Front (+Z)
            Vertex(position: [-hw, -hh,  ht], normal: [0, 0, 1], uv: [0, 1], tangent: [1, 0, 0]),
            Vertex(position: [ hw, -hh,  ht], normal: [0, 0, 1], uv: [1, 1], tangent: [1, 0, 0]),
            Vertex(position: [ hw,  hh,  ht], normal: [0, 0, 1], uv: [1, 0], tangent: [1, 0, 0]),
            Vertex(position: [-hw,  hh,  ht], normal: [0, 0, 1], uv: [0, 0], tangent: [1, 0, 0]),
            // Back (-Z)
            Vertex(position: [ hw, -hh, -ht], normal: [0, 0, -1], uv: [0, 1], tangent: [-1, 0, 0]),
            Vertex(position: [-hw, -hh, -ht], normal: [0, 0, -1], uv: [1, 1], tangent: [-1, 0, 0]),
            Vertex(position: [-hw,  hh, -ht], normal: [0, 0, -1], uv: [1, 0], tangent: [-1, 0, 0]),
            Vertex(position: [ hw,  hh, -ht], normal: [0, 0, -1], uv: [0, 0], tangent: [-1, 0, 0]),
            // Top (+Y)
            Vertex(position: [-hw,  hh,  ht], normal: [0, 1, 0], uv: [0, 1], tangent: [1, 0, 0]),
            Vertex(position: [ hw,  hh,  ht], normal: [0, 1, 0], uv: [1, 1], tangent: [1, 0, 0]),
            Vertex(position: [ hw,  hh, -ht], normal: [0, 1, 0], uv: [1, 0], tangent: [1, 0, 0]),
            Vertex(position: [-hw,  hh, -ht], normal: [0, 1, 0], uv: [0, 0], tangent: [1, 0, 0]),
            // Bottom (-Y)
            Vertex(position: [-hw, -hh, -ht], normal: [0, -1, 0], uv: [0, 1], tangent: [1, 0, 0]),
            Vertex(position: [ hw, -hh, -ht], normal: [0, -1, 0], uv: [1, 1], tangent: [1, 0, 0]),
            Vertex(position: [ hw, -hh,  ht], normal: [0, -1, 0], uv: [1, 0], tangent: [1, 0, 0]),
            Vertex(position: [-hw, -hh,  ht], normal: [0, -1, 0], uv: [0, 0], tangent: [1, 0, 0]),
            // Right (+X) — spine
            Vertex(position: [ hw, -hh,  ht], normal: [1, 0, 0], uv: [0, 1], tangent: [0, 0, -1]),
            Vertex(position: [ hw, -hh, -ht], normal: [1, 0, 0], uv: [1, 1], tangent: [0, 0, -1]),
            Vertex(position: [ hw,  hh, -ht], normal: [1, 0, 0], uv: [1, 0], tangent: [0, 0, -1]),
            Vertex(position: [ hw,  hh,  ht], normal: [1, 0, 0], uv: [0, 0], tangent: [0, 0, -1]),
            // Left (-X)
            Vertex(position: [-hw, -hh, -ht], normal: [-1, 0, 0], uv: [0, 1], tangent: [0, 0, 1]),
            Vertex(position: [-hw, -hh,  ht], normal: [-1, 0, 0], uv: [1, 1], tangent: [0, 0, 1]),
            Vertex(position: [-hw,  hh,  ht], normal: [-1, 0, 0], uv: [1, 0], tangent: [0, 0, 1]),
            Vertex(position: [-hw,  hh, -ht], normal: [-1, 0, 0], uv: [0, 0], tangent: [0, 0, 1]),
        ]

        let indices: [UInt16] = [
            0, 1, 2, 0, 2, 3,
            4, 5, 6, 4, 6, 7,
            8, 9, 10, 8, 10, 11,
            12, 13, 14, 12, 14, 15,
            16, 17, 18, 16, 18, 19,
            20, 21, 22, 20, 22, 23,
        ]

        let vertexBuffer = device.makeBuffer(bytes: vertices, length: MemoryLayout<Vertex>.stride * vertices.count, options: .storageModeShared)!
        let indexBuffer = device.makeBuffer(bytes: indices, length: MemoryLayout<UInt16>.stride * indices.count, options: .storageModeShared)!

        return (vertexBuffer, indexBuffer, indices.count)
    }
}
