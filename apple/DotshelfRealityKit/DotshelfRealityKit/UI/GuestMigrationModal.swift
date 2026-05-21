import SwiftUI

/// §8 게스트 → 정식 가입 시 데이터 이전 여부 묻는 모달.
/// "로컬 데이터 유지" / "새로 시작" 2가지.
struct GuestMigrationModal: View {
    let auth: AuthStore
    let summary: GuestSessionSummary

    var body: some View {
        ZStack {
            Color.black.opacity(0.5).ignoresSafeArea()
            card
                .frame(maxWidth: 420)
                .padding(24)
        }
    }

    private var card: some View {
        VStack(spacing: 0) {
            header
            Divider().opacity(0.3)
            body_
            Divider().opacity(0.3)
            footer
        }
        .background(
            RoundedRectangle(cornerRadius: 18)
                .fill(.regularMaterial)
        )
        .overlay(
            RoundedRectangle(cornerRadius: 18)
                .strokeBorder(.white.opacity(0.1), lineWidth: 1)
        )
        .shadow(color: .black.opacity(0.35), radius: 18, y: 8)
    }

    private var header: some View {
        VStack(spacing: 6) {
            Image(systemName: "arrow.triangle.2.circlepath")
                .font(.system(size: 30))
                .foregroundStyle(.tint)
            Text("게스트 데이터 이전")
                .font(.system(size: 16, weight: .bold))
            Text("게스트로 만들던 공간을 계정에 가져올까요?")
                .font(.system(size: 12, weight: .medium))
                .foregroundStyle(.secondary)
                .multilineTextAlignment(.center)
        }
        .padding(18)
    }

    private var body_: some View {
        VStack(spacing: 10) {
            summaryRow(icon: "books.vertical.fill", label: "책", value: summary.bookCount, color: .orange)
            summaryRow(icon: "square.stack.3d.up.fill", label: "책장", value: summary.bookshelfCount, color: .blue)
            summaryRow(icon: "text.bubble.fill", label: "받은 방명록", value: summary.guestbookCount, color: .cyan)
            summaryRow(icon: "paintpalette.fill", label: "꾸미기 오브젝트", value: summary.decorationCount, color: .purple)
        }
        .padding(18)
    }

    private func summaryRow(icon: String, label: String, value: Int, color: Color) -> some View {
        HStack(spacing: 10) {
            Image(systemName: icon)
                .font(.system(size: 13, weight: .bold))
                .foregroundStyle(color)
                .frame(width: 22)
            Text(label)
                .font(.system(size: 13, weight: .medium))
            Spacer()
            Text("\(value)")
                .font(.system(size: 13, weight: .bold))
                .foregroundStyle(value > 0 ? Color.primary : Color.secondary)
        }
        .padding(.horizontal, 12).padding(.vertical, 8)
        .background(RoundedRectangle(cornerRadius: 8).fill(.white.opacity(0.04)))
    }

    private var footer: some View {
        VStack(spacing: 6) {
            Button(action: { auth.resolveGuestMigration(keepData: true) }) {
                HStack(spacing: 6) {
                    Image(systemName: "tray.and.arrow.down.fill")
                        .font(.system(size: 13, weight: .bold))
                    Text("계정으로 가져오기")
                        .font(.system(size: 13, weight: .bold))
                }
                .foregroundStyle(.white)
                .frame(maxWidth: .infinity)
                .padding(.vertical, 12)
                .background(Capsule().fill(Color.accentColor))
            }
            .buttonStyle(.plain)

            Button(action: { auth.resolveGuestMigration(keepData: false) }) {
                Text("새로 시작")
                    .font(.system(size: 12, weight: .semibold))
                    .foregroundStyle(.secondary)
                    .frame(maxWidth: .infinity)
                    .padding(.vertical, 10)
            }
            .buttonStyle(.plain)

            Text("이전 선택은 설정 → 계정에서 바꿀 수 없어요")
                .font(.system(size: 10))
                .foregroundStyle(.secondary.opacity(0.7))
        }
        .padding(.horizontal, 14).padding(.vertical, 12)
    }
}
