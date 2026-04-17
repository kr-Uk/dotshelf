import SwiftUI

struct GuestEntry: Identifiable {
    let id = UUID()
    let name: String
    let initials: String
    let color: Color
    let time: String
    let message: String
    let emoji: String
}

/// 화이트보드 표면에 텍스처로 렌더링되는 대시보드.
/// 다크 모드 — 어두운 bg에 밝은 요소가 발광하듯 떠 있는 디스플레이 룩.
/// 크기: 1200×750 (1.6:1 비율).
struct WhiteboardDashboard: View {
    let bookCount: Int
    let likeCount: Int
    let isLiked: Bool

    private let todayVisits = 12
    private let totalVisits = 847

    private let entries: [GuestEntry] = [
        GuestEntry(name: "지호", initials: "J", color: .orange, time: "10분 전",
                   message: "책장 완전 예뻐 👀", emoji: "✨"),
        GuestEntry(name: "수민", initials: "S", color: .cyan, time: "1시간 전",
                   message: "다음 책 추천해줘 진짜", emoji: "📚"),
        GuestEntry(name: "민준", initials: "M", color: .green, time: "3시간 전",
                   message: "45도로 꽂은 거 멋지네", emoji: "🤔"),
        GuestEntry(name: "유나", initials: "Y", color: .pink, time: "어제",
                   message: "서재 느낌 좋아요 ☕️", emoji: "💕"),
    ]

    // 다크 디스플레이 팔레트
    private let bgColor = Color(red: 0.08, green: 0.09, blue: 0.12)       // 거의 검정 (네이비 살짝)
    private let cardBg = Color(red: 0.15, green: 0.17, blue: 0.22)        // 카드 bg
    private let textPrimary = Color(red: 0.95, green: 0.96, blue: 0.98)   // 거의 흰색
    private let textSecondary = Color(red: 0.62, green: 0.66, blue: 0.73) // 중간 회색

    var body: some View {
        HStack(alignment: .top, spacing: 24) {
            leftColumn
            rightColumn
        }
        .padding(32)
        .frame(width: 1200, height: 750)
        .background(bgColor)
    }

    private var leftColumn: some View {
        VStack(alignment: .leading, spacing: 20) {
            header
            likeCard
            statsGrid
            Spacer(minLength: 0)
        }
        .frame(width: 460, alignment: .leading)
    }

    private var rightColumn: some View {
        VStack(alignment: .leading, spacing: 14) {
            HStack(alignment: .firstTextBaseline) {
                Text("방명록")
                    .font(.system(size: 22, weight: .bold))
                    .foregroundStyle(textPrimary)
                Text("\(entries.count)건")
                    .font(.system(size: 14, weight: .medium))
                    .foregroundStyle(textSecondary)
                Spacer()
            }

            VStack(spacing: 10) {
                ForEach(entries) { entry in
                    GuestEntryRow(entry: entry, cardBg: cardBg, textPrimary: textPrimary, textSecondary: textSecondary)
                }
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    private var header: some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack(spacing: 8) {
                Text("📝")
                    .font(.system(size: 28))
                Text("Dotshelf 서재")
                    .font(.system(size: 32, weight: .black))
                    .foregroundStyle(textPrimary)
            }
            Text("이 공간에 방문해줘서 고마워요")
                .font(.system(size: 14, weight: .medium))
                .foregroundStyle(textSecondary)
        }
    }

    private var likeCard: some View {
        HStack(spacing: 14) {
            Image(systemName: isLiked ? "heart.fill" : "heart")
                .font(.system(size: 30, weight: .bold))
                .foregroundStyle(isLiked ? Color.red : textSecondary)

            VStack(alignment: .leading, spacing: 3) {
                Text("\(likeCount.formatted())")
                    .font(.system(size: 28, weight: .black))
                    .foregroundStyle(textPrimary)
                Text(isLiked ? "이 서재를 좋아하고 있어요" : "이 공간이 마음에 드나요?")
                    .font(.system(size: 12, weight: .medium))
                    .foregroundStyle(textSecondary)
            }
            Spacer()
        }
        .padding(18)
        .background(
            RoundedRectangle(cornerRadius: 14)
                .fill(isLiked ? Color.red.opacity(0.18) : cardBg)
        )
        .overlay(
            RoundedRectangle(cornerRadius: 14)
                .strokeBorder(isLiked ? Color.red.opacity(0.5) : .white.opacity(0.08), lineWidth: 1)
        )
    }

    private var statsGrid: some View {
        HStack(spacing: 10) {
            StatCard(icon: "eye.fill", title: "오늘 방문", value: "\(todayVisits)", tint: .cyan, cardBg: cardBg, textPrimary: textPrimary, textSecondary: textSecondary)
            StatCard(icon: "person.2.fill", title: "누적 방문", value: "\(totalVisits)", tint: .purple, cardBg: cardBg, textPrimary: textPrimary, textSecondary: textSecondary)
            StatCard(icon: "book.closed.fill", title: "꽂힌 책", value: "\(bookCount)", tint: .orange, cardBg: cardBg, textPrimary: textPrimary, textSecondary: textSecondary)
        }
    }
}

private struct StatCard: View {
    let icon: String
    let title: String
    let value: String
    let tint: Color
    let cardBg: Color
    let textPrimary: Color
    let textSecondary: Color

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            Image(systemName: icon)
                .font(.system(size: 15, weight: .bold))
                .foregroundStyle(tint)
            Text(value)
                .font(.system(size: 24, weight: .black))
                .foregroundStyle(textPrimary)
            Text(title)
                .font(.system(size: 11, weight: .medium))
                .foregroundStyle(textSecondary)
        }
        .padding(14)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(
            RoundedRectangle(cornerRadius: 12)
                .fill(cardBg)
        )
        .overlay(
            RoundedRectangle(cornerRadius: 12)
                .strokeBorder(tint.opacity(0.3), lineWidth: 1)
        )
    }
}

private struct GuestEntryRow: View {
    let entry: GuestEntry
    let cardBg: Color
    let textPrimary: Color
    let textSecondary: Color

    var body: some View {
        HStack(spacing: 12) {
            Circle()
                .fill(entry.color.gradient)
                .overlay(
                    Text(entry.initials)
                        .font(.system(size: 14, weight: .black))
                        .foregroundStyle(.white)
                )
                .frame(width: 38, height: 38)

            VStack(alignment: .leading, spacing: 3) {
                HStack(spacing: 6) {
                    Text(entry.name)
                        .font(.system(size: 15, weight: .bold))
                        .foregroundStyle(textPrimary)
                    Text("·")
                        .foregroundStyle(textSecondary)
                    Text(entry.time)
                        .font(.system(size: 12, weight: .medium))
                        .foregroundStyle(textSecondary)
                    Spacer()
                    Text(entry.emoji)
                        .font(.system(size: 16))
                }
                Text(entry.message)
                    .font(.system(size: 13, weight: .medium))
                    .foregroundStyle(textPrimary.opacity(0.85))
            }
        }
        .padding(12)
        .background(
            RoundedRectangle(cornerRadius: 12)
                .fill(cardBg)
        )
        .overlay(
            RoundedRectangle(cornerRadius: 12)
                .strokeBorder(.white.opacity(0.06), lineWidth: 1)
        )
    }
}
