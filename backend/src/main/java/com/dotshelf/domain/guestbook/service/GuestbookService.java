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

    public GuestbookListResponse getGuestbooks(Long spaceId) {
        return new GuestbookListResponse(guestbookRepository.findBySpaceIdAndStatusOrderByIdDesc(spaceId, "ACTIVE")
                .stream()
                .map(GuestbookResponse::from)
                .toList());
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
                .build());
        return GuestbookResponse.from(guestbook);
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
                .content(request.content())
                .visibilityScope(request.visibilityScope())
                .build());
        guestbook.increaseReplyCount();
        return GuestbookReplyResponse.from(reply);
    }

    private Guestbook getGuestbook(Long guestbookId) {
        return guestbookRepository.findById(guestbookId)
                .orElseThrow(() -> new CustomException(ErrorCode.GUESTBOOK_NOT_FOUND));
    }
}
