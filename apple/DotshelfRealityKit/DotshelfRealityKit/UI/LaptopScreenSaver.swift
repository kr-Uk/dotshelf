import SwiftUI

/// 노트북 디스플레이에 텍스처로 구워지는 화면 보호기.
/// - 1200×750 (1.6:1) — laptop_screen_glow plane 비율과 동일.
/// - 중앙에 큰 책이 X축 -30° 기울어진 채 Y축으로 360° 회전, 하단에 "dotshelf" 브랜드.
///   ContentView가 1초 간격으로 재렌더해 emissive 텍스처로 반영. 1초에 약 30° → 1회전 ~12초.
struct LaptopScreenSaver: View {
    let time: Date

    // 팔레트 (dotshelf 따뜻한 서재 톤)
    private let cream  = Color(red: 0.99, green: 0.95, blue: 0.88)
    private let sand   = Color(red: 0.97, green: 0.89, blue: 0.78)
    private let peach  = Color(red: 1.00, green: 0.82, blue: 0.72)
    private let sage   = Color(red: 0.74, green: 0.86, blue: 0.74)
    private let lavender = Color(red: 0.84, green: 0.78, blue: 0.94)
    private let ink    = Color(red: 0.28, green: 0.20, blue: 0.18)
    private let subInk = Color(red: 0.42, green: 0.32, blue: 0.28)

    // 표지 팔레트 (회전하면서 느낌이 살아나도록 깊이감 있는 배색)
    private let coverTop    = Color(red: 0.82, green: 0.42, blue: 0.30)
    private let coverBottom = Color(red: 0.58, green: 0.24, blue: 0.16)

    var body: some View {
        let t = time.timeIntervalSince1970

        ZStack {
            background
            glowOrbs(t: t)
            rotatingBook(t: t)
            bottomBranding
        }
        .frame(width: 1200, height: 750)
    }

    // MARK: - Base gradient

    private var background: some View {
        LinearGradient(
            colors: [cream, sand, peach],
            startPoint: .topLeading,
            endPoint: .bottomTrailing
        )
    }

    // MARK: - 블러 글로우 원 (피치 / 세이지 / 라벤더) — 배경 장식

    @ViewBuilder
    private func glowOrbs(t: TimeInterval) -> some View {
        let ax = CGFloat(sin(t * 0.09) * 280)
        let ay = CGFloat(cos(t * 0.07) * 140)
        let bx = CGFloat(cos(t * 0.06) * 300)
        let by = CGFloat(sin(t * 0.10) * 160)

        Circle()
            .fill(RadialGradient(colors: [peach.opacity(0.55), .clear],
                                 center: .center, startRadius: 0, endRadius: 260))
            .frame(width: 520, height: 520)
            .offset(x: ax, y: ay)
            .blur(radius: 72)

        Circle()
            .fill(RadialGradient(colors: [sage.opacity(0.45), .clear],
                                 center: .center, startRadius: 0, endRadius: 240))
            .frame(width: 480, height: 480)
            .offset(x: -bx, y: by)
            .blur(radius: 80)

        Circle()
            .fill(RadialGradient(colors: [lavender.opacity(0.35), .clear],
                                 center: .center, startRadius: 0, endRadius: 220))
            .frame(width: 440, height: 440)
            .offset(x: CGFloat(sin(t * 0.11 + 1.2) * 240), y: -CGFloat(cos(t * 0.08 + 0.8) * 120))
            .blur(radius: 68)
    }

    // MARK: - 회전하는 큰 책 (30° 기울어짐 + Y축 연속 회전)

    @ViewBuilder
    private func rotatingBook(t: TimeInterval) -> some View {
        // 1초 재렌더 × 30°/s = 1회전 12초. 부드럽진 않지만 넘기는 듯한 spin 느낌.
        let yDeg = (t * 30).truncatingRemainder(dividingBy: 360)

        ZStack {
            // 표지
            RoundedRectangle(cornerRadius: 10)
                .fill(LinearGradient(
                    colors: [coverTop, coverBottom],
                    startPoint: .topLeading, endPoint: .bottomTrailing
                ))

            // 표지 가장자리 장식 라인
            RoundedRectangle(cornerRadius: 7)
                .strokeBorder(.white.opacity(0.25), lineWidth: 2)
                .padding(10)

            // 표지 디테일 — 상·하단 얇은 금색 라인 + 중앙 크림 이니셜
            VStack(spacing: 14) {
                Rectangle()
                    .fill(Color(red: 0.96, green: 0.82, blue: 0.45))
                    .frame(height: 2)
                    .padding(.horizontal, 30)
                    .padding(.top, 36)
                Spacer(minLength: 0)
                Text("D")
                    .font(.system(size: 128, weight: .black, design: .serif))
                    .foregroundStyle(.white.opacity(0.92))
                    .shadow(color: .black.opacity(0.2), radius: 3)
                Spacer(minLength: 0)
                Rectangle()
                    .fill(Color(red: 0.96, green: 0.82, blue: 0.45))
                    .frame(height: 2)
                    .padding(.horizontal, 30)
                    .padding(.bottom, 36)
            }
        }
        .frame(width: 240, height: 330)
        .shadow(color: .black.opacity(0.25), radius: 28, x: 0, y: 18)
        .rotation3DEffect(.degrees(yDeg), axis: (0, 1, 0))
        .rotation3DEffect(.degrees(-30), axis: (1, 0, 0))
        // 책을 살짝 위로 — 하단 브랜드 텍스트와 겹치지 않게
        .offset(y: -70)
    }

    // MARK: - 하단 브랜드 + 시계

    private var bottomBranding: some View {
        VStack(spacing: 4) {
            Spacer()
            Text("dotshelf")
                .font(.system(size: 72, weight: .black, design: .serif))
                .foregroundStyle(ink)
                .shadow(color: .white.opacity(0.55), radius: 2)
                .shadow(color: peach.opacity(0.35), radius: 16, y: 4)

            Text("책장에 꽂힌 책을 보고 그 사람을 알다")
                .font(.system(size: 14, weight: .medium, design: .serif))
                .italic()
                .foregroundStyle(subInk.opacity(0.78))

            HStack(spacing: 8) {
                Text(clockString(time))
                Text("·")
                Text(dateString(time))
            }
            .font(.system(size: 12, weight: .medium, design: .monospaced))
            .foregroundStyle(subInk.opacity(0.6))
            .padding(.top, 8)
        }
        .padding(.bottom, 48)
    }

    private func clockString(_ d: Date) -> String {
        let f = DateFormatter()
        f.dateFormat = "HH:mm:ss"
        return f.string(from: d)
    }

    private func dateString(_ d: Date) -> String {
        let f = DateFormatter()
        f.locale = Locale(identifier: "ko_KR")
        f.dateFormat = "yyyy년 M월 d일 EEEE"
        return f.string(from: d)
    }
}
