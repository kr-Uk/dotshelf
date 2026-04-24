import Foundation
import Observation
import RealityKit
import SwiftUI

/// 방 배치 꾸미기 오브젝트 종류 — 명세 §1.1 · §에셋 정의.
/// - 벽 걸이 (액자/시계/포스터 등)
/// - 바닥 놓기 (화분/러그/소파/햄스터 등)
enum RoomDecorKind: String, CaseIterable, Identifiable {
    case floorPot
    case floorRug
    case floorSofa
    case floorHamster
    case wallFrame
    case wallClock
    case wallPoster

    var id: String { rawValue }

    var label: String {
        switch self {
        case .floorPot:     return "화분"
        case .floorRug:     return "러그"
        case .floorSofa:    return "소파"
        case .floorHamster: return "햄스터"
        case .wallFrame:    return "액자"
        case .wallClock:    return "벽시계"
        case .wallPoster:   return "포스터"
        }
    }

    var systemImage: String {
        switch self {
        case .floorPot:     return "leaf"
        case .floorRug:     return "rectangle.fill"
        case .floorSofa:    return "sofa.fill"
        case .floorHamster: return "pawprint.fill"
        case .wallFrame:    return "photo"
        case .wallClock:    return "clock"
        case .wallPoster:   return "doc.richtext"
        }
    }

    var isWallMounted: Bool {
        switch self {
        case .wallFrame, .wallClock, .wallPoster: return true
        case .floorPot, .floorRug, .floorSofa, .floorHamster: return false
        }
    }

    /// 갤러리 미리보기 색.
    var previewColor: Color {
        switch self {
        case .floorPot:     return Color(red: 0.38, green: 0.58, blue: 0.32)
        case .floorRug:     return Color(red: 0.72, green: 0.48, blue: 0.36)
        case .floorSofa:    return Color(red: 0.45, green: 0.35, blue: 0.55)
        case .floorHamster: return Color(red: 0.82, green: 0.68, blue: 0.48)
        case .wallFrame:    return Color(red: 0.88, green: 0.82, blue: 0.72)
        case .wallClock:    return Color(red: 0.20, green: 0.22, blue: 0.26)
        case .wallPoster:   return Color(red: 0.85, green: 0.40, blue: 0.38)
        }
    }
}

/// 배치된 데코 메타데이터 — 엔티티 참조는 런타임 weak로.
@Observable
final class PlacedDecor: Identifiable {
    let id: UUID
    let kind: RoomDecorKind
    var position: SIMD3<Float>
    /// 45° 단위 회전 스텝 (0…7). 바닥 오브젝트는 Y축 yaw, 벽걸이는 벽 법선 기준 roll.
    var yawSteps: Int = 0
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

    /// 선택된 배치 꾸미기를 45° 회전 (§1.1 "배치 꾸미기 — 45° 단위 Y축/법선축 회전").
    /// - 바닥(floor*): world Y축 yaw. 카메라에서 봤을 때 시계 반대 방향.
    /// - 벽걸이(wall*): 벽 법선 기준 roll. 엔티티 로컬 Z축.
    func rotate45(id: UUID) {
        guard let idx = items.firstIndex(where: { $0.id == id }) else { return }
        let next = (items[idx].yawSteps + 1) % 8
        items[idx].yawSteps = next
        guard let entity = items[idx].entity else { return }
        let angle = Float.pi / 4 * Float(next)
        let axis: SIMD3<Float> = items[idx].kind.isWallMounted
            ? SIMD3<Float>(0, 0, 1)   // 벽 법선(로컬 Z) 기준 roll
            : SIMD3<Float>(0, 1, 0)   // 바닥 Y축 yaw
        entity.orientation = simd_quatf(angle: angle, axis: axis)
    }

    /// entity로 아이템 조회 (탭/드래그 핸들러용).
    func item(for entity: Entity) -> PlacedDecor? {
        items.first(where: { $0.entity === entity })
    }
}
