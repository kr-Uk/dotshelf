import SwiftUI
import RealityKit

@main
struct DotshelfRealityKitApp: App {
    @State private var auth = AuthStore()

    init() {
        BirdComponent.registerComponent()
        BirdSystem.registerSystem()
    }

    var body: some SwiftUI.Scene {
        WindowGroup {
            RootRouter(auth: auth)
        }
        #if os(macOS)
        .defaultSize(width: 1200, height: 800)
        #endif
    }
}

/// 인증 상태에 따라 Login → Onboarding → Main 분기.
private struct RootRouter: View {
    let auth: AuthStore

    var body: some View {
        ZStack {
            switch auth.route {
            case .login:
                LoginView(auth: auth)
                    .transition(.opacity)
            case .onboarding:
                OnboardingFlow(auth: auth)
                    .transition(.opacity)
            case .main:
                ContentView(auth: auth)
                    .transition(.opacity)
            }

            // 게스트 → 정식 가입 시 데이터 이전 모달 (§8)
            if auth.pendingGuestMigration, let summary = auth.pendingGuestSummary {
                GuestMigrationModal(auth: auth, summary: summary)
                    .transition(.opacity.combined(with: .scale(scale: 0.96)))
            }
        }
        .animation(.easeInOut(duration: 0.3), value: auth.route)
        .animation(.easeInOut(duration: 0.2), value: auth.pendingGuestMigration)
    }
}
