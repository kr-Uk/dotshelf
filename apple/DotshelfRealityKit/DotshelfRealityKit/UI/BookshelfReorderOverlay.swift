import SwiftUI

/// 책장 순서 변경 오버레이 — 페이즈 C 스텝 5.
/// 위/아래 버튼으로 한 칸씩 이동. 현재 책장은 원형 배지로 표시.
struct BookshelfReorderOverlay: View {
    let state: BookshelfState
    var onMove: (_ from: Int, _ to: Int) -> Void
    var onClose: () -> Void

    var body: some View {
        ZStack {
            Color.black.opacity(0.4).ignoresSafeArea()
                .onTapGesture { onClose() }

            VStack(spacing: 0) {
                header
                Divider().opacity(0.3)
                ScrollView {
                    VStack(spacing: 8) {
                        ForEach(Array(state.bookshelves.enumerated()), id: \.offset) { idx, shelf in
                            row(idx: idx, shelf: shelf)
                        }
                    }
                    .padding(16)
                }
            }
            .frame(maxWidth: 440, maxHeight: 520)
            .background(
                RoundedRectangle(cornerRadius: 22).fill(.ultraThinMaterial)
            )
            .clipShape(RoundedRectangle(cornerRadius: 22))
            .shadow(color: .black.opacity(0.25), radius: 22, y: 10)
            .padding(30)
        }
    }

    private var header: some View {
        HStack {
            Image(systemName: "arrow.up.arrow.down")
                .font(.system(size: 14, weight: .bold))
                .foregroundStyle(.tint)
            Text("책장 순서")
                .font(.system(size: 16, weight: .bold))
            Spacer()
            Button(action: onClose) {
                Image(systemName: "xmark.circle.fill")
                    .font(.system(size: 20))
                    .foregroundStyle(.secondary, .quaternary)
            }
            .buttonStyle(.plain)
        }
        .padding(.horizontal, 18)
        .padding(.vertical, 14)
    }

    private func bookCount(for shelf: Bookshelf) -> Int {
        shelf.sections.reduce(0) { $0 + $1.books.count }
    }

    @ViewBuilder
    private func row(idx: Int, shelf: Bookshelf) -> some View {
        let isCurrent = idx == state.currentIndex
        let atTop = idx == 0
        let atBottom = idx == state.bookshelves.count - 1

        HStack(spacing: 10) {
            // 현재 책장 배지
            Image(systemName: isCurrent ? "circle.inset.filled" : "circle")
                .font(.system(size: 13, weight: .bold))
                .foregroundStyle(isCurrent ? Color.accentColor : Color.secondary)
                .frame(width: 20)

            VStack(alignment: .leading, spacing: 2) {
                Text(shelf.name)
                    .font(.system(size: 14, weight: .semibold))
                Text("\(bookCount(for: shelf))권 · \(shelf.visibility.label)")
                    .font(.system(size: 11))
                    .foregroundStyle(.secondary)
            }

            Spacer()

            Button {
                onMove(idx, idx - 1)
            } label: {
                Image(systemName: "arrow.up")
                    .font(.system(size: 12, weight: .bold))
                    .foregroundStyle(atTop ? .secondary : .primary)
                    .frame(width: 30, height: 30)
                    .background(Color.gray.opacity(atTop ? 0.04 : 0.12), in: Circle())
            }
            .buttonStyle(.plain)
            .disabled(atTop)

            Button {
                onMove(idx, idx + 1)
            } label: {
                Image(systemName: "arrow.down")
                    .font(.system(size: 12, weight: .bold))
                    .foregroundStyle(atBottom ? .secondary : .primary)
                    .frame(width: 30, height: 30)
                    .background(Color.gray.opacity(atBottom ? 0.04 : 0.12), in: Circle())
            }
            .buttonStyle(.plain)
            .disabled(atBottom)
        }
        .padding(10)
        .background(
            RoundedRectangle(cornerRadius: 10)
                .fill(isCurrent ? Color.accentColor.opacity(0.08) : Color.gray.opacity(0.05))
        )
        .overlay(
            RoundedRectangle(cornerRadius: 10)
                .strokeBorder(isCurrent ? Color.accentColor.opacity(0.25) : .clear, lineWidth: 1)
        )
    }
}
