package com.dotshelf.domain.readingrecord.service;

import java.util.List;

import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import com.dotshelf.domain.book.entity.UserBook;
import com.dotshelf.domain.book.repository.UserBookRepository;
import com.dotshelf.domain.book.service.BookService;
import com.dotshelf.domain.readingrecord.dto.ReadingRecordDtos.ReadingRecordListResponse;
import com.dotshelf.domain.readingrecord.dto.ReadingRecordDtos.ReadingRecordRequest;
import com.dotshelf.domain.readingrecord.dto.ReadingRecordDtos.ReadingRecordResponse;
import com.dotshelf.domain.readingrecord.entity.ReadingRecord;
import com.dotshelf.domain.readingrecord.entity.ReadingRecordLike;
import com.dotshelf.domain.readingrecord.repository.ReadingRecordLikeRepository;
import com.dotshelf.domain.readingrecord.repository.ReadingRecordRepository;
import com.dotshelf.domain.user.service.UserService;
import com.dotshelf.global.common.ToggleResponse;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;

import lombok.RequiredArgsConstructor;

@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class ReadingRecordService {

    private final ReadingRecordRepository readingRecordRepository;
    private final ReadingRecordLikeRepository likeRepository;
    private final UserBookRepository userBookRepository;
    private final BookService bookService;
    private final UserService userService;

    public ReadingRecordResponse getRecord(Long recordId) {
        return ReadingRecordResponse.from(getRecordEntity(recordId));
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

    public ReadingRecordListResponse getUserRecords(Long userId) {
        List<Long> userBookIds = userBookRepository.findByUserIdAndStorageStatusNot(userId, "DELETED").stream()
                .map(UserBook::getId)
                .toList();
        return new ReadingRecordListResponse(readingRecordRepository.findByUserBookIdInAndStatus(userBookIds, "ACTIVE").stream()
                .map(ReadingRecordResponse::from)
                .toList());
    }

    @Transactional
    public ReadingRecordResponse create(Long userId, ReadingRecordRequest request) {
        Long currentUserId = userService.requireUserId(userId);
        UserBook userBook = bookService.getUserBook(request.userBookId());
        if (!userBook.getUserId().equals(currentUserId)) {
            throw new CustomException(ErrorCode.FORBIDDEN);
        }
        ReadingRecord record = readingRecordRepository.findByUserBookId(request.userBookId())
                .map(existing -> {
                    existing.update(request.title(), request.content(), request.visibilityScope());
                    return existing;
                })
                .orElseGet(() -> readingRecordRepository.save(ReadingRecord.builder()
                        .userBookId(request.userBookId())
                        .title(request.title())
                        .content(request.content())
                        .visibilityScope(request.visibilityScope())
                        .build()));
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
        validateOwner(userId, recordId);
        getRecordEntity(recordId).delete();
    }

    @Transactional
    public ToggleResponse like(Long userId, Long recordId) {
        Long currentUserId = userService.requireUserId(userId);
        ReadingRecord record = getRecordEntity(recordId);
        if (!likeRepository.existsByReadingRecordIdAndUserId(recordId, currentUserId)) {
            likeRepository.save(ReadingRecordLike.builder()
                    .readingRecordId(recordId)
                    .userId(currentUserId)
                    .build());
            record.increaseLikeCount();
        }
        return new ToggleResponse(true, record.getLikeCount());
    }

    @Transactional
    public ToggleResponse unlike(Long userId, Long recordId) {
        Long currentUserId = userService.requireUserId(userId);
        ReadingRecord record = getRecordEntity(recordId);
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

    private void validateOwner(Long userId, Long recordId) {
        Long currentUserId = userService.requireUserId(userId);
        ReadingRecord record = getRecordEntity(recordId);
        UserBook userBook = bookService.getUserBook(record.getUserBookId());
        if (!userBook.getUserId().equals(currentUserId)) {
            throw new CustomException(ErrorCode.FORBIDDEN);
        }
    }
}
