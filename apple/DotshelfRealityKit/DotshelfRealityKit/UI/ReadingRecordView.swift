import SwiftUI

/// 독서기록 2D 풀 오버레이 — 명세 §6.
/// 책 포커싱 뷰 위에 큰 오버레이로 뜨며, 본문 편집 / 공개 범위 / 댓글·대댓글을 다룸.
struct ReadingRecordView: View {
    let bookName: String
    let store: BookRecordStore
    var onDismiss: () -> Void

    // 편집 상태
    @State private var isEditing: Bool = false
    @State private var draft: String = ""
    @State private var showDeleteConfirm: Bool = false

    // 댓글 입력
    @State private var commentInput: String = ""
    @State private var replyingTo: BookComment?
    @State private var replyInput: String = ""

    private var record: BookRecord { store.record(for: bookName) }
    private var hasRecord: Bool { !record.readingRecord.isEmpty }

    // MVP — 권한 분기는 전역 플래그. 향후 페이즈 E/F에서 로그인/방문자 구분.
    private let isOwner: Bool = true

    var body: some View {
        ZStack {
            // 배경 — 탭 시 닫기
            Color.black.opacity(0.45)
                .ignoresSafeArea()
                .onTapGesture { requestDismiss() }

            card
                .frame(maxWidth: 760)
                .frame(maxHeight: .infinity)
                .padding(.vertical, 40)
                .padding(.horizontal, 24)
        }
        .alert("이 독서기록을 삭제할까요?", isPresented: $showDeleteConfirm) {
            Button("취소", role: .cancel) {}
            Button("삭제", role: .destructive) {
                store.setReadingRecord(bookName, text: "")
                isEditing = false
            }
        } message: {
            Text("댓글·대댓글도 함께 남아있지만 본문은 사라져요.")
        }
    }

    // MARK: - Card

    private var card: some View {
        VStack(spacing: 0) {
            header
            Divider()
            ScrollView {
                VStack(alignment: .leading, spacing: 20) {
                    bodySection
                    Divider()
                    recordLikeRow
                    Divider()
                    commentsSection
                }
                .padding(24)
            }
            Divider()
            commentComposer
                .padding(.horizontal, 16)
                .padding(.vertical, 12)
                .background(.regularMaterial)
        }
        .background(
            RoundedRectangle(cornerRadius: 22)
                .fill(.ultraThinMaterial)
                .shadow(color: .black.opacity(0.2), radius: 24, y: 8)
        )
        .clipShape(RoundedRectangle(cornerRadius: 22))
    }

    // MARK: - Header

    private var header: some View {
        HStack(spacing: 12) {
            Button(action: requestDismiss) {
                Image(systemName: "chevron.left")
                    .font(.system(size: 14, weight: .bold))
                    .foregroundStyle(.primary)
                    .frame(width: 32, height: 32)
                    .background(Color.gray.opacity(0.12), in: Circle())
            }
            .buttonStyle(.plain)
            .help("뒤로")

            VStack(alignment: .leading, spacing: 2) {
                Text(record.title)
                    .font(.system(size: 17, weight: .bold))
                    .lineLimit(1)
                Text("\(record.author) · \(record.publisher)")
                    .font(.system(size: 12))
                    .foregroundStyle(.secondary)
                    .lineLimit(1)
            }

            Spacer()

            if isOwner, hasRecord {
                visibilityMenu
            }
        }
        .padding(.horizontal, 20)
        .padding(.vertical, 14)
    }

    private var visibilityMenu: some View {
        Menu {
            ForEach(RecordVisibility.allCases, id: \.self) { v in
                Button {
                    store.setRecordVisibility(bookName, visibility: v)
                } label: {
                    Label(v.label, systemImage: v.systemImage)
                }
            }
        } label: {
            HStack(spacing: 5) {
                Image(systemName: record.recordVisibility.systemImage)
                    .font(.system(size: 11, weight: .bold))
                Text(record.recordVisibility.label)
                    .font(.system(size: 12, weight: .semibold))
            }
            .padding(.horizontal, 10)
            .padding(.vertical, 6)
            .background(Color.gray.opacity(0.1), in: Capsule())
            .foregroundStyle(.secondary)
        }
        .menuStyle(.borderlessButton)
        .menuIndicator(.hidden)
        .fixedSize()
    }

    // MARK: - Body (본문)

