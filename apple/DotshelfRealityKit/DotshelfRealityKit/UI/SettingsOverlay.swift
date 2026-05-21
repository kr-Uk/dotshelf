import SwiftUI

/// §9 설정 화면 2D 풀 오버레이.
/// 좌측: 섹션 리스트 / 우측: 섹션별 상세.
struct SettingsOverlay: View {
    enum Section: String, CaseIterable, Identifiable, Hashable {
        case account, payment, notifications, feed, moderation, legal

        var id: String { rawValue }
        var label: String {
            switch self {
            case .account:       return "계정"
            case .payment:       return "결제"
            case .notifications: return "알림"
            case .feed:          return "피드 설정"
            case .moderation:    return "신고·차단 내역"
            case .legal:         return "약관·정책"
            }
        }
        var systemImage: String {
            switch self {
            case .account:       return "person.crop.circle"
            case .payment:       return "creditcard.fill"
            case .notifications: return "bell.fill"
            case .feed:          return "list.bullet.rectangle"
            case .moderation:    return "exclamationmark.shield.fill"
            case .legal:         return "doc.text.fill"
            }
        }
    }

    let settings: SettingsStore
    let guestbook: GuestbookStore
    var onClose: () -> Void
    var onLogout: () -> Void = {}
    var onLogin: () -> Void = {}
    var onWithdraw: () -> Void = {}

    @State private var section: Section = .account

    var body: some View {
        ZStack {
            Color.black.opacity(0.4).ignoresSafeArea()
                .onTapGesture { onClose() }
            sheet
        }
    }

    private var sheet: some View {
        HStack(spacing: 0) {
            sidebar
            Divider().opacity(0.3)
            detail
        }
        .frame(maxWidth: 820, maxHeight: 620)
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

    // MARK: - Sidebar

    private var sidebar: some View {
        VStack(alignment: .leading, spacing: 4) {
            HStack {
                Image(systemName: "gearshape.fill")
                    .font(.system(size: 16, weight: .bold))
                    .foregroundStyle(.tint)
                Text("설정")
                    .font(.system(size: 17, weight: .bold))
                Spacer()
                Button(action: onClose) {
                    Image(systemName: "xmark.circle.fill")
                        .font(.system(size: 18))
                        .foregroundStyle(.secondary, .quaternary)
                }
                .buttonStyle(.plain)
            }
            .padding(.horizontal, 14)
            .padding(.vertical, 16)

            ForEach(Section.allCases) { (s: Section) in
                sidebarRow(for: s)
            }
            Spacer()
        }
        .frame(width: 220)
    }

    @ViewBuilder
    private func sidebarRow(for s: Section) -> some View {
        let isActive = section == s
        Button(action: { section = s }) {
            HStack(spacing: 10) {
                Image(systemName: s.systemImage)
                    .font(.system(size: 13, weight: .semibold))
                    .frame(width: 18)
                Text(s.label)
                    .font(.system(size: 13, weight: isActive ? .bold : .medium))
                Spacer()
            }
            .foregroundStyle(isActive ? Color.accentColor : Color.primary)
            .padding(.horizontal, 12)
            .padding(.vertical, 9)
            .background(
                RoundedRectangle(cornerRadius: 8)
                    .fill(isActive ? Color.accentColor.opacity(0.15) : Color.clear)
            )
        }
        .buttonStyle(.plain)
        .padding(.horizontal, 10)
    }

    // MARK: - Detail

    @ViewBuilder
    private var detail: some View {
        ScrollView(.vertical, showsIndicators: false) {
            VStack(alignment: .leading, spacing: 16) {
                Text(section.label)
                    .font(.system(size: 20, weight: .bold))

                switch section {
                case .account:       accountDetail
                case .payment:       paymentDetail
                case .notifications: notificationsDetail
                case .feed:          feedDetail
                case .moderation:    moderationDetail
                case .legal:         legalDetail
                }
            }
            .padding(24)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
    }

    // MARK: - Account

    private var accountDetail: some View {
        VStack(alignment: .leading, spacing: 14) {
            profileCard
            visibilityCard
            sessionButtons
        }
    }

    private var profileCard: some View {
        HStack(spacing: 14) {
            Image(systemName: "person.crop.circle.fill")
                .font(.system(size: 40))
                .foregroundStyle(.tint)
            VStack(alignment: .leading, spacing: 2) {
                Text(settings.displayName)
                    .font(.system(size: 15, weight: .bold))
                Text(settings.email)
                    .font(.system(size: 12))
                    .foregroundStyle(.secondary)
                Text(settings.isLoggedIn ? "로그인됨" : "게스트")
                    .font(.system(size: 10, weight: .semibold))
                    .padding(.horizontal, 6)
                    .padding(.vertical, 2)
                    .background(
                        Capsule().fill(settings.isLoggedIn ? Color.accentColor.opacity(0.2) : .secondary.opacity(0.15))
                    )
                    .foregroundStyle(settings.isLoggedIn ? Color.accentColor : .secondary)
            }
            Spacer()
            Button(action: {}) {
                Text("프로필 편집")
                    .font(.system(size: 12, weight: .semibold))
                    .padding(.horizontal, 12)
                    .padding(.vertical, 6)
                    .background(.ultraThinMaterial, in: Capsule())
            }
            .buttonStyle(.plain)
        }
        .padding(14)
        .background(RoundedRectangle(cornerRadius: 12).fill(.white.opacity(0.04)))
    }

    private var visibilityCard: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text("공간 공개 범위")
                .font(.system(size: 12, weight: .semibold))
                .foregroundStyle(.secondary)
            VStack(spacing: 6) {
                ForEach(GuestbookVisibility.allCases) { (v: GuestbookVisibility) in
                    visibilityRow(for: v)
                }
            }
            Text("공간이 비공개면 책장도 모두 비공개로 처리돼요")
                .font(.system(size: 10))
                .foregroundStyle(.secondary.opacity(0.8))
        }
        .padding(14)
        .background(RoundedRectangle(cornerRadius: 12).fill(.white.opacity(0.04)))
    }

