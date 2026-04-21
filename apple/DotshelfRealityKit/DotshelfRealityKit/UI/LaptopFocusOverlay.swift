import SwiftUI

/// §5 노트북 포커싱 2D 풀 오버레이.
/// 첫 진입 시 광고 오버레이 → X 탭 시 본 화면. 4개 탭(책 시장/공간 목록/피드/꾸미기 시장).
struct LaptopFocusOverlay: View {
    enum Tab: String, CaseIterable, Identifiable, Hashable {
        case market, spaces, feed, decoration
        var id: String { rawValue }
        var label: String {
            switch self {
            case .market: return "책 시장"
            case .spaces: return "공간 목록"
            case .feed:   return "피드"
            case .decoration: return "꾸미기 시장"
            }
        }
        var systemImage: String {
            switch self {
            case .market: return "books.vertical.fill"
            case .spaces: return "house.lodge.fill"
            case .feed:   return "rectangle.stack.fill"
            case .decoration: return "paintpalette.fill"
            }
        }
    }

    let market: MarketStore
    let settings: SettingsStore
    var onClose: () -> Void

    @State private var tab: Tab = .feed
    @State private var showAd: Bool = true

    var body: some View {
        ZStack {
            Color.black.opacity(0.55).ignoresSafeArea()
                .onTapGesture { onClose() }
            sheet
            if showAd {
                adOverlay
                    .transition(.opacity)
            }
        }
        .animation(.easeInOut(duration: 0.25), value: showAd)
    }

    private var sheet: some View {
        VStack(spacing: 0) {
            header
            Divider().opacity(0.3)
            tabBar
            Divider().opacity(0.3)
            content
        }
        .frame(maxWidth: 960, maxHeight: 660)
        .background(
            RoundedRectangle(cornerRadius: 22)
                .fill(.regularMaterial)
        )
        .overlay(
            RoundedRectangle(cornerRadius: 22)
                .strokeBorder(.white.opacity(0.1), lineWidth: 1)
        )
        .shadow(color: .black.opacity(0.4), radius: 24, y: 12)
        .padding(20)
    }

    private var header: some View {
        HStack(spacing: 10) {
            Button(action: onClose) {
                HStack(spacing: 4) {
                    Image(systemName: "chevron.left").font(.system(size: 12, weight: .bold))
                    Text("뒤로").font(.system(size: 12, weight: .semibold))
                }
                .padding(.horizontal, 10).padding(.vertical, 6)
                .background(.ultraThinMaterial, in: Capsule())
            }
            .buttonStyle(.plain)

            Image(systemName: "laptopcomputer")
                .font(.system(size: 15, weight: .bold))
                .foregroundStyle(.tint)
            Text("노트북")
                .font(.system(size: 15, weight: .bold))

            Spacer()

            if tab == .market || tab == .decoration {
                HStack(spacing: 8) {
                    Image(systemName: "bookmark.square.fill")
                        .font(.system(size: 14, weight: .bold))
                        .foregroundStyle(.orange)
                    Text("\(settings.pageBalance.formatted()) P")
                        .font(.system(size: 13, weight: .bold))
                    Button(action: {}) {
                        Text("충전").font(.system(size: 11, weight: .bold))
                            .padding(.horizontal, 10).padding(.vertical, 4)
                            .background(Color.accentColor, in: Capsule())
                            .foregroundStyle(.white)
                    }
                    .buttonStyle(.plain)
                }
                .padding(.horizontal, 10).padding(.vertical, 6)
                .background(Capsule().fill(.white.opacity(0.08)))
            }

            Button(action: onClose) {
                Image(systemName: "xmark.circle.fill")
                    .font(.system(size: 20))
                    .foregroundStyle(.secondary, .quaternary)
            }
            .buttonStyle(.plain)
        }
        .padding(.horizontal, 18).padding(.vertical, 12)
    }

