import SwiftUI

/// §8 온보딩 플로우.
/// - 일반 가입: 프로필 → 방 템플릿 → 첫 책 3단계
/// - 게스트: 프로필 스킵 → 방 템플릿 → 첫 책
struct OnboardingFlow: View {
    let auth: AuthStore
    var onBookCatalogRequest: () -> Void = {}  // "책 검색" 시 §5 책 시장 진입용 콜백 (mock)

    @State private var step: Int = 0
    @State private var nickname: String = ""
    @State private var bio: String = ""
    @State private var template: RoomTemplate = .warmWood
    @State private var pickedBooks: [String] = []
    @State private var searchQuery: String = ""

    private var totalSteps: Int { auth.isGuest ? 2 : 3 }

    private var currentStepIndex: Int {
        auth.isGuest ? step + 1 : step  // 게스트는 프로필(0) 건너뛰고 템플릿부터
    }

    private var headerTitle: String {
        switch effectiveStep {
        case .profile:  return "프로필 설정"
        case .template: return "내 방 꾸미기"
        case .firstBook: return "첫 책 고르기"
        }
    }

    private enum Step { case profile, template, firstBook }

    private var effectiveStep: Step {
        if auth.isGuest {
            return step == 0 ? .template : .firstBook
        }
        switch step {
        case 0: return .profile
        case 1: return .template
        default: return .firstBook
        }
    }

    var body: some View {
        ZStack {
            background.ignoresSafeArea()
            VStack(spacing: 0) {
                progressBar
                Spacer(minLength: 0)
                content
                    .frame(maxWidth: 560)
                    .padding(.horizontal, 24)
                Spacer(minLength: 0)
                navBar
                    .padding(.horizontal, 24)
                    .padding(.bottom, 28)
            }
        }
        .animation(.easeInOut(duration: 0.2), value: step)
    }

    private var background: some View {
        LinearGradient(
            colors: [
                Color(red: 0.97, green: 0.94, blue: 0.88),
                Color(red: 0.88, green: 0.82, blue: 0.72),
            ],
            startPoint: .top, endPoint: .bottom
        )
    }

    private var progressBar: some View {
        VStack(spacing: 8) {
            HStack(spacing: 6) {
                ForEach(0..<totalSteps, id: \.self) { i in
                    Capsule()
                        .fill(i <= step ? Color.accentColor : .secondary.opacity(0.2))
                        .frame(height: 4)
                }
            }
            HStack {
                Text(headerTitle)
                    .font(.system(size: 14, weight: .bold))
                Spacer()
                Text("\(step + 1) / \(totalSteps)")
                    .font(.system(size: 11, weight: .medium))
                    .foregroundStyle(.secondary)
            }
        }
        .padding(.horizontal, 24)
        .padding(.top, 20)
    }

    @ViewBuilder
    private var content: some View {
        switch effectiveStep {
        case .profile:   profileStep
        case .template:  templateStep
        case .firstBook: firstBookStep
        }
    }

    // MARK: - Step: 프로필

    private var profileStep: some View {
        VStack(alignment: .leading, spacing: 18) {
            Text("당신을 소개해주세요")
                .font(.system(size: 22, weight: .black))
            Text("책장에 꽂힌 책과 함께 이 프로필이 방명록·팔로워에게 노출돼요")
                .font(.system(size: 12)).foregroundStyle(.secondary)

            HStack(spacing: 16) {
                Image(systemName: "person.crop.circle.fill")
                    .font(.system(size: 56))
                    .foregroundStyle(.tint)
                Button(action: {}) {
                    Label("사진 선택", systemImage: "photo")
                        .font(.system(size: 12, weight: .semibold))
                        .padding(.horizontal, 12).padding(.vertical, 7)
                        .background(.ultraThinMaterial, in: Capsule())
                }
                .buttonStyle(.plain)
            }

            VStack(alignment: .leading, spacing: 6) {
                Text("닉네임")
                    .font(.system(size: 12, weight: .semibold)).foregroundStyle(.secondary)
                TextField("닉네임 (2~16자)", text: $nickname)
                    .textFieldStyle(.plain)
                    .font(.system(size: 15, weight: .medium))
                    .padding(12)
                    .background(RoundedRectangle(cornerRadius: 10).fill(.white.opacity(0.7)))
                nicknameFeedback
            }

            VStack(alignment: .leading, spacing: 6) {
                Text("한 줄 소개")
                    .font(.system(size: 12, weight: .semibold)).foregroundStyle(.secondary)
                TextField("이 공간에 대한 한 줄 소개", text: $bio, axis: .vertical)
                    .lineLimit(2...3)
                    .textFieldStyle(.plain)
                    .font(.system(size: 14))
                    .padding(12)
                    .background(RoundedRectangle(cornerRadius: 10).fill(.white.opacity(0.7)))
            }
        }
    }

