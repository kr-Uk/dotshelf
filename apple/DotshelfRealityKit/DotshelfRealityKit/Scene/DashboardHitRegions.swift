import Foundation
import RealityKit

/// 화이트보드 surface 위에 element별 투명 hit 박스를 배치하는 헬퍼.
/// 고정 element(bio/like/followers/following/guestbook header)는 한 번만 설치하고,
/// 방명록 카드별 hit 박스는 entries 변경 시마다 재생성한다.
///
/// - 탭 이름 규칙:
///   - `dashboard_hit_bio` / `dashboard_hit_like`
///   - `dashboard_hit_stat_books`
///   - `dashboard_hit_stat_followers` / `dashboard_hit_stat_following`
///   - `dashboard_hit_guestbook_header` (방명록 전체 탭 — composer 진입)
///   - `dashboard_hit_guestbook_entry_<UUID>` (개별 방명록 카드 — 답장 시트)
enum DashboardHitRegions {
    static let bioName = "dashboard_hit_bio"
    static let likeName = "dashboard_hit_like"
    static let booksName = "dashboard_hit_stat_books"
    static let followersName = "dashboard_hit_stat_followers"
    static let followingName = "dashboard_hit_stat_following"
    static let guestbookHeaderName = "dashboard_hit_guestbook_header"
    static let guestbookEntryPrefix = "dashboard_hit_guestbook_entry_"
    static let container = "dashboard_hit_container"

    /// 고정 element hit 박스 5개를 surface 아래 container에 설치. 최초 1회만 호출.
    /// - surface: whiteboard_surface ModelEntity (XY plane, center=(0,0,0))
    /// - surfaceWidth / surfaceHeight: plane 실물 크기 (m 단위)
    static func install(on surface: Entity, surfaceWidth: Float, surfaceHeight: Float) {
        let existing = surface.findEntity(named: container)
        existing?.removeFromParent()

        let root = Entity()
        root.name = container
        surface.addChild(root)

        addBox(name: bioName, rect: DashboardLayout.bioRect,
               surfaceWidth: surfaceWidth, surfaceHeight: surfaceHeight, parent: root)
        addBox(name: likeName, rect: DashboardLayout.likeRect,
               surfaceWidth: surfaceWidth, surfaceHeight: surfaceHeight, parent: root)
        addBox(name: booksName, rect: DashboardLayout.booksStatRect,
               surfaceWidth: surfaceWidth, surfaceHeight: surfaceHeight, parent: root)
        addBox(name: followersName, rect: DashboardLayout.followersStatRect,
               surfaceWidth: surfaceWidth, surfaceHeight: surfaceHeight, parent: root)
        addBox(name: followingName, rect: DashboardLayout.followingStatRect,
               surfaceWidth: surfaceWidth, surfaceHeight: surfaceHeight, parent: root)
        addBox(name: guestbookHeaderName, rect: DashboardLayout.guestbookHeaderRect,
               surfaceWidth: surfaceWidth, surfaceHeight: surfaceHeight, parent: root)
    }

    /// 방명록 카드별 hit 박스를 재생성. entries 변경 시마다 호출.
    /// entry의 UUID를 이름에 포함해 탭 핸들러에서 복원 가능.
    static func syncEntryHits(on surface: Entity,
                               surfaceWidth: Float, surfaceHeight: Float,
                               entryIds: [UUID]) {
        guard let root = surface.findEntity(named: container) else { return }

        // 기존 entry hit 제거
        let toRemove = root.children.filter { $0.name.hasPrefix(guestbookEntryPrefix) }
        for e in toRemove { e.removeFromParent() }

        // 최대 guestbookMaxCards개만 hit 생성 (텍스처도 이만큼만 표시)
        for (offset, id) in entryIds.prefix(DashboardLayout.guestbookMaxCards).enumerated() {
            let rect = DashboardLayout.guestbookCardRect(offset)
            addBox(name: "\(guestbookEntryPrefix)\(id.uuidString)",
                   rect: rect,
                   surfaceWidth: surfaceWidth, surfaceHeight: surfaceHeight, parent: root)
        }
    }

    /// 탭된 이름이 guestbook entry hit면 UUID 복원.
    static func entryId(fromHitName name: String) -> UUID? {
        guard name.hasPrefix(guestbookEntryPrefix) else { return nil }
        let uuidStr = String(name.dropFirst(guestbookEntryPrefix.count))
        return UUID(uuidString: uuidStr)
    }

    // MARK: - Private

    private static func addBox(name: String, rect: CGRect,
                                surfaceWidth: Float, surfaceHeight: Float,
                                parent: Entity) {
        let frame = DashboardLayout.surfaceFrame(for: rect,
                                                 surfaceWidth: surfaceWidth,
                                                 surfaceHeight: surfaceHeight)
        let hit = Entity()
        hit.name = name
        // surface plane(+Z normal) 앞쪽 2mm에 배치해 frame/surface보다 먼저 hit되게.
        hit.position = SIMD3<Float>(frame.center.x, frame.center.y, 0.002)
        hit.components.set(CollisionComponent(shapes: [
            .generateBox(width: frame.size.x, height: frame.size.y, depth: 0.004)
        ]))
        hit.components.set(InputTargetComponent())
        parent.addChild(hit)
    }
}
