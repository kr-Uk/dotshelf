import SwiftUI

/// 대시보드의 한 줄 소개를 편집하는 오버레이 (공간 주인용).
/// - 명세 §4: 한 줄 소개 편집.
struct ProfileBioEditor: View {
    let store: GuestbookStore
    var onClose: () -> Void

    @State private var text: String = ""
    @State private var visibility: GuestbookVisibility

    private let bioLimit = 60

    init(store: GuestbookStore, onClose: @escaping () -> Void) {
        self.store = store
        self.onClose = onClose
        _text = State(initialValue: store.bioText)
        _visibility = State(initialValue: store.listVisibility)
    }

    private var canSubmit: Bool {
        !text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
    }

    var body: some View {
        VStack(spacing: 0) {
            header
            Divider().opacity(0.3)
            ScrollView(.vertical, showsIndicators: false) {
                VStack(alignment: .leading, spacing: 0) {
                    bioField
                    visibilitySection
                    styleToggleSection
                }
                .padding(.bottom, 8)
            }
            .frame(maxHeight: 460)
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
            Text("프로필 편집")
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

    private var bioField: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text("한 줄 소개")
                .font(.system(size: 12, weight: .semibold))
                .foregroundStyle(.secondary)
            TextField("이 공간에 대해 한 줄로 소개해보세요", text: $text, axis: .vertical)
                .lineLimit(2...3)
                .textFieldStyle(.plain)
                .font(.system(size: 14))
                .onChange(of: text) { _, new in
                    if new.count > bioLimit {
                        text = String(new.prefix(bioLimit))
                    }
                }
                .padding(10)
                .background(
                    RoundedRectangle(cornerRadius: 8).fill(.white.opacity(0.06))
                )
            HStack {
                Spacer()
                Text("\(bioLimit - text.count)자 남음")
                    .font(.system(size: 11))
                    .foregroundStyle(.secondary)
            }
        }
        .padding(.horizontal, 18)
        .padding(.top, 12)
    }

    private var visibilitySection: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text("방명록 공개 범위")
                .font(.system(size: 12, weight: .semibold))
                .foregroundStyle(.secondary)
            VStack(spacing: 6) {
                ForEach(GuestbookVisibility.allCases) { (v: GuestbookVisibility) in
                    visibilityRow(for: v)
                }
            }
        }
        .padding(.horizontal, 18)
        .padding(.top, 10)
    }

    private var styleToggleSection: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text("방문자에게 허용할 포스트잇 스타일")
                .font(.system(size: 12, weight: .semibold))
                .foregroundStyle(.secondary)
            Text("방문자가 방명록을 남길 때 고를 수 있는 스타일이에요")
                .font(.system(size: 10))
                .foregroundStyle(.secondary.opacity(0.8))
                .padding(.bottom, 2)

            LazyVGrid(columns: [GridItem(.flexible()), GridItem(.flexible())], spacing: 6) {
                ForEach(PostItStyle.allCases) { (s: PostItStyle) in
                    styleRow(for: s)
                }
            }
        }
        .padding(.horizontal, 18)
        .padding(.top, 12)
    }

    @ViewBuilder
    private func styleRow(for s: PostItStyle) -> some View {
        let isEnabled = store.enabledStyles.contains(s)
        Button(action: { store.toggleStyle(s, enabled: !isEnabled) }) {
            HStack(spacing: 8) {
                Circle().fill(s.tint).frame(width: 14, height: 14)
                Text(s.label)
                    .font(.system(size: 12, weight: .medium))
                Spacer()
                Image(systemName: isEnabled ? "checkmark.square.fill" : "square")
                    .font(.system(size: 14))
                    .foregroundStyle(isEnabled ? Color.accentColor : .secondary)
            }
            .padding(.horizontal, 10)
            .padding(.vertical, 7)
            .background(
                RoundedRectangle(cornerRadius: 8)
                    .fill(.white.opacity(0.04))
            )
        }
        .buttonStyle(.plain)
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
                Text("저장")
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
        store.updateBio(text)
        store.setListVisibility(visibility)
        onClose()
    }

    @ViewBuilder
    private func visibilityRow(for v: GuestbookVisibility) -> some View {
        let isSelected = visibility == v
        Button(action: { visibility = v }) {
            HStack(spacing: 10) {
                Image(systemName: v.systemImage)
                    .font(.system(size: 13, weight: .semibold))
                    .frame(width: 20)
                Text(v.label)
                    .font(.system(size: 13, weight: .medium))
                Spacer()
                if isSelected {
                    Image(systemName: "checkmark")
                        .font(.system(size: 12, weight: .bold))
                        .foregroundStyle(.tint)
                }
            }
            .padding(.horizontal, 12)
            .padding(.vertical, 8)
            .background(
                RoundedRectangle(cornerRadius: 8)
                    .fill(isSelected ? Color.accentColor.opacity(0.15) : .white.opacity(0.04))
            )
        }
        .buttonStyle(.plain)
    }
}