    @ViewBuilder
    private var bodySection: some View {
        if isEditing {
            editingBody
        } else if hasRecord {
            readOnlyBody
        } else {
            emptyBody
        }
    }

    private var readOnlyBody: some View {
        VStack(alignment: .leading, spacing: 14) {
            HStack(spacing: 6) {
                Image(systemName: "text.book.closed.fill")
                    .font(.system(size: 12, weight: .bold))
                Text("독서기록")
                    .font(.system(size: 12, weight: .semibold))
                Spacer()
                if isOwner {
                    Button {
                        draft = record.readingRecord
                        isEditing = true
                    } label: {
                        Label("수정", systemImage: "pencil")
                            .font(.system(size: 12, weight: .medium))
                    }
                    .buttonStyle(.plain)
                    .foregroundStyle(.secondary)
                    Button {
                        showDeleteConfirm = true
                    } label: {
                        Label("삭제", systemImage: "trash")
                            .font(.system(size: 12, weight: .medium))
                    }
                    .buttonStyle(.plain)
                    .foregroundStyle(.red)
                }
            }
            .foregroundStyle(.secondary)

            Text(record.readingRecord)
                .font(.system(size: 14, weight: .regular))
                .lineSpacing(5)
                .foregroundStyle(.primary)
                .frame(maxWidth: .infinity, alignment: .leading)
                .padding(16)
                .background(Color.yellow.opacity(0.08), in: RoundedRectangle(cornerRadius: 12))
                .overlay(
                    RoundedRectangle(cornerRadius: 12)
                        .strokeBorder(Color.yellow.opacity(0.22), lineWidth: 1)
                )
        }
    }

    private var emptyBody: some View {
        VStack(spacing: 10) {
            Image(systemName: "square.and.pencil")
                .font(.system(size: 30))
                .foregroundStyle(.secondary)
            Text("아직 이 책의 독서기록이 없어요")
                .font(.system(size: 14, weight: .semibold))
            Text("느낀 점을 길게 남겨두면 언제든 다시 꺼내볼 수 있어요.")
                .font(.system(size: 12))
                .foregroundStyle(.secondary)
                .multilineTextAlignment(.center)
            if isOwner {
                Button {
                    draft = ""
                    isEditing = true
                } label: {
                    Label("독서기록 작성", systemImage: "square.and.pencil")
                        .font(.system(size: 13, weight: .semibold))
                        .padding(.horizontal, 16)
                        .padding(.vertical, 9)
                        .background(Color.accentColor, in: Capsule())
                        .foregroundStyle(.white)
                }
                .buttonStyle(.plain)
                .padding(.top, 4)
            }
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, 28)
    }

