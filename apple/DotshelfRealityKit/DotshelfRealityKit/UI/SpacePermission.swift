import Foundation

/// 공간 접근·편집 권한 모델.
/// 기능 명세상 각 액션이 "공간 주인" / "방문자(로그인)" / "게스트" / "전부"로 갈리는데,
/// 각 오버레이/버튼에서 하드코딩된 분기 대신 이 모델을 조회하도록 한다.
enum SpacePermission: Equatable {
    /// 현재 보고 있는 공간의 주인 본인.
    case owner
    /// 로그인한 방문자 (주인 아님).
    case visitor(isFollower: Bool)
    /// 게스트 모드 (로컬 세션).
    case guest

    var isOwner: Bool { self == .owner }

    var isLoggedIn: Bool {
        switch self {
        case .owner, .visitor: return true
        case .guest:           return false
        }
    }

    var isFollower: Bool {
        if case .visitor(let f) = self { return f }
        return false
    }

    // MARK: - 명세 기반 액션 권한

    /// §1 편집 버튼 / §2 책장 편집 / §3 책 배치·상태·별점 등.
    var canEditSpace: Bool { isOwner }

    /// §4 방명록·§6 독서기록 좋아요·댓글 (방문자 로그인 필요).
    var canLeaveGuestbook: Bool { isLoggedIn && !isOwner }
    var canLikeOrComment: Bool { isLoggedIn }
    var canFollow: Bool { isLoggedIn && !isOwner }

    /// §1 공간 좋아요 (방문자 전용).
    var canLikeSpace: Bool { isLoggedIn && !isOwner }

    /// §4 방명록 공개 설정·답장·한줄소개·포스트잇 스킨 토글 (주인 전용).
    var canManageGuestbook: Bool { isOwner }

    /// §6 독서기록 작성/수정/삭제 (주인 전용).
    var canManageReadingRecord: Bool { isOwner }

    /// §9 설정 접근 (로그인 전용, 게스트는 제한된 메뉴).
    var canOpenSettings: Bool { isLoggedIn }

    /// §5 유료 표지·꾸미기 구매 (로그인 전용).
    var canPurchase: Bool { isLoggedIn }

    /// §4 신고·차단 (방문자 로그인 필요).
    var canReport: Bool { isLoggedIn }

    /// UI 라벨용 설명.
    var shortLabel: String {
        switch self {
        case .owner:                  return "주인"
        case .visitor(let f):         return f ? "맞팔 방문자" : "방문자"
        case .guest:                  return "게스트"
        }
    }
}

/// 화면에 필요한 권한을 한 곳에서 계산.
/// - `viewerIsOwner`: AuthStore의 로그인 상태와 현재 공간 주인 일치 여부.
///   (MVP는 내 공간만 접근하므로 거의 true. 다른 공간 방문 기능 붙으면 실제 비교.)
/// - `viewerIsFollower`: 공간 주인을 팔로잉 중인지.
struct SpacePermissionContext {
    let viewerIsOwner: Bool
    let viewerIsFollower: Bool
    let viewerIsGuest: Bool

    var permission: SpacePermission {
        if viewerIsGuest { return .guest }
        if viewerIsOwner { return .owner }
        return .visitor(isFollower: viewerIsFollower)
    }
}

extension SpacePermissionContext {
    /// MVP 편의 생성자 — AuthStore + "방문자 모드 디버그 토글" 기반.
    /// 다른 에이전트가 쿼터뷰에서 owner↔visitor 토글을 만들고 있어 그 flag를 그대로 쓴다.
    static func debug(auth: AuthStore, isVisitorMode: Bool, isFollowingSpace: Bool = false) -> SpacePermissionContext {
        let isGuest = auth.isGuest
        // 방문자 모드 on이면 주인 아님, off면 (게스트가 아닌 한) 주인.
        let isOwner = !isVisitorMode && !isGuest
        return SpacePermissionContext(
            viewerIsOwner: isOwner,
            viewerIsFollower: isFollowingSpace,
            viewerIsGuest: isGuest
        )
    }
}
