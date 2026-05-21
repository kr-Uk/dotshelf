import SwiftUI
import Observation

enum GuestbookVisibility: String, CaseIterable, Hashable, Identifiable {
    case all, followers, closed

    var id: String { rawValue }

    var label: String {
        switch self {
        case .all: return "전체 공개"
        case .followers: return "팔로워 공개"
        case .closed: return "비공개"
        }
    }

    var systemImage: String {
        switch self {
        case .all: return "globe"
        case .followers: return "person.2.fill"
        case .closed: return "lock.fill"
        }
    }
}

enum PostItStyle: String, CaseIterable, Identifiable {
    case yellow, pink, cyan, mint, orange

    var id: String { rawValue }

    var tint: Color {
        switch self {
        case .yellow: return Color(red: 1.00, green: 0.92, blue: 0.55)
        case .pink:   return Color(red: 0.98, green: 0.72, blue: 0.80)
        case .cyan:   return Color(red: 0.62, green: 0.88, blue: 0.96)
        case .mint:   return Color(red: 0.68, green: 0.93, blue: 0.78)
        case .orange: return Color(red: 1.00, green: 0.78, blue: 0.55)
        }
    }

    var label: String {
        switch self {
        case .yellow: return "노랑"
        case .pink:   return "핑크"
        case .cyan:   return "하늘"
        case .mint:   return "민트"
        case .orange: return "주황"
        }
    }
}

struct FollowUser: Identifiable, Hashable {
    let id: UUID
    let name: String
    let initials: String
    let avatarColor: Color
    let bioSnippet: String
    /// 현재 공간 주인이 이 유저를 팔로잉 중 → 맞팔 표시용
    var isMutual: Bool

    init(id: UUID = UUID(), name: String, initials: String, avatarColor: Color, bioSnippet: String, isMutual: Bool = false) {
        self.id = id
        self.name = name
        self.initials = initials
        self.avatarColor = avatarColor
        self.bioSnippet = bioSnippet
        self.isMutual = isMutual
    }

    static func == (lhs: FollowUser, rhs: FollowUser) -> Bool { lhs.id == rhs.id }
    func hash(into hasher: inout Hasher) { hasher.combine(id) }
}

struct GuestbookReply: Identifiable {
    let id = UUID()
    let authorName: String
    let initials: String
    let avatarColor: Color
    let time: String
    let message: String
    let isPublic: Bool
}

/// 방명록 엔트리. 기존 WhiteboardDashboard의 GuestEntry를 대체.
struct GuestbookEntry: Identifiable {
    let id = UUID()
    let authorName: String
    let initials: String
    let avatarColor: Color
    let time: String
    var message: String
    var emoji: String
    var style: PostItStyle
    var isPublic: Bool
    var replies: [GuestbookReply]
}

/// 방명록 · 한 줄 소개 인메모리 저장소.
/// MVP 단계에서는 샘플 시드로 시작해 메모리에만 유지.
@Observable
final class GuestbookStore {
    static let bodyLimit = 30

    var entries: [GuestbookEntry] = []
    var bioText: String = "이 공간에 방문해줘서 고마워요"
    var listVisibility: GuestbookVisibility = .all
    /// 방문자가 방명록 작성할 때 선택 가능한 포스트잇 스타일 (공간 주인이 구매/활성화).
    var enabledStyles: Set<PostItStyle> = Set(PostItStyle.allCases)

    var followers: [FollowUser] = []
    var following: [FollowUser] = []
    var blockedIds: Set<UUID> = []
    /// 차단 유저 전체 레코드. 설정 화면 "차단 내역"에서 표시.
    var blockedUsers: [FollowUser] = []

    var visibleFollowers: [FollowUser] { followers.filter { !blockedIds.contains($0.id) } }
    var visibleFollowing: [FollowUser] { following.filter { !blockedIds.contains($0.id) } }

    init(seed: Bool = true) {
        if seed {
            seedSamples()
            seedFollowUsers()
        }
    }

    private func seedSamples() {
        entries = [
            GuestbookEntry(
                authorName: "지호", initials: "J", avatarColor: .orange,
                time: "10분 전", message: "책장 완전 예뻐 👀", emoji: "✨",
                style: .yellow, isPublic: true, replies: []
            ),
            GuestbookEntry(
                authorName: "수민", initials: "S", avatarColor: .cyan,
                time: "1시간 전", message: "다음 책 추천해줘 진짜", emoji: "📚",
                style: .cyan, isPublic: true,
                replies: [
                    GuestbookReply(
                        authorName: "주인", initials: "M", avatarColor: .purple,
                        time: "30분 전", message: "미드나잇 라이브러리!", isPublic: true
                    )
                ]
            ),
            GuestbookEntry(
                authorName: "민준", initials: "M", avatarColor: .green,
                time: "3시간 전", message: "45도로 꽂은 거 멋지네", emoji: "🤔",
                style: .mint, isPublic: true, replies: []
            ),
            GuestbookEntry(
                authorName: "유나", initials: "Y", avatarColor: .pink,
                time: "어제", message: "서재 느낌 좋아요 ☕️", emoji: "💕",
                style: .pink, isPublic: true, replies: []
            ),
        ]
    }

