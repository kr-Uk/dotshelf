import SwiftUI

/// 화이트보드 표면에 텍스처로 렌더링되는 대시보드.
/// 다크 모드 — 어두운 bg에 밝은 요소가 발광하듯 떠 있는 디스플레이 룩.
/// element 별 pixel rect는 DashboardLayout이 관리 (3D hit region과 공유).
struct WhiteboardDashboard: View {
    let bookCount: Int
    let likeCount: Int
    let isLiked: Bool
    let bioText: String
    let entries: [GuestbookEntry]
    let visibility: GuestbookVisibility
    let followerCount: Int
    let followingCount: Int

    // 다크 디스플레이 팔레트
    private let bgColor = Color(red: 0.08, green: 0.09, blue: 0.12)
    private let cardBg = Color(red: 0.15, green: 0.17, blue: 0.22)
    private let textPrimary = Color(red: 0.95, green: 0.96, blue: 0.98)
    private let textSecondary = Color(red: 0.62, green: 0.66, blue: 0.73)

    var body: some View {
        ZStack(alignment: .topLeading) {
            bgColor

            placedRect(DashboardLayout.bioRect) { bio }
            placedRect(DashboardLayout.likeRect) { likeCard }
            placedRect(DashboardLayout.booksStatRect) {
                StatCard(icon: "book.closed.fill", title: "꽂힌 책",
                         value: "\(bookCount)", tint: .orange,
                         cardBg: cardBg, textPrimary: textPrimary, textSecondary: textSecondary)
            }
            placedRect(DashboardLayout.followersStatRect) {
                StatCard(icon: "person.2.fill", title: "팔로워",
                         value: "\(followerCount)", tint: .purple,
                         cardBg: cardBg, textPrimary: textPrimary, textSecondary: textSecondary,
                         hintText: "탭해서 목록 보기")
            }
            placedRect(DashboardLayout.followingStatRect) {
                StatCard(icon: "person.crop.circle.badge.checkmark", title: "팔로잉",
                         value: "\(followingCount)", tint: .indigo,
                         cardBg: cardBg, textPrimary: textPrimary, textSecondary: textSecondary,
                         hintText: "탭해서 목록 보기")
            }

            placedRect(DashboardLayout.guestbookHeaderRect) { guestbookHeader }

            if visibility == .closed {
                let noticeRect = CGRect(
                    x: DashboardLayout.rightX,
                    y: DashboardLayout.guestbookListY,
                    width: DashboardLayout.rightWidth,
                    height: DashboardLayout.guestbookCardHeight
                )
                placedRect(noticeRect) { privateNotice }
            } else {
                let visibleEntries = Array(entries.prefix(DashboardLayout.guestbookMaxCards))
                ForEach(Array(visibleEntries.enumerated()), id: \.element.id) { (item: (offset: Int, element: GuestbookEntry)) in
                    placedRect(DashboardLayout.guestbookCardRect(item.offset)) {
                        GuestEntryRow(entry: item.element,
                                      cardBg: cardBg,
                                      textPrimary: textPrimary,
                                      textSecondary: textSecondary)
                    }
                }
                if visibleEntries.isEmpty {
                    let emptyRect = CGRect(
                        x: DashboardLayout.rightX,
                        y: DashboardLayout.guestbookListY,
                        width: DashboardLayout.rightWidth,
                        height: DashboardLayout.guestbookCardHeight
                    )
                    placedRect(emptyRect) { emptyGuestbookNotice }
                }
            }
        }
        .frame(width: DashboardLayout.canvasWidth, height: DashboardLayout.canvasHeight)
    }

    @ViewBuilder
    private func placedRect<Content: View>(_ rect: CGRect, @ViewBuilder _ content: () -> Content) -> some View {
        content()
            .frame(width: rect.width, height: rect.height)
            .offset(x: rect.minX, y: rect.minY)
    }

    // MARK: - Cards

