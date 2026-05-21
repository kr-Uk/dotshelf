package com.dotshelf.integration;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import java.util.List;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc;
import org.springframework.http.MediaType;
import org.springframework.test.context.ActiveProfiles;
import org.springframework.test.web.servlet.MockMvc;

import com.dotshelf.domain.book.repository.BookRepository;
import com.dotshelf.domain.book.repository.UserBookRepository;
import com.dotshelf.domain.guestbook.repository.GuestbookReplyRepository;
import com.dotshelf.domain.guestbook.repository.GuestbookRepository;
import com.dotshelf.domain.notification.entity.Notification;
import com.dotshelf.domain.notification.repository.NotificationRepository;
import com.dotshelf.domain.readingrecord.repository.ReadingRecordCommentLikeRepository;
import com.dotshelf.domain.readingrecord.repository.ReadingRecordCommentRepository;
import com.dotshelf.domain.readingrecord.repository.ReadingRecordLikeRepository;
import com.dotshelf.domain.readingrecord.repository.ReadingRecordRepository;
import com.dotshelf.domain.space.repository.SpaceRepository;
import com.dotshelf.domain.user.repository.UserNotificationSettingRepository;
import com.dotshelf.domain.user.repository.UserRepository;
import com.dotshelf.support.TestFixtures;

@SpringBootTest
@AutoConfigureMockMvc
@ActiveProfiles("test")
class NotificationFlowIntegrationTest {

    private static final String TEST_USER_HEADER = "X-Test-Admin-User-Id";

    @Autowired
    private MockMvc mockMvc;

    @Autowired
    private UserRepository userRepository;

    @Autowired
    private UserNotificationSettingRepository notificationSettingRepository;

    @Autowired
    private BookRepository bookRepository;

    @Autowired
    private UserBookRepository userBookRepository;

    @Autowired
    private ReadingRecordRepository readingRecordRepository;

    @Autowired
    private ReadingRecordLikeRepository readingRecordLikeRepository;

    @Autowired
    private ReadingRecordCommentRepository commentRepository;

    @Autowired
    private ReadingRecordCommentLikeRepository commentLikeRepository;

    @Autowired
    private SpaceRepository spaceRepository;

    @Autowired
    private GuestbookRepository guestbookRepository;

    @Autowired
    private GuestbookReplyRepository guestbookReplyRepository;

    @Autowired
    private NotificationRepository notificationRepository;

    @BeforeEach
    void cleanDatabase() {
        notificationRepository.deleteAll();
        commentLikeRepository.deleteAll();
        commentRepository.deleteAll();
        readingRecordLikeRepository.deleteAll();
        readingRecordRepository.deleteAll();
        guestbookReplyRepository.deleteAll();
        guestbookRepository.deleteAll();
        userBookRepository.deleteAll();
        bookRepository.deleteAll();
        spaceRepository.deleteAll();
        notificationSettingRepository.deleteAll();
        userRepository.deleteAll();
    }

