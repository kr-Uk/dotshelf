import SwiftUI

/// 꾸미기 오브젝트 갤러리 — 페이즈 C 스텝 5.
/// 프리셋을 고르면 현재 책장의 빈 섹션에 배치.
struct DecorationGalleryOverlay: View {
    var onAdd: (DecorationPreset) -> Bool
    var onClose: () -> Void

    @State private var addedCount: Int = 0
    @State private var errorMessage: String?

    var body: some View {
        ZStack {
            Color.black.opacity(0.4).ignoresSafeArea()
                .onTapGesture { onClose() }

            VStack(spacing: 0) {
                header
                Divider().opacity(0.3)
                if let errorMessage {
                    errorBanner(errorMessage)
                }
                ScrollView {
                    LazyVGrid(
                        columns: [GridItem(.flexible()), GridItem(.flexible())],
                        spacing: 10
                    ) {
                        ForEach(DecorationPreset.allCases) { preset in
                            cell(for: preset)
                        }
                    }
                    .padding(16)
                }
            }
            .frame(maxWidth: 480, maxHeight: 560)
            .background(
                RoundedRectangle(cornerRadius: 22).fill(.ultraThinMaterial)
            )
            .clipShape(RoundedRectangle(cornerRadius: 22))
            .shadow(color: .black.opacity(0.25), radius: 22, y: 10)
            .padding(30)
        }
    }

    private var header: some View {
        HStack {
            Image(systemName: "sparkles")
                .font(.system(size: 14, weight: .bold))
                .foregroundStyle(.tint)
            Text("꾸미기 오브젝트")
                .font(.system(size: 16, weight: .bold))
            if addedCount > 0 {
                Text("+\(addedCount)")
                    .font(.system(size: 11, weight: .bold))
                    .padding(.horizontal, 6)
                    .padding(.vertical, 2)
                    .background(Color.accentColor.opacity(0.15), in: Capsule())
                    .foregroundStyle(Color.accentColor)
            }
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

    private func errorBanner(_ msg: String) -> some View {
        HStack(spacing: 6) {
            Image(systemName: "exclamationmark.triangle.fill")
                .font(.system(size: 11, weight: .bold))
            Text(msg).font(.system(size: 12, weight: .medium))
            Spacer()
            Button {
                errorMessage = nil
            } label: {
                Image(systemName: "xmark").font(.system(size: 10, weight: .bold))
            }
            .buttonStyle(.plain)
        }
        .padding(.horizontal, 14)
        .padding(.vertical, 8)
        .background(Color.red.opacity(0.15))
        .foregroundStyle(.red)
    }

    @ViewBuilder
    private func cell(for preset: DecorationPreset) -> some View {
        let rgb = preset.previewRGB
        let previewColor = Color(red: rgb.0, green: rgb.1, blue: rgb.2)

        Button {
            let ok = onAdd(preset)
            if ok {
                addedCount += 1
                errorMessage = nil
            } else {
                errorMessage = "현재 책장에 빈 공간이 없어요"
            }
        } label: {
            VStack(spacing: 8) {
                ZStack {
                    RoundedRectangle(cornerRadius: 10)
                        .fill(previewColor.opacity(0.85))
                    Image(systemName: preset.systemImage)
                        .font(.system(size: 28, weight: .bold))
                        .foregroundStyle(.white.opacity(0.9))
                }
                .frame(height: 88)

                Text(preset.label)
                    .font(.system(size: 12, weight: .semibold))
                    .foregroundStyle(.primary)
            }
            .padding(10)
            .background(Color.gray.opacity(0.06), in: RoundedRectangle(cornerRadius: 14))
        }
        .buttonStyle(.plain)
    }
}