    private var editingBody: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                Text(hasRecord ? "독서기록 수정" : "독서기록 작성")
                    .font(.system(size: 12, weight: .semibold))
                    .foregroundStyle(.secondary)
                Spacer()
                Text("\(draft.count) / 10,000")
                    .font(.system(size: 11, weight: .medium))
                    .foregroundStyle(draft.count > 10_000 ? .red : .secondary)
            }

            TextEditor(text: $draft)
                .font(.system(size: 14))
                .lineSpacing(4)
                .frame(minHeight: 180, maxHeight: 320)
                .padding(8)
                .scrollContentBackground(.hidden)
                .background(Color.yellow.opacity(0.06), in: RoundedRectangle(cornerRadius: 10))
                .overlay(
                    RoundedRectangle(cornerRadius: 10)
                        .strokeBorder(Color.yellow.opacity(0.25), lineWidth: 1)
                )
                .onChange(of: draft) { _, new in
                    if new.count > 10_000 { draft = String(new.prefix(10_000)) }
                }

            HStack(spacing: 10) {
                Spacer()
                Button("취소") {
                    isEditing = false
                    draft = ""
                }
                .buttonStyle(.plain)
                .foregroundStyle(.secondary)
                .padding(.horizontal, 14)
                .padding(.vertical, 7)
                .background(Color.gray.opacity(0.12), in: Capsule())

                Button {
                    store.setReadingRecord(bookName, text: draft)
                    isEditing = false
                } label: {
                    Text("저장")
                        .font(.system(size: 13, weight: .semibold))
                        .foregroundStyle(.white)
                        .padding(.horizontal, 16)
                        .padding(.vertical, 7)
                        .background(Color.accentColor, in: Capsule())
                }
                .buttonStyle(.plain)
                .disabled(draft.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
                .opacity(draft.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty ? 0.5 : 1)
            }
        }
    }

    // MARK: - 독서기록 좋아요 (방문자/주인 공통 — 자기 기록도 누를 수 있도록 MVP 허용)

    @ViewBuilder
    private var recordLikeRow: some View {
        if hasRecord {
            HStack(spacing: 10) {
                Button {
                    store.toggleRecordLike(bookName)
                } label: {
                    HStack(spacing: 6) {
                        Image(systemName: record.isRecordLikedByMe ? "heart.fill" : "heart")
                            .font(.system(size: 14, weight: .semibold))
                            .foregroundStyle(record.isRecordLikedByMe ? .red : .primary)
                        Text("\(record.recordLikes)")
                            .font(.system(size: 13, weight: .semibold))
                    }
                    .padding(.horizontal, 12)
                    .padding(.vertical, 6)
                    .background(Color.gray.opacity(0.1), in: Capsule())
                }
                .buttonStyle(.plain)

                Text("독서기록에 좋아요")
                    .font(.system(size: 12))
                    .foregroundStyle(.secondary)
                Spacer()
            }
        }
    }

    // MARK: - 댓글

    private var commentsSection: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack(spacing: 5) {
                Image(systemName: "bubble.left.fill")
                    .font(.system(size: 12, weight: .semibold))
                Text("댓글 \(totalCommentCount)")
                    .font(.system(size: 13, weight: .semibold))
            }
            .foregroundStyle(.secondary)

            if record.comments.isEmpty {
                Text("아직 댓글이 없어요. 첫 댓글을 남겨보세요.")
                    .font(.system(size: 12))
                    .foregroundStyle(.secondary)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .padding(.vertical, 6)
            } else {
                ForEach(record.comments) { c in
                    CommentThread(
                        comment: c,
                        onToggleLike: { store.toggleCommentLike(bookName, commentId: c.id) },
                        onToggleReplyLike: { replyId in
                            store.toggleCommentLike(bookName, commentId: replyId, parentCommentId: c.id)
                        },
                        onReply: {
                            replyingTo = c
                            replyInput = ""
                        }
                    )
                }
            }

            if let target = replyingTo {
                replyComposer(for: target)
            }
        }
    }

    private var totalCommentCount: Int {
        record.comments.reduce(0) { $0 + 1 + $1.replies.count }
    }

    private func replyComposer(for parent: BookComment) -> some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack(spacing: 4) {
                Image(systemName: "arrow.turn.down.right")
                    .font(.system(size: 10, weight: .bold))
                Text("\(parent.name) 님에게 답글")
                    .font(.system(size: 11, weight: .semibold))
                Spacer()
                Button {
                    replyingTo = nil
                    replyInput = ""
                } label: {
                    Image(systemName: "xmark")
                        .font(.system(size: 10, weight: .bold))
                        .foregroundStyle(.secondary)
                }
                .buttonStyle(.plain)
            }
            .foregroundStyle(.secondary)

            HStack(spacing: 8) {
                TextField("답글 입력…", text: $replyInput)
                    .textFieldStyle(.plain)
                    .font(.system(size: 12))
                    .padding(.horizontal, 10)
                    .padding(.vertical, 7)
                    .background(Color.gray.opacity(0.1), in: Capsule())
                Button {
                    store.addReply(bookName, parentCommentId: parent.id, message: replyInput)
                    replyInput = ""
                    replyingTo = nil
                } label: {
                    Image(systemName: "paperplane.fill")
                        .font(.system(size: 11, weight: .semibold))
                        .foregroundStyle(.white)
                        .padding(7)
                        .background(
                            Circle().fill(replyInput.trimmingCharacters(in: .whitespaces).isEmpty
                                          ? Color.gray.opacity(0.4)
                                          : Color.accentColor)
                        )
                }
                .buttonStyle(.plain)
                .disabled(replyInput.trimmingCharacters(in: .whitespaces).isEmpty)
            }
        }
        .padding(10)
        .background(Color.accentColor.opacity(0.06), in: RoundedRectangle(cornerRadius: 10))
        .overlay(
            RoundedRectangle(cornerRadius: 10)
                .strokeBorder(Color.accentColor.opacity(0.2), lineWidth: 1)
        )
        .padding(.leading, 38)
    }

    private var commentComposer: some View {
        HStack(spacing: 8) {
            TextField("댓글 달기…", text: $commentInput)
                .textFieldStyle(.plain)
                .font(.system(size: 13))
                .padding(.horizontal, 12)
                .padding(.vertical, 8)
                .background(Color.gray.opacity(0.12), in: Capsule())
            Button {
                store.addComment(bookName, message: commentInput)
                commentInput = ""
            } label: {
                Image(systemName: "paperplane.fill")
                    .font(.system(size: 13, weight: .semibold))
                    .foregroundStyle(.white)
                    .padding(9)
                    .background(
                        Circle().fill(commentInput.trimmingCharacters(in: .whitespaces).isEmpty
                                      ? Color.gray.opacity(0.4)
                                      : Color.accentColor)
                    )
            }
            .buttonStyle(.plain)
            .disabled(commentInput.trimmingCharacters(in: .whitespaces).isEmpty)
        }
    }

    // MARK: - 닫기 제어

    private func requestDismiss() {
        // 편집 중이면 작성자 의도 확인 없이 닫지 않음 — 드래프트를 잃지 않도록 단순히 편집 해제만.
        if isEditing {
            isEditing = false
            draft = ""
        }
        onDismiss()
    }
}

