import SwiftUI

/// 책 확대 상태에서 화면 하단에 뜨는 액션 바.
/// 공간 주인 전용 — 방문자용 "내 책장에 추가"는 권한 모델 확립(페이즈 E/F) 이후.
struct BookActionBar: View {
    let bookName: String
    let store: BookRecordStore

    /// 책장 목록 (index, display name). 책장 변경 메뉴에서 사용.
    let bookshelfNames: [String]
    let currentBookshelfIndex: Int

    var onChangePlacement: () -> Void
    var onChangeBookshelf: (Int) -> Void
    var onArchive: () -> Void
    var onDelete: () -> Void
    var onOpenReadingRecord: () -> Void

    @State private var showDeleteConfirm = false
    @State private var showArchiveConfirm = false

    private var record: BookRecord { store.record(for: bookName) }

    var body: some View {
        HStack(spacing: 4) {
            likeButton
            divider
            statusMenu
            divider
            iconButton(systemImage: "rotate.3d", tint: .primary, help: "배치 변경", action: onChangePlacement)
            bookshelfMenu
            iconButton(systemImage: "text.book.closed", tint: .primary, help: "독서기록", action: onOpenReadingRecord)
            divider
            iconButton(systemImage: "archivebox", tint: .secondary, help: "보관함으로") {
                showArchiveConfirm = true
            }
            iconButton(systemImage: "trash", tint: .red, help: "삭제") {
                showDeleteConfirm = true
            }
        }
        .padding(.horizontal, 10)
        .padding(.vertical, 6)
        .background(.ultraThinMaterial, in: Capsule())
        .shadow(color: .black.opacity(0.18), radius: 8, y: 3)
        .alert("이 책을 보관함으로 옮길까요?", isPresented: $showArchiveConfirm) {
            Button("취소", role: .cancel) {}
            Button("보관") { onArchive() }
        } message: {
            Text("책장에서 숨겨지고, 별점·독서기록·좋아요는 유지돼요.")
        }
        .alert("이 책을 삭제할까요?", isPresented: $showDeleteConfirm) {
            Button("취소", role: .cancel) {}
            Button("삭제", role: .destructive) { onDelete() }
        } message: {
            Text("책·독서기록·별점·좋아요가 모두 사라져요. 복구할 수 없습니다.")
        }
    }

    // MARK: - Like

    private var likeButton: some View {
        Button {
            store.update(bookName) { $0.liked.toggle() }
        } label: {
            Image(systemName: record.liked ? "heart.fill" : "heart")
                .font(.system(size: 15, weight: .semibold))
                .foregroundStyle(record.liked ? .red : .primary)
                .frame(width: 36, height: 36)
        }
        .buttonStyle(.plain)
        .help(record.liked ? "좋아요 취소" : "좋아요")
    }

    // MARK: - Reading status menu

    private var statusMenu: some View {
        Menu {
            ForEach(ReadingStatus.allCases, id: \.self) { s in
                Button {
                    store.update(bookName) { $0.status = s }
                } label: {
                    Label(s.label, systemImage: s.systemImage)
                }
            }
        } label: {
            HStack(spacing: 4) {
                Image(systemName: record.status.systemImage)
                    .font(.system(size: 13, weight: .bold))
                Text(record.status.label)
                    .font(.system(size: 12, weight: .semibold))
            }
            .foregroundStyle(record.status.tint)
            .padding(.horizontal, 8)
            .padding(.vertical, 6)
            .background(record.status.tint.opacity(0.15), in: Capsule())
        }
        .menuStyle(.borderlessButton)
        .menuIndicator(.hidden)
        .fixedSize()
        .help("독서 상태 변경")
    }

    // MARK: - Bookshelf menu

    private var bookshelfMenu: some View {
        Menu {
            if bookshelfNames.count <= 1 {
                Text("다른 책장이 없어요")
            } else {
                Section("다른 책장으로 이동") {
                    ForEach(Array(bookshelfNames.enumerated()), id: \.offset) { idx, name in
                        if idx != currentBookshelfIndex {
                            Button(name) { onChangeBookshelf(idx) }
                        }
                    }
                }
            }
        } label: {
            Image(systemName: "books.vertical")
                .font(.system(size: 15, weight: .semibold))
                .foregroundStyle(.primary)
                .frame(width: 36, height: 36)
        }
        .menuStyle(.borderlessButton)
        .menuIndicator(.hidden)
        .fixedSize()
        .help("책장 변경")
    }

    // MARK: - Generic icon button

    private func iconButton(
        systemImage: String,
        tint: Color,
        help: String,
        action: @escaping () -> Void
    ) -> some View {
        Button(action: action) {
            Image(systemName: systemImage)
                .font(.system(size: 15, weight: .semibold))
                .foregroundStyle(tint)
                .frame(width: 36, height: 36)
        }
        .buttonStyle(.plain)
        .help(help)
    }

    private var divider: some View {
        Rectangle()
            .fill(Color.primary.opacity(0.12))
            .frame(width: 1, height: 20)
            .padding(.horizontal, 2)
    }
}
