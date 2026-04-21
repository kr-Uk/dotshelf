import SwiftUI

/// 팔로워 / 팔로잉 탭에서 공유하는 유저 카드 리스트.
/// - 명세 §4: 유저 카드 탭 → '방문하기' 버튼, 공간 주인 전용 차단/팔로우 해제.
enum FollowListMode {
    case followers, following

    var emptyLabel: String {
        switch self {
        case .followers: return "아직 팔로워가 없어요"
        case .following: return "아직 팔로잉 중인 공간이 없어요"
        }
    }
}

struct FollowListView: View {
    let store: GuestbookStore
    let mode: FollowListMode

    /// 공간 주인 여부. 차단·팔로우 해제 버튼 표시 여부 결정.
    let isOwner: Bool
    var onVisit: (FollowUser) -> Void = { _ in }

    private var users: [FollowUser] {
        switch mode {
        case .followers: return store.visibleFollowers
        case .following: return store.visibleFollowing
        }
    }

    var body: some View {
        if users.isEmpty {
            emptyState
        } else {
            ScrollView(.vertical, showsIndicators: false) {
                LazyVStack(spacing: 8) {
                    ForEach(users) { user in
                        FollowUserCard(
                            user: user,
                            mode: mode,
                            isOwner: isOwner,
                            onVisit: { onVisit(user) },
                            onUnfollow: { store.unfollow(user.id) },
                            onRemoveFollower: { store.removeFollower(user.id) },
                            onBlock: { store.block(user) }
                        )
                    }
                }
                .padding(.horizontal, 18)
                .padding(.vertical, 14)
            }
        }
    }

    private var emptyState: some View {
        VStack(spacing: 8) {
            Image(systemName: "person.crop.circle.badge.questionmark")
                .font(.system(size: 32))
                .foregroundStyle(.secondary)
            Text(mode.emptyLabel)
                .font(.system(size: 13, weight: .medium))
                .foregroundStyle(.secondary)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
}

private struct FollowUserCard: View {
    let user: FollowUser
    let mode: FollowListMode
    let isOwner: Bool
    var onVisit: () -> Void
    var onUnfollow: () -> Void
    var onRemoveFollower: () -> Void
    var onBlock: () -> Void

    var body: some View {
        HStack(alignment: .center, spacing: 12) {
            Circle()
                .fill(user.avatarColor.gradient)
                .overlay(
                    Text(user.initials)
                        .font(.system(size: 14, weight: .black))
                        .foregroundStyle(.white)
                )
                .frame(width: 40, height: 40)

            VStack(alignment: .leading, spacing: 2) {
                HStack(spacing: 6) {
                    Text(user.name)
                        .font(.system(size: 14, weight: .bold))
                    if user.isMutual {
                        Text("맞팔")
                            .font(.system(size: 9, weight: .bold))
                            .padding(.horizontal, 6)
                            .padding(.vertical, 2)
                            .background(Color.accentColor.opacity(0.2), in: Capsule())
                            .foregroundStyle(.tint)
                    }
                }
                Text(user.bioSnippet)
                    .font(.system(size: 11))
                    .foregroundStyle(.secondary)
                    .lineLimit(1)
            }

            Spacer()

            actions
        }
        .padding(.vertical, 8)
        .padding(.horizontal, 10)
        .background(
            RoundedRectangle(cornerRadius: 10)
                .fill(.white.opacity(0.04))
        )
    }

    @ViewBuilder
    private var actions: some View {
        HStack(spacing: 6) {
            Button(action: onVisit) {
                Text("방문하기")
                    .font(.system(size: 11, weight: .semibold))
                    .padding(.horizontal, 10)
                    .padding(.vertical, 6)
                    .background(.ultraThinMaterial, in: Capsule())
            }
            .buttonStyle(.plain)

            if isOwner {
                Menu {
                    switch mode {
                    case .following:
                        Button(role: .destructive, action: onUnfollow) {
                            Label("팔로우 해제", systemImage: "person.fill.xmark")
                        }
                    case .followers:
                        Button(role: .destructive, action: onBlock) {
                            Label("차단", systemImage: "nosign")
                        }
                        Button(action: onRemoveFollower) {
                            Label("팔로워에서 제거", systemImage: "person.crop.circle.badge.minus")
                        }
                    }
                } label: {
                    Image(systemName: "ellipsis")
                        .font(.system(size: 13, weight: .bold))
                        .padding(6)
                        .background(.ultraThinMaterial, in: Circle())
                }
                .menuStyle(.borderlessButton)
                .fixedSize()
            }
        }
    }
}
