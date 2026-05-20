package com.dotshelf.domain.guestbook.service;

import java.util.HashMap;
import java.util.HashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.function.Function;
import java.util.stream.Collectors;

import org.springframework.context.ApplicationEventPublisher;
import org.springframework.data.domain.PageRequest;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import com.dotshelf.domain.guestbook.dto.GuestbookDtos.GuestbookListResponse;
import com.dotshelf.domain.guestbook.dto.GuestbookDtos.GuestbookReplyRequest;
import com.dotshelf.domain.guestbook.dto.GuestbookDtos.GuestbookReplyResponse;
import com.dotshelf.domain.guestbook.dto.GuestbookDtos.GuestbookRequest;
import com.dotshelf.domain.guestbook.dto.GuestbookDtos.GuestbookResponse;
import com.dotshelf.domain.guestbook.entity.Guestbook;
import com.dotshelf.domain.guestbook.entity.GuestbookReply;
import com.dotshelf.domain.guestbook.repository.GuestbookReplyRepository;
import com.dotshelf.domain.guestbook.repository.GuestbookRepository;
import com.dotshelf.domain.notification.event.NotificationEvents.GuestbookCreated;
import com.dotshelf.domain.notification.event.NotificationEvents.GuestbookReplyCreated;
import com.dotshelf.domain.space.entity.Space;
import com.dotshelf.domain.space.service.SpaceService;
import com.dotshelf.domain.user.entity.User;
import com.dotshelf.domain.user.repository.FollowRepository;
import com.dotshelf.domain.user.repository.UserBlockRepository;
import com.dotshelf.domain.user.repository.UserRepository;
import com.dotshelf.domain.user.service.UserService;
import com.dotshelf.global.common.VisibilityScope;
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
    private final ApplicationEventPublisher eventPublisher;
    private final UserRepository userRepository;
    private final UserBlockRepository userBlockRepository;
    private final FollowRepository followRepository;

    private static final int DEFAULT_GUESTBOOK_LIMIT = 15;
    private static final int MAX_GUESTBOOK_LIMIT = 50;

    public GuestbookListResponse getGuestbooks(Long currentUserId, Long spaceId, String cursor, int limit) {
        int validLimit = clampLimit(limit, DEFAULT_GUESTBOOK_LIMIT, MAX_GUESTBOOK_LIMIT);
        Long cursorId = parseCursorId(cursor);

        // 방명록 조회도 공간 접근 가드를 거친다 — 비공개 공간 / 차단 관계 차단.
        Space space = spaceService.getSpaceEntity(spaceId);
        spaceService.ensureCanViewSpace(currentUserId, space);

        boolean isOwner = space.getOwnerUserId().equals(currentUserId);
        boolean mutual = !isOwner
                && followRepository.existsMutualFollow(currentUserId, space.getOwnerUserId());

        // board 공개 범위(guestbookVisibilityScope) — PUBLIC 외에는 권한 있는 viewer 만 board 접근.
        if (!isOwner && !isViewerAllowedByScope(space.getGuestbookVisibilityScope(), mutual)) {
            throw new CustomException(ErrorCode.FORBIDDEN);
        }

        // 개별 글 visibilityScope 는 쿼리에서 직접 필터 — 페이지네이션 정합성 유지.
        List<Guestbook> page = guestbookRepository.findGuestbookPage(
                spaceId, "ACTIVE", cursorId, currentUserId, isOwner, mutual,
                PageRequest.of(0, validLimit + 1));
        boolean hasMore = page.size() > validLimit;
        List<Guestbook> trimmed = hasMore ? page.subList(0, validLimit) : page;
        String nextCursor = hasMore ? String.valueOf(trimmed.get(trimmed.size() - 1).getId()) : null;

        // MR #60 #3 batch — 페이지 1회당 최대 45+ select → user/reply 한 번에 IN 쿼리.
        List<GuestbookResponse> responses = buildGuestbookResponses(trimmed, currentUserId, isOwner, mutual);
        return new GuestbookListResponse(responses, nextCursor);
    }

    private List<GuestbookResponse> buildGuestbookResponses(
            List<Guestbook> guestbooks, Long viewerUserId, boolean isOwner, boolean mutual) {
        if (guestbooks.isEmpty()) {
            return List.of();
        }

        // 1) 활성 답장 batch 조회
        Set<Long> guestbookIdsWithReply = new HashSet<>();
        for (Guestbook gb : guestbooks) {
            if (gb.getReplyCount() > 0) guestbookIdsWithReply.add(gb.getId());
        }
        Map<Long, GuestbookReply> replyByGuestbookId = guestbookIdsWithReply.isEmpty()
                ? Map.of()
                : replyRepository.findActiveByGuestbookIdIn(guestbookIdsWithReply).stream()
                        .collect(Collectors.toMap(GuestbookReply::getGuestbookId, Function.identity(), (a, b) -> a));

        // 2) writer + replyWriter ids 모아서 한 번에 user 조회
        Set<Long> userIds = new HashSet<>();
        for (Guestbook gb : guestbooks) {
            userIds.add(gb.getWriterUserId());
        }
        for (GuestbookReply reply : replyByGuestbookId.values()) {
            userIds.add(reply.getWriterUserId());
        }
        Map<Long, User> userById = userRepository.findAllById(userIds).stream()
                .collect(Collectors.toMap(User::getId, Function.identity()));

        // 3) 매핑 구성
        List<GuestbookResponse> result = new java.util.ArrayList<>(guestbooks.size());
        for (Guestbook gb : guestbooks) {
            User writer = userById.get(gb.getWriterUserId());
            GuestbookReply reply = replyByGuestbookId.get(gb.getId());
            if (reply != null && !isReplyVisible(reply, gb, viewerUserId, isOwner, mutual)) {
                reply = null;
            }
            User replyWriter = reply == null ? null : userById.get(reply.getWriterUserId());
            result.add(GuestbookResponse.from(gb, writer, reply, replyWriter, viewerUserId));
        }
        return result;
    }

    private static int clampLimit(int limit, int defaultLimit, int max) {
        if (limit <= 0) return defaultLimit;
        return Math.min(limit, max);
    }

    /** MR #60 #6 — 잘못된 cursor는 silent 폴백 대신 명시적 INVALID_INPUT. */
    private static Long parseCursorId(String cursor) {
        if (cursor == null || cursor.isBlank()) return null;
        try {
            return Long.parseLong(cursor);
        } catch (NumberFormatException e) {
            throw new CustomException(ErrorCode.INVALID_INPUT, "잘못된 cursor 형식입니다.");
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
        eventPublisher.publishEvent(new GuestbookCreated(
                space.getOwnerUserId(),
                currentUserId,
                guestbook.getId(),
                guestbook.isAnonymous()
        ));
        return buildGuestbookResponses(List.of(guestbook), currentUserId, false, false).get(0);
    }

    @Transactional
    public void delete(Long userId, Long guestbookId) {
        Long currentUserId = userService.requireUserId(userId);
        Guestbook guestbook = getGuestbook(guestbookId);
        // 방명록은 작성자 본인뿐 아니라 공간 주인도 삭제할 수 있다 — 본인 공간에 달린 글이므로.
        Space space = spaceService.getSpaceEntity(guestbook.getSpaceId());
        boolean isWriter = currentUserId.equals(guestbook.getWriterUserId());
        boolean isSpaceOwner = currentUserId.equals(space.getOwnerUserId());
        if (!isWriter && !isSpaceOwner) {
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

        // MR #60 #1 — 답장은 정책상 \"공간 주인\"만 작성 가능. 클라 UI 제약은 우회 가능하므로 서버 enforce.
        Space space = spaceService.getSpaceEntity(guestbook.getSpaceId());
        if (!space.getOwnerUserId().equals(currentUserId)) {
            throw new CustomException(ErrorCode.GUESTBOOK_REPLY_FORBIDDEN);
        }

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
        eventPublisher.publishEvent(new GuestbookReplyCreated(guestbook.getWriterUserId(), currentUserId, reply.getId()));
        return GuestbookReplyResponse.from(reply, userService.getUser(currentUserId));
    }

    /** MR #60 #5 — 양방향 차단 단일 OR 쿼리. */
    private boolean isBlockedRelation(Long ownerUserId, Long currentUserId) {
        if (ownerUserId.equals(currentUserId)) {
            return false;
        }
        return userBlockRepository.existsBlockBetween(ownerUserId, currentUserId);
    }

    private Guestbook getGuestbook(Long guestbookId) {
        return guestbookRepository.findById(guestbookId)
                .orElseThrow(() -> new CustomException(ErrorCode.GUESTBOOK_NOT_FOUND));
    }

    // 답장(공간 주인 작성)의 visibilityScope 가시성 — board/글과 동일한 owner-relative 기준.
    // 공간 주인 본인, 그리고 답장이 달린 글의 작성자는 항상 답장을 볼 수 있다.
    private boolean isReplyVisible(GuestbookReply reply, Guestbook guestbook,
                                   Long viewerUserId, boolean isOwner, boolean mutual) {
        if (isOwner) {
            return true;
        }
        if (viewerUserId != null && viewerUserId.equals(guestbook.getWriterUserId())) {
            return true;
        }
        return isViewerAllowedByScope(reply.getVisibilityScope(), mutual);
    }

    // PUBLIC → 모두, MUTUAL → 맞팔 viewer 만, 그 외(PRIVATE) → 불가.
    private static boolean isViewerAllowedByScope(String scope, boolean mutual) {
        if (VisibilityScope.isPublic(scope)) {
            return true;
        }
        return VisibilityScope.isMutual(scope) && mutual;
    }
}