    @ViewBuilder
    private func visibilityRow(for v: GuestbookVisibility) -> some View {
        let isSelected = settings.spaceVisibility == v
        Button(action: { settings.spaceVisibility = v }) {
            HStack(spacing: 10) {
                Image(systemName: v.systemImage).font(.system(size: 12, weight: .semibold))
                    .frame(width: 16)
                Text(v.label).font(.system(size: 13, weight: .medium))
                Spacer()
                if isSelected {
                    Image(systemName: "checkmark")
                        .font(.system(size: 12, weight: .bold))
                        .foregroundStyle(.tint)
                }
            }
            .padding(.horizontal, 12).padding(.vertical, 8)
            .background(
                RoundedRectangle(cornerRadius: 8)
                    .fill(isSelected ? Color.accentColor.opacity(0.15) : .white.opacity(0.04))
            )
        }
        .buttonStyle(.plain)
    }

    private var sessionButtons: some View {
        HStack(spacing: 10) {
            if settings.isLoggedIn {
                Button(action: onLogout) {
                    Label("로그아웃", systemImage: "rectangle.portrait.and.arrow.right")
                        .font(.system(size: 12, weight: .semibold))
                        .padding(.horizontal, 14)
                        .padding(.vertical, 8)
                        .background(.ultraThinMaterial, in: Capsule())
                }
                .buttonStyle(.plain)
                Button(action: onWithdraw) {
                    Label("회원 탈퇴", systemImage: "person.crop.circle.badge.xmark")
                        .font(.system(size: 12, weight: .semibold))
                        .padding(.horizontal, 14)
                        .padding(.vertical, 8)
                        .background(.ultraThinMaterial, in: Capsule())
                        .foregroundStyle(.red)
                }
                .buttonStyle(.plain)
            } else {
                Button(action: onLogin) {
                    Label("로그인", systemImage: "rectangle.portrait.and.arrow.forward")
                        .font(.system(size: 12, weight: .semibold))
                        .padding(.horizontal, 14)
                        .padding(.vertical, 8)
                        .background(Color.accentColor, in: Capsule())
                        .foregroundStyle(.white)
                }
                .buttonStyle(.plain)
            }
        }
    }

    // MARK: - Payment