    @ViewBuilder
    private var nicknameFeedback: some View {
        let status = auth.nicknameStatus(nickname)
        if let msg = status.message {
            HStack(spacing: 6) {
                Image(systemName: status.isValid ? "checkmark.circle.fill" : "exclamationmark.circle.fill")
                    .font(.system(size: 11, weight: .semibold))
                Text(msg).font(.system(size: 11, weight: .medium))
            }
            .foregroundStyle(status.isValid ? Color.green : Color.red)
        }
    }

    // MARK: - Step: 템플릿

    private var templateStep: some View {
        VStack(alignment: .leading, spacing: 18) {
            Text("방 분위기를 골라주세요")
                .font(.system(size: 22, weight: .black))
            Text("언제든 '설정 → 공간 편집'에서 바꿀 수 있어요")
                .font(.system(size: 12)).foregroundStyle(.secondary)

            LazyVGrid(columns: [GridItem(.flexible()), GridItem(.flexible())], spacing: 10) {
                ForEach(RoomTemplate.allCases) { (t: RoomTemplate) in
                    templateCard(for: t)
                }
            }
        }
    }

    @ViewBuilder
    private func templateCard(for t: RoomTemplate) -> some View {
        let isSelected = template == t
        Button(action: {
            template = t == .random ? RoomTemplate.allCases.filter { $0 != .random }.randomElement() ?? .warmWood : t
        }) {
            HStack(spacing: 10) {
                Image(systemName: t.systemImage)
                    .font(.system(size: 20, weight: .bold))
                    .foregroundStyle(.tint)
                    .frame(width: 30)
                VStack(alignment: .leading, spacing: 2) {
                    Text(t.label).font(.system(size: 13, weight: .bold))
                    if t == .random {
                        Text("놀라운 조합을 자동으로")
                            .font(.system(size: 10)).foregroundStyle(.secondary)
                    }
                }
                Spacer()
                if isSelected && t != .random {
                    Image(systemName: "checkmark.circle.fill")
                        .font(.system(size: 14))
                        .foregroundStyle(.tint)
                }
            }
            .padding(12)
            .background(
                RoundedRectangle(cornerRadius: 12)
                    .fill(isSelected && t != .random ? Color.accentColor.opacity(0.15) : Color.white.opacity(0.6))
            )
            .overlay(
                RoundedRectangle(cornerRadius: 12)
                    .strokeBorder(isSelected && t != .random ? Color.accentColor : Color.clear, lineWidth: 1.5)
            )
        }
        .buttonStyle(.plain)
    }

    // MARK: - Step: 첫 책

    private let sampleBooks = [
        "달러구트 꿈 백화점", "코스모스", "미드나잇 라이브러리",
        "사피엔스", "아몬드", "작별하지 않는다", "파과", "모순", "시선으로부터,",
    ]

    private var filteredSamples: [String] {
        let q = searchQuery.trimmingCharacters(in: .whitespacesAndNewlines)
        if q.isEmpty { return sampleBooks }
        return sampleBooks.filter { $0.localizedCaseInsensitiveContains(q) }
    }

