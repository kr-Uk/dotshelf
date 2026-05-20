package com.dotshelf.domain.guestbook.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyBoolean;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;

import java.util.List;
import java.util.Optional;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.ArgumentCaptor;
import org.mockito.InjectMocks;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;
import org.springframework.context.ApplicationEventPublisher;

import com.dotshelf.domain.guestbook.dto.GuestbookDtos.GuestbookReplyRequest;
import com.dotshelf.domain.guestbook.dto.GuestbookDtos.GuestbookRequest;
import com.dotshelf.domain.guestbook.entity.Guestbook;
import com.dotshelf.domain.guestbook.entity.GuestbookReply;
import com.dotshelf.domain.guestbook.repository.GuestbookReplyRepository;
import com.dotshelf.domain.guestbook.repository.GuestbookRepository;
import com.dotshelf.domain.notification.event.NotificationEvents.GuestbookCreated;
import com.dotshelf.domain.notification.event.NotificationEvents.GuestbookReplyCreated;
import com.dotshelf.domain.space.entity.Space;
import com.dotshelf.domain.space.service.SpaceService;
import com.dotshelf.domain.user.repository.FollowRepository;
import com.dotshelf.domain.user.repository.UserBlockRepository;
import com.dotshelf.domain.user.repository.UserRepository;
import com.dotshelf.domain.user.service.UserService;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;
import com.dotshelf.support.TestFixtures;

@ExtendWith(MockitoExtension.class)
class GuestbookServiceTest {

    @Mock
    private GuestbookRepository guestbookRepository;

    @Mock
    private GuestbookReplyRepository replyRepository;

    @Mock
    private SpaceService spaceService;

    @Mock
    private UserService userService;

    @Mock
    private ApplicationEventPublisher eventPublisher;

    @Mock
    private UserRepository userRepository;

    @Mock
    private UserBlockRepository userBlockRepository;

    @Mock
    private FollowRepository followRepository;

    @InjectMocks
    private GuestbookService guestbookService;

