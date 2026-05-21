import SwiftUI

/// 책 확대 상태에서 우측에 붙는 독서 정보 패널.
/// 모델은 BookRecord / 저장소는 BookRecordStore.
struct BookReadingPanel: View {
    let bookName: String
    let store: BookRecordStore
    var onOpenReadingRecord: () -> Void = {}

    @State private var commentInput: String = ""

    private var record: BookRecord { store.record(for: bookName) }

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            ScrollView {
                VStack(alignment: .leading, spacing: 18) {
                    bookInfoSection
                    Divider()
                    readingRecordSection
                    Divider()
                    readingRecordCTA
                    Divider()
                    commentsSection
                }
                .padding(20)
            }
            commentComposer
                .padding(.horizontal, 16)
                .padding(.vertical, 12)
                .background(.regularMaterial)
        }
        .frame(width: 380)
        .frame(maxHeight: 560)
        .background(
            RoundedRectangle(cornerRadius: 18)
                .fill(.ultraThinMaterial)
                .shadow(color: .black.opacity(0.18), radius: 18, y: 6)
        )
        .clipShape(RoundedRectangle(cornerRadius: 18))
    }

    // MARK: - 책 정보 (제목/저자/별점 편집)

    private var bookInfoSection: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack(alignment: .top) {
                VStack(alignment: .leading, spacing: 4) {
                    Text(record.title)
                        .font(.system(size: 22, weight: .bold))
                    Text(record.author)
                        .font(.system(size: 13, weight: .medium))
                        .foregroundStyle(.secondary)
                }
                Spacer()
                if record.liked {
                    Image(systemName: "heart.fill")
                        .font(.system(size: 18, weight: .bold))
                        .foregroundStyle(.red)
                }
            }
            EditableStarRating(rating: record.rating) { newValue in
                store.update(bookName) { $0.rating = newValue }
            }
        }
    }

    // MARK: - 독서 상태 + 날짜 + 메모

    private var readingRecordSection: some View {
        VStack(alignment: .leading, spacing: 12) {
            sectionHeader("독서 기록", systemImage: "book.closed.fill")

            ReadingStatusPicker(status: record.status) { newStatus in
                store.update(bookName) { $0.status = newStatus }
            }

            if record.startedAt != nil || record.finishedAt != nil {
                HStack(spacing: 10) {
                    if let start = record.startedAt {
                        dateChip(label: "시작", value: start)
                    }
                    if let finish = record.finishedAt {
                        dateChip(label: "완독", value: finish)
                    }
                }
            }

            if !record.personalNote.isEmpty {
                Text(record.personalNote)
                    .font(.system(size: 12))
                    .italic()
                    .foregroundStyle(.primary.opacity(0.85))
                    .padding(10)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .background(
                        RoundedRectangle(cornerRadius: 10)
                            .fill(Color.yellow.opacity(0.12))
                    )
                    .overlay(
                        RoundedRectangle(cornerRadius: 10)
                            .strokeBorder(Color.yellow.opacity(0.3), lineWidth: 1)
                    )
            }
        }
    }

    // MARK: - 독서기록 화면 진입 CTA (페이즈 B 연결)

    private var readingRecordCTA: some View {
        Button(action: onOpenReadingRecord) {
            HStack {
                VStack(alignment: .leading, spacing: 2) {
                    Text("독서기록 보기")
                        .font(.system(size: 13, weight: .semibold))
                    Text("감상을 길게 기록하고 댓글로 이야기해요")
                        .font(.system(size: 11))
                        .foregroundStyle(.secondary)
                }
                Spacer()
                Image(systemName: "chevron.right")
                    .font(.system(size: 12, weight: .semibold))
                    .foregroundStyle(.secondary)
            }
            .padding(12)
            .background(
                RoundedRectangle(cornerRadius: 10)
                    .fill(Color.accentColor.opacity(0.08))
            )
            .overlay(
                RoundedRectangle(cornerRadius: 10)
                    .strokeBorder(Color.accentColor.opacity(0.25), lineWidth: 1)
            )
        }
        .buttonStyle(.plain)
    }

    // MARK: - 댓글

    private var commentsSection: some View {
        VStack(alignment: .leading, spacing: 10) {
            sectionHeader("댓글 \(record.comments.count)", systemImage: "bubble.left.fill")
            VStack(spacing: 8) {
                ForEach(record.comments) { comment in
                    CommentRow(comment: comment)
                }
            }
        }
    }

    private var commentComposer: some View {
        HStack(spacing: 8) {
            TextField("댓글 달기…", text: $commentInput)
                .textFieldStyle(.plain)
                .font(.system(size: 13))
                .padding(.horizontal, 12)
                .padding(.vertical, 8)
                .background(Color.gray.opacity(0.12), in: Capsule())
            Button(action: addComment) {
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

    // MARK: - Helpers

    private func sectionHeader(_ text: String, systemImage: String) -> some View {
        HStack(spacing: 5) {
            Image(systemName: systemImage)
                .font(.system(size: 11, weight: .semibold))
            Text(text)
                .font(.system(size: 12, weight: .semibold))
        }
        .foregroundStyle(.secondary)
    }

    private func dateChip(label: String, value: String) -> some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(label)
                .font(.system(size: 10, weight: .semibold))
                .foregroundStyle(.secondary)
            Text(value)
                .font(.system(size: 12, weight: .medium))
        }
        .padding(.horizontal, 10)
        .padding(.vertical, 6)
        .background(Color.gray.opacity(0.12), in: RoundedRectangle(cornerRadius: 8))
    }

    private func addComment() {
        let trimmed = commentInput.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return }
        let new = BookComment(
            name: "나",
            initials: "나",
            avatarColor: .accentColor,
            time: "방금 전",
            message: trimmed
        )
        store.update(bookName) { $0.comments.append(new) }
        commentInput = ""
    }
}