    private var tabBar: some View {
        HStack(spacing: 6) {
            ForEach(Tab.allCases) { (t: Tab) in
                tabButton(for: t)
            }
        }
        .padding(.horizontal, 14).padding(.vertical, 10)
    }

    @ViewBuilder
    private func tabButton(for t: Tab) -> some View {
        let isActive = tab == t
        Button(action: { tab = t }) {
            HStack(spacing: 6) {
                Image(systemName: t.systemImage).font(.system(size: 11, weight: .semibold))
                Text(t.label).font(.system(size: 12, weight: .semibold))
            }
            .foregroundStyle(isActive ? Color.accentColor : Color.primary)
            .padding(.horizontal, 12).padding(.vertical, 7)
            .background(Capsule().fill(isActive ? Color.accentColor.opacity(0.18) : Color.clear))
        }
        .buttonStyle(.plain)
    }

    @ViewBuilder
    private var content: some View {
        switch tab {
        case .market:     MarketTab(market: market)
        case .spaces:     SpacesTab(market: market)
        case .feed:       FeedTab(market: market)
        case .decoration: DecorationTab(market: market, balance: settings.pageBalance)
        }
    }

    // 첫 진입 광고
    private var adOverlay: some View {
        ZStack {
            Color.black.opacity(0.7).ignoresSafeArea()
            VStack(spacing: 16) {
                Image(systemName: "sparkles.rectangle.stack.fill")
                    .font(.system(size: 56))
                    .foregroundStyle(.orange)
                Text("신간 출시 — 파과")
                    .font(.system(size: 24, weight: .black))
                Text("구병모 · 위즈덤하우스")
                    .font(.system(size: 14, weight: .medium))
                    .foregroundStyle(.secondary)
                Text("유료 표지 구매 시 170P")
                    .font(.system(size: 12, weight: .medium))
                    .padding(.horizontal, 12).padding(.vertical, 4)
                    .background(Capsule().fill(Color.orange.opacity(0.25)))
                Button(action: { showAd = false }) {
                    Text("닫기").font(.system(size: 13, weight: .bold))
                        .padding(.horizontal, 24).padding(.vertical, 10)
                        .background(Color.white, in: Capsule())
                        .foregroundStyle(.black)
                }
                .buttonStyle(.plain)
            }
            .padding(36)
            .background(
                RoundedRectangle(cornerRadius: 18)
                    .fill(Color(red: 0.12, green: 0.12, blue: 0.16))
            )
            .overlay(alignment: .topTrailing) {
                Button(action: { showAd = false }) {
                    Image(systemName: "xmark.circle.fill")
                        .font(.system(size: 22))
                        .foregroundStyle(.white, .black.opacity(0.4))
                }
                .buttonStyle(.plain)
                .padding(12)
            }
            .frame(maxWidth: 440)
            .padding(30)
        }
    }
}

// MARK: - Market (5.1)

private struct MarketTab: View {
    let market: MarketStore
    @State private var genre: BookGenre = .all
    @State private var sort: MarketSort = .rating
    @State private var query: String = ""

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack(spacing: 8) {
                Image(systemName: "magnifyingglass").foregroundStyle(.secondary)
                TextField("제목 · 저자 · 출판사", text: $query)
                    .textFieldStyle(.plain).font(.system(size: 13))
            }
            .padding(8)
            .background(RoundedRectangle(cornerRadius: 10).fill(.white.opacity(0.06)))

            HStack(spacing: 6) {
                genreChips
                Spacer(minLength: 10)
                Menu {
                    ForEach(MarketSort.allCases) { (s: MarketSort) in
                        Button(action: { sort = s }) {
                            Label(s.label, systemImage: sort == s ? "checkmark" : "")
                        }
                    }
                } label: {
                    HStack(spacing: 4) {
                        Image(systemName: "arrow.up.arrow.down").font(.system(size: 11))
                        Text(sort.label).font(.system(size: 12, weight: .semibold))
                    }
                    .padding(.horizontal, 10).padding(.vertical, 6)
                    .background(Capsule().fill(.white.opacity(0.08)))
                }
            }