    @Test
    @DisplayName("공간 주인은 본인 공간에 방명록을 작성할 수 없다")
    void create_spaceOwner_throwsException() {
        given(userService.requireUserId(1L)).willReturn(1L);
        given(spaceService.getSpaceEntity(10L)).willReturn(TestFixtures.space(10L, 1L));

        assertThatThrownBy(() -> guestbookService.create(1L, 10L, guestbookRequest(false)))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.SPACE_OWNER_CANNOT_POST);
    }

    @Test
    @DisplayName("차단 관계가 있으면 방명록 작성을 거절한다")
    void create_blockedRelation_throwsException() {
        given(userService.requireUserId(2L)).willReturn(2L);
        given(spaceService.getSpaceEntity(10L)).willReturn(TestFixtures.space(10L, 1L));
        given(userBlockRepository.existsBlockBetween(1L, 2L)).willReturn(true);

        assertThatThrownBy(() -> guestbookService.create(2L, 10L, guestbookRequest(false)))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.SPACE_ACCESS_BLOCKED);
    }

    @Test
    @DisplayName("허용되지 않은 포스트잇이면 방명록 작성을 거절한다")
    void create_postItNotAllowed_throwsException() {
        given(userService.requireUserId(2L)).willReturn(2L);
        given(spaceService.getSpaceEntity(10L)).willReturn(TestFixtures.space(10L, 1L));
        given(userBlockRepository.existsBlockBetween(1L, 2L)).willReturn(false);
        given(spaceService.isPostItAllowed(10L, 100L)).willReturn(false);

        assertThatThrownBy(() -> guestbookService.create(2L, 10L, guestbookRequest(false)))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.GUESTBOOK_POSTIT_NOT_ALLOWED);
    }

    @Test
    @DisplayName("방명록 작성 성공 시 저장하고 알림 이벤트를 발행한다")
    void create_validRequest_savesGuestbookAndPublishesEvent() {
        Space space = TestFixtures.space(10L, 1L);
        given(userService.requireUserId(2L)).willReturn(2L);
        given(spaceService.getSpaceEntity(10L)).willReturn(space);
        given(userBlockRepository.existsBlockBetween(1L, 2L)).willReturn(false);
        given(spaceService.isPostItAllowed(10L, 100L)).willReturn(true);
        given(guestbookRepository.save(any(Guestbook.class))).willAnswer(invocation -> {
            Guestbook guestbook = invocation.getArgument(0);
            TestFixtures.setId(guestbook, 20L);
            return guestbook;
        });
        given(userRepository.findAllById(any())).willReturn(List.of(TestFixtures.user(2L, "writer")));
        ArgumentCaptor<GuestbookCreated> eventCaptor = ArgumentCaptor.forClass(GuestbookCreated.class);

        var response = guestbookService.create(2L, 10L, guestbookRequest(true));

        assertThat(response.id()).isEqualTo(20L);
        assertThat(response.anonymous()).isTrue();
        then(eventPublisher).should().publishEvent(eventCaptor.capture());
        assertThat(eventCaptor.getValue().receiverUserId()).isEqualTo(1L);
        assertThat(eventCaptor.getValue().senderUserId()).isEqualTo(2L);
    }

    @Test
    @DisplayName("방명록 목록은 cursor/limit과 답장 batch를 반영하고 hasMore면 nextCursor를 반환한다")
    void getGuestbooks_hasMoreBuildsRepliesAndNextCursor() {
        Guestbook first = guestbook(20L, 10L, 2L, true);
        first.increaseReplyCount();
        Guestbook second = guestbook(19L, 10L, 3L);
        Guestbook extra = guestbook(18L, 10L, 4L);
        GuestbookReply reply = reply(30L, 20L, 1L);
        given(spaceService.getSpaceEntity(10L)).willReturn(TestFixtures.space(10L, 1L));
        given(guestbookRepository.findGuestbookPage(any(), any(), any(), any(), anyBoolean(), anyBoolean(), any()))
                .willReturn(List.of(first, second, extra));
        given(replyRepository.findActiveByGuestbookIdIn(any())).willReturn(List.of(reply));
        given(userRepository.findAllById(any())).willReturn(List.of(
                TestFixtures.user(1L, "owner"),
                TestFixtures.user(2L, "writer"),
                TestFixtures.user(3L, "second")
        ));

        var response = guestbookService.getGuestbooks(99L, 10L, null, 2);

        assertThat(response.guestbooks()).hasSize(2);
        assertThat(response.nextCursor()).isEqualTo("19");
        assertThat(response.guestbooks().getFirst().writerNickname()).isEqualTo("익명");
        assertThat(response.guestbooks().getFirst().reply().writerNickname()).isEqualTo("owner");
    }

    @Test
    @DisplayName("방명록 목록 cursor가 숫자가 아니면 INVALID_INPUT을 던진다")
    void getGuestbooks_invalidCursor_throwsInvalidInput() {
        assertThatThrownBy(() -> guestbookService.getGuestbooks(1L, 10L, "bad-cursor", 15))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.INVALID_INPUT);
    }

    @Test
    @DisplayName("방명록 board가 PRIVATE면 비주인은 조회할 수 없다")
    void getGuestbooks_privateBoardNonOwner_throwsForbidden() {
        Space space = TestFixtures.space(10L, 1L);
        space.changeGuestbookVisibility("PRIVATE");
        given(spaceService.getSpaceEntity(10L)).willReturn(space);

        assertThatThrownBy(() -> guestbookService.getGuestbooks(99L, 10L, null, 15))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.FORBIDDEN);
    }

    @Test
    @DisplayName("방명록 board가 MUTUAL이면 맞팔 viewer는 조회할 수 있다")
    void getGuestbooks_mutualBoardMutualViewer_succeeds() {
        Space space = TestFixtures.space(10L, 1L);
        space.changeGuestbookVisibility("MUTUAL");
        given(spaceService.getSpaceEntity(10L)).willReturn(space);
        given(followRepository.existsMutualFollow(99L, 1L)).willReturn(true);
        given(guestbookRepository.findGuestbookPage(any(), any(), any(), any(), anyBoolean(), anyBoolean(), any()))
                .willReturn(List.of());

        var response = guestbookService.getGuestbooks(99L, 10L, null, 15);

        assertThat(response.guestbooks()).isEmpty();
    }

    @Test
    @DisplayName("공간 주인은 board가 PRIVATE여도 자기 방명록을 조회할 수 있다")
    void getGuestbooks_ownerBypassesBoardScope() {
        Space space = TestFixtures.space(10L, 1L);
        space.changeGuestbookVisibility("PRIVATE");
        given(spaceService.getSpaceEntity(10L)).willReturn(space);
        given(guestbookRepository.findGuestbookPage(any(), any(), any(), any(), anyBoolean(), anyBoolean(), any()))
                .willReturn(List.of());

        var response = guestbookService.getGuestbooks(1L, 10L, null, 15);

        assertThat(response.guestbooks()).isEmpty();
    }

    @Test
    @DisplayName("방명록 답장은 공간 주인만 작성할 수 있다")
    void reply_notSpaceOwner_throwsException() {
        Guestbook guestbook = guestbook(20L, 10L, 2L);
        given(userService.requireUserId(3L)).willReturn(3L);
        given(guestbookRepository.findById(20L)).willReturn(Optional.of(guestbook));
        given(spaceService.getSpaceEntity(10L)).willReturn(TestFixtures.space(10L, 1L));

        assertThatThrownBy(() -> guestbookService.reply(3L, 20L, replyRequest()))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.GUESTBOOK_REPLY_FORBIDDEN);
    }

    @Test
    @DisplayName("이미 활성 답장이 있으면 새 답장을 작성할 수 없다")
    void reply_existingActiveReply_throwsAlreadyExists() {
        Guestbook guestbook = guestbook(20L, 10L, 2L);
        given(userService.requireUserId(1L)).willReturn(1L);
        given(guestbookRepository.findById(20L)).willReturn(Optional.of(guestbook));
        given(spaceService.getSpaceEntity(10L)).willReturn(TestFixtures.space(10L, 1L));
        given(replyRepository.findFirstByGuestbookIdAndDeletedAtIsNullOrderByIdAsc(20L))
                .willReturn(Optional.of(reply(30L, 20L, 1L)));

        assertThatThrownBy(() -> guestbookService.reply(1L, 20L, replyRequest()))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.GUESTBOOK_REPLY_ALREADY_EXISTS);
    }

    @Test
    @DisplayName("허용되지 않은 포스트잇이면 답장 작성을 거절한다")
    void reply_postItNotAllowed_throwsException() {
        Guestbook guestbook = guestbook(20L, 10L, 2L);
        given(userService.requireUserId(1L)).willReturn(1L);
        given(guestbookRepository.findById(20L)).willReturn(Optional.of(guestbook));
        given(spaceService.getSpaceEntity(10L)).willReturn(TestFixtures.space(10L, 1L));
        given(replyRepository.findFirstByGuestbookIdAndDeletedAtIsNullOrderByIdAsc(20L)).willReturn(Optional.empty());
        given(spaceService.isPostItAllowed(10L, 101L)).willReturn(false);

        assertThatThrownBy(() -> guestbookService.reply(1L, 20L, replyRequest()))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.GUESTBOOK_POSTIT_NOT_ALLOWED);
    }

    @Test
    @DisplayName("방명록 답장 성공 시 replyCount를 증가시키고 알림 이벤트를 발행한다")
    void reply_validRequest_savesReplyAndPublishesEvent() {
        Guestbook guestbook = guestbook(20L, 10L, 2L);
        given(userService.requireUserId(1L)).willReturn(1L);
        given(guestbookRepository.findById(20L)).willReturn(Optional.of(guestbook));
        given(spaceService.getSpaceEntity(10L)).willReturn(TestFixtures.space(10L, 1L));
        given(replyRepository.findFirstByGuestbookIdAndDeletedAtIsNullOrderByIdAsc(20L)).willReturn(Optional.empty());
        given(spaceService.isPostItAllowed(10L, 101L)).willReturn(true);
        given(replyRepository.save(any(GuestbookReply.class))).willAnswer(invocation -> {
            GuestbookReply reply = invocation.getArgument(0);
            TestFixtures.setId(reply, 30L);
            return reply;
        });
        given(userService.getUser(1L)).willReturn(TestFixtures.user(1L, "owner"));
        ArgumentCaptor<GuestbookReplyCreated> eventCaptor = ArgumentCaptor.forClass(GuestbookReplyCreated.class);

        var response = guestbookService.reply(1L, 20L, replyRequest());

        assertThat(response.id()).isEqualTo(30L);
        assertThat(guestbook.getReplyCount()).isEqualTo(1);
        then(eventPublisher).should().publishEvent(eventCaptor.capture());
        assertThat(eventCaptor.getValue().receiverUserId()).isEqualTo(2L);
    }

    @Test
    @DisplayName("작성자도 공간 주인도 아니면 방명록을 삭제할 수 없다")
    void delete_notWriterNorOwner_throwsForbidden() {
        given(userService.requireUserId(3L)).willReturn(3L);
        given(guestbookRepository.findById(20L)).willReturn(Optional.of(guestbook(20L, 10L, 2L)));
        given(spaceService.getSpaceEntity(10L)).willReturn(TestFixtures.space(10L, 1L));

        assertThatThrownBy(() -> guestbookService.delete(3L, 20L))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.FORBIDDEN);
    }

    @Test
    @DisplayName("방명록 작성자는 방명록을 삭제할 수 있다")
    void delete_writer_marksDeleted() {
        Guestbook guestbook = guestbook(20L, 10L, 2L);
        given(userService.requireUserId(2L)).willReturn(2L);
        given(guestbookRepository.findById(20L)).willReturn(Optional.of(guestbook));
        given(spaceService.getSpaceEntity(10L)).willReturn(TestFixtures.space(10L, 1L));

        guestbookService.delete(2L, 20L);

        assertThat(guestbook.getStatus()).isEqualTo("DELETED");
        assertThat(guestbook.getDeletedAt()).isNotNull();
    }

    @Test
    @DisplayName("공간 주인은 다른 사람이 쓴 방명록도 삭제할 수 있다")
    void delete_spaceOwner_marksDeleted() {
        Guestbook guestbook = guestbook(20L, 10L, 2L);
        given(userService.requireUserId(1L)).willReturn(1L);
        given(guestbookRepository.findById(20L)).willReturn(Optional.of(guestbook));
        given(spaceService.getSpaceEntity(10L)).willReturn(TestFixtures.space(10L, 1L));

        guestbookService.delete(1L, 20L);

        assertThat(guestbook.getStatus()).isEqualTo("DELETED");
        assertThat(guestbook.getDeletedAt()).isNotNull();
    }

    @Test
    @DisplayName("존재하지 않는 방명록 삭제는 GUESTBOOK_NOT_FOUND를 던진다")
    void delete_missingGuestbook_throwsNotFound() {
        given(userService.requireUserId(2L)).willReturn(2L);
        given(guestbookRepository.findById(20L)).willReturn(Optional.empty());

        assertThatThrownBy(() -> guestbookService.delete(2L, 20L))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.GUESTBOOK_NOT_FOUND);
    }

    @Test
    @DisplayName("답장 작성자는 답장을 삭제하고 방명록 replyCount를 감소시킨다")
    void deleteReply_replyWriter_deletesReplyAndDecreasesCount() {
        Guestbook guestbook = guestbook(20L, 10L, 2L);
        guestbook.increaseReplyCount();
        GuestbookReply reply = reply(30L, 20L, 1L);
        given(userService.requireUserId(1L)).willReturn(1L);
        given(replyRepository.findById(30L)).willReturn(Optional.of(reply));
        given(guestbookRepository.findById(20L)).willReturn(Optional.of(guestbook));

        guestbookService.deleteReply(1L, 20L, 30L);

        assertThat(reply.getDeletedAt()).isNotNull();
        assertThat(guestbook.getReplyCount()).isZero();
    }

    @Test
    @DisplayName("답장 id가 방명록 id와 맞지 않으면 GUESTBOOK_REPLY_NOT_FOUND를 던진다")
    void deleteReply_mismatchedGuestbook_throwsReplyNotFound() {
        given(userService.requireUserId(1L)).willReturn(1L);
        given(replyRepository.findById(30L)).willReturn(Optional.of(reply(30L, 99L, 1L)));

        assertThatThrownBy(() -> guestbookService.deleteReply(1L, 20L, 30L))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.GUESTBOOK_REPLY_NOT_FOUND);
    }

    private GuestbookRequest guestbookRequest(boolean anonymous) {
        return new GuestbookRequest(100L, "hello", "PUBLIC", anonymous);
    }

    private GuestbookReplyRequest replyRequest() {
        return new GuestbookReplyRequest(101L, "reply", "PUBLIC");
    }

    private Guestbook guestbook(Long id, Long spaceId, Long writerUserId) {
        return guestbook(id, spaceId, writerUserId, false);
    }

    private Guestbook guestbook(Long id, Long spaceId, Long writerUserId, boolean anonymous) {
        Guestbook guestbook = Guestbook.builder()
                .spaceId(spaceId)
                .writerUserId(writerUserId)
                .postItInventoryId(100L)
                .content("hello")
                .visibilityScope("PUBLIC")
                .anonymous(anonymous)
                .build();
        TestFixtures.setId(guestbook, id);
        return guestbook;
    }

    private GuestbookReply reply(Long id, Long guestbookId, Long writerUserId) {
        GuestbookReply reply = GuestbookReply.builder()
                .guestbookId(guestbookId)
                .writerUserId(writerUserId)
                .postItInventoryId(101L)
                .content("reply")
                .visibilityScope("PUBLIC")
                .build();
        TestFixtures.setId(reply, id);
        return reply;
    }
}