// MARK: - 편집 가능한 별점 (0.5 단위)

struct EditableStarRating: View {
    let rating: Double
    var onChange: (Double) -> Void

    var body: some View {
        HStack(spacing: 2) {
            ForEach(0..<5) { i in
                starGlyph(for: i)
                    .font(.system(size: 16))
                    .foregroundStyle(.yellow)
                    .contentShape(Rectangle())
                    .overlay {
                        // 좌/우 반쪽 탭 영역
                        HStack(spacing: 0) {
                            Color.clear.contentShape(Rectangle())
                                .onTapGesture { tap(star: i, half: true) }
                            Color.clear.contentShape(Rectangle())
                                .onTapGesture { tap(star: i, half: false) }
                        }
                    }
            }
            Text(String(format: "%.1f", rating))
                .font(.system(size: 12, weight: .semibold))
                .foregroundStyle(.secondary)
                .padding(.leading, 4)
        }
    }

    private func starGlyph(for i: Int) -> Image {
        let fill = max(0, min(1, rating - Double(i)))
        if fill >= 0.9 { return Image(systemName: "star.fill") }
        if fill >= 0.3 { return Image(systemName: "star.leadinghalf.filled") }
        return Image(systemName: "star")
    }

    private func tap(star i: Int, half: Bool) {
        let newValue = Double(i) + (half ? 0.5 : 1.0)
        // 같은 값 두 번 누르면 그 별을 해제 (반 단계씩 줄어드는 느낌)
        if abs(newValue - rating) < 0.01 {
            onChange(max(0, newValue - 0.5))
        } else {
            onChange(newValue)
        }
    }
}

// MARK: - 독서 상태 세그먼트 피커

struct ReadingStatusPicker: View {
    let status: ReadingStatus
    var onChange: (ReadingStatus) -> Void

    var body: some View {
        HStack(spacing: 6) {
            ForEach(ReadingStatus.allCases, id: \.self) { s in
                pickerItem(for: s)
            }
        }
    }

    @ViewBuilder
    private func pickerItem(for s: ReadingStatus) -> some View {
        let selected = (s == status)
        let bgFill: Color = selected ? s.tint.opacity(0.2) : Color.gray.opacity(0.08)
        let borderColor: Color = selected ? s.tint.opacity(0.45) : Color.clear
        let labelColor: Color = selected ? s.tint : .primary

        Button {
            onChange(s)
        } label: {
            HStack(spacing: 4) {
                Image(systemName: s.systemImage)
                    .font(.system(size: 11, weight: .bold))
                Text(s.label)
                    .font(.system(size: 12, weight: .semibold))
            }
            .padding(.horizontal, 10)
            .padding(.vertical, 6)
            .frame(maxWidth: .infinity)
            .background(RoundedRectangle(cornerRadius: 8).fill(bgFill))
            .overlay(RoundedRectangle(cornerRadius: 8).strokeBorder(borderColor, lineWidth: 1))
            .foregroundStyle(labelColor)
        }
        .buttonStyle(.plain)
    }
}

// MARK: - 댓글 row

private struct CommentRow: View {
    let comment: BookComment

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

            VStack(alignment: .leading, spacing: 3) {
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
                    .font(.system(size: 12))
                    .foregroundStyle(.primary.opacity(0.85))
            }
            Spacer(minLength: 0)
        }
        .padding(10)
        .background(Color.gray.opacity(0.08), in: RoundedRectangle(cornerRadius: 10))
    }
}
