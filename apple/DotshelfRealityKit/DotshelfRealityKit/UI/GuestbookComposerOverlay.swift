import SwiftUI

/// 화이트보드 뷰 위에 띄우는 방명록 작성 오버레이.
/// - 명세 §4: 30자 제한, 포스트잇 스타일 선택, 공개/비공개 설정.
struct GuestbookComposerOverlay: View {
    let store: GuestbookStore
    var onClose: () -> Void

    @State private var text: String = ""
    @State private var emoji: String = "✨"
    @State private var style: PostItStyle = .yellow
    @State private var isPublic: Bool = true

    private let emojiChoices = ["✨", "📚", "☕️", "💕", "🌿", "🤔", "👀", "🔥"]

    private var remaining: Int { GuestbookStore.bodyLimit - text.count }
    private var canSubmit: Bool {
        let trimmed = text.trimmingCharacters(in: .whitespacesAndNewlines)
        return !trimmed.isEmpty && trimmed.count <= GuestbookStore.bodyLimit
    }

    var body: some View {
        VStack(spacing: 0) {
            header
            Divider().opacity(0.3)
            postItPreview
            messageField
            emojiPicker
            stylePicker
            visibilityPicker
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
            Text("방명록 남기기")
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

    private var postItPreview: some View {
        HStack(alignment: .top, spacing: 10) {
            Text(emoji)
                .font(.system(size: 28))
            Text(text.isEmpty ? "여기에 짧은 인사를 남겨주세요" : text)
                .font(.system(size: 14, weight: .medium))
                .foregroundStyle(text.isEmpty ? .secondary : .primary)
                .frame(maxWidth: .infinity, alignment: .leading)
        }
        .padding(14)
        .background(
            RoundedRectangle(cornerRadius: 10)
                .fill(style.tint.opacity(0.55))
        )
        .overlay(
            RoundedRectangle(cornerRadius: 10)
                .strokeBorder(style.tint.opacity(0.8), lineWidth: 1)
        )
        .padding(.horizontal, 18)
        .padding(.top, 14)
    }

    private var messageField: some View {
        VStack(alignment: .leading, spacing: 4) {
            TextField("한 마디 남겨보세요", text: $text, axis: .vertical)
                .lineLimit(2...3)
                .textFieldStyle(.plain)
                .font(.system(size: 14))
                .onChange(of: text) { _, new in
                    if new.count > GuestbookStore.bodyLimit {
                        text = String(new.prefix(GuestbookStore.bodyLimit))
                    }
                }
                .padding(10)
                .background(
                    RoundedRectangle(cornerRadius: 8)
                        .fill(.white.opacity(0.06))
                )
            HStack {
                Spacer()
                Text("\(remaining)자 남음")
                    .font(.system(size: 11, weight: .medium))
                    .foregroundStyle(remaining < 0 ? .red : .secondary)
            }
        }
        .padding(.horizontal, 18)
        .padding(.top, 12)
    }

    private var emojiPicker: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text("이모지")
                .font(.system(size: 12, weight: .semibold))
                .foregroundStyle(.secondary)
            HStack(spacing: 6) {
                ForEach(emojiChoices, id: \.self) { e in
                    Button(action: { emoji = e }) {
                        Text(e)
                            .font(.system(size: 20))
                            .padding(8)
                            .background(
                                Circle().fill(emoji == e ? Color.accentColor.opacity(0.3) : Color.clear)
                            )
                    }
                    .buttonStyle(.plain)
                }
            }
        }
        .padding(.horizontal, 18)
        .padding(.top, 10)
    }

    private var stylePicker: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text("포스트잇 스타일")
                .font(.system(size: 12, weight: .semibold))
                .foregroundStyle(.secondary)
            HStack(spacing: 8) {
                ForEach(PostItStyle.allCases) { s in
                    let enabled = store.enabledStyles.contains(s)
                    Button(action: { if enabled { style = s } }) {
                        Circle()
                            .fill(s.tint)
                            .frame(width: 26, height: 26)
                            .overlay(
                                Circle()
                                    .strokeBorder(style == s ? Color.white : Color.black.opacity(0.2),
                                                  lineWidth: style == s ? 2 : 1)
                            )
                            .opacity(enabled ? 1.0 : 0.25)
                    }
                    .buttonStyle(.plain)
                    .disabled(!enabled)
                }
            }
        }
        .padding(.horizontal, 18)
        .padding(.top, 10)
    }

    private var visibilityPicker: some View {
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
        .padding(.horizontal, 18)
        .padding(.top, 10)
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
                Text("남기기")
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
        store.addEntry(
            authorName: "방문자",
            initials: "V",
            avatarColor: .indigo,
            message: text,
            emoji: emoji,
            style: style,
            isPublic: isPublic
        )
        onClose()
    }
}