    private var paymentDetail: some View {
        VStack(alignment: .leading, spacing: 14) {
            HStack(spacing: 14) {
                Image(systemName: "bookmark.square.fill")
                    .font(.system(size: 28, weight: .bold))
                    .foregroundStyle(.orange)
                VStack(alignment: .leading, spacing: 2) {
                    Text("\(settings.pageBalance.formatted()) P")
                        .font(.system(size: 22, weight: .black))
                    Text("현재 보유 페이지 (1P = 10원)")
                        .font(.system(size: 11))
                        .foregroundStyle(.secondary)
                }
                Spacer()
                Button(action: {}) {
                    Text("충전")
                        .font(.system(size: 13, weight: .bold))
                        .padding(.horizontal, 18).padding(.vertical, 8)
                        .background(Color.accentColor, in: Capsule())
                        .foregroundStyle(.white)
                }
                .buttonStyle(.plain)
            }
            .padding(14)
            .background(RoundedRectangle(cornerRadius: 12).fill(.white.opacity(0.04)))

            VStack(alignment: .leading, spacing: 6) {
                Text("결제 수단")
                    .font(.system(size: 12, weight: .semibold)).foregroundStyle(.secondary)
                ForEach(settings.paymentMethods, id: \.self) { m in
                    HStack {
                        Image(systemName: "creditcard.fill").font(.system(size: 13))
                        Text(m).font(.system(size: 13, weight: .medium))
                        Spacer()
                        Text("기본").font(.system(size: 10, weight: .semibold))
                            .padding(.horizontal, 6).padding(.vertical, 2)
                            .background(Capsule().fill(Color.accentColor.opacity(0.2)))
                            .foregroundStyle(.tint)
                    }
                    .padding(10)
                    .background(RoundedRectangle(cornerRadius: 8).fill(.white.opacity(0.04)))
                }
                Button(action: {}) {
                    Label("결제 수단 추가", systemImage: "plus.circle")
                        .font(.system(size: 12, weight: .medium))
                        .padding(.horizontal, 12).padding(.vertical, 6)
                        .background(.ultraThinMaterial, in: Capsule())
                }
                .buttonStyle(.plain)
                .padding(.top, 2)
            }

            VStack(alignment: .leading, spacing: 6) {
                Text("구매 내역")
                    .font(.system(size: 12, weight: .semibold)).foregroundStyle(.secondary)
                ForEach(settings.purchaseHistory) { p in
                    HStack {
                        VStack(alignment: .leading, spacing: 2) {
                            Text(p.title).font(.system(size: 12, weight: .semibold))
                            Text(p.date).font(.system(size: 10)).foregroundStyle(.secondary)
                        }
                        Spacer()
                        Text(p.subtitle).font(.system(size: 12, weight: .medium))
                    }
                    .padding(10)
                    .background(RoundedRectangle(cornerRadius: 8).fill(.white.opacity(0.04)))
                }
            }
        }
    }

    // MARK: - Notifications

