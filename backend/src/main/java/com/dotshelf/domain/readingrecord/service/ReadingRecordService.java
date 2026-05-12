package com.dotshelf.domain.readingrecord.service;

import java.util.Comparator;
import java.util.List;

import org.springframework.context.ApplicationEventPublisher;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import com.dotshelf.domain.book.entity.UserBook;
import com.dotshelf.domain.book.service.BookService;
import com.dotshelf.domain.space.service.SpaceService;
import com.dotshelf.domain.notification.event.NotificationEvents.ReadingRecordLiked;
import com.dotshelf.domain.readingrecord.dto.ReadingRecordDtos.ReadingRecordPageResponse;
import com.dotshelf.domain.readingrecord.dto.ReadingRecordDtos.ReadingRecordRequest;
import com.dotshelf.domain.readingrecord.dto.ReadingRecordDtos.ReadingRecordResponse;
import com.dotshelf.domain.readingrecord.dto.ReadingRecordDtos.ReadingRecordSummary;
import com.dotshelf.domain.readingrecord.entity.ReadingRecord;
import com.dotshelf.domain.readingrecord.entity.ReadingRecordLike;
import com.dotshelf.domain.readingrecord.repository.ReadingRecordLikeRepository;
import com.dotshelf.domain.readingrecord.repository.ReadingRecordRepository;
import com.dotshelf.domain.user.repository.FollowRepository;
import com.dotshelf.domain.user.service.UserService;
import com.dotshelf.global.common.CursorParser;
import com.dotshelf.global.common.ToggleResponse;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;

import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;

