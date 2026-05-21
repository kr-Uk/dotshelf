import SwiftUI

/// §4 / §5.3 / §6 공통 신고 모달.
/// 대상(방명록·댓글·독서기록·유저) + 사유 picker + 추가 메모.
/// 제출 시 SettingsStore.reports에 기록 + 즉시 시야에서 가림 mock.
enum ReportTargetKind: String {
    case guestbook, guestbookReply, readingRecord, recordComment, user, feedItem

    var label: String {
        switch self {
        case .guestbook:      return "방명록"
        case .guestbookReply: return "방명록 답장"
        case .readingRecord:  return "독서기록"
        case .recordComment:  return "댓글"
        case .user:           return "사용자"
        case .feedItem:       return "피드 항목"
        }
    }
}

enum ReportReason: String, CaseIterable, Identifiable, Hashable {
    case spam, harassment, hate, sexual, violence, illegal, privacy, other

    var id: String { rawValue }
    var label: String {
        switch self {
        case .spam:       return "스팸 · 광고"
        case .harassment: return "괴롭힘 · 욕설"
        case .hate:       return "혐오 · 차별"
        case .sexual:     return "성적 콘텐츠"
        case .violence:   return "폭력 · 위협"
        case .illegal:    return "불법 · 유해 정보"
        case .privacy:    return "개인정보 유출"
        case .other:      return "기타"
        }
    }
    var systemImage: String {
        switch self {
        case .spam:       return "megaphone.fill"
        case .harassment: return "exclamationmark.bubble.fill"
        case .hate:       return "person.fill.xmark"
        case .sexual:     return "eye.slash.fill"
        case .violence:   return "hand.raised.fill"
        case .illegal:    return "xmark.shield.fill"
        case .privacy:    return "lock.trianglebadge.exclamationmark.fill"
        case .other:      return "ellipsis.circle.fill"
        }
    }
}

struct ReportTarget: Equatable {
    let kind: ReportTargetKind
    /// 대상의 대표 라벨 (e.g. 방명록 본문 일부 or 유저 닉네임).
    let displayText: String
}

struct ReportComposer: View {
    let settings: SettingsStore
    let target: ReportTarget
    var onClose: () -> Void
    var onSubmitted: (ReportReason) -> Void = { _ in }

    @State private var reason: ReportReason?
    @State private var memo: String = ""
    @State private var didSubmit: Bool = false

    private var canSubmit: Bool { reason != nil && !didSubmit }

    var body: some View {
        ZStack {
            Color.black.opacity(0.45).ignoresSafeArea()
                .onTapGesture { onClose() }
            sheet
        }
    }

    private var sheet: some View {
        VStack(spacing: 0) {
            header
            Divider().opacity(0.3)
            if didSubmit {
                confirmationView
            } else {
                ScrollView(.vertical, showsIndicators: false) {
                    VStack(alignment: .leading, spacing: 14) {
                        targetCard
                        reasonList
                        memoField
                    }
                    .padding(18)
                }
                footer
            }
        }
        .frame(maxWidth: 440, maxHeight: 600)
        .background(
            RoundedRectangle(cornerRadius: 18)
                .fill(.regularMaterial)
        )
        .overlay(
            RoundedRectangle(cornerRadius: 18)
                .strokeBorder(.white.opacity(0.1), lineWidth: 1)
        )
        .shadow(color: .black.opacity(0.3), radius: 20, y: 8)
        .padding(20)
    }

    private var header: some View {
        HStack {
            Image(systemName: "exclamationmark.triangle.fill")
                .font(.system(size: 14, weight: .bold))
                .foregroundStyle(.orange)
            Text("신고")
                .font(.system(size: 15, weight: .bold))
            Spacer()
            Button(action: onClose) {
                Image(systemName: "xmark.circle.fill")
                    .font(.system(size: 18))
                    .foregroundStyle(.secondary, .quaternary)
            }
            .buttonStyle(.plain)
        }
        .padding(.horizontal, 16).padding(.vertical, 14)
    }