            ScrollView(.vertical, showsIndicators: false) {
                LazyVGrid(columns: [GridItem(.flexible()), GridItem(.flexible())], spacing: 10) {
                    ForEach(market.sortedBooks(genre: genre, sort: sort, query: query)) { book in
                        marketBookCard(book)
                    }
                }
                .padding(.bottom, 10)
            }
        }
        .padding(.horizontal, 18).padding(.vertical, 12)
    }

    private var genreChips: some View {
        ScrollView(.horizontal, showsIndicators: false) {
            HStack(spacing: 6) {
                ForEach(BookGenre.allCases) { (g: BookGenre) in
                    let isActive = genre == g
                    Button(action: { genre = g }) {
                        Text(g.label)
                            .font(.system(size: 11, weight: .semibold))
                            .padding(.horizontal, 10).padding(.vertical, 5)
                            .background(Capsule().fill(isActive ? Color.accentColor.opacity(0.2) : .white.opacity(0.06)))
                            .foregroundStyle(isActive ? Color.accentColor : .primary)
                    }
                    .buttonStyle(.plain)
                }
            }
        }
    }

    private func marketBookCard(_ book: MarketBook) -> some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack(alignment: .top, spacing: 10) {
                RoundedRectangle(cornerRadius: 6)
                    .fill(LinearGradient(colors: [.orange, .red], startPoint: .topLeading, endPoint: .bottomTrailing))
                    .frame(width: 56, height: 82)
                    .overlay(
                        Text(book.title.prefix(1))
                            .font(.system(size: 24, weight: .black))
                            .foregroundStyle(.white)
                    )
                VStack(alignment: .leading, spacing: 2) {
                    Text(book.title)
                        .font(.system(size: 13, weight: .bold))
                        .lineLimit(2)
                    Text("\(book.author) · \(book.publisher)")
                        .font(.system(size: 10))
                        .foregroundStyle(.secondary)
                        .lineLimit(1)
                    HStack(spacing: 4) {
                        Image(systemName: "star.fill").font(.system(size: 9)).foregroundStyle(.yellow)
                        Text(String(format: "%.1f", book.rating))
                            .font(.system(size: 10, weight: .bold))
                        Image(systemName: "books.vertical.fill").font(.system(size: 9)).foregroundStyle(.secondary)
                        Text("\(book.placements)").font(.system(size: 10))
                    }
                    HStack(spacing: 4) {
                        Image(systemName: "heart.fill").font(.system(size: 9)).foregroundStyle(.pink)
                        Text("\(book.likes)").font(.system(size: 10))
                        Image(systemName: "square.and.pencil").font(.system(size: 9)).foregroundStyle(.secondary)
                        Text("\(book.recordCount)").font(.system(size: 10))
                    }
                }
                Spacer(minLength: 0)
            }
            HStack(spacing: 6) {
                Button(action: {}) {
                    HStack(spacing: 4) {
                        Image(systemName: "plus")
                        Text("내 책장에 추가 (무료)")
                    }
                    .font(.system(size: 10, weight: .semibold))
                    .padding(.horizontal, 8).padding(.vertical, 5)
                    .background(Capsule().fill(.white.opacity(0.08)))
                }
                .buttonStyle(.plain)
                if book.priceCover > 0 {
                    Button(action: {}) {
                        HStack(spacing: 4) {
                            Image(systemName: "sparkles")
                            Text("표지 \(book.priceCover)P")
                        }
                        .font(.system(size: 10, weight: .bold))
                        .padding(.horizontal, 8).padding(.vertical, 5)
                        .foregroundStyle(.white)
                        .background(Capsule().fill(Color.orange))
                    }
                    .buttonStyle(.plain)
                }
            }
        }
        .padding(10)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(RoundedRectangle(cornerRadius: 12).fill(.white.opacity(0.04)))
    }
}

