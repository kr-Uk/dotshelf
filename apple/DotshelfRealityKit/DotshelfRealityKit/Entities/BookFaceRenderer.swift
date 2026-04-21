import SwiftUI
import RealityKit

/// 기본 박스 책(비-3D-스캔)의 표지/책등/후면에 텍스트를 입혀주기 위한 SwiftUI → Texture 렌더러.
/// 각 면은 UnlitMaterial + PlanePBR 교차 사용. 조명 반응보다 텍스트 가독성 우선.
enum BookFaceRenderer {
    // MARK: - 픽셀 사이즈 (메모리/가독성 균형)
    private static let coverPixelWidth: CGFloat = 192
    private static let coverPixelHeight: CGFloat = 288
    private static let spinePixelWidth: CGFloat = 48
    private static let spinePixelHeight: CGFloat = 288

    // MARK: - Public

    @MainActor
    static func makeFaceMaterial(view: some View, pixelWidth: CGFloat, pixelHeight: CGFloat) -> (any RealityKit.Material)? {
        let renderer = ImageRenderer(content:
            view.frame(width: pixelWidth, height: pixelHeight)
        )
        renderer.scale = 2.0   // 2× 해상도 (로드 무거워지지 않게 px 자체는 작게 유지)
        renderer.isOpaque = true

        guard let cg = renderer.cgImage else { return nil }
        guard let tex = try? TextureResource(image: cg, options: .init(semantic: .color)) else {
            return nil
        }
        var mat = UnlitMaterial()
        mat.color = .init(tint: .white, texture: .init(tex))
        return mat
    }

    @MainActor
    static func coverMaterial(record: BookRecord, bgColor: PlatformColor) -> (any RealityKit.Material)? {
        let view = BookCoverFace(
            record: record,
            bgColor: Color(platformColor: bgColor),
            textColor: contrastTextColor(for: bgColor)
        )
        return makeFaceMaterial(view: view, pixelWidth: coverPixelWidth, pixelHeight: coverPixelHeight)
    }

    @MainActor
    static func backMaterial(record: BookRecord, bgColor: PlatformColor) -> (any RealityKit.Material)? {
        let view = BookBackFace(
            record: record,
            bgColor: Color(platformColor: bgColor),
            textColor: contrastTextColor(for: bgColor)
        )
        return makeFaceMaterial(view: view, pixelWidth: coverPixelWidth, pixelHeight: coverPixelHeight)
    }

    @MainActor
    static func spineMaterial(record: BookRecord, bgColor: PlatformColor) -> (any RealityKit.Material)? {
        let view = BookSpineFace(
            record: record,
            bgColor: Color(platformColor: bgColor),
            textColor: contrastTextColor(for: bgColor)
        )
        return makeFaceMaterial(view: view, pixelWidth: spinePixelWidth, pixelHeight: spinePixelHeight)
    }

    // MARK: - Color helpers

    /// 배경 밝기에 따라 가독성 좋은 텍스트 색 선택.
    static func contrastTextColor(for color: PlatformColor) -> Color {
        var r: CGFloat = 0, g: CGFloat = 0, b: CGFloat = 0, a: CGFloat = 0
        #if os(macOS)
        let resolved = color.usingColorSpace(.sRGB) ?? color
        resolved.getRed(&r, green: &g, blue: &b, alpha: &a)
        #else
        color.getRed(&r, green: &g, blue: &b, alpha: &a)
        #endif
        let luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b
        return luminance > 0.55 ? .black : .white
    }
}

// MARK: - SwiftUI.Color 플랫폼 변환

private extension Color {
    init(platformColor: PlatformColor) {
        #if os(macOS)
        self.init(nsColor: platformColor)
        #else
        self.init(uiColor: platformColor)
        #endif
    }
}

// MARK: - Face views

/// 표지: 제목·저자·출판사
private struct BookCoverFace: View {
    let record: BookRecord
    let bgColor: Color
    let textColor: Color

    var body: some View {
        ZStack {
            bgColor
            // 배경 위에 아주 옅은 비네트로 깊이감
            LinearGradient(
                colors: [.clear, .black.opacity(0.18)],
                startPoint: .top,
                endPoint: .bottom
            )
            VStack(spacing: 4) {
                Spacer(minLength: 10)
                Text(record.title)
                    .font(.system(size: 22, weight: .heavy, design: .serif))
                    .multilineTextAlignment(.center)
                    .lineLimit(5)
                    .minimumScaleFactor(0.6)
                    .foregroundStyle(textColor)
                    .padding(.horizontal, 12)
                Spacer()
                VStack(spacing: 2) {
                    Text(record.author)
                        .font(.system(size: 12, weight: .semibold))
                        .foregroundStyle(textColor.opacity(0.9))
                    Text(record.publisher)
                        .font(.system(size: 10, weight: .medium))
                        .foregroundStyle(textColor.opacity(0.7))
                }
                Spacer(minLength: 14)
            }
        }
    }
}

/// 책등: 세로로 긴 영역. 세로 텍스트로 제목 표시.
private struct BookSpineFace: View {
    let record: BookRecord
    let bgColor: Color
    let textColor: Color

    var body: some View {
        ZStack {
            bgColor
            // 책등 상·하 얇은 장식 라인
            VStack {
                Rectangle()
                    .fill(textColor.opacity(0.35))
                    .frame(height: 2)
                    .padding(.horizontal, 6)
                    .padding(.top, 6)
                Spacer()
                Rectangle()
                    .fill(textColor.opacity(0.35))
                    .frame(height: 2)
                    .padding(.horizontal, 6)
                    .padding(.bottom, 6)
            }
            // 세로로 읽히는 제목 (시계방향 90° 회전)
            Text(record.title)
                .font(.system(size: 12, weight: .bold, design: .serif))
                .lineLimit(1)
                .minimumScaleFactor(0.4)
                .foregroundStyle(textColor)
                .rotationEffect(.degrees(90))
                .fixedSize()
                .frame(maxWidth: .infinity, maxHeight: .infinity)
        }
    }
}

/// 후면: 책 소개(summary) 본문.
private struct BookBackFace: View {
    let record: BookRecord
    let bgColor: Color
    let textColor: Color

    var body: some View {
        ZStack {
            bgColor
            VStack(alignment: .leading, spacing: 8) {
                Text("소개")
                    .font(.system(size: 9, weight: .bold))
                    .foregroundStyle(textColor.opacity(0.7))
                    .textCase(.uppercase)
                Text(record.summary)
                    .font(.system(size: 9, weight: .regular, design: .serif))
                    .multilineTextAlignment(.leading)
                    .foregroundStyle(textColor.opacity(0.92))
                    .lineSpacing(1.5)
                Spacer(minLength: 0)
                HStack {
                    Spacer()
                    Text(record.publisher)
                        .font(.system(size: 8, weight: .semibold))
                        .foregroundStyle(textColor.opacity(0.55))
                }
            }
            .padding(10)
        }
    }
}
