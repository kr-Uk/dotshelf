import Foundation
import Observation

/// §9 설정 화면에서 토글되는 상태 mock.
/// MVP 단계에서는 메모리에만 유지. 추후 UserDefaults / 백엔드 연동.
@Observable
final class SettingsStore {
    // MARK: - 계정

    /// 공간 공개 범위. GuestbookVisibility(.all / .followers / .closed) 재사용.
    var spaceVisibility: GuestbookVisibility = .all

    /// 로그인 상태 mock. 로그아웃 / 로그인 / 회원 탈퇴 버튼 분기용.
    var isLoggedIn: Bool = true
    var displayName: String = "주인"
    var email: String = "owner@example.com"

    // MARK: - 알림 (명세 §9 — 카테고리 5종)

    var notifyNewGuestbook: Bool = true
    var notifyGuestbookReply: Bool = true
    var notifyReadingRecordReaction: Bool = true
    var notifyNewFollower: Bool = true
    var notifyBookAddedByOthers: Bool = true

    // MARK: - 피드 표시 설정 (명세 §9 — 내 활동 5종)

    var feedShowBookAdd: Bool = true
    var feedShowReadingRecord: Bool = true
    var feedShowRecentVisit: Bool = true
    var feedShowGuestbook: Bool = true
    var feedShowReadingRecordComment: Bool = true

    // MARK: - 결제 mock

    /// 페이지 잔액 (1페이지 = 10원).
    var pageBalance: Int = 1_200
    var paymentMethods: [String] = ["•••• 4242 (Visa)"]
    var purchaseHistory: [PurchaseRecord] = [
        PurchaseRecord(title: "페이지 충전 1000P", subtitle: "10,000원", date: "2026-04-10"),
        PurchaseRecord(title: "책 표지 3D — \"아몬드\"", subtitle: "-120P", date: "2026-04-12"),
        PurchaseRecord(title: "전체 테마 — \"나무의 오후\"", subtitle: "-800P", date: "2026-04-14"),
    ]

    // MARK: - 신고 내역 mock

    var reports: [ReportRecord] = []
}

struct PurchaseRecord: Identifiable, Hashable {
    let id = UUID()
    let title: String
    let subtitle: String
    let date: String
}

struct ReportRecord: Identifiable, Hashable {
    let id = UUID()
    let target: String
    let reason: String
    let date: String
}