// MARK: - Spaces (5.2)

private struct SpacesTab: View {
    let market: MarketStore
    @State private var period: RankPeriod = .week
    @State private var kind: SpaceRankKind = .visitors
    @State private var query: String = ""

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack(spacing: 8) {
                Image(systemName: "magnifyingglass").foregroundStyle(.secondary)
                TextField("닉네임 검색", text: $query)
                    .textFieldStyle(.plain).font(.system(size: 13))
            }
            .padding(8)
            .background(RoundedRectangle(cornerRadius: 10).fill(.white.opacity(0.06)))

            HStack(spacing: 6) {
                periodChips
                Spacer(minLength: 10)
                Menu {
                    ForEach(SpaceRankKind.allCases) { (k: SpaceRankKind) in
                        Button(action: { kind = k }) {
                            Label(k.label, systemImage: kind == k ? "checkmark" : "")
                        }
                    }
                } label: {
                    HStack(spacing: 4) {
                        Image(systemName: "chart.bar.fill").font(.system(size: 11))
                        Text(kind.label).font(.system(size: 12, weight: .semibold))
                    }
                    .padding(.horizontal, 10).padding(.vertical, 6)
                    .background(Capsule().fill(.white.opacity(0.08)))
                }
            }

            Text("나와 비슷한 공간")
                .font(.system(size: 12, weight: .bold)).foregroundStyle(.secondary)

            ScrollView(.vertical, showsIndicators: false) {
                LazyVStack(spacing: 8) {
                    ForEach(market.sortedSpaces(kind: kind, query: query)) { space in
                        spaceCard(space)
                    }
                }
                .padding(.bottom, 10)
            }
        }
        .padding(.horizontal, 18).padding(.vertical, 12)
    }

    private var periodChips: some View {
        HStack(spacing: 6) {
            ForEach(RankPeriod.allCases) { (p: RankPeriod) in
                let isActive = period == p
                Button(action: { period = p }) {
                    Text(p.label)
                        .font(.system(size: 11, weight: .semibold))
                        .padding(.horizontal, 10).padding(.vertical, 5)
                        .background(Capsule().fill(isActive ? Color.accentColor.opacity(0.2) : .white.opacity(0.06)))
                        .foregroundStyle(isActive ? Color.accentColor : .primary)
                }
                .buttonStyle(.plain)
            }
        }
    }

    private func spaceCard(_ s: MarketSpace) -> some View {
        HStack(alignment: .center, spacing: 12) {
            Circle()
                .fill(LinearGradient(colors: [.purple, .indigo], startPoint: .topLeading, endPoint: .bottomTrailing))
                .frame(width: 42, height: 42)
                .overlay(
                    Text(s.handle.replacingOccurrences(of: "@", with: "").prefix(1).uppercased())
                        .font(.system(size: 16, weight: .black))
                        .foregroundStyle(.white)
                )
            VStack(alignment: .leading, spacing: 2) {
                Text(s.handle).font(.system(size: 13, weight: .bold))
                Text(s.bio).font(.system(size: 11)).foregroundStyle(.secondary).lineLimit(1)
                HStack(spacing: 10) {
                    statLabel(icon: "eye.fill", value: s.visitors, color: .cyan)
                    statLabel(icon: "heart.fill", value: s.spaceLikes, color: .pink)
                    statLabel(icon: "books.vertical.fill", value: s.bookCount, color: .orange)
                    if s.overlapBooks > 0 {
                        Text("겹치는 책 \(s.overlapBooks)권")
                            .font(.system(size: 9, weight: .bold))
                            .padding(.horizontal, 6).padding(.vertical, 2)
                            .background(Capsule().fill(Color.accentColor.opacity(0.2)))
                            .foregroundStyle(.tint)
                    }
                }
            }
            Spacer()
            Button(action: {}) {
                Text("방문하기").font(.system(size: 11, weight: .semibold))
                    .padding(.horizontal, 10).padding(.vertical, 5)
                    .background(Capsule().fill(.white.opacity(0.08)))
            }
            .buttonStyle(.plain)
            Button(action: {}) {
                Text("팔로우").font(.system(size: 11, weight: .bold))
                    .padding(.horizontal, 10).padding(.vertical, 5)
                    .background(Capsule().fill(Color.accentColor))
                    .foregroundStyle(.white)
            }
            .buttonStyle(.plain)
        }
        .padding(10)
        .background(RoundedRectangle(cornerRadius: 10).fill(.white.opacity(0.04)))
    }

    private func statLabel(icon: String, value: Int, color: Color) -> some View {
        HStack(spacing: 3) {
            Image(systemName: icon).font(.system(size: 9)).foregroundStyle(color)
            Text("\(value)").font(.system(size: 10))
        }
    }
}

