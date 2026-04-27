import SwiftUI

/// WhiteboardDashboard의 element별 pixel rect를 한 곳에서 관리.
/// DashboardHitRegions가 동일 상수를 사용해 3D surface 위에 투명 hit 박스를 배치하므로,
/// 여기가 2D 텍스처 레이아웃 ↔ 3D hit 영역의 유일한 source-of-truth.
enum DashboardLayout {
    static let canvasWidth: CGFloat = 1200
    static let canvasHeight: CGFloat = 750
    static let padding: CGFloat = 32

    /// 실제 3D surface plane 크기 (m). RoomEnvironment의 화이트보드 상수와 일치시켜야 함.
    /// = whiteboard width(1.2) - frameThickness(0.03)*2
    static let surfaceWidth: Float = 1.14
    /// = whiteboard height(0.75) - frameThickness(0.03)*2
    static let surfaceHeight: Float = 0.69

    // MARK: Left column

    static let leftX: CGFloat = padding
    static let leftWidth: CGFloat = 460

    static let bioRect = CGRect(x: leftX, y: padding, width: leftWidth, height: 96)
    static let likeRect = CGRect(x: leftX, y: bioRect.maxY + 16, width: leftWidth, height: 80)

    static let statsY: CGFloat = likeRect.maxY + 16
    static let statsHeight: CGFloat = 92
    static let statSpacing: CGFloat = 10
    static let statCardWidth: CGFloat = (leftWidth - statSpacing * 2) / 3

    static let booksStatRect = CGRect(x: leftX, y: statsY,
                                       width: statCardWidth, height: statsHeight)
    static let followersStatRect = CGRect(x: leftX + statCardWidth + statSpacing, y: statsY,
                                           width: statCardWidth, height: statsHeight)
    static let followingStatRect = CGRect(x: leftX + (statCardWidth + statSpacing) * 2, y: statsY,
                                           width: statCardWidth, height: statsHeight)

    // MARK: Right column

    static let rightX: CGFloat = leftX + leftWidth + 24
    static let rightWidth: CGFloat = canvasWidth - rightX - padding

    static let guestbookHeaderRect = CGRect(x: rightX, y: padding, width: rightWidth, height: 36)
    static let guestbookListY: CGFloat = guestbookHeaderRect.maxY + 14
    static let guestbookCardHeight: CGFloat = 88
    static let guestbookCardSpacing: CGFloat = 10
    static let guestbookMaxCards: Int = 6

    static func guestbookCardRect(_ index: Int) -> CGRect {
        CGRect(
            x: rightX,
            y: guestbookListY + CGFloat(index) * (guestbookCardHeight + guestbookCardSpacing),
            width: rightWidth,
            height: guestbookCardHeight
        )
    }

    // MARK: Texture pixel → surface local 좌표 변환

    /// 대시보드 texture pixel rect → surface local (XY plane) 중심 좌표/크기.
    /// - surface mesh: generatePlane(width: surfaceWidth, height: surfaceHeight), XY plane, center=(0,0,0).
    /// - 텍스처는 canvas 전체(1200×750)가 surface 전체에 uniform 매핑된다고 가정.
    static func surfaceFrame(for rect: CGRect,
                             surfaceWidth: Float,
                             surfaceHeight: Float) -> (center: SIMD2<Float>, size: SIMD2<Float>) {
        let cxNorm = Float((rect.midX / canvasWidth) - 0.5)
        let cyNorm = Float(0.5 - (rect.midY / canvasHeight))
        let wNorm = Float(rect.width / canvasWidth)
        let hNorm = Float(rect.height / canvasHeight)
        return (
            center: SIMD2<Float>(cxNorm * surfaceWidth, cyNorm * surfaceHeight),
            size: SIMD2<Float>(wNorm * surfaceWidth, hNorm * surfaceHeight)
        )
    }
}