    private var notificationsDetail: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text("받고 싶은 알림을 선택하세요")
                .font(.system(size: 12)).foregroundStyle(.secondary)
            toggleRow("내 공간에 방명록이 달릴 때", icon: "text.bubble.fill",
                      on: Binding(get: { settings.notifyNewGuestbook },
                                  set: { settings.notifyNewGuestbook = $0 }))
            toggleRow("내 방명록에 답장이 달릴 때", icon: "arrowshape.turn.up.left.fill",
                      on: Binding(get: { settings.notifyGuestbookReply },
                                  set: { settings.notifyGuestbookReply = $0 }))
            toggleRow("내 독서기록에 좋아요/댓글", icon: "heart.fill",
                      on: Binding(get: { settings.notifyReadingRecordReaction },
                                  set: { settings.notifyReadingRecordReaction = $0 }))
            toggleRow("새 팔로워 추가", icon: "person.fill.badge.plus",
                      on: Binding(get: { settings.notifyNewFollower },
                                  set: { settings.notifyNewFollower = $0 }))
            toggleRow("내 공간의 책을 누가 자기 공간에 추가했을 때", icon: "book.closed.fill",
                      on: Binding(get: { settings.notifyBookAddedByOthers },
                                  set: { settings.notifyBookAddedByOthers = $0 }))
        }
    }

    // MARK: - Feed

    private var feedDetail: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text("내 활동 중 상대방 피드에 노출할 항목을 선택하세요")
                .font(.system(size: 12)).foregroundStyle(.secondary)
            toggleRow("책 추가", icon: "book.closed.fill",
                      on: Binding(get: { settings.feedShowBookAdd },
                                  set: { settings.feedShowBookAdd = $0 }))
            toggleRow("독서기록", icon: "square.and.pencil",
                      on: Binding(get: { settings.feedShowReadingRecord },
                                  set: { settings.feedShowReadingRecord = $0 }))
            toggleRow("최근 방문 공간", icon: "figure.walk",
                      on: Binding(get: { settings.feedShowRecentVisit },
                                  set: { settings.feedShowRecentVisit = $0 }))
            toggleRow("남긴 방명록", icon: "text.bubble.fill",
                      on: Binding(get: { settings.feedShowGuestbook },
                                  set: { settings.feedShowGuestbook = $0 }))
            toggleRow("독서기록 댓글", icon: "bubble.right.fill",
                      on: Binding(get: { settings.feedShowReadingRecordComment },
                                  set: { settings.feedShowReadingRecordComment = $0 }))
        }
    }

    @ViewBuilder
    private func toggleRow(_ title: String, icon: String, on: Binding<Bool>) -> some View {
        HStack(spacing: 10) {
            Image(systemName: icon).font(.system(size: 13)).frame(width: 20)
                .foregroundStyle(.tint)
            Text(title).font(.system(size: 13, weight: .medium))
            Spacer()
            Toggle("", isOn: on).labelsHidden()
        }
        .padding(.horizontal, 12).padding(.vertical, 8)
        .background(RoundedRectangle(cornerRadius: 8).fill(.white.opacity(0.04)))
    }

    // MARK: - Moderation

    private var moderationDetail: some View {
        VStack(alignment: .leading, spacing: 14) {
            VStack(alignment: .leading, spacing: 8) {
                Text("차단 내역 (\(guestbook.blockedUsers.count))")
                    .font(.system(size: 13, weight: .bold))
                if guestbook.blockedUsers.isEmpty {
                    placeholderRow(text: "차단한 사용자가 없어요", icon: "checkmark.shield")
                } else {
                    ForEach(guestbook.blockedUsers) { user in
                        HStack(spacing: 10) {
                            Circle().fill(user.avatarColor.gradient)
                                .frame(width: 32, height: 32)
                                .overlay(
                                    Text(user.initials)
                                        .font(.system(size: 12, weight: .black))
                                        .foregroundStyle(.white)
                                )
                            VStack(alignment: .leading, spacing: 2) {
                                Text(user.name).font(.system(size: 13, weight: .bold))
                                Text(user.bioSnippet).font(.system(size: 11)).foregroundStyle(.secondary)
                            }
                            Spacer()
                            Button(action: { guestbook.unblock(user.id) }) {
                                Text("해제")
                                    .font(.system(size: 11, weight: .semibold))
                                    .padding(.horizontal, 10).padding(.vertical, 5)
                                    .background(.ultraThinMaterial, in: Capsule())
                            }
                            .buttonStyle(.plain)
                        }
                        .padding(10)
                        .background(RoundedRectangle(cornerRadius: 8).fill(.white.opacity(0.04)))
                    }
                }
            }

            VStack(alignment: .leading, spacing: 8) {
                Text("신고 내역 (\(settings.reports.count))")
                    .font(.system(size: 13, weight: .bold))
                if settings.reports.isEmpty {
                    placeholderRow(text: "신고한 내역이 없어요", icon: "exclamationmark.triangle")
                } else {
                    ForEach(settings.reports) { r in
                        HStack {
                            VStack(alignment: .leading, spacing: 2) {
                                Text(r.target).font(.system(size: 12, weight: .bold))
                                Text(r.reason).font(.system(size: 11)).foregroundStyle(.secondary)
                            }
                            Spacer()
                            Text(r.date).font(.system(size: 10)).foregroundStyle(.secondary)
                        }
                        .padding(10)
                        .background(RoundedRectangle(cornerRadius: 8).fill(.white.opacity(0.04)))
                    }
                }
            }
        }
    }

    private func placeholderRow(text: String, icon: String) -> some View {
        HStack(spacing: 10) {
            Image(systemName: icon).font(.system(size: 14)).foregroundStyle(.secondary)
            Text(text).font(.system(size: 12)).foregroundStyle(.secondary)
            Spacer()
        }
        .padding(12)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(RoundedRectangle(cornerRadius: 8).fill(.white.opacity(0.04)))
    }

    // MARK: - Legal

    private var legalDetail: some View {
        VStack(alignment: .leading, spacing: 8) {
            legalRow("이용약관", icon: "doc.text.fill")
            legalRow("개인정보처리방침", icon: "lock.shield.fill")
            legalRow("환불 정책", icon: "arrow.uturn.backward.circle.fill")
            legalRow("오픈소스 라이선스", icon: "chevron.left.forwardslash.chevron.right")
        }
    }

    private func legalRow(_ title: String, icon: String) -> some View {
        Button(action: {}) {
            HStack(spacing: 10) {
                Image(systemName: icon).font(.system(size: 13)).foregroundStyle(.tint)
                    .frame(width: 20)
                Text(title).font(.system(size: 13, weight: .medium))
                Spacer()
                Image(systemName: "chevron.right").font(.system(size: 11)).foregroundStyle(.secondary)
            }
            .padding(12)
            .background(RoundedRectangle(cornerRadius: 8).fill(.white.opacity(0.04)))
        }
        .buttonStyle(.plain)
    }
}