// MARK: - Feed (5.3)

private struct FeedTab: View {
    let market: MarketStore

    var body: some View {
        ScrollView(.vertical, showsIndicators: false) {
            LazyVStack(spacing: 10) {
                ForEach(market.feed) { item in
                    feedCard(item)
                }
            }
            .padding(.horizontal, 18).padding(.vertical, 12)
        }
    }

    @ViewBuilder
    private func feedCard(_ item: FeedItem) -> some View {
        HStack(alignment: .top, spacing: 10) {
            Circle()
                .fill(item.actorColor.gradient)
                .frame(width: 34, height: 34)
                .overlay(
                    Text(item.actorInitials)
                        .font(.system(size: 12, weight: .black))
                        .foregroundStyle(.white)
                )
            VStack(alignment: .leading, spacing: 3) {
                HStack(spacing: 6) {
                    Text(item.actorName).font(.system(size: 12, weight: .bold))
                    Text("·").foregroundStyle(.secondary)
                    Text(item.time).font(.system(size: 11)).foregroundStyle(.secondary)
                    if item.kind == .ad {
                        Text("AD").font(.system(size: 9, weight: .black))
                            .padding(.horizontal, 5).padding(.vertical, 1)
                            .background(Capsule().fill(Color.orange))
                            .foregroundStyle(.white)
                    }
                    if item.kind == .recommendation {
                        Text("추천").font(.system(size: 9, weight: .black))
                            .padding(.horizontal, 5).padding(.vertical, 1)
                            .background(Capsule().fill(Color.purple.opacity(0.25)))
                            .foregroundStyle(.purple)
                    }
                }
                Text(item.headline).font(.system(size: 13, weight: .semibold))
                Text(item.subtitle)
                    .font(.system(size: 12))
                    .foregroundStyle(.primary.opacity(0.8))
                    .lineLimit(2)
                HStack(spacing: 10) {
                    iconButton("heart")
                    iconButton("bubble.right")
                    if item.kind == .bookAdded || item.kind == .ad {
                        iconButton("plus.square")
                    }
                    Spacer()
                    Menu {
                        Button(role: .destructive) {} label: { Label("신고", systemImage: "exclamationmark.triangle") }
                        Button(role: .destructive) {} label: { Label("작성자 차단", systemImage: "nosign") }
                    } label: {
                        Image(systemName: "ellipsis")
                            .font(.system(size: 12, weight: .bold))
                            .foregroundStyle(.secondary)
                            .padding(4)
                    }
                }
            }
            Spacer(minLength: 0)
        }
        .padding(12)
        .background(RoundedRectangle(cornerRadius: 12).fill(.white.opacity(0.04)))
    }

    private func iconButton(_ icon: String) -> some View {
        Image(systemName: icon)
            .font(.system(size: 12))
            .foregroundStyle(.secondary)
            .padding(.horizontal, 6).padding(.vertical, 3)
            .background(Capsule().fill(.white.opacity(0.05)))
    }
}