    // MARK: - Entries

    @discardableResult
    func addEntry(
        authorName: String,
        initials: String,
        avatarColor: Color,
        message: String,
        emoji: String,
        style: PostItStyle,
        isPublic: Bool
    ) -> GuestbookEntry? {
        let trimmed = message.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty, trimmed.count <= Self.bodyLimit else { return nil }
        let entry = GuestbookEntry(
            authorName: authorName, initials: initials, avatarColor: avatarColor,
            time: "방금", message: trimmed, emoji: emoji,
            style: style, isPublic: isPublic, replies: []
        )
        entries.insert(entry, at: 0)
        return entry
    }

    func removeEntry(id: UUID) {
        entries.removeAll { $0.id == id }
    }

    // MARK: - Replies

    func addReply(to entryId: UUID, message: String, isPublic: Bool) {
        let trimmed = message.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return }
        guard let idx = entries.firstIndex(where: { $0.id == entryId }) else { return }
        let reply = GuestbookReply(
            authorName: "주인", initials: "M", avatarColor: .purple,
            time: "방금", message: trimmed, isPublic: isPublic
        )
        entries[idx].replies.append(reply)
    }

    // MARK: - Bio

    func updateBio(_ text: String) {
        let trimmed = text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return }
        bioText = trimmed
    }

    // MARK: - Visibility / style toggle

    func setListVisibility(_ v: GuestbookVisibility) { listVisibility = v }

    func toggleStyle(_ style: PostItStyle, enabled: Bool) {
        if enabled { enabledStyles.insert(style) } else { enabledStyles.remove(style) }
    }

    // MARK: - Follow

    func unfollow(_ userId: UUID) {
        following.removeAll { $0.id == userId }
        if let idx = followers.firstIndex(where: { $0.id == userId }) {
            followers[idx].isMutual = false
        }
    }

    func removeFollower(_ userId: UUID) {
        followers.removeAll { $0.id == userId }
        if let idx = following.firstIndex(where: { $0.id == userId }) {
            following[idx].isMutual = false
        }
    }

    func block(_ user: FollowUser) {
        blockedIds.insert(user.id)
        if !blockedUsers.contains(where: { $0.id == user.id }) {
            blockedUsers.append(user)
        }
        // 명세: 차단 시 팔로워/팔로잉 자동 해제
        unfollow(user.id)
        removeFollower(user.id)
    }

    func unblock(_ userId: UUID) {
        blockedIds.remove(userId)
        blockedUsers.removeAll { $0.id == userId }
    }

    private func seedFollowUsers() {
        let followerSamples: [FollowUser] = [
            FollowUser(name: "지호", initials: "J", avatarColor: .orange,
                       bioSnippet: "에세이 · 여행책 좋아함", isMutual: true),
            FollowUser(name: "수민", initials: "S", avatarColor: .cyan,
                       bioSnippet: "SF · 판타지 덕후", isMutual: true),
            FollowUser(name: "민준", initials: "M", avatarColor: .green,
                       bioSnippet: "역사 · 과학 교양", isMutual: false),
            FollowUser(name: "유나", initials: "Y", avatarColor: .pink,
                       bioSnippet: "시 · 일러스트 북", isMutual: true),
            FollowUser(name: "태호", initials: "T", avatarColor: .indigo,
                       bioSnippet: "경제경영 · 자기계발", isMutual: false),
            FollowUser(name: "서진", initials: "S", avatarColor: .teal,
                       bioSnippet: "만화 · 그래픽 노블", isMutual: false),
        ]
        followers = followerSamples

        let followingSamples: [FollowUser] = [
            FollowUser(name: "지호", initials: "J", avatarColor: .orange,
                       bioSnippet: "에세이 · 여행책 좋아함", isMutual: true),
            FollowUser(name: "수민", initials: "S", avatarColor: .cyan,
                       bioSnippet: "SF · 판타지 덕후", isMutual: true),
            FollowUser(name: "유나", initials: "Y", avatarColor: .pink,
                       bioSnippet: "시 · 일러스트 북", isMutual: true),
            FollowUser(name: "하린", initials: "H", avatarColor: .purple,
                       bioSnippet: "요리 · 라이프스타일", isMutual: false),
            FollowUser(name: "도윤", initials: "D", avatarColor: .red,
                       bioSnippet: "추리 · 스릴러", isMutual: false),
        ]
        following = followingSamples
    }
}
