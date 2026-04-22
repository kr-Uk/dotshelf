import Foundation
import Observation
import RealityKit
import SwiftUI

/// 방 배치 꾸미기 오브젝트 종류 — 명세 §1.1.
/// 벽걸이와 바닥 놓기 두 가지로 구분.
enum RoomDecorKind: String, CaseIterable, Identifiable {
    case floorPot
    case floorRug
    case floorStool
    case wallFrame
    case wallClock

    var id: String { rawValue }

    var label: String {
        switch self {
        case .floorPot:   return "화분"
        case .floorRug:   return "러그"
        case .floorStool: return "스툴"
        case .wallFrame:  return "액자"
        case .wallClock:  return "벽시계"
        }
    }

    var systemImage: String {
        switch self {
        case .floorPot:   return "leaf"
        case .floorRug:   return "rectangle.fill"
        case .floorStool: return "square.3.layers.3d.down.backward"
        case .wallFrame:  return "photo"
        case .wallClock:  return "clock"
        }
    }

    var isWallMounted: Bool {
        switch self {
        case .wallFrame, .wallClock: return true
        case .floorPot, .floorRug, .floorStool: return false
        }
    }

    /// 갤러리 미리보기 색.
    var previewColor: Color {
        switch self {
        case .floorPot:   return Color(red: 0.38, green: 0.58, blue: 0.32)
        case .floorRug:   return Color(red: 0.72, green: 0.48, blue: 0.36)
        case .floorStool: return Color(red: 0.55, green: 0.42, blue: 0.32)
        case .wallFrame:  return Color(red: 0.88, green: 0.82, blue: 0.72)
        case .wallClock:  return Color(red: 0.20, green: 0.22, blue: 0.26)
        }
    }
}

/// 배치된 데코 메타데이터 — 엔티티 참조는 런타임 weak로.
@Observable
final class PlacedDecor: Identifiable {
    let id: UUID
    let kind: RoomDecorKind
    var position: SIMD3<Float>
    weak var entity: Entity?

    init(id: UUID = UUID(), kind: RoomDecorKind, position: SIMD3<Float>, entity: Entity) {
        self.id = id
        self.kind = kind
        self.position = position
        self.entity = entity
    }
}

/// 방 배치 꾸미기 저장소. 씬에 추가된 데코 엔티티의 메타데이터 리스트.
@Observable
final class RoomDecorStore {
    var items: [PlacedDecor] = []

    /// 현재 선택된 데코 (단일 선택). 드래그/삭제 시 타겟.
    var selectedId: UUID?

    func add(kind: RoomDecorKind, entity: Entity, at position: SIMD3<Float>) {
        let item = PlacedDecor(kind: kind, position: position, entity: entity)
        items.append(item)
        selectedId = item.id
    }

    @discardableResult
    func remove(id: UUID) -> Entity? {
        guard let idx = items.firstIndex(where: { $0.id == id }) else { return nil }
        let removed = items.remove(at: idx)
        if selectedId == id { selectedId = nil }
        return removed.entity
    }

    func updatePosition(id: UUID, to pos: SIMD3<Float>) {
        guard let idx = items.firstIndex(where: { $0.id == id }) else { return }
        items[idx].position = pos
        items[idx].entity?.setPosition(pos, relativeTo: nil)
    }

    /// entity로 아이템 조회 (탭/드래그 핸들러용).
    func item(for entity: Entity) -> PlacedDecor? {
        items.first(where: { $0.entity === entity })
    }
}