    private var bio: some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack(spacing: 8) {
                Text("📝").font(.system(size: 28))
                Text("Dotshelf 서재")
                    .font(.system(size: 30, weight: .black))
                    .foregroundStyle(textPrimary)
            }
            Text(bioText)
                .font(.system(size: 14, weight: .medium))
                .foregroundStyle(textSecondary)
                .lineLimit(2)
                .multilineTextAlignment(.leading)
            Text("탭해서 프로필 편집")
                .font(.system(size: 10, weight: .medium))
                .foregroundStyle(textSecondary.opacity(0.7))
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    private var likeCard: some View {
        HStack(spacing: 14) {
            Image(systemName: isLiked ? "heart.fill" : "heart")
                .font(.system(size: 28, weight: .bold))
                .foregroundStyle(isLiked ? Color.red : textSecondary)
            VStack(alignment: .leading, spacing: 2) {
                Text("\(likeCount.formatted())")
                    .font(.system(size: 26, weight: .black))
                    .foregroundStyle(textPrimary)
                Text(isLiked ? "탭해서 좋아요 취소" : "탭해서 이 서재에 좋아요")
                    .font(.system(size: 11, weight: .medium))
                    .foregroundStyle(textSecondary)
            }
            Spacer()
        }
        .padding(16)
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .leading)
        .background(
            RoundedRectangle(cornerRadius: 14)
                .fill(isLiked ? Color.red.opacity(0.18) : cardBg)
        )
        .overlay(
            RoundedRectangle(cornerRadius: 14)
                .strokeBorder(isLiked ? Color.red.opacity(0.5) : .white.opacity(0.08), lineWidth: 1)
        )
    }

    private var guestbookHeader: some View {
        HStack(alignment: .center, spacing: 8) {
            Text("방명록")
                .font(.system(size: 20, weight: .bold))
                .foregroundStyle(textPrimary)
            Text("\(entries.count)건")
                .font(.system(size: 13, weight: .medium))
                .foregroundStyle(textSecondary)
            Spacer()
            HStack(spacing: 4) {
                Image(systemName: visibility.systemImage)
                    .font(.system(size: 10, weight: .semibold))
                Text(visibility.label)
                    .font(.system(size: 11, weight: .medium))
            }
            .foregroundStyle(textSecondary)
            .padding(.horizontal, 8)
            .padding(.vertical, 4)
            .background(Capsule().fill(.white.opacity(0.06)))

            HStack(spacing: 4) {
                Image(systemName: "list.bullet.rectangle")
                    .font(.system(size: 11, weight: .bold))
                Text("목록 보기")
                    .font(.system(size: 11, weight: .bold))
            }
            .foregroundStyle(Color.white)
            .padding(.horizontal, 10)
            .padding(.vertical, 5)
            .background(Capsule().fill(Color.accentColor))
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    private var privateNotice: some View {
        HStack(spacing: 10) {
            Image(systemName: "lock.fill")
                .font(.system(size: 13, weight: .semibold))
            Text("비공개로 설정된 방명록이에요")
                .font(.system(size: 12, weight: .medium))
            Spacer()
        }
        .foregroundStyle(textSecondary)
        .padding(12)
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .leading)
        .background(RoundedRectangle(cornerRadius: 12).fill(cardBg))
    }

    private var emptyGuestbookNotice: some View {
        HStack(spacing: 10) {
            Image(systemName: "text.bubble")
                .font(.system(size: 13, weight: .semibold))
            Text("첫 번째 방명록을 남겨보세요")
                .font(.system(size: 12, weight: .medium))
            Spacer()
        }
        .foregroundStyle(textSecondary)
        .padding(12)
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .leading)
        .background(RoundedRectangle(cornerRadius: 12).fill(cardBg))
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
    var hintText: String? = nil

    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            Image(systemName: icon)
                .font(.system(size: 15, weight: .bold))
                .foregroundStyle(tint)
            Text(value)
                .font(.system(size: 24, weight: .black))
                .foregroundStyle(textPrimary)
            Text(title)
                .font(.system(size: 11, weight: .medium))
                .foregroundStyle(textSecondary)
            if let hintText {
                Text(hintText)
                    .font(.system(size: 9, weight: .medium))
                    .foregroundStyle(textSecondary.opacity(0.7))
            }
        }
        .padding(12)
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
        .background(RoundedRectangle(cornerRadius: 12).fill(cardBg))
        .overlay(
            RoundedRectangle(cornerRadius: 12)
                .strokeBorder(tint.opacity(0.3), lineWidth: 1)
        )
    }
}

private struct GuestEntryRow: View {
    let entry: GuestbookEntry
    let cardBg: Color
    let textPrimary: Color
    let textSecondary: Color

    var body: some View {
        HStack(spacing: 10) {
            Circle()
                .fill(entry.avatarColor.gradient)
                .overlay(
                    Text(entry.initials)
                        .font(.system(size: 13, weight: .black))
                        .foregroundStyle(.white)
                )
                .frame(width: 34, height: 34)

            VStack(alignment: .leading, spacing: 2) {
                HStack(spacing: 5) {
                    Text(entry.authorName)
                        .font(.system(size: 13, weight: .bold))
                        .foregroundStyle(textPrimary)
                    Text("·").foregroundStyle(textSecondary)
                    Text(entry.time)
                        .font(.system(size: 11, weight: .medium))
                        .foregroundStyle(textSecondary)
                    if !entry.isPublic {
                        Image(systemName: "lock.fill")
                            .font(.system(size: 8, weight: .semibold))
                            .foregroundStyle(textSecondary)
                    }
                    Spacer()
                    Text(entry.emoji).font(.system(size: 15))
                }
                Text(entry.message)
                    .font(.system(size: 12, weight: .medium))
                    .foregroundStyle(textPrimary.opacity(0.85))
                    .lineLimit(entry.replies.isEmpty ? 2 : 1)
                if let reply = entry.replies.first {
                    HStack(spacing: 4) {
                        Image(systemName: "arrow.turn.down.right")
                            .font(.system(size: 9, weight: .semibold))
                            .foregroundStyle(textSecondary)
                        Text(reply.authorName)
                            .font(.system(size: 10, weight: .bold))
                            .foregroundStyle(textPrimary)
                        Text(reply.message)
                            .font(.system(size: 10, weight: .medium))
                            .foregroundStyle(textPrimary.opacity(0.75))
                            .lineLimit(1)
                    }
                }
            }
        }
        .padding(10)
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .leading)
        .background(RoundedRectangle(cornerRadius: 12).fill(entry.style.tint.opacity(0.18)))
        .overlay(
            RoundedRectangle(cornerRadius: 12)
                .strokeBorder(entry.style.tint.opacity(0.5), lineWidth: 1)
        )
    }
}
