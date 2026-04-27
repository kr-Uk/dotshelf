import SwiftUI

/// 방명록 엔트리에 답장하는 시트 오버레이 (공간 주인용).
/// - 명세 §4: 공개/비공개 답장.
struct GuestbookReplySheet: View {
    let store: GuestbookStore
    let entry: GuestbookEntry
    var onClose: () -> Void

    @State private var text: String = ""
    @State private var isPublic: Bool = true

    private var canSubmit: Bool {
        !text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
    }

    var body: some View {
        VStack(spacing: 0) {
            header
            Divider().opacity(0.3)
            originalCard
            replyField
            footer
        }
        .frame(width: 420)
        .background(
            RoundedRectangle(cornerRadius: 18)
                .fill(.ultraThinMaterial)
        )
        .overlay(
            RoundedRectangle(cornerRadius: 18)
                .strokeBorder(.white.opacity(0.08), lineWidth: 1)
        )
        .shadow(color: .black.opacity(0.25), radius: 18, y: 8)
    }

    private var header: some View {
        HStack {
            Text("답장하기")
                .font(.system(size: 16, weight: .bold))
            Spacer()
            Button(action: onClose) {
                Image(systemName: "xmark.circle.fill")
                    .font(.system(size: 20))
                    .foregroundStyle(.secondary, .quaternary)
            }
            .buttonStyle(.plain)
        }
        .padding(.horizontal, 18)
        .padding(.vertical, 14)
    }

    private var originalCard: some View {
        HStack(alignment: .top, spacing: 10) {
            Circle()
                .fill(entry.avatarColor.gradient)
                .overlay(
                    Text(entry.initials)
                        .font(.system(size: 12, weight: .black))
                        .foregroundStyle(.white)
                )
                .frame(width: 32, height: 32)

            VStack(alignment: .leading, spacing: 2) {
                HStack(spacing: 6) {
                    Text(entry.authorName)
                        .font(.system(size: 13, weight: .bold))
                    Text(entry.time)
                        .font(.system(size: 11))
                        .foregroundStyle(.secondary)
                }
                Text(entry.message)
                    .font(.system(size: 13))
                    .foregroundStyle(.primary.opacity(0.85))
            }
            Spacer(minLength: 0)
        }
        .padding(12)
        .background(
            RoundedRectangle(cornerRadius: 10)
                .fill(entry.style.tint.opacity(0.3))
        )
        .padding(.horizontal, 18)
        .padding(.top, 14)
    }

    private var replyField: some View {
        VStack(alignment: .leading, spacing: 8) {
            TextField("답장을 남겨보세요", text: $text, axis: .vertical)
                .lineLimit(2...4)
                .textFieldStyle(.plain)
                .font(.system(size: 14))
                .padding(10)
                .background(
                    RoundedRectangle(cornerRadius: 8)
                        .fill(.white.opacity(0.06))
                )
            HStack(spacing: 10) {
                Text("공개 설정")
                    .font(.system(size: 12, weight: .semibold))
                    .foregroundStyle(.secondary)
                Spacer()
                Picker("", selection: $isPublic) {
                    Label("공개", systemImage: "globe").tag(true)
                    Label("비공개", systemImage: "lock.fill").tag(false)
                }
                .pickerStyle(.segmented)
                .frame(width: 180)
            }
        }
        .padding(.horizontal, 18)
        .padding(.top, 12)
    }

    private var footer: some View {
        HStack(spacing: 10) {
            Spacer()
            Button(action: onClose) {
                Text("취소")
                    .font(.system(size: 13, weight: .medium))
                    .padding(.horizontal, 16)
                    .padding(.vertical, 8)
                    .background(.ultraThinMaterial, in: Capsule())
            }
            .buttonStyle(.plain)
            Button(action: submit) {
                Text("답장 보내기")
                    .font(.system(size: 13, weight: .semibold))
                    .foregroundStyle(.white)
                    .padding(.horizontal, 18)
                    .padding(.vertical, 8)
                    .background(canSubmit ? Color.accentColor : Color.gray, in: Capsule())
            }
            .buttonStyle(.plain)
            .disabled(!canSubmit)
        }
        .padding(18)
    }

    private func submit() {
        guard canSubmit else { return }
        store.addReply(to: entry.id, message: text, isPublic: isPublic)
        onClose()
    }
}
