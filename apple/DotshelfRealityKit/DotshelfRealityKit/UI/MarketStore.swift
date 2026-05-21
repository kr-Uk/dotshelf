import Foundation
import SwiftUI
import Observation

// MARK: - Catalog models

struct MarketBook: Identifiable, Hashable {
    let id = UUID()
    let title: String
    let author: String
    let publisher: String
    let genre: BookGenre
    /// 0 = 무료 기본 책 오브젝트 / >0 = 유료 3D 스캔 표지 (페이지)
    let priceCover: Int
    let rating: Double
    let placements: Int
    let likes: Int
    let recordCount: Int
    let coverPurchases: Int
    let releasedAt: String
}

enum BookGenre: String, CaseIterable, Identifiable, Hashable {
    case all, essay, novel, science, history, selfhelp, art, poem
    var id: String { rawValue }
    var label: String {
        switch self {
        case .all: return "전체"
        case .essay: return "에세이"
        case .novel: return "소설"
        case .science: return "과학"
        case .history: return "역사"
        case .selfhelp: return "자기계발"
        case .art: return "예술"
        case .poem: return "시"
        }
    }
}

enum MarketSort: String, CaseIterable, Identifiable, Hashable {
    case rating, placements, coverPurchases, likes, records, latest
    var id: String { rawValue }
    var label: String {
        switch self {
        case .rating: return "별점순"
        case .placements: return "공간 배치순"
        case .coverPurchases: return "유료 표지순"
        case .likes: return "좋아요순"
        case .records: return "독서기록순"
        case .latest: return "최신순"
        }
    }
}

// MARK: - Other spaces

enum RankPeriod: String, CaseIterable, Identifiable, Hashable {
    case day, week, month, year, all
    var id: String { rawValue }
    var label: String {
        switch self {
        case .day: return "일간"
        case .week: return "주간"
        case .month: return "월간"
        case .year: return "연간"
        case .all: return "누적"
        }
    }
}

enum SpaceRankKind: String, CaseIterable, Identifiable, Hashable {
    case visitors, spaceLikes, recordLikes, bookCount
    var id: String { rawValue }
    var label: String {
        switch self {
        case .visitors: return "방문자"
        case .spaceLikes: return "공간 좋아요"
        case .recordLikes: return "독서기록 좋아요"
        case .bookCount: return "책 개수"
        }
    }
}

struct MarketSpace: Identifiable, Hashable {
    let id = UUID()
    let handle: String
    let bio: String
    let visitors: Int
    let spaceLikes: Int
    let recordLikes: Int
    let bookCount: Int
    let recordCount: Int
    let overlapBooks: Int  // 나와 겹치는 책 수
}

// MARK: - Feed

enum FeedItemKind: String, Hashable {
    case record, bookAdded, visit, guestbook, comment, recommendation, ad
}

struct FeedItem: Identifiable, Hashable {
    let id = UUID()
    let kind: FeedItemKind
    let actorName: String
    let actorInitials: String
    let actorColor: Color
    let headline: String
    let subtitle: String
    let time: String

    static func == (lhs: FeedItem, rhs: FeedItem) -> Bool { lhs.id == rhs.id }
    func hash(into hasher: inout Hasher) { hasher.combine(id) }
}

// MARK: - Decoration catalog

enum DecorationCategory: String, CaseIterable, Identifiable, Hashable {
    case essentials, wall, floor, theme
    var id: String { rawValue }
    var label: String {
        switch self {
        case .essentials: return "필수 오브젝트 스킨"
        case .wall: return "벽걸이"
        case .floor: return "바닥 배치"
        case .theme: return "전체 테마"
        }
    }
}

enum DecorationSort: String, CaseIterable, Identifiable, Hashable {
    case popular, latest, priceAsc
    var id: String { rawValue }
    var label: String {
        switch self {
        case .popular: return "인기순"
        case .latest: return "최신순"
        case .priceAsc: return "가격 낮은 순"
        }
    }
}

struct DecorationAsset: Identifiable, Hashable {
    let id = UUID()
    let name: String
    let category: DecorationCategory
    let priceP: Int
    let creator: String
    let popularity: Int
    let systemImage: String
    let tint: Color

    static func == (lhs: DecorationAsset, rhs: DecorationAsset) -> Bool { lhs.id == rhs.id }
    func hash(into hasher: inout Hasher) { hasher.combine(id) }
}

// MARK: - Store

/// §5 노트북 포커싱 탭에서 쓰는 mock 카탈로그 / 피드.
@Observable
final class MarketStore {
    var books: [MarketBook] = []
    var spaces: [MarketSpace] = []
    var feed: [FeedItem] = []
    var decorations: [DecorationAsset] = []
    var wishlist: Set<UUID> = []
    var ownedDecorations: Set<UUID> = []

    init(seed: Bool = true) { if seed { seedAll() } }

