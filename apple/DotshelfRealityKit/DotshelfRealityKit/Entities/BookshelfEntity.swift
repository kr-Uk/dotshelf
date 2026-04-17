import RealityKit

enum BookshelfEntity {
    static func create(
        width: Float = 1.60,
        height: Float = 1.60,
        depth: Float = 0.25,
        rows: Int = 4,
        columns: Int = 4
    ) -> Entity {
        let root = Entity()
        root.name = "bookshelf"
        let material = MaterialFactory.woodMaterial()

        let panelThickness: Float = 0.018
        let boardThickness: Float = 0.018

        // 좌/우 기둥
        let sideMesh = MeshResource.generateBox(width: panelThickness, height: height, depth: depth)

        // 모든 shelf 구조물에 공통으로 붙일 static physics (떨어진 책과 충돌)
        let staticBody = PhysicsBodyComponent(
            massProperties: .default,
            material: .generate(friction: 0.5, restitution: 0.1),
            mode: .static
        )

        let leftPanel = ModelEntity(mesh: sideMesh, materials: [material])
        leftPanel.name = "shelf_panel"
        leftPanel.position = SIMD3(-width / 2 + panelThickness / 2, height / 2, 0)
        leftPanel.collision = CollisionComponent(shapes: [.generateBox(width: panelThickness, height: height, depth: depth)])
        leftPanel.components.set(staticBody)
        root.addChild(leftPanel)

        let rightPanel = ModelEntity(mesh: sideMesh, materials: [material])
        rightPanel.name = "shelf_panel"
        rightPanel.position = SIMD3(width / 2 - panelThickness / 2, height / 2, 0)
        rightPanel.collision = CollisionComponent(shapes: [.generateBox(width: panelThickness, height: height, depth: depth)])
        rightPanel.components.set(staticBody)
        root.addChild(rightPanel)

        // 수평 선반 (rows + 1개)
        let innerWidth = width - panelThickness * 2
        let shelfMesh = MeshResource.generateBox(width: innerWidth, height: boardThickness, depth: depth)
        let sectionHeight = (height - boardThickness * Float(rows + 1)) / Float(rows)

        for i in 0...rows {
            let shelf = ModelEntity(mesh: shelfMesh, materials: [material])
            shelf.name = "shelf_board"
            let y = boardThickness / 2 + Float(i) * (sectionHeight + boardThickness)
            shelf.position = SIMD3(0, y, 0)
            shelf.collision = CollisionComponent(shapes: [.generateBox(width: innerWidth, height: boardThickness, depth: depth)])
            shelf.components.set(staticBody)
            root.addChild(shelf)
        }

        // 세로 파티션 (columns - 1개)
        let dividerCount = columns - 1
        for d in 1...dividerCount {
            let xFraction = Float(d) / Float(columns)
            let dividerX = -width / 2 + panelThickness + xFraction * innerWidth

            for row in 0..<rows {
                let shelfTopY = boardThickness + Float(row) * (sectionHeight + boardThickness)
                let dividerMesh = MeshResource.generateBox(width: panelThickness, height: sectionHeight, depth: depth)
                let divider = ModelEntity(mesh: dividerMesh, materials: [material])
                divider.name = "shelf_divider"
                divider.position = SIMD3(dividerX, shelfTopY + sectionHeight / 2, 0)
                divider.collision = CollisionComponent(shapes: [.generateBox(width: panelThickness, height: sectionHeight, depth: depth)])
                divider.components.set(staticBody)
                root.addChild(divider)
            }
        }

        // 뒷판
        let backMesh = MeshResource.generateBox(width: width, height: height, depth: 0.006)
        let backPanel = ModelEntity(mesh: backMesh, materials: [material])
        backPanel.name = "shelf_back"
        backPanel.position = SIMD3(0, height / 2, -depth / 2 + 0.003)
        backPanel.collision = CollisionComponent(shapes: [.generateBox(width: width, height: height, depth: 0.006)])
        backPanel.components.set(staticBody)
        root.addChild(backPanel)

        return root
    }
}
