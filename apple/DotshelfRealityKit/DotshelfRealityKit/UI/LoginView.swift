import SwiftUI

/// §7 로그인 화면. 앱 최초 진입 또는 로그아웃 상태에서 표시.
struct LoginView: View {
    let auth: AuthStore

    var body: some View {
        ZStack {
            backgroundGradient.ignoresSafeArea()

            VStack(spacing: 0) {
                if let error = auth.errorMessage {
                    errorToast(error)
                        .padding(.top, 16)
                        .transition(.move(edge: .top).combined(with: .opacity))
                }
                Spacer()
                logoBlock
                Spacer()
                buttons
                    .padding(.horizontal, 40)
                    .padding(.bottom, 36)
                footerNotice
                    .padding(.bottom, 20)
            }
        }
        .animation(.easeInOut(duration: 0.25), value: auth.errorMessage)
    }

    private var backgroundGradient: some View {
        LinearGradient(
            colors: [
                Color(red: 0.95, green: 0.91, blue: 0.85),
                Color(red: 0.80, green: 0.72, blue: 0.62),
                Color(red: 0.48, green: 0.38, blue: 0.32),
            ],
            startPoint: .top, endPoint: .bottom
        )
    }

    private var logoBlock: some View {
        VStack(spacing: 10) {
            Text("📚")
                .font(.system(size: 64))
            Text("dotshelf")
                .font(.system(size: 38, weight: .black, design: .serif))
                .foregroundStyle(.white)
                .shadow(color: .black.opacity(0.2), radius: 6, y: 2)
            Text("책장에 꽂힌 책을 보고 그 사람을 알다")
                .font(.system(size: 14, weight: .medium))
                .foregroundStyle(.white.opacity(0.9))
        }
    }

    private var buttons: some View {
        VStack(spacing: 10) {
            socialButton(.apple, icon: "applelogo",
                         bg: Color.black, fg: .white)
            socialButton(.google, icon: "g.circle.fill",
                         bg: .white, fg: .black)
            socialButton(.kakao, icon: "bubble.left.fill",
                         bg: Color(red: 1.00, green: 0.90, blue: 0.13), fg: .black)

            HStack {
                Rectangle().fill(.white.opacity(0.4)).frame(height: 1)
                Text("또는").font(.system(size: 11, weight: .medium)).foregroundStyle(.white.opacity(0.8))
                Rectangle().fill(.white.opacity(0.4)).frame(height: 1)
            }
            .padding(.vertical, 6)

            Button(action: { auth.signIn(with: .guest) }) {
                HStack(spacing: 8) {
                    Image(systemName: "person.crop.circle.dashed")
                        .font(.system(size: 14, weight: .semibold))
                    Text("게스트로 시작")
                        .font(.system(size: 14, weight: .semibold))
                }
                .foregroundStyle(.white)
                .frame(maxWidth: .infinity)
                .padding(.vertical, 12)
                .background(Capsule().strokeBorder(.white.opacity(0.6), lineWidth: 1.5))
            }
            .buttonStyle(.plain)
            .disabled(auth.isAuthenticating)
        }
    }

    private func socialButton(_ provider: AuthProvider, icon: String, bg: Color, fg: Color) -> some View {
        Button(action: { auth.signIn(with: provider) }) {
            HStack(spacing: 10) {
                Image(systemName: icon).font(.system(size: 16, weight: .semibold))
                Text("\(provider.label)로 계속하기")
                    .font(.system(size: 14, weight: .semibold))
            }
            .foregroundStyle(fg)
            .frame(maxWidth: .infinity)
            .padding(.vertical, 12)
            .background(Capsule().fill(bg))
            // ProgressView를 overlay로 띄워 HStack 레이아웃을 흔들지 않음
            .overlay(alignment: .trailing) {
                if auth.isAuthenticating && auth.provider == provider {
                    ProgressView()
                        .controlSize(.small)
                        .tint(fg)
                        .padding(.trailing, 14)
                }
            }
            .shadow(color: .black.opacity(0.15), radius: 6, y: 2)
        }
        .buttonStyle(.plain)
        .disabled(auth.isAuthenticating)
        .opacity(auth.isAuthenticating && auth.provider != provider ? 0.5 : 1.0)
    }

    private var footerNotice: some View {
        VStack(spacing: 2) {
            Text("게스트 모드: 로컬 저장 · 서버 미전송 · 기능 제한")
                .font(.system(size: 10, weight: .medium))
                .foregroundStyle(.white.opacity(0.7))
            Text("계속 진행하면 이용약관 · 개인정보처리방침에 동의하게 됩니다")
                .font(.system(size: 10, weight: .medium))
                .foregroundStyle(.white.opacity(0.55))
        }
    }

    private func errorToast(_ message: String) -> some View {
        HStack(spacing: 8) {
            Image(systemName: "exclamationmark.triangle.fill")
                .font(.system(size: 12, weight: .bold))
                .foregroundStyle(.white)
            Text(message)
                .font(.system(size: 12, weight: .semibold))
                .foregroundStyle(.white)
            Spacer(minLength: 6)
            Button(action: { auth.errorMessage = nil }) {
                Text("다시 시도")
                    .font(.system(size: 11, weight: .bold))
                    .padding(.horizontal, 8).padding(.vertical, 3)
                    .background(Capsule().fill(.white.opacity(0.25)))
                    .foregroundStyle(.white)
            }
            .buttonStyle(.plain)
        }
        .padding(.horizontal, 14).padding(.vertical, 10)
        .background(Capsule().fill(Color.red.opacity(0.9)))
        .padding(.horizontal, 40)
    }
}
