package com.dotshelf.domain.guestbook.service;

import org.springframework.data.domain.PageRequest;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.List;

import com.dotshelf.domain.guestbook.dto.GuestbookDtos.GuestbookListResponse;
import com.dotshelf.domain.guestbook.dto.GuestbookDtos.GuestbookReplyRequest;
import com.dotshelf.domain.guestbook.dto.GuestbookDtos.GuestbookReplyResponse;
import com.dotshelf.domain.guestbook.dto.GuestbookDtos.GuestbookRequest;
import com.dotshelf.domain.guestbook.dto.GuestbookDtos.GuestbookResponse;
import com.dotshelf.domain.guestbook.entity.Guestbook;
import com.dotshelf.domain.guestbook.entity.GuestbookReply;
import com.dotshelf.domain.guestbook.repository.GuestbookReplyRepository;
import com.dotshelf.domain.guestbook.repository.GuestbookRepository;
import com.dotshelf.domain.space.entity.Space;
import com.dotshelf.domain.space.service.SpaceService;
import com.dotshelf.domain.user.entity.User;
import com.dotshelf.domain.user.repository.UserBlockRepository;
import com.dotshelf.domain.user.service.UserService;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;

import lombok.RequiredArgsConstructor;

@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class GuestbookService {

    private final GuestbookRepository guestbookRepository;
    private final GuestbookReplyRepository replyRepository;
    private final SpaceService spaceService;
    private final UserService userService;
    private final UserBlockRepository userBlockRepository;

    private static final int DEFAULT_GUESTBOOK_LIMIT = 15;
    private static final int MAX_GUESTBOOK_LIMIT = 50;

    public GuestbookListResponse getGuestbooks(Long currentUserId, Long spaceId, String cursor, int limit) {
        int validLimit = clampLimit(limit, DEFAULT_GUESTBOOK_LIMIT, MAX_GUESTBOOK_LIMIT);
        Long cursorId = parseCursorId(cursor);
        List<Guestbook> page = guestbookRepository.findGuestbookPage(
                spaceId, "ACTIVE", cursorId, PageRequest.of(0, validLimit + 1));
        boolean hasMore = page.size() > validLimit;
        List<Guestbook> trimmed = hasMore ? page.subList(0, validLimit) : page;
        String nextCursor = hasMore ? String.valueOf(trimmed.get(trimmed.size() - 1).getId()) : null;
        List<GuestbookResponse> responses = trimmed.stream()
                .map(gb -> buildGuestbookResponse(gb, currentUserId))
                .toList();
        return new GuestbookListResponse(responses, nextCursor);
    }

    private static int clampLimit(int limit, int defaultLimit, int max) {
        if (limit <= 0) return defaultLimit;
        return Math.min(limit, max);
    }

    private static Long parseCursorId(String cursor) {
        if (cursor == null || cursor.isBlank()) return null;
        try {
            return Long.parseLong(cursor);
        } catch (NumberFormatException e) {
            return null;
        }
    }

    @Transactional
    public GuestbookResponse create(Long userId, Long spaceId, GuestbookRequest request) {
        Long currentUserId = userService.requireUserId(userId);
        Space space = spaceService.getSpaceEntity(spaceId);
        if (space.getOwnerUserId().equals(currentUserId)) {
            throw new CustomException(ErrorCode.SPACE_OWNER_CANNOT_POST);
        }
        if (isBlockedRelation(space.getOwnerUserId(), currentUserId)) {
            throw new CustomException(ErrorCode.SPACE_ACCESS_BLOCKED);
        }
        if (!spaceService.isPostItAllowed(spaceId, request.postItInventoryId())) {
            throw new CustomException(ErrorCode.GUESTBOOK_POSTIT_NOT_ALLOWED);
        }
        Guestbook guestbook = guestbookRepository.save(Guestbook.builder()
                .spaceId(spaceId)
                .writerUserId(currentUserId)
                .postItInventoryId(request.postItInventoryId())
                .content(request.content())
                .visibilityScope(request.visibilityScope())
                .anonymous(request.anonymous())
                .build());
        return buildGuestbookResponse(guestbook, currentUserId);
    }

    @Transactional
    public void delete(Long userId, Long guestbookId) {
        Long currentUserId = userService.requireUserId(userId);
        Guestbook guestbook = getGuestbook(guestbookId);
        if (!guestbook.getWriterUserId().equals(currentUserId)) {
            throw new CustomException(ErrorCode.FORBIDDEN);
        }
        guestbook.delete();
    }

    @Transactional
    public void deleteReply(Long userId, Long guestbookId, Long replyId) {
        Long currentUserId = userService.requireUserId(userId);
        GuestbookReply reply = replyRepository.findById(replyId)
                .orElseThrow(() -> new CustomException(ErrorCode.GUESTBOOK_REPLY_NOT_FOUND));
        if (!reply.getGuestbookId().equals(guestbookId) || reply.getDeletedAt() != null) {
            throw new CustomException(ErrorCode.GUESTBOOK_REPLY_NOT_FOUND);
        }
        Guestbook guestbook = getGuestbook(guestbookId);
        boolean isReplyWriter = currentUserId.equals(reply.getWriterUserId());
        boolean isGuestbookWriter = currentUserId.equals(guestbook.getWriterUserId());
        if (!isReplyWriter && !isGuestbookWriter) {
            throw new CustomException(ErrorCode.GUESTBOOK_REPLY_FORBIDDEN);
        }
        reply.delete();
        guestbook.decreaseReplyCount();
    }

    @Transactional
    public GuestbookReplyResponse reply(Long userId, Long guestbookId, GuestbookReplyRequest request) {
        Long currentUserId = userService.requireUserId(userId);
        Guestbook guestbook = getGuestbook(guestbookId);
        if (replyRepository.findFirstByGuestbookIdAndDeletedAtIsNullOrderByIdAsc(guestbookId).isPresent()) {
            throw new CustomException(ErrorCode.GUESTBOOK_REPLY_ALREADY_EXISTS);
        }
        if (!spaceService.isPostItAllowed(guestbook.getSpaceId(), request.postItInventoryId())) {
            throw new CustomException(ErrorCode.GUESTBOOK_POSTIT_NOT_ALLOWED);
        }
        GuestbookReply reply = replyRepository.save(GuestbookReply.builder()
                .guestbookId(guestbookId)
                .writerUserId(currentUserId)
                .postItInventoryId(request.postItInventoryId())
                .content(request.content())
                .visibilityScope(request.visibilityScope())
                .build());
        guestbook.increaseReplyCount();
        return GuestbookReplyResponse.from(reply, userService.getUser(currentUserId));
    }

    private boolean isBlockedRelation(Long ownerUserId, Long currentUserId) {
        if (ownerUserId.equals(currentUserId)) {
            return false;
        }
        return userBlockRepository.existsByBlockerUserIdAndBlockedUserId(ownerUserId, currentUserId)
                || userBlockRepository.existsByBlockerUserIdAndBlockedUserId(currentUserId, ownerUserId);
    }

    private GuestbookResponse buildGuestbookResponse(Guestbook guestbook, Long viewerUserId) {
        User writer = userService.getUser(guestbook.getWriterUserId());
        GuestbookReply reply = guestbook.getReplyCount() > 0
                ? replyRepository.findFirstByGuestbookIdAndDeletedAtIsNullOrderByIdAsc(guestbook.getId()).orElse(null)
                : null;
        User replyWriter = reply == null ? null : userService.getUser(reply.getWriterUserId());
        return GuestbookResponse.from(guestbook, writer, reply, replyWriter, viewerUserId);
    }

    private Guestbook getGuestbook(Long guestbookId) {
        return guestbookRepository.findById(guestbookId)
                .orElseThrow(() -> new CustomException(ErrorCode.GUESTBOOK_NOT_FOUND));
    }
}
