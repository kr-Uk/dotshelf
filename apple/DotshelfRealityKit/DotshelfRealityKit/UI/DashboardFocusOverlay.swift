import SwiftUI

/// 화이트보드(대시보드) 탭 시 뜨는 풀스크린 2D 상세 오버레이.
/// - 명세 §4: 방명록 목록, 팔로워/팔로잉 목록, 통계 요약, 방명록 작성 / 답장 / 프로필 편집 진입.
/// - 3D 화이트보드 뷰를 흐린 배경으로 유지하고 그 위에 큼직한 2D 시트를 띄운다.
struct DashboardFocusOverlay: View {
    enum Tab: String, CaseIterable, Identifiable, Hashable {
        case summary, guestbook, followers, following

        var id: String { rawValue }
        var label: String {
            switch self {
            case .summary:   return "요약"
            case .guestbook: return "방명록"
            case .followers: return "팔로워"
            case .following: return "팔로잉"
            }
        }
        var systemImage: String {
            switch self {
            case .summary:   return "chart.bar.fill"
            case .guestbook: return "text.bubble.fill"
            case .followers: return "person.2.fill"
            case .following: return "person.crop.circle.badge.checkmark"
            }
        }
    }

    let store: GuestbookStore
    let totalBookCount: Int
    let likeCount: Int
    let isLiked: Bool

    var onClose: () -> Void
    var onOpenComposer: () -> Void
    var onOpenBioEditor: () -> Void
    var onReply: (GuestbookEntry) -> Void
    var onToggleLike: () -> Void
    var onVisitUser: (FollowUser) -> Void = { _ in }
    var onReport: (ReportTarget) -> Void = { _ in }

    var initialTab: Tab = .summary

    @State private var tab: Tab

    init(store: GuestbookStore,
         totalBookCount: Int,
         likeCount: Int,
         isLiked: Bool,
         initialTab: Tab = .summary,
         onClose: @escaping () -> Void,
         onOpenComposer: @escaping () -> Void,
         onOpenBioEditor: @escaping () -> Void,
         onReply: @escaping (GuestbookEntry) -> Void,
         onToggleLike: @escaping () -> Void,
         onVisitUser: @escaping (FollowUser) -> Void = { _ in },
         onReport: @escaping (ReportTarget) -> Void = { _ in }) {
        self.store = store
        self.totalBookCount = totalBookCount
        self.likeCount = likeCount
        self.isLiked = isLiked
        self.initialTab = initialTab
        self.onClose = onClose
        self.onOpenComposer = onOpenComposer
        self.onOpenBioEditor = onOpenBioEditor
        self.onReply = onReply
        self.onToggleLike = onToggleLike
        self.onVisitUser = onVisitUser
        self.onReport = onReport
        _tab = State(initialValue: initialTab)
    }

    var body: some View {
        ZStack {
            Color.black.opacity(0.35).ignoresSafeArea()
                .onTapGesture { onClose() }
            sheet
        }
    }

    /// 개별 요소에서 진입한 경우(.followers / .following / .guestbook) tabBar를 감춰
        /// 해당 탭만 노출. `.summary`(booksName 등 요약 영역 hit)는 기존대로 전체 탭 노출.
    private var isLockedToInitialTab: Bool { initialTab != .summary }

    private var sheet: some View {
        VStack(spacing: 0) {
            header
            Divider().opacity(0.3)
            if !isLockedToInitialTab {
                tabBar
                Divider().opacity(0.3)
            }
            content
        }
        .frame(maxWidth: 680, maxHeight: 620)
        .background(
            RoundedRectangle(cornerRadius: 22)
                .fill(.regularMaterial)
        )
        .overlay(
            RoundedRectangle(cornerRadius: 22)
                .strokeBorder(.white.opacity(0.1), lineWidth: 1)
        )
        .shadow(color: .black.opacity(0.35), radius: 22, y: 10)
        .padding(20)
    }