    private var targetCard: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text("신고 대상: \(target.kind.label)")
                .font(.system(size: 11, weight: .semibold))
                .foregroundStyle(.secondary)
            Text(target.displayText.isEmpty ? "(내용 없음)" : target.displayText)
                .font(.system(size: 13, weight: .medium))
                .lineLimit(2)
        }
        .padding(12)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(RoundedRectangle(cornerRadius: 10).fill(.white.opacity(0.05)))
    }

    private var reasonList: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text("사유")
                .font(.system(size: 11, weight: .semibold)).foregroundStyle(.secondary)
            LazyVGrid(columns: [GridItem(.flexible()), GridItem(.flexible())], spacing: 6) {
                ForEach(ReportReason.allCases) { (r: ReportReason) in
                    reasonButton(r)
                }
            }
        }
    }

    @ViewBuilder
    private func reasonButton(_ r: ReportReason) -> some View {
        let isSelected = reason == r
        Button(action: { reason = r }) {
            HStack(spacing: 6) {
                Image(systemName: r.systemImage)
                    .font(.system(size: 11, weight: .semibold))
                    .foregroundStyle(isSelected ? Color.accentColor : .secondary)
                Text(r.label)
                    .font(.system(size: 12, weight: .medium))
                Spacer(minLength: 0)
            }
            .padding(.horizontal, 10).padding(.vertical, 8)
            .background(
                RoundedRectangle(cornerRadius: 8)
                    .fill(isSelected ? Color.accentColor.opacity(0.18) : Color.white.opacity(0.04))
            )
            .overlay(
                RoundedRectangle(cornerRadius: 8)
                    .strokeBorder(isSelected ? Color.accentColor : Color.clear, lineWidth: 1)
            )
        }
        .buttonStyle(.plain)
    }

    private var memoField: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text("추가 설명 (선택)")
                .font(.system(size: 11, weight: .semibold)).foregroundStyle(.secondary)
            TextField("상황을 조금 더 알려주세요", text: $memo, axis: .vertical)
                .lineLimit(2...4)
                .textFieldStyle(.plain)
                .font(.system(size: 12))
                .padding(10)
                .background(RoundedRectangle(cornerRadius: 8).fill(.white.opacity(0.05)))
        }
    }

    private var footer: some View {
        HStack(spacing: 10) {
            Spacer()
            Button(action: onClose) {
                Text("취소")
                    .font(.system(size: 12, weight: .medium))
                    .padding(.horizontal, 16).padding(.vertical, 8)
                    .background(.ultraThinMaterial, in: Capsule())
            }
            .buttonStyle(.plain)
            Button(action: submit) {
                Text("신고하기")
                    .font(.system(size: 12, weight: .bold))
                    .foregroundStyle(.white)
                    .padding(.horizontal, 18).padding(.vertical, 8)
                    .background(Capsule().fill(canSubmit ? Color.orange : Color.gray))
            }
            .buttonStyle(.plain)
            .disabled(!canSubmit)
        }
        .padding(16)
    }

    private var confirmationView: some View {
        VStack(spacing: 12) {
            Image(systemName: "checkmark.seal.fill")
                .font(.system(size: 44))
                .foregroundStyle(.green)
            Text("신고가 접수되었어요")
                .font(.system(size: 15, weight: .bold))
            Text("해당 콘텐츠는 즉시 내 화면에서 가려지고,\n운영팀이 검토 후 조치할게요.")
                .font(.system(size: 12))
                .foregroundStyle(.secondary)
                .multilineTextAlignment(.center)
            Button(action: onClose) {
                Text("확인")
                    .font(.system(size: 13, weight: .bold))
                    .foregroundStyle(.white)
                    .padding(.horizontal, 24).padding(.vertical, 10)
                    .background(Capsule().fill(Color.accentColor))
            }
            .buttonStyle(.plain)
            .padding(.top, 4)
        }
        .padding(24)
    }

    private func submit() {
        guard let reason else { return }
        let record = ReportRecord(
            target: "\(target.kind.label) · \(target.displayText.prefix(30))",
            reason: reason.label + (memo.isEmpty ? "" : " — \(memo.prefix(50))"),
            date: Self.today()
        )
        settings.reports.insert(record, at: 0)
        didSubmit = true
        onSubmitted(reason)
    }

    private static func today() -> String {
        let f = DateFormatter()
        f.dateFormat = "yyyy-MM-dd"
        return f.string(from: Date())
    }
}