    func toggleWishlist(_ id: UUID) {
        if wishlist.contains(id) { wishlist.remove(id) } else { wishlist.insert(id) }
    }

    private func seedAll() {
        books = [
            MarketBook(title: "달러구트 꿈 백화점", author: "이미예", publisher: "팩토리나인",
                       genre: .novel, priceCover: 180, rating: 4.6, placements: 1240, likes: 2800, recordCount: 412, coverPurchases: 98, releasedAt: "2026-02-10"),
            MarketBook(title: "코스모스", author: "칼 세이건", publisher: "사이언스북스",
                       genre: .science, priceCover: 240, rating: 4.8, placements: 980, likes: 3500, recordCount: 520, coverPurchases: 140, releasedAt: "2025-11-03"),
            MarketBook(title: "미드나잇 라이브러리", author: "매트 헤이그", publisher: "인플루엔셜",
                       genre: .novel, priceCover: 160, rating: 4.2, placements: 800, likes: 1900, recordCount: 280, coverPurchases: 62, releasedAt: "2026-01-12"),
            MarketBook(title: "사피엔스", author: "유발 하라리", publisher: "김영사",
                       genre: .history, priceCover: 220, rating: 4.4, placements: 1560, likes: 4200, recordCount: 640, coverPurchases: 185, releasedAt: "2025-09-20"),
            MarketBook(title: "아몬드", author: "손원평", publisher: "창비",
                       genre: .novel, priceCover: 140, rating: 4.3, placements: 1120, likes: 2500, recordCount: 345, coverPurchases: 78, releasedAt: "2025-12-05"),
            MarketBook(title: "작별하지 않는다", author: "한강", publisher: "문학동네",
                       genre: .novel, priceCover: 190, rating: 4.7, placements: 610, likes: 1700, recordCount: 215, coverPurchases: 52, releasedAt: "2026-03-18"),
            MarketBook(title: "시선으로부터,", author: "정세랑", publisher: "문학동네",
                       genre: .essay, priceCover: 0, rating: 4.4, placements: 420, likes: 950, recordCount: 112, coverPurchases: 0, releasedAt: "2026-02-25"),
            MarketBook(title: "모순", author: "양귀자", publisher: "쓰다",
                       genre: .novel, priceCover: 150, rating: 4.5, placements: 720, likes: 1600, recordCount: 180, coverPurchases: 36, releasedAt: "2025-10-14"),
            MarketBook(title: "파과", author: "구병모", publisher: "위즈덤하우스",
                       genre: .novel, priceCover: 170, rating: 4.3, placements: 320, likes: 840, recordCount: 95, coverPurchases: 22, releasedAt: "2026-03-02"),
        ]

        spaces = [
            MarketSpace(handle: "@bamboo_reader", bio: "SF · 판타지 덕후", visitors: 4820, spaceLikes: 980, recordLikes: 2200, bookCount: 128, recordCount: 64, overlapBooks: 12),
            MarketSpace(handle: "@cafe_lee", bio: "카페에서 읽는 에세이", visitors: 3120, spaceLikes: 720, recordLikes: 1400, bookCount: 92, recordCount: 38, overlapBooks: 9),
            MarketSpace(handle: "@night_lab", bio: "과학자의 밤 서재", visitors: 2480, spaceLikes: 640, recordLikes: 1850, bookCount: 110, recordCount: 72, overlapBooks: 15),
            MarketSpace(handle: "@pink_pages", bio: "시 · 일러스트 북", visitors: 1890, spaceLikes: 520, recordLikes: 820, bookCount: 78, recordCount: 44, overlapBooks: 5),
            MarketSpace(handle: "@deepdive", bio: "역사 · 인문 · 교양", visitors: 1620, spaceLikes: 410, recordLikes: 1080, bookCount: 156, recordCount: 58, overlapBooks: 8),
        ]

        feed = [
            FeedItem(kind: .record, actorName: "지호", actorInitials: "J", actorColor: .orange,
                     headline: "새 독서기록: 달러구트 꿈 백화점", subtitle: "\"꿈이라는 소재를 이렇게…\"", time: "10분 전"),
            FeedItem(kind: .bookAdded, actorName: "수민", actorInitials: "S", actorColor: .cyan,
                     headline: "수민님이 '코스모스'를 책장에 추가했어요", subtitle: "사이언스북스 · 4.8★", time: "40분 전"),
            FeedItem(kind: .comment, actorName: "민준", actorInitials: "M", actorColor: .green,
                     headline: "민준님이 독서기록에 댓글", subtitle: "\"4장부터 속도 붙더라\"", time: "1시간 전"),
            FeedItem(kind: .visit, actorName: "유나", actorInitials: "Y", actorColor: .pink,
                     headline: "유나님이 @bamboo_reader 공간 방문", subtitle: "공간 좋아요 +1", time: "2시간 전"),
            FeedItem(kind: .recommendation, actorName: "dotshelf", actorInitials: "D", actorColor: .purple,
                     headline: "추천 독서기록: \"미드나잇 라이브러리\"", subtitle: "by @quiet_room · 좋아요 240", time: "오늘"),
            FeedItem(kind: .guestbook, actorName: "태호", actorInitials: "T", actorColor: .indigo,
                     headline: "태호님이 내 방명록에 답장", subtitle: "\"추천 책 고마워요 ☕️\"", time: "어제"),
            FeedItem(kind: .ad, actorName: "광고", actorInitials: "AD", actorColor: .yellow,
                     headline: "신간 — 파과 (구병모)", subtitle: "유료 표지 지금 출시", time: "광고"),
        ]

        decorations = [
            DecorationAsset(name: "오크 원목 책장", category: .essentials, priceP: 1200,
                            creator: "dotshelf studio", popularity: 980, systemImage: "books.vertical.fill", tint: .brown),
            DecorationAsset(name: "미니멀 화이트 책장", category: .essentials, priceP: 900,
                            creator: "dotshelf studio", popularity: 720, systemImage: "books.vertical", tint: .gray),
            DecorationAsset(name: "인더스트리얼 메탈 책장", category: .essentials, priceP: 1500,
                            creator: "ironframe", popularity: 510, systemImage: "square.grid.3x3.fill", tint: .gray),
            DecorationAsset(name: "빈티지 시계", category: .wall, priceP: 400,
                            creator: "home & home", popularity: 640, systemImage: "clock.fill", tint: .orange),
            DecorationAsset(name: "식물 화분 (몬스테라)", category: .floor, priceP: 600,
                            creator: "green studio", popularity: 820, systemImage: "leaf.fill", tint: .green),
            DecorationAsset(name: "따뜻한 러그", category: .floor, priceP: 750,
                            creator: "cozy lab", popularity: 480, systemImage: "rectangle.fill", tint: .pink),
            DecorationAsset(name: "프레임 액자 (3 set)", category: .wall, priceP: 500,
                            creator: "gallery g", popularity: 390, systemImage: "photo.fill", tint: .indigo),
            DecorationAsset(name: "나무의 오후 (전체 테마)", category: .theme, priceP: 2200,
                            creator: "dotshelf studio", popularity: 1120, systemImage: "tree.fill", tint: .brown),
            DecorationAsset(name: "다크 스튜디오 (전체 테마)", category: .theme, priceP: 2200,
                            creator: "dotshelf studio", popularity: 960, systemImage: "sparkles", tint: .purple),
            DecorationAsset(name: "숲속 밤 (전체 테마)", category: .theme, priceP: 2400,
                            creator: "forest co", popularity: 740, systemImage: "moon.stars.fill", tint: .blue),
        ]
    }
}