    private var header: some View {
        HStack(alignment: .center, spacing: 12) {
            Image(systemName: isLockedToInitialTab ? tab.systemImage : "square.grid.2x2.fill")
                .font(.system(size: 16, weight: .bold))
                .foregroundStyle(.tint)
            Text(isLockedToInitialTab ? tab.label : "대시보드")
                .font(.system(size: 17, weight: .bold))
            Spacer()
            Button(action: onOpenBioEditor) {
                Label("편집", systemImage: "pencil")
                    .font(.system(size: 12, weight: .semibold))
                    .padding(.horizontal, 10)
                    .padding(.vertical, 6)
                    .background(.ultraThinMaterial, in: Capsule())
            }
            .buttonStyle(.plain)
            Button(action: onClose) {
                Image(systemName: "xmark.circle.fill")
                    .font(.system(size: 20))
                    .foregroundStyle(.secondary, .quaternary)
            }
            .buttonStyle(.plain)
        }
        .padding(.horizontal, 20)
        .padding(.vertical, 14)
    }

    private var tabBar: some View {
        HStack(spacing: 6) {
            ForEach(Tab.allCases) { (t: Tab) in
                tabButton(for: t)
            }
        }
        .padding(.horizontal, 14)
        .padding(.vertical, 10)
    }

    @ViewBuilder
    private func tabButton(for t: Tab) -> some View {
        let isActive = tab == t
        let badge: String? = {
            switch t {
            case .guestbook: return "\(store.entries.count)"
            case .followers: return "\(store.visibleFollowers.count)"
            case .following: return "\(store.visibleFollowing.count)"
            default: return nil
            }
        }()
        Button(action: { tab = t }) {
            HStack(spacing: 6) {
                Image(systemName: t.systemImage).font(.system(size: 11, weight: .semibold))
                Text(t.label).font(.system(size: 12, weight: .semibold))
                if let badge {
                    Text(badge)
                        .font(.system(size: 10, weight: .bold))
                        .padding(.horizontal, 6)
                        .padding(.vertical, 2)
                        .background(.ultraThinMaterial, in: Capsule())
                }
            }
            .padding(.horizontal, 12)
            .padding(.vertical, 7)
            .background(
                Capsule().fill(isActive ? Color.accentColor.opacity(0.2) : Color.clear)
            )
            .foregroundStyle(isActive ? Color.accentColor : Color.primary)
        }
        .buttonStyle(.plain)
    }

    @ViewBuilder
    private var content: some View {
        switch tab {
        case .summary:   summaryTab
        case .guestbook: guestbookTab
        case .followers: FollowListView(store: store, mode: .followers, isOwner: true, onVisit: onVisitUser)
        case .following: FollowListView(store: store, mode: .following, isOwner: true, onVisit: onVisitUser)
        }
    }

    // MARK: - Summary tab

    private var summaryTab: some View {
        ScrollView(.vertical, showsIndicators: false) {
            VStack(alignment: .leading, spacing: 14) {
                bioCard
                statsGrid
                visibilityCard
            }
            .padding(20)
        }
    }

    private var bioCard: some View {
        HStack(alignment: .top, spacing: 12) {
            Image(systemName: "person.crop.circle.fill")
                .font(.system(size: 32))
                .foregroundStyle(.tint)
            VStack(alignment: .leading, spacing: 4) {
                Text("한 줄 소개")
                    .font(.system(size: 11, weight: .semibold))
                    .foregroundStyle(.secondary)
                Text(store.bioText)
                    .font(.system(size: 14, weight: .medium))
                    .lineLimit(3)
            }
            Spacer()
            Button(action: onOpenBioEditor) {
                Image(systemName: "pencil")
                    .font(.system(size: 12, weight: .bold))
                    .padding(8)
                    .background(.ultraThinMaterial, in: Circle())
            }
            .buttonStyle(.plain)
        }
        .padding(14)
        .background(
            RoundedRectangle(cornerRadius: 12)
                .fill(.white.opacity(0.04))
        )
    }

