import Metal
import simd

struct MeshGenerator {
    static func createCube(device: MTLDevice, size: Float = 1.0) -> (vertexBuffer: MTLBuffer, indexBuffer: MTLBuffer, indexCount: Int) {
        let h = size * 0.5
        let vertices: [Vertex] = [
            // Front face (+Z)
            Vertex(position: [-h, -h,  h], normal: [0, 0, 1], uv: [0, 1], tangent: [1, 0, 0]),
            Vertex(position: [ h, -h,  h], normal: [0, 0, 1], uv: [1, 1], tangent: [1, 0, 0]),
            Vertex(position: [ h,  h,  h], normal: [0, 0, 1], uv: [1, 0], tangent: [1, 0, 0]),
            Vertex(position: [-h,  h,  h], normal: [0, 0, 1], uv: [0, 0], tangent: [1, 0, 0]),
            // Back face (-Z)
            Vertex(position: [ h, -h, -h], normal: [0, 0, -1], uv: [0, 1], tangent: [-1, 0, 0]),
            Vertex(position: [-h, -h, -h], normal: [0, 0, -1], uv: [1, 1], tangent: [-1, 0, 0]),
            Vertex(position: [-h,  h, -h], normal: [0, 0, -1], uv: [1, 0], tangent: [-1, 0, 0]),
            Vertex(position: [ h,  h, -h], normal: [0, 0, -1], uv: [0, 0], tangent: [-1, 0, 0]),
            // Top face (+Y)
            Vertex(position: [-h,  h,  h], normal: [0, 1, 0], uv: [0, 1], tangent: [1, 0, 0]),
            Vertex(position: [ h,  h,  h], normal: [0, 1, 0], uv: [1, 1], tangent: [1, 0, 0]),
            Vertex(position: [ h,  h, -h], normal: [0, 1, 0], uv: [1, 0], tangent: [1, 0, 0]),
            Vertex(position: [-h,  h, -h], normal: [0, 1, 0], uv: [0, 0], tangent: [1, 0, 0]),
            // Bottom face (-Y)
            Vertex(position: [-h, -h, -h], normal: [0, -1, 0], uv: [0, 1], tangent: [1, 0, 0]),
            Vertex(position: [ h, -h, -h], normal: [0, -1, 0], uv: [1, 1], tangent: [1, 0, 0]),
            Vertex(position: [ h, -h,  h], normal: [0, -1, 0], uv: [1, 0], tangent: [1, 0, 0]),
            Vertex(position: [-h, -h,  h], normal: [0, -1, 0], uv: [0, 0], tangent: [1, 0, 0]),
            // Right face (+X)
            Vertex(position: [ h, -h,  h], normal: [1, 0, 0], uv: [0, 1], tangent: [0, 0, -1]),
            Vertex(position: [ h, -h, -h], normal: [1, 0, 0], uv: [1, 1], tangent: [0, 0, -1]),
            Vertex(position: [ h,  h, -h], normal: [1, 0, 0], uv: [1, 0], tangent: [0, 0, -1]),
            Vertex(position: [ h,  h,  h], normal: [1, 0, 0], uv: [0, 0], tangent: [0, 0, -1]),
            // Left face (-X)
            Vertex(position: [-h, -h, -h], normal: [-1, 0, 0], uv: [0, 1], tangent: [0, 0, 1]),
            Vertex(position: [-h, -h,  h], normal: [-1, 0, 0], uv: [1, 1], tangent: [0, 0, 1]),
            Vertex(position: [-h,  h,  h], normal: [-1, 0, 0], uv: [1, 0], tangent: [0, 0, 1]),
            Vertex(position: [-h,  h, -h], normal: [-1, 0, 0], uv: [0, 0], tangent: [0, 0, 1]),
        ]

        let indices: [UInt16] = [
            0, 1, 2, 0, 2, 3,       // front
            4, 5, 6, 4, 6, 7,       // back
            8, 9, 10, 8, 10, 11,    // top
            12, 13, 14, 12, 14, 15, // bottom
            16, 17, 18, 16, 18, 19, // right
            20, 21, 22, 20, 22, 23, // left
        ]

        let vertexBuffer = device.makeBuffer(bytes: vertices, length: MemoryLayout<Vertex>.stride * vertices.count, options: .storageModeShared)!
        let indexBuffer = device.makeBuffer(bytes: indices, length: MemoryLayout<UInt16>.stride * indices.count, options: .storageModeShared)!

        return (vertexBuffer, indexBuffer, indices.count)
    }
}