extension MarketStore {
    func sortedBooks(genre: BookGenre, sort: MarketSort, query: String) -> [MarketBook] {
        let q = query.trimmingCharacters(in: .whitespacesAndNewlines)
        let filtered = books.filter { b in
            (genre == .all || b.genre == genre) &&
            (q.isEmpty ||
             b.title.localizedCaseInsensitiveContains(q) ||
             b.author.localizedCaseInsensitiveContains(q) ||
             b.publisher.localizedCaseInsensitiveContains(q))
        }
        return filtered.sorted { a, b in
            switch sort {
            case .rating:         return a.rating > b.rating
            case .placements:     return a.placements > b.placements
            case .coverPurchases: return a.coverPurchases > b.coverPurchases
            case .likes:          return a.likes > b.likes
            case .records:        return a.recordCount > b.recordCount
            case .latest:         return a.releasedAt > b.releasedAt
            }
        }
    }

    func sortedSpaces(kind: SpaceRankKind, query: String) -> [MarketSpace] {
        let q = query.trimmingCharacters(in: .whitespacesAndNewlines)
        let filtered = q.isEmpty ? spaces
            : spaces.filter { $0.handle.localizedCaseInsensitiveContains(q) || $0.bio.localizedCaseInsensitiveContains(q) }
        return filtered.sorted { a, b in
            switch kind {
            case .visitors:    return a.visitors > b.visitors
            case .spaceLikes:  return a.spaceLikes > b.spaceLikes
            case .recordLikes: return a.recordLikes > b.recordLikes
            case .bookCount:   return a.bookCount > b.bookCount
            }
        }
    }

    func filteredDecorations(category: DecorationCategory?, sort: DecorationSort) -> [DecorationAsset] {
        let filtered = decorations.filter { category == nil || $0.category == category }
        return filtered.sorted { a, b in
            switch sort {
            case .popular:  return a.popularity > b.popularity
            case .latest:   return a.name > b.name  // mock
            case .priceAsc: return a.priceP < b.priceP
            }
        }
    }
}