// MARK: - Decoration (5.4)

private struct DecorationTab: View {
    let market: MarketStore
    let balance: Int
    @State private var category: DecorationCategory? = nil
    @State private var sort: DecorationSort = .popular

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack(spacing: 6) {
                categoryChip(nil, label: "전체")
                ForEach(DecorationCategory.allCases) { (c: DecorationCategory) in
                    categoryChip(c, label: c.label)
                }
                Spacer(minLength: 10)
                Menu {
                    ForEach(DecorationSort.allCases) { (s: DecorationSort) in
                        Button(action: { sort = s }) {
                            Label(s.label, systemImage: sort == s ? "checkmark" : "")
                        }
                    }
                } label: {
                    HStack(spacing: 4) {
                        Image(systemName: "arrow.up.arrow.down").font(.system(size: 11))
                        Text(sort.label).font(.system(size: 12, weight: .semibold))
                    }
                    .padding(.horizontal, 10).padding(.vertical, 6)
                    .background(Capsule().fill(.white.opacity(0.08)))
                }
            }

            ScrollView(.vertical, showsIndicators: false) {
                LazyVGrid(columns: [GridItem(.flexible()), GridItem(.flexible()), GridItem(.flexible())], spacing: 10) {
                    ForEach(market.filteredDecorations(category: category, sort: sort)) { d in
                        decorationCard(d)
                    }
                }
                .padding(.bottom, 10)
            }
        }
        .padding(.horizontal, 18).padding(.vertical, 12)
    }

    @ViewBuilder
    private func categoryChip(_ c: DecorationCategory?, label: String) -> some View {
        let isActive = category == c
        Button(action: { category = c }) {
            Text(label)
                .font(.system(size: 11, weight: .semibold))
                .padding(.horizontal, 10).padding(.vertical, 5)
                .background(Capsule().fill(isActive ? Color.accentColor.opacity(0.2) : .white.opacity(0.06)))
                .foregroundStyle(isActive ? Color.accentColor : .primary)
        }
        .buttonStyle(.plain)
    }

    private func decorationCard(_ d: DecorationAsset) -> some View {
        let canAfford = balance >= d.priceP
        let wished = market.wishlist.contains(d.id)
        return VStack(alignment: .leading, spacing: 8) {
            ZStack {
                RoundedRectangle(cornerRadius: 10)
                    .fill(d.tint.opacity(0.25))
                Image(systemName: d.systemImage)
                    .font(.system(size: 42, weight: .bold))
                    .foregroundStyle(d.tint)
            }
            .frame(height: 100)

            Text(d.name).font(.system(size: 12, weight: .bold)).lineLimit(1)
            Text(d.creator).font(.system(size: 10)).foregroundStyle(.secondary).lineLimit(1)

            HStack(spacing: 6) {
                Text("\(d.priceP)P")
                    .font(.system(size: 12, weight: .black))
                    .foregroundStyle(canAfford ? Color.primary : Color.red)
                Spacer()
                Button(action: { market.toggleWishlist(d.id) }) {
                    Image(systemName: wished ? "heart.fill" : "heart")
                        .font(.system(size: 12))
                        .foregroundStyle(wished ? .pink : .secondary)
                }
                .buttonStyle(.plain)
                Button(action: {}) {
                    Text(canAfford ? "구매" : "부족")
                        .font(.system(size: 10, weight: .bold))
                        .padding(.horizontal, 8).padding(.vertical, 4)
                        .background(Capsule().fill(canAfford ? Color.accentColor : Color.gray))
                        .foregroundStyle(.white)
                }
                .buttonStyle(.plain)
                .disabled(!canAfford)
            }
        }
        .padding(10)
        .background(RoundedRectangle(cornerRadius: 12).fill(.white.opacity(0.04)))
    }
}