// MARK: - 댓글 한 스레드 (댓글 + 대댓글 리스트)

private struct CommentThread: View {
    let comment: BookComment
    var onToggleLike: () -> Void
    var onToggleReplyLike: (UUID) -> Void
    var onReply: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            CommentRow(
                comment: comment,
                onToggleLike: onToggleLike,
                showReplyButton: true,
                onReply: onReply
            )
            if !comment.replies.isEmpty {
                VStack(alignment: .leading, spacing: 6) {
                    ForEach(comment.replies) { r in
                        CommentRow(
                            comment: r,
                            onToggleLike: { onToggleReplyLike(r.id) },
                            showReplyButton: false,
                            onReply: {}
                        )
                    }
                }
                .padding(.leading, 38)
            }
        }
    }
}

private struct CommentRow: View {
    let comment: BookComment
    var onToggleLike: () -> Void
    var showReplyButton: Bool
    var onReply: () -> Void

    var body: some View {
        HStack(alignment: .top, spacing: 10) {
            Circle()
                .fill(comment.avatarColor.gradient)
                .overlay(
                    Text(comment.initials)
                        .font(.system(size: 11, weight: .bold))
                        .foregroundStyle(.white)
                )
                .frame(width: 28, height: 28)

            VStack(alignment: .leading, spacing: 4) {
                HStack(spacing: 5) {
                    Text(comment.name)
                        .font(.system(size: 12, weight: .semibold))
                    Text("·")
                        .foregroundStyle(.tertiary)
                    Text(comment.time)
                        .font(.system(size: 10))
                        .foregroundStyle(.secondary)
                }
                Text(comment.message)
                    .font(.system(size: 13))
                    .foregroundStyle(.primary.opacity(0.9))

                HStack(spacing: 12) {
                    Button(action: onToggleLike) {
                        HStack(spacing: 4) {
                            Image(systemName: comment.isLikedByMe ? "heart.fill" : "heart")
                                .font(.system(size: 10, weight: .bold))
                                .foregroundStyle(comment.isLikedByMe ? .red : .secondary)
                            if comment.likes > 0 {
                                Text("\(comment.likes)")
                                    .font(.system(size: 10, weight: .semibold))
                                    .foregroundStyle(.secondary)
                            }
                        }
                    }
                    .buttonStyle(.plain)

                    if showReplyButton {
                        Button(action: onReply) {
                            HStack(spacing: 4) {
                                Image(systemName: "arrow.turn.down.right")
                                    .font(.system(size: 10, weight: .bold))
                                Text("답글")
                                    .font(.system(size: 10, weight: .semibold))
                            }
                            .foregroundStyle(.secondary)
                        }
                        .buttonStyle(.plain)
                    }

                    Menu {
                        Button("신고", role: .destructive) { /* TODO */ }
                        Button("작성자 차단", role: .destructive) { /* TODO */ }
                    } label: {
                        Image(systemName: "ellipsis")
                            .font(.system(size: 11, weight: .bold))
                            .foregroundStyle(.secondary)
                    }
                    .menuStyle(.borderlessButton)
                    .menuIndicator(.hidden)
                    .fixedSize()
                }
                .padding(.top, 2)
            }
            Spacer(minLength: 0)
        }
        .padding(10)
        .background(Color.gray.opacity(0.06), in: RoundedRectangle(cornerRadius: 10))
    }
}