    private var statsGrid: some View {
        LazyVGrid(columns: [GridItem(.flexible()), GridItem(.flexible()), GridItem(.flexible())], spacing: 10) {
            summaryTile(icon: "book.closed.fill", label: "책", value: "\(totalBookCount)", tint: .orange)
            summaryTile(icon: "heart.fill", label: "공간 좋아요", value: "\(likeCount)", tint: isLiked ? .red : .pink)
            summaryTile(icon: "text.bubble.fill", label: "방명록", value: "\(store.entries.count)", tint: .cyan)
            summaryTile(icon: "person.2.fill", label: "팔로워", value: "\(store.visibleFollowers.count)", tint: .purple)
            summaryTile(icon: "person.crop.circle.badge.checkmark", label: "팔로잉", value: "\(store.visibleFollowing.count)", tint: .indigo)
            Button(action: onToggleLike) {
                VStack(alignment: .leading, spacing: 6) {
                    Image(systemName: isLiked ? "heart.slash.fill" : "heart.fill")
                        .font(.system(size: 16, weight: .bold))
                        .foregroundStyle(isLiked ? .gray : .red)
                    Text(isLiked ? "좋아요 취소" : "좋아요 누르기")
                        .font(.system(size: 12, weight: .semibold))
                    Text("공간 자체에 대한 반응")
                        .font(.system(size: 10))
                        .foregroundStyle(.secondary)
                }
                .padding(12)
                .frame(maxWidth: .infinity, alignment: .leading)
                .background(
                    RoundedRectangle(cornerRadius: 10)
                        .fill(.white.opacity(0.04))
                )
            }
            .buttonStyle(.plain)
        }
    }

    private func summaryTile(icon: String, label: String, value: String, tint: Color) -> some View {
        VStack(alignment: .leading, spacing: 6) {
            Image(systemName: icon)
                .font(.system(size: 14, weight: .bold))
                .foregroundStyle(tint)
            Text(value)
                .font(.system(size: 20, weight: .black))
            Text(label)
                .font(.system(size: 11, weight: .medium))
                .foregroundStyle(.secondary)
        }
        .padding(12)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(
            RoundedRectangle(cornerRadius: 10)
                .fill(.white.opacity(0.04))
        )
    }

    private var visibilityCard: some View {
        HStack(spacing: 10) {
            Image(systemName: store.listVisibility.systemImage)
                .font(.system(size: 14, weight: .bold))
                .foregroundStyle(.tint)
            VStack(alignment: .leading, spacing: 2) {
                Text("방명록 공개 범위")
                    .font(.system(size: 11, weight: .semibold))
                    .foregroundStyle(.secondary)
                Text(store.listVisibility.label)
                    .font(.system(size: 13, weight: .bold))
            }
            Spacer()
            Button(action: onOpenBioEditor) {
                Text("변경")
                    .font(.system(size: 12, weight: .semibold))
                    .padding(.horizontal, 12)
                    .padding(.vertical, 6)
                    .background(.ultraThinMaterial, in: Capsule())
            }
            .buttonStyle(.plain)
        }
        .padding(12)
        .background(
            RoundedRectangle(cornerRadius: 10)
                .fill(.white.opacity(0.04))
        )
    }

    // MARK: - Guestbook tab

    private var guestbookTab: some View {
        VStack(spacing: 0) {
            HStack {
                Text("방명록 전체")
                    .font(.system(size: 13, weight: .bold))
                    .foregroundStyle(.secondary)
                Spacer()
                Button(action: onOpenComposer) {
                    Label("남기기", systemImage: "square.and.pencil")
                        .font(.system(size: 12, weight: .semibold))
                        .padding(.horizontal, 12)
                        .padding(.vertical, 7)
                        .background(Color.accentColor, in: Capsule())
                        .foregroundStyle(.white)
                }
                .buttonStyle(.plain)
            }
            .padding(.horizontal, 20)
            .padding(.vertical, 10)

            if store.listVisibility == .closed {
                VStack(spacing: 6) {
                    Image(systemName: "lock.fill")
                        .font(.system(size: 28))
                        .foregroundStyle(.secondary)
                    Text("방명록이 비공개로 설정되어 있어요")
                        .font(.system(size: 13, weight: .medium))
                        .foregroundStyle(.secondary)
                    Text("공개 범위를 변경하려면 프로필 편집을 열어주세요")
                        .font(.system(size: 11))
                        .foregroundStyle(.secondary.opacity(0.8))
                }
                .frame(maxWidth: .infinity, maxHeight: .infinity)
            } else if store.entries.isEmpty {
                VStack(spacing: 6) {
                    Image(systemName: "text.bubble")
                        .font(.system(size: 28))
                        .foregroundStyle(.secondary)
                    Text("첫 번째 방명록을 남겨보세요")
                        .font(.system(size: 13, weight: .medium))
                        .foregroundStyle(.secondary)
                }
                .frame(maxWidth: .infinity, maxHeight: .infinity)
            } else {
                ScrollView(.vertical, showsIndicators: false) {
                    LazyVStack(spacing: 10) {
                        ForEach(store.entries) { entry in
                            GuestbookDetailRow(
                                entry: entry,
                                onReply: { onReply(entry) },
                                onDelete: { store.removeEntry(id: entry.id) },
                                onReport: {
                                    onReport(ReportTarget(kind: .guestbook, displayText: entry.message))
                                }
                            )
                        }
                    }
                    .padding(.horizontal, 18)
                    .padding(.bottom, 14)
                }
            }
        }
    }
}

