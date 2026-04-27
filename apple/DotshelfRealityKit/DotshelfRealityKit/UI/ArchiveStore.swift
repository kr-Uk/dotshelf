import Foundation
import Observation
import RealityKit  // archiveBookEntity에서 Entity/BookComponent 읽기

/// 보관함(명세 §2·§3) — 사용자가 책을 책장에서 숨기거나, 책장 삭제 시 이전할 공간이 없을 때
/// 이 저장소로 메타데이터만 이관. 복구 시 메타데이터로 새 엔티티를 재생성.
/// MVP는 메모리 only. SwiftData 도입은 추후.
@Observable
final class ArchiveStore {
    struct Item: Identifiable {
        let id = UUID()
        /// 원래 책의 name (레코드 복구 키)
        let bookName: String
        /// 표시용 제목 — 보관함 리스트에서 빠르게 보여주기 위해 캐시
        let title: String
        /// 재생성용 메타 (프로시저럴 기본 책만 복구 가능)
        let thickness: Float
        let height: Float
        let width: Float
        /// sRGB 컴포넌트 4개 — PlatformColor에 플랫폼 바인딩되지 않도록 Float 저장.
        let tintR: Float
        let tintG: Float
        let tintB: Float
        let tintA: Float
        /// 카탈로그 원본 id — 중복 추가 방지용. 복구 시 다시 BookComponent.catalogId로 주입.
        let catalogId: UUID?
        /// 책장 내 꾸미기 오브젝트 프리셋 (DecorationPreset.rawValue) — 복구 시 재생성 키.
        /// nil이면 일반 책 (tint 기반으로 재생성).
        let decorationKind: String?
        /// 저장 시각 (정렬용)
        let archivedAt: Date
    }

    var items: [Item] = []

    func archive(
        bookName: String,
        title: String,
        thickness: Float,
        height: Float,
        width: Float,
        tintR: Float, tintG: Float, tintB: Float, tintA: Float,
        catalogId: UUID? = nil,
        decorationKind: String? = nil
    ) {
        items.append(Item(
            bookName: bookName,
            title: title,
            thickness: thickness,
            height: height,
            width: width,
            tintR: tintR, tintG: tintG, tintB: tintB, tintA: tintA,
            catalogId: catalogId,
            decorationKind: decorationKind,
            archivedAt: Date()
        ))
    }

    func remove(id: UUID) {
        items.removeAll { $0.id == id }
    }

    /// 최근 보관 순(역순)으로 정렬해 반환.
    var sortedItems: [Item] {
        items.sorted { $0.archivedAt > $1.archivedAt }
    }

    /// 엔티티를 보관함으로 이동 (메타데이터만 복사). 엔티티 removeFromParent는 호출자 책임.
    /// - isDecoration인 꾸미기 오브젝트는 tintColor 없어도 보관 허용 — `decorationKind` rawValue로 재생성.
    /// - 일반 책은 tintColor가 있어야 복구 가능. tintColor nil (스캔/광고)은 보관 불가.
    func archiveBookEntity(_ book: Entity, using recordStore: BookRecordStore) -> Bool {
        guard let comp = book.components[BookComponent.self] else { return false }

        // 꾸미기 오브젝트: tintColor 없어도 decorationKind로 복구 가능.
        if comp.isDecoration, let kind = comp.decorationKind {
            archive(
                bookName: book.name,
                title: "장식",  // 장식은 별도 제목 없음 — 보관함 표시용 라벨
                thickness: comp.thickness,
                height: comp.height,
                width: comp.width,
                tintR: 0, tintG: 0, tintB: 0, tintA: 0,
                catalogId: nil,
                decorationKind: kind
            )
            return true
        }

        // 일반 책 — tintColor 필요.
        guard let tint = comp.tintColor else { return false }
        let rec = recordStore.record(for: book.name)

        var r: CGFloat = 0, g: CGFloat = 0, b: CGFloat = 0, a: CGFloat = 0
        #if os(macOS)
        let srgb = tint.usingColorSpace(.sRGB) ?? tint
        srgb.getRed(&r, green: &g, blue: &b, alpha: &a)
        #else
        tint.getRed(&r, green: &g, blue: &b, alpha: &a)
        #endif

        archive(
            bookName: book.name,
            title: rec.title,
            thickness: comp.thickness,
            height: comp.height,
            width: comp.width,
            tintR: Float(r), tintG: Float(g), tintB: Float(b), tintA: Float(a),
            catalogId: comp.catalogId,
            decorationKind: nil
        )
        return true
    }
}
