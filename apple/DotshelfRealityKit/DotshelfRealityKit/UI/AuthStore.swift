import Foundation
import Observation

enum AuthProvider: String {
    case apple, google, kakao, guest

    var label: String {
        switch self {
        case .apple:  return "Apple"
        case .google: return "Google"
        case .kakao:  return "Kakao"
        case .guest:  return "게스트"
        }
    }
}

enum AuthRoute: Equatable {
    /// 최초 진입 or 로그아웃 상태.
    case login
    /// 소셜 로그인 완료 후 프로필/방 템플릿/첫 책 플로우.
    case onboarding
    /// 로그인 + 온보딩 완료 → 공간 진입.
    case main
}

/// §7·§8 세션 상태 mock.
/// 실제 소셜 연동은 추후 — MVP는 mock delay + 성공 전제.
@Observable
final class AuthStore {
    var route: AuthRoute = .login
    var provider: AuthProvider?
    var isGuest: Bool = false
    /// 소셜 로그인 진행 중 (버튼 disable + 로딩 표시).
    var isAuthenticating: Bool = false
    var errorMessage: String?

    var nickname: String = ""
    var bio: String = ""
    var avatarSystemImage: String = "person.crop.circle.fill"

    /// 이미 사용 중인 닉네임 mock (중복 체크용).
    let takenNicknames: Set<String> = ["admin", "test", "owner", "guest", "dotshelf"]

    /// 로그인 플로우 시작 (mock: 0.6초 딜레이 후 성공 → onboarding으로).
    func signIn(with provider: AuthProvider) {
        errorMessage = nil
        isAuthenticating = true
        self.provider = provider
        self.isGuest = (provider == .guest)

        Task { @MainActor in
            try? await Task.sleep(for: .milliseconds(600))
            isAuthenticating = false
            // 게스트는 프로필 단계 스킵하는 간소 온보딩 — OnboardingFlow 내부에서 분기.
            route = .onboarding
        }
    }

    func finishOnboarding() {
        route = .main
    }

    func logout() {
        provider = nil
        isGuest = false
        nickname = ""
        bio = ""
        route = .login
    }

    func withdraw() {
        logout()
    }

    /// 닉네임 유효성: 2자 이상 + 중복 없음.
    func nicknameStatus(_ value: String) -> NicknameStatus {
        let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
        if trimmed.isEmpty { return .empty }
        if trimmed.count < 2 { return .tooShort }
        if takenNicknames.contains(trimmed.lowercased()) { return .taken }
        return .ok
    }
}

enum NicknameStatus {
    case empty, tooShort, taken, ok

    var message: String? {
        switch self {
        case .empty:    return nil
        case .tooShort: return "2자 이상 입력해주세요"
        case .taken:    return "이미 사용 중인 닉네임이에요"
        case .ok:       return "사용 가능한 닉네임이에요"
        }
    }

    var isValid: Bool { self == .ok }
}

enum RoomTemplate: String, CaseIterable, Identifiable, Hashable {
    case warmWood, minimalWhite, forestNight, cafePastel, darkStudio, random

    var id: String { rawValue }
    var label: String {
        switch self {
        case .warmWood:     return "따뜻한 원목"
        case .minimalWhite: return "미니멀 화이트"
        case .forestNight:  return "숲속 밤"
        case .cafePastel:   return "카페 파스텔"
        case .darkStudio:   return "다크 스튜디오"
        case .random:       return "랜덤으로"
        }
    }
    var systemImage: String {
        switch self {
        case .warmWood:     return "tree.fill"
        case .minimalWhite: return "square.fill"
        case .forestNight:  return "moon.stars.fill"
        case .cafePastel:   return "cup.and.saucer.fill"
        case .darkStudio:   return "sparkles"
        case .random:       return "dice.fill"
        }
    }
}