private struct GuestbookDetailRow: View {
    let entry: GuestbookEntry
    var onReply: () -> Void
    var onDelete: () -> Void
    var onReport: () -> Void = {}

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack(alignment: .top, spacing: 10) {
                Circle()
                    .fill(entry.avatarColor.gradient)
                    .overlay(
                        Text(entry.initials)
                            .font(.system(size: 13, weight: .black))
                            .foregroundStyle(.white)
                    )
                    .frame(width: 36, height: 36)
                VStack(alignment: .leading, spacing: 2) {
                    HStack(spacing: 6) {
                        Text(entry.authorName)
                            .font(.system(size: 13, weight: .bold))
                        Text(entry.time)
                            .font(.system(size: 11))
                            .foregroundStyle(.secondary)
                        if !entry.isPublic {
                            Image(systemName: "lock.fill")
                                .font(.system(size: 10))
                                .foregroundStyle(.secondary)
                        }
                        Spacer()
                        Text(entry.emoji)
                            .font(.system(size: 14))
                    }
                    Text(entry.message)
                        .font(.system(size: 13, weight: .medium))
                }
                Spacer(minLength: 0)
                Menu {
                    Button(action: onReply) { Label("답장", systemImage: "arrowshape.turn.up.left") }
                    Button(role: .destructive, action: onReport) { Label("신고", systemImage: "exclamationmark.triangle") }
                    Button(role: .destructive, action: onDelete) { Label("삭제", systemImage: "trash") }
                } label: {
                    Image(systemName: "ellipsis")
                        .font(.system(size: 13, weight: .bold))
                        .padding(6)
                        .background(.ultraThinMaterial, in: Circle())
                }
                .menuStyle(.borderlessButton)
                .fixedSize()
            }

            if !entry.replies.isEmpty {
                VStack(alignment: .leading, spacing: 4) {
                    ForEach(entry.replies) { reply in
                        HStack(alignment: .top, spacing: 6) {
                            Image(systemName: "arrow.turn.down.right")
                                .font(.system(size: 10))
                                .foregroundStyle(.secondary)
                            Text(reply.authorName)
                                .font(.system(size: 11, weight: .bold))
                            if !reply.isPublic {
                                Image(systemName: "lock.fill")
                                    .font(.system(size: 9))
                                    .foregroundStyle(.secondary)
                            }
                            Text(reply.message)
                                .font(.system(size: 11))
                                .foregroundStyle(.primary.opacity(0.85))
                        }
                    }
                }
                .padding(.leading, 46)
            }

            HStack {
                Spacer()
                Button(action: onReply) {
                    Label("답장", systemImage: "arrowshape.turn.up.left")
                        .font(.system(size: 11, weight: .semibold))
                        .padding(.horizontal, 10)
                        .padding(.vertical, 5)
                        .background(.ultraThinMaterial, in: Capsule())
                }
                .buttonStyle(.plain)
            }
        }
        .padding(12)
        .background(
            RoundedRectangle(cornerRadius: 12)
                .fill(entry.style.tint.opacity(0.18))
        )
        .overlay(
            RoundedRectangle(cornerRadius: 12)
                .strokeBorder(entry.style.tint.opacity(0.35), lineWidth: 1)
        )
    }
}
