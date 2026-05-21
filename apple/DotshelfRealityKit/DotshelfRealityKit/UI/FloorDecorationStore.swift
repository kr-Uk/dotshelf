import Foundation
import SwiftUI
import RealityKit
import Observation

/// 바닥 꾸미기 오브젝트의 인메모리 상태 관리.
/// - 활성 오브젝트(바닥에 보이는 것) + 보관함(숨겨진 것) 분리
/// - 드래그/선택 상태
@Observable
final class FloorDecorationStore {
    struct Active: Identifiable {
        let id: UUID
        let assetKey: String
        let displayName: String
        /// 현 씬의 Entity 참조 (방 root 하위). 제거/재배치 시 사용.
        weak var entity: Entity?
        var position: SIMD3<Float>
        /// Y축 yaw 회전 스텝 — 45° 단위, 0…7.
        var yawSteps: Int = 0
    }

    struct Archived: Identifiable {
        let id: UUID
        let assetKey: String
        let displayName: String
        let archivedAt: Date
    }

    var active: [Active] = []
    var archived: [Archived] = []

    /// 선택된 꾸미기 오브젝트 id. 액션바 노출 / 드래그 허용 판정.
    var selectedId: UUID?
    /// 드래그 중 여부 (탭과 드래그 구분).
    var isDragging: Bool = false
    /// 드래그 시작 시점의 월드 좌표 (translation을 이 위치에 누적).
    var dragStartPosition: SIMD3<Float>?

    // MARK: - Active 관리

    func addActive(id: UUID, assetKey: String, displayName: String, entity: Entity, position: SIMD3<Float>) {
        active.append(Active(id: id, assetKey: assetKey, displayName: displayName, entity: entity, position: position))
    }

    func active(id: UUID) -> Active? { active.first { $0.id == id } }

    func updatePosition(id: UUID, to newPos: SIMD3<Float>) {
        guard let idx = active.firstIndex(where: { $0.id == id }) else { return }
        active[idx].position = newPos
    }

    /// 45° 단위로 오른쪽(시계 반대 — Y+축 기준) 회전. entity orientation에 즉시 반영.
    /// 명세 §1.1 — 바닥/벽걸이 배치 꾸미기 탭 시 45° 단위 회전.
    func rotate45(id: UUID) {
        guard let idx = active.firstIndex(where: { $0.id == id }) else { return }
        let next = (active[idx].yawSteps + 1) % 8
        active[idx].yawSteps = next
        let angle = Float.pi / 4 * Float(next)
        active[idx].entity?.orientation = simd_quatf(angle: angle, axis: SIMD3<Float>(0, 1, 0))
    }

    // MARK: - 선택 / 드래그

    func select(_ id: UUID?) {
        selectedId = id
    }

    func beginDrag(id: UUID) {
        guard let item = active(id: id), let entity = item.entity else { return }
        isDragging = true
        dragStartPosition = entity.position(relativeTo: nil)
    }

    func endDrag() {
        isDragging = false
        dragStartPosition = nil
    }

    // MARK: - 보관함

    /// 활성 꾸미기를 씬에서 제거하고 보관함으로 이동.
    @discardableResult
    func archive(id: UUID) -> Bool {
        guard let idx = active.firstIndex(where: { $0.id == id }) else { return false }
        let item = active[idx]
        item.entity?.removeFromParent()
        archived.insert(Archived(
            id: item.id,
            assetKey: item.assetKey,
            displayName: item.displayName,
            archivedAt: Date()
        ), at: 0)
        active.remove(at: idx)
        if selectedId == id { selectedId = nil }
        return true
    }

    /// 보관함에서 꺼내 씬에 복구 (Entity 재생성은 호출자가 담당).
    func restore(id: UUID) {
        archived.removeAll { $0.id == id }
    }
}

/// 카탈로그용 메타 (MVP: 햄햄 인형 하나 샘플).
/// 실제 서비스에서는 MarketStore.decorations와 연결.
enum FloorDecorationCatalog {
    struct Entry: Identifiable {
        var id: String { assetKey }
        let assetKey: String
        let displayName: String
        let previewEmoji: String
    }

    static let samples: [Entry] = [
        Entry(assetKey: "doll_hamham", displayName: "햄햄 인형", previewEmoji: "🧸"),
    ]
}
