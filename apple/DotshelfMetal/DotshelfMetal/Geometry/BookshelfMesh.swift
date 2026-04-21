import Metal
import simd

struct BookshelfMesh {
    struct MeshData {
        var vertexBuffer: MTLBuffer
        var indexBuffer: MTLBuffer
        var indexCount: Int
    }

    static func create(
        device: MTLDevice,
        columns: Int = 2,
        rows: Int = 3,
        width: Float = 1.8,
        height: Float = 2.0,
        depth: Float = 0.35
    ) -> [MeshData] {
        var meshes: [MeshData] = []
        let boardThickness: Float = 0.03

        let columnWidth = (width - Float(columns + 1) * boardThickness) / Float(columns)
        let rowHeight = (height - Float(rows + 1) * boardThickness) / Float(rows)

        // Back panel
        meshes.append(createBox(device: device,
                                w: width, h: height, d: boardThickness,
                                offset: SIMD3<Float>(0, height * 0.5, -depth * 0.5 + boardThickness * 0.5)))

        // Horizontal shelves (rows + 1)
        for r in 0...rows {
            let y = Float(r) * (rowHeight + boardThickness) + boardThickness * 0.5
            meshes.append(createBox(device: device,
                                    w: width, h: boardThickness, d: depth,
                                    offset: SIMD3<Float>(0, y, 0)))
        }

        // Vertical dividers (columns + 1)
        for c in 0...columns {
            let x = -width * 0.5 + Float(c) * (columnWidth + boardThickness) + boardThickness * 0.5
            meshes.append(createBox(device: device,
                                    w: boardThickness, h: height, d: depth,
                                    offset: SIMD3<Float>(x, height * 0.5, 0)))
        }

        return meshes
    }

    private static func createBox(device: MTLDevice, w: Float, h: Float, d: Float, offset: SIMD3<Float>) -> MeshData {
        let hw = w * 0.5, hh = h * 0.5, hd = d * 0.5
        let ox = offset.x, oy = offset.y, oz = offset.z

        let vertices: [Vertex] = [
            // Front
            Vertex(position: [ox - hw, oy - hh, oz + hd], normal: [0, 0, 1], uv: [0, 1], tangent: [1, 0, 0]),
            Vertex(position: [ox + hw, oy - hh, oz + hd], normal: [0, 0, 1], uv: [1, 1], tangent: [1, 0, 0]),
            Vertex(position: [ox + hw, oy + hh, oz + hd], normal: [0, 0, 1], uv: [1, 0], tangent: [1, 0, 0]),
            Vertex(position: [ox - hw, oy + hh, oz + hd], normal: [0, 0, 1], uv: [0, 0], tangent: [1, 0, 0]),
            // Back
            Vertex(position: [ox + hw, oy - hh, oz - hd], normal: [0, 0, -1], uv: [0, 1], tangent: [-1, 0, 0]),
            Vertex(position: [ox - hw, oy - hh, oz - hd], normal: [0, 0, -1], uv: [1, 1], tangent: [-1, 0, 0]),
            Vertex(position: [ox - hw, oy + hh, oz - hd], normal: [0, 0, -1], uv: [1, 0], tangent: [-1, 0, 0]),
            Vertex(position: [ox + hw, oy + hh, oz - hd], normal: [0, 0, -1], uv: [0, 0], tangent: [-1, 0, 0]),
            // Top
            Vertex(position: [ox - hw, oy + hh, oz + hd], normal: [0, 1, 0], uv: [0, 1], tangent: [1, 0, 0]),
            Vertex(position: [ox + hw, oy + hh, oz + hd], normal: [0, 1, 0], uv: [1, 1], tangent: [1, 0, 0]),
            Vertex(position: [ox + hw, oy + hh, oz - hd], normal: [0, 1, 0], uv: [1, 0], tangent: [1, 0, 0]),
            Vertex(position: [ox - hw, oy + hh, oz - hd], normal: [0, 1, 0], uv: [0, 0], tangent: [1, 0, 0]),
            // Bottom
            Vertex(position: [ox - hw, oy - hh, oz - hd], normal: [0, -1, 0], uv: [0, 1], tangent: [1, 0, 0]),
            Vertex(position: [ox + hw, oy - hh, oz - hd], normal: [0, -1, 0], uv: [1, 1], tangent: [1, 0, 0]),
            Vertex(position: [ox + hw, oy - hh, oz + hd], normal: [0, -1, 0], uv: [1, 0], tangent: [1, 0, 0]),
            Vertex(position: [ox - hw, oy - hh, oz + hd], normal: [0, -1, 0], uv: [0, 0], tangent: [1, 0, 0]),
            // Right
            Vertex(position: [ox + hw, oy - hh, oz + hd], normal: [1, 0, 0], uv: [0, 1], tangent: [0, 0, -1]),
            Vertex(position: [ox + hw, oy - hh, oz - hd], normal: [1, 0, 0], uv: [1, 1], tangent: [0, 0, -1]),
            Vertex(position: [ox + hw, oy + hh, oz - hd], normal: [1, 0, 0], uv: [1, 0], tangent: [0, 0, -1]),
            Vertex(position: [ox + hw, oy + hh, oz + hd], normal: [1, 0, 0], uv: [0, 0], tangent: [0, 0, -1]),
            // Left
            Vertex(position: [ox - hw, oy - hh, oz - hd], normal: [-1, 0, 0], uv: [0, 1], tangent: [0, 0, 1]),
            Vertex(position: [ox - hw, oy - hh, oz + hd], normal: [-1, 0, 0], uv: [1, 1], tangent: [0, 0, 1]),
            Vertex(position: [ox - hw, oy + hh, oz + hd], normal: [-1, 0, 0], uv: [1, 0], tangent: [0, 0, 1]),
            Vertex(position: [ox - hw, oy + hh, oz - hd], normal: [-1, 0, 0], uv: [0, 0], tangent: [0, 0, 1]),
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

        return MeshData(vertexBuffer: vertexBuffer, indexBuffer: indexBuffer, indexCount: indices.count)
    }
}