    private var firstBookStep: some View {
        VStack(alignment: .leading, spacing: 14) {
            Text("책장에 올릴 첫 책을 골라주세요")
                .font(.system(size: 22, weight: .black))
            Text("여러 권 선택 가능해요. 나중에 더 추가할 수 있어요.")
                .font(.system(size: 12)).foregroundStyle(.secondary)

            HStack(spacing: 8) {
                Image(systemName: "magnifyingglass")
                    .foregroundStyle(.secondary)
                TextField("제목 · 저자 · 출판사", text: $searchQuery)
                    .textFieldStyle(.plain)
                    .font(.system(size: 13))
            }
            .padding(10)
            .background(RoundedRectangle(cornerRadius: 10).fill(.white.opacity(0.7)))

            ScrollView(.vertical, showsIndicators: false) {
                LazyVStack(spacing: 6) {
                    ForEach(filteredSamples, id: \.self) { book in
                        bookRow(book)
                    }
                }
            }
            .frame(maxHeight: 260)

            if !pickedBooks.isEmpty {
                HStack(spacing: 6) {
                    Image(systemName: "books.vertical.fill")
                        .foregroundStyle(.tint)
                    Text("선택한 책 \(pickedBooks.count)권")
                        .font(.system(size: 12, weight: .bold))
                    Spacer()
                    Button(action: { pickedBooks.removeAll() }) {
                        Text("전체 해제").font(.system(size: 11))
                    }
                    .buttonStyle(.plain)
                }
            }
        }
    }

    @ViewBuilder
    private func bookRow(_ book: String) -> some View {
        let picked = pickedBooks.contains(book)
        Button(action: {
            if picked { pickedBooks.removeAll { $0 == book } } else { pickedBooks.append(book) }
        }) {
            HStack(spacing: 10) {
                Image(systemName: "book.closed.fill")
                    .foregroundStyle(.orange)
                Text(book).font(.system(size: 13, weight: .medium))
                Spacer()
                Image(systemName: picked ? "checkmark.square.fill" : "plus.square")
                    .font(.system(size: 16))
                    .foregroundStyle(picked ? Color.accentColor : .secondary)
            }
            .padding(10)
            .background(RoundedRectangle(cornerRadius: 8).fill(picked ? Color.accentColor.opacity(0.12) : Color.white.opacity(0.6)))
        }
        .buttonStyle(.plain)
    }

    // MARK: - Nav

    private var navBar: some View {
        HStack(spacing: 10) {
            if step > 0 {
                Button(action: { step -= 1 }) {
                    Label("뒤로", systemImage: "chevron.left")
                        .font(.system(size: 13, weight: .semibold))
                        .padding(.horizontal, 14).padding(.vertical, 10)
                        .background(.ultraThinMaterial, in: Capsule())
                }
                .buttonStyle(.plain)
            }
            Spacer()
            Button(action: nextTapped) {
                HStack(spacing: 6) {
                    Text(isLastStep ? "시작하기" : "다음")
                        .font(.system(size: 13, weight: .bold))
                    if !isLastStep {
                        Image(systemName: "chevron.right")
                            .font(.system(size: 11, weight: .bold))
                    }
                }
                .foregroundStyle(.white)
                .padding(.horizontal, 22).padding(.vertical, 11)
                .background(Capsule().fill(canProceed ? Color.accentColor : Color.gray))
                .shadow(color: .black.opacity(0.15), radius: 5, y: 2)
            }
            .buttonStyle(.plain)
            .disabled(!canProceed)
        }
    }

    private var isLastStep: Bool { step == totalSteps - 1 }

    private var canProceed: Bool {
        switch effectiveStep {
        case .profile:   return auth.nicknameStatus(nickname).isValid
        case .template:  return true
        case .firstBook: return true  // 0권도 허용 (나중에 추가 가능)
        }
    }

    private func nextTapped() {
        guard canProceed else { return }
        if isLastStep {
            // 최종 반영
            if !auth.isGuest {
                auth.nickname = nickname
                auth.bio = bio
            }
            auth.finishOnboarding()
        } else {
            step += 1
        }
    }
}