    @Test
    @DisplayName("독서기록 좋아요/댓글/대댓글은 AFTER_COMMIT 비동기 알림을 생성한다")
    void readingRecordInteractions_createNotificationsAfterCommit() throws Exception {
        var owner = userRepository.save(TestFixtures.user("owner"));
        var actor = userRepository.save(TestFixtures.user("actor"));
        userRepository.flush();
        var book = bookRepository.saveAndFlush(TestFixtures.book(null, "알림 책"));
        var userBook = userBookRepository.saveAndFlush(TestFixtures.userBook(null, owner.getId(), book.getId()));
        var record = readingRecordRepository.saveAndFlush(TestFixtures.readingRecord(null, userBook.getId()));

        mockMvc.perform(post("/v1/reading-records/{recordId}/like", record.getId())
                        .header(TEST_USER_HEADER, actor.getId().toString()))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.active").value(true));
        List<Notification> ownerNotifications = waitForNotifications(owner.getId(), 1);
        assertThat(ownerNotifications.get(0).getType()).isEqualTo("READING_RECORD_LIKED");
        assertThat(ownerNotifications.get(0).getTargetId()).isEqualTo(record.getId());

        mockMvc.perform(post("/v1/reading-records/{recordId}/comments", record.getId())
                        .header(TEST_USER_HEADER, actor.getId().toString())
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"content":"좋은 기록이네요"}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.content").value("좋은 기록이네요"));
        ownerNotifications = waitForNotifications(owner.getId(), 2);
        assertThat(ownerNotifications)
                .extracting(Notification::getType)
                .contains("READING_RECORD_COMMENTED", "READING_RECORD_LIKED");

        var parentComment = commentRepository.findByReadingRecordIdOrderByIdAsc(record.getId()).get(0);
        mockMvc.perform(post("/v1/reading-records/{recordId}/comments", record.getId())
                        .header(TEST_USER_HEADER, owner.getId().toString())
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"parentCommentId":%d,"content":"고마워요"}
                                """.formatted(parentComment.getId())))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.depth").value(1));
        List<Notification> actorNotifications = waitForNotifications(actor.getId(), 1);
        assertThat(actorNotifications.get(0).getType()).isEqualTo("READING_RECORD_COMMENT_REPLIED");
        assertThat(actorNotifications.get(0).getTargetType()).isEqualTo("READING_RECORD_COMMENT");
    }

    @Test
    @DisplayName("방명록 작성과 공간 주인의 답글은 각각 상대방에게 알림을 생성한다")
    void guestbookAndReply_createNotificationsAfterCommit() throws Exception {
        var owner = userRepository.save(TestFixtures.user("space-owner"));
        var writer = userRepository.save(TestFixtures.user("guest"));
        userRepository.flush();
        var space = spaceRepository.saveAndFlush(TestFixtures.space(null, owner.getId()));

        mockMvc.perform(post("/v1/spaces/{spaceId}/guestbooks", space.getId())
                        .header(TEST_USER_HEADER, writer.getId().toString())
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"content":"잘 보고 갑니다","visibilityScope":"PUBLIC","anonymous":false}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.content").value("잘 보고 갑니다"));
        List<Notification> ownerNotifications = waitForNotifications(owner.getId(), 1);
        assertThat(ownerNotifications.get(0).getType()).isEqualTo("GUESTBOOK_CREATED");
        assertThat(ownerNotifications.get(0).getSenderUserId()).isEqualTo(writer.getId());

        var guestbook = guestbookRepository.findAll().get(0);
        mockMvc.perform(post("/v1/guestbooks/{guestbookId}/replies", guestbook.getId())
                        .header(TEST_USER_HEADER, owner.getId().toString())
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"content":"방문 감사해요","visibilityScope":"PUBLIC"}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.content").value("방문 감사해요"));
        List<Notification> writerNotifications = waitForNotifications(writer.getId(), 1);
        assertThat(writerNotifications.get(0).getType()).isEqualTo("GUESTBOOK_REPLY_CREATED");
        assertThat(writerNotifications.get(0).getSenderUserId()).isEqualTo(owner.getId());
    }

    @Test
    @DisplayName("자기 독서기록에 직접 반응해도 자기 자신에게는 알림을 만들지 않는다")
    void selfReadingRecordInteraction_doesNotCreateNotification() throws Exception {
        var owner = userRepository.save(TestFixtures.user("self-owner"));
        userRepository.flush();
        var book = bookRepository.saveAndFlush(TestFixtures.book(null, "셀프 알림 책"));
        var userBook = userBookRepository.saveAndFlush(TestFixtures.userBook(null, owner.getId(), book.getId()));
        var record = readingRecordRepository.saveAndFlush(TestFixtures.readingRecord(null, userBook.getId()));

        mockMvc.perform(post("/v1/reading-records/{recordId}/like", record.getId())
                        .header(TEST_USER_HEADER, owner.getId().toString()))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.active").value(true));
        mockMvc.perform(post("/v1/reading-records/{recordId}/comments", record.getId())
                        .header(TEST_USER_HEADER, owner.getId().toString())
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"content":"내 기록 보충"}
                                """))
                .andExpect(status().isOk());

        assertNoNotificationsAfterAsyncWindow(owner.getId());
    }

    @Test
    @DisplayName("수신자가 독서기록 반응/댓글 알림을 끄면 AFTER_COMMIT 이벤트가 와도 알림을 저장하지 않는다")
    void disabledReadingRecordNotificationSettings_skipNotificationRows() throws Exception {
        var owner = userRepository.save(TestFixtures.user("setting-owner"));
        var actor = userRepository.save(TestFixtures.user("setting-actor"));
        userRepository.flush();
        var setting = notificationSettingRepository.saveAndFlush(TestFixtures.notificationSetting(owner.getId()));
        setting.update(null, null, false, false, null, null);
        notificationSettingRepository.saveAndFlush(setting);
        var book = bookRepository.saveAndFlush(TestFixtures.book(null, "설정 알림 책"));
        var userBook = userBookRepository.saveAndFlush(TestFixtures.userBook(null, owner.getId(), book.getId()));
        var record = readingRecordRepository.saveAndFlush(TestFixtures.readingRecord(null, userBook.getId()));

        mockMvc.perform(post("/v1/reading-records/{recordId}/like", record.getId())
                        .header(TEST_USER_HEADER, actor.getId().toString()))
                .andExpect(status().isOk());
        mockMvc.perform(post("/v1/reading-records/{recordId}/comments", record.getId())
                        .header(TEST_USER_HEADER, actor.getId().toString())
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"content":"알림 꺼짐"}
                                """))
                .andExpect(status().isOk());

        assertNoNotificationsAfterAsyncWindow(owner.getId());
    }

    private List<Notification> waitForNotifications(Long receiverUserId, int expectedCount) throws InterruptedException {
        long deadline = System.currentTimeMillis() + 5_000;
        List<Notification> notifications;
        do {
            notifications = notificationRepository.findByReceiverUserIdOrderByIdDesc(receiverUserId);
            if (notifications.size() >= expectedCount) {
                return notifications;
            }
            Thread.sleep(50);
        } while (System.currentTimeMillis() < deadline);
        return notifications;
    }

    private void assertNoNotificationsAfterAsyncWindow(Long receiverUserId) throws InterruptedException {
        long deadline = System.currentTimeMillis() + 1_000;
        do {
            assertThat(notificationRepository.findByReceiverUserIdOrderByIdDesc(receiverUserId)).isEmpty();
            Thread.sleep(50);
        } while (System.currentTimeMillis() < deadline);
    }
}