@Service
@Slf4j
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class ReadingRecordService {

    private static final int DEFAULT_RECORD_LIMIT = 20;
    private static final int MAX_RECORD_LIMIT = 50;
    private static final int MEMORY_SORT_WARNING_THRESHOLD = 500;

    private final ReadingRecordRepository readingRecordRepository;
    private final ReadingRecordLikeRepository likeRepository;
    private final BookService bookService;
    private final SpaceService spaceService;
    private final UserService userService;
    private final FollowRepository followRepository;
    private final ApplicationEventPublisher eventPublisher;

    public ReadingRecordResponse getRecord(Long userId, Long recordId) {
        return ReadingRecordResponse.from(getReadableRecord(userId, recordId));
    }

    public ReadingRecordResponse getMyRecordByUserBook(Long userId, Long userBookId) {
        Long currentUserId = userService.requireUserId(userId);
        UserBook userBook = bookService.getUserBook(userBookId);
        if (!userBook.getUserId().equals(currentUserId)) {
            throw new CustomException(ErrorCode.FORBIDDEN);
        }
        return ReadingRecordResponse.from(readingRecordRepository.findByUserBookId(userBookId)
                .filter(record -> "ACTIVE".equals(record.getStatus()))
                .orElse(null));
    }

    /**
     * 대시보드 \"독후감 풀 목록\" 모달용. v1.0 갱신 — 정렬·cursor 페이지네이션 합류.
     * 보유 책 정렬과 동일하게 사용자당 ~수십건 가정으로 메모리 정렬·슬라이싱.
     */
    public ReadingRecordPageResponse getUserRecords(Long viewerUserId, Long ownerUserId, String sort, String cursor, int limit) {
        int validLimit = clampLimit(limit);
        Long cursorId = CursorParser.parseId(cursor);

        List<ReadingRecordSummary> all = readingRecordRepository.findUserRecordSummaries(ownerUserId);
        // (MR #61 리뷰 #5) 메모리 정렬 fallback 한도. 가정 무너지면 즉시 경고.
        if (all.size() > MEMORY_SORT_WARNING_THRESHOLD) {
            log.warn("[ReadingRecords] memory-sort threshold 초과: userId={} count={}. server-side ORDER BY 전환 검토 필요.",
                    ownerUserId, all.size());
        }
        boolean owner = ownerUserId.equals(viewerUserId);
        boolean follower = viewerUserId != null
                && !owner
                && followRepository.existsByFollowerUserIdAndFollowingUserId(viewerUserId, ownerUserId);
        List<ReadingRecordSummary> visible = all.stream()
                .filter(record -> isReadableVisibility(owner, follower, record.visibilityScope()))
                .toList();
        List<ReadingRecordSummary> sorted = sortRecords(visible, sort);

        int startIdx = 0;
        if (cursorId != null) {
            for (int i = 0; i < sorted.size(); i++) {
                if (cursorId.equals(sorted.get(i).id())) {
                    startIdx = i + 1;
                    break;
                }
            }
        }
        int endIdx = Math.min(startIdx + validLimit, sorted.size());
        boolean hasMore = endIdx < sorted.size();
        String nextCursor = hasMore ? String.valueOf(sorted.get(endIdx - 1).id()) : null;
        return new ReadingRecordPageResponse(sorted.subList(startIdx, endIdx), nextCursor);
    }

    private static List<ReadingRecordSummary> sortRecords(List<ReadingRecordSummary> items, String sort) {
        Comparator<ReadingRecordSummary> primary = switch (sort == null ? "LATEST" : sort.toUpperCase()) {
            case "OLDEST" ->
                    Comparator.comparing(ReadingRecordSummary::createdAt,
                            Comparator.nullsLast(Comparator.naturalOrder()));
            case "MOST_LIKED" ->
                    Comparator.comparingInt(ReadingRecordSummary::likeCount).reversed();
            case "MOST_VIEWED" ->
                    Comparator.comparingInt(ReadingRecordSummary::viewCount).reversed();
            case "MOST_COMMENTED" ->
                    Comparator.comparingInt(ReadingRecordSummary::commentCount).reversed();
            default ->
                    Comparator.comparing(ReadingRecordSummary::createdAt,
                            Comparator.nullsLast(Comparator.reverseOrder()));
        };
        Comparator<ReadingRecordSummary> tiebreaker = Comparator.comparing(
                ReadingRecordSummary::id, Comparator.reverseOrder());
        return items.stream().sorted(primary.thenComparing(tiebreaker)).toList();
    }

    private static int clampLimit(int limit) {
        if (limit <= 0) {
            return DEFAULT_RECORD_LIMIT;
        }
        return Math.min(limit, MAX_RECORD_LIMIT);
    }


    @Transactional
    public ReadingRecordResponse create(Long userId, ReadingRecordRequest request) {
        Long currentUserId = userService.requireUserId(userId);
        UserBook userBook = bookService.getUserBook(request.userBookId());
        if (!userBook.getUserId().equals(currentUserId)) {
            throw new CustomException(ErrorCode.FORBIDDEN);
        }
        var existing = readingRecordRepository.findByUserBookId(request.userBookId());
        ReadingRecord record;
        if (existing.isPresent()) {
            record = existing.get();
            record.update(request.title(), request.content(), request.visibilityScope());
        } else {
            record = readingRecordRepository.save(ReadingRecord.builder()
                    .userBookId(request.userBookId())
                    .title(request.title())
                    .content(request.content())
                    .visibilityScope(request.visibilityScope())
                    .build());
            spaceService.findOrCreateMySpace(currentUserId).increaseReadingRecordCount();
        }
        return ReadingRecordResponse.from(record);
    }

    @Transactional
    public ReadingRecordResponse update(Long userId, Long recordId, ReadingRecordRequest request) {
        validateOwner(userId, recordId);
        ReadingRecord record = getRecordEntity(recordId);
        record.update(request.title(), request.content(), request.visibilityScope());
        return ReadingRecordResponse.from(record);
    }

    @Transactional
    public void delete(Long userId, Long recordId) {
        Long currentUserId = userService.requireUserId(userId);
        validateOwner(currentUserId, recordId);
        getRecordEntity(recordId).delete();
        spaceService.findOrCreateMySpace(currentUserId).decreaseReadingRecordCount();
    }

    @Transactional
    public ToggleResponse like(Long userId, Long recordId) {
        Long currentUserId = userService.requireUserId(userId);
        ReadingRecord record = getReadableRecord(currentUserId, recordId);
        boolean createdLike = false;
        if (!likeRepository.existsByReadingRecordIdAndUserId(recordId, currentUserId)) {
            likeRepository.save(ReadingRecordLike.builder()
                    .readingRecordId(recordId)
                    .userId(currentUserId)
                    .build());
            record.increaseLikeCount();
            createdLike = true;
        }
        if (createdLike) {
            eventPublisher.publishEvent(new ReadingRecordLiked(getRecordOwnerUserId(record), currentUserId, recordId));
        }
        return new ToggleResponse(true, record.getLikeCount());
    }

    @Transactional
    public ToggleResponse unlike(Long userId, Long recordId) {
        Long currentUserId = userService.requireUserId(userId);
        ReadingRecord record = getReadableRecord(currentUserId, recordId);
        if (likeRepository.existsByReadingRecordIdAndUserId(recordId, currentUserId)) {
            likeRepository.deleteByReadingRecordIdAndUserId(recordId, currentUserId);
            record.decreaseLikeCount();
        }
        return new ToggleResponse(false, record.getLikeCount());
    }

    public ReadingRecord getRecordEntity(Long recordId) {
        return readingRecordRepository.findById(recordId)
                .orElseThrow(() -> new CustomException(ErrorCode.READING_RECORD_NOT_FOUND));
    }

    public ReadingRecord getReadableRecord(Long viewerUserId, Long recordId) {
        ReadingRecord record = getActiveRecordEntity(recordId);
        requireReadable(viewerUserId, record);
        return record;
    }

    public Long getRecordOwnerUserId(ReadingRecord record) {
        return bookService.getUserBook(record.getUserBookId()).getUserId();
    }

    private ReadingRecord getActiveRecordEntity(Long recordId) {
        ReadingRecord record = getRecordEntity(recordId);
        if (!"ACTIVE".equals(record.getStatus())) {
            throw new CustomException(ErrorCode.READING_RECORD_NOT_FOUND);
        }
        return record;
    }

    private void requireReadable(Long viewerUserId, ReadingRecord record) {
        UserBook userBook = bookService.getUserBook(record.getUserBookId());
        boolean owner = userBook.getUserId().equals(viewerUserId);
        boolean follower = !owner
                && viewerUserId != null
                && followRepository.existsByFollowerUserIdAndFollowingUserId(viewerUserId, userBook.getUserId());
        if (isReadableVisibility(owner, follower, record.getVisibilityScope())) {
            return;
        }
        throw new CustomException(ErrorCode.FORBIDDEN);
    }

    private static boolean isReadableVisibility(boolean owner, boolean follower, String visibilityScope) {
        if (owner) {
            return true;
        }
        if ("PUBLIC".equalsIgnoreCase(visibilityScope)) {
            return true;
        }
        return "FOLLOWERS".equalsIgnoreCase(visibilityScope) && follower;
    }

    private void validateOwner(Long userId, Long recordId) {
        Long currentUserId = userService.requireUserId(userId);
        ReadingRecord record = getRecordEntity(recordId);
        UserBook userBook = bookService.getUserBook(record.getUserBookId());
        if (!userBook.getUserId().equals(currentUserId)) {
            throw new CustomException(ErrorCode.FORBIDDEN);
        }
    }
}
