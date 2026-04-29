package com.dotshelf.domain.guestbook.service;

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
import com.dotshelf.domain.space.service.SpaceService;
import com.dotshelf.domain.user.entity.User;
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

    public GuestbookListResponse getGuestbooks(Long currentUserId, Long spaceId) {
        return new GuestbookListResponse(
                guestbookRepository.findBySpaceIdAndStatusOrderByIdDesc(spaceId, "ACTIVE").stream()
                        .map(gb -> buildGuestbookResponse(gb, currentUserId))
                        .toList(),
                null
        );
    }

    @Transactional
    public GuestbookResponse create(Long userId, Long spaceId, GuestbookRequest request) {
        Long currentUserId = userService.requireUserId(userId);
        spaceService.getSpaceEntity(spaceId);
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
    public GuestbookReplyResponse reply(Long userId, Long guestbookId, GuestbookReplyRequest request) {
        Long currentUserId = userService.requireUserId(userId);
        Guestbook guestbook = getGuestbook(guestbookId);
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
