import Metal
import simd

struct BookshelfScene {
    let root: SceneNode
    let state: BookshelfState
}

struct SlotSystem {
    static func buildScene(device: MTLDevice) -> BookshelfScene {
        let root = SceneNode()
        root.name = "root"

        let layout = BookshelfLayout.default
        let shelfWidth = layout.shelfWidth
        let shelfHeight = layout.shelfHeight
        let shelfDepth: Float = 0.35
        let columns = layout.columns
        let rows = layout.rows
        let boardThickness = layout.boardThickness

        // Bookshelf
        let shelfMeshes = BookshelfMesh.create(device: device, columns: columns, rows: rows,
                                                width: shelfWidth, height: shelfHeight, depth: shelfDepth)
        let woodAlbedo = TextureLoader.loadTexture(device: device, name: "Wood049_1K-JPG_Color.jpg")
        let woodNormal = TextureLoader.loadTexture(device: device, name: "Wood049_1K-JPG_NormalGL.jpg")
        let woodRoughness = TextureLoader.loadTexture(device: device, name: "Wood049_1K-JPG_Roughness.jpg")

        let shelfMaterial = MaterialUniformsSwift(
            albedoR: 0.545, albedoG: 0.412, albedoB: 0.078,
            metallic: 0.0,
            roughness: 0.7,
            hasAlbedoMap: woodAlbedo != nil ? 1 : 0,
            hasNormalMap: woodNormal != nil ? 1 : 0,
            hasRoughnessMap: woodRoughness != nil ? 1 : 0
        )

        for (i, meshData) in shelfMeshes.enumerated() {
            let node = SceneNode()
            node.name = "shelf_\(i)"
            node.mesh = SceneNode.MeshRef(vertexBuffer: meshData.vertexBuffer,
                                          indexBuffer: meshData.indexBuffer,
                                          indexCount: meshData.indexCount)
            node.material = shelfMaterial
            node.albedoTexture = woodAlbedo
            node.normalTexture = woodNormal
            node.roughnessTexture = woodRoughness
            root.addChild(node)
        }

        // Build BookshelfState with sections
        let state = BookshelfState()
        state.root = root
        let columnWidth = layout.columnWidth
        let rowHeight = layout.rowHeight

        for row in 0..<rows {
            let shelfTopY = Float(row) * (rowHeight + boardThickness) + boardThickness
            for col in 0..<columns {
                let leftBound = -shelfWidth * 0.5 + boardThickness + Float(col) * (columnWidth + boardThickness)
                let rightBound = leftBound + columnWidth
                let section = ShelfSection(
                    row: row, column: col,
                    leftBound: leftBound, rightBound: rightBound,
                    shelfTopY: shelfTopY,
                    sectionHeight: rowHeight
                )
                state.sections.append(section)
            }
        }

        // Book colors
        let bookColors: [SIMD3<Float>] = [
            SIMD3<Float>(0.75, 0.22, 0.17),   // red
            SIMD3<Float>(0.18, 0.35, 0.65),   // blue
            SIMD3<Float>(0.22, 0.55, 0.30),   // green
            SIMD3<Float>(0.55, 0.35, 0.18),   // brown
            SIMD3<Float>(0.30, 0.25, 0.55),   // indigo
            SIMD3<Float>(0.90, 0.88, 0.82),   // cream
            SIMD3<Float>(0.15, 0.15, 0.18),   // black
            SIMD3<Float>(0.85, 0.68, 0.20),   // gold
            SIMD3<Float>(0.80, 0.40, 0.15),   // orange
            SIMD3<Float>(0.60, 0.20, 0.45),   // magenta
        ]

        // Populate books into sections
        var bookIndex = 0
        for secIdx in 0..<state.sections.count {
            let sec = state.sections[secIdx]
            var x = sec.leftBound + 0.005
            while x + 0.06 < sec.rightBound {
                let thickness = Float.random(in: 0.025...0.045)
                let height = Float.random(in: 0.22...0.28)
                let width: Float = 0.16

                let bookMeshData = BookMesh.create(device: device,
                                                    thickness: thickness,
                                                    height: height,
                                                    width: width)

                let node = SceneNode()
                node.name = "book_\(bookIndex)"
                node.mesh = SceneNode.MeshRef(vertexBuffer: bookMeshData.vertexBuffer,
                                              indexBuffer: bookMeshData.indexBuffer,
                                              indexCount: bookMeshData.indexCount)
                node.bookMetadata = BookMetadata(thickness: thickness, height: height, width: width)

                let color = bookColors[bookIndex % bookColors.count]
                node.material = MaterialUniformsSwift(
                    albedoR: color.x, albedoG: color.y, albedoB: color.z,
                    metallic: 0.0, roughness: 0.3,
                    hasAlbedoMap: 0, hasNormalMap: 0, hasRoughnessMap: 0
                )

                root.addChild(node)
                state.sections[secIdx].books.append(node)

                x += thickness
                bookIndex += 1
            }
        }

        // Layout all sections (applies positions)
        state.layoutAll()
        root.updateWorldTransforms()

        return BookshelfScene(root: root, state: state)
    }
}
