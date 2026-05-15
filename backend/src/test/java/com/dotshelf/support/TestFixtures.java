package com.dotshelf.support;

import java.time.Instant;

import org.springframework.test.util.ReflectionTestUtils;

import com.dotshelf.domain.admin.entity.AdminAnnouncement;
import com.dotshelf.domain.admin.entity.AdminAuditLog;
import com.dotshelf.domain.admin.entity.AdminUserNote;
import com.dotshelf.domain.admin.entity.CreatorProfile;
import com.dotshelf.domain.admin.entity.MaintenanceSetting;
import com.dotshelf.domain.admin.entity.PolicyVersion;
import com.dotshelf.domain.admin.entity.SettlementAccountChangeRequest;
import com.dotshelf.domain.admin.entity.SettlementAdjustment;
import com.dotshelf.domain.admin.entity.SettlementPayoutBatch;
import com.dotshelf.domain.admin.entity.SettlementWithdrawal;
import com.dotshelf.domain.asset.entity.Asset;
import com.dotshelf.domain.asset.entity.AssetCategory;
import com.dotshelf.domain.asset.entity.AssetPurchase;
import com.dotshelf.domain.asset.entity.AssetWishlist;
import com.dotshelf.domain.asset.entity.UserAssetInventory;
import com.dotshelf.domain.auth.entity.AuthRefreshToken;
import com.dotshelf.domain.auth.entity.SocialProvider;
import com.dotshelf.domain.auth.entity.UserSocialAccount;
import com.dotshelf.domain.book.entity.Book;
import com.dotshelf.domain.book.entity.BookReview;
import com.dotshelf.domain.book.entity.UserBook;
import com.dotshelf.domain.bookshelf.entity.Bookshelf;
import com.dotshelf.domain.bookshelf.entity.BookshelfItem;
import com.dotshelf.domain.bookshelf.entity.BookshelfSection;
import com.dotshelf.domain.notification.entity.Notification;
import com.dotshelf.domain.payment.entity.PageTransaction;
import com.dotshelf.domain.payment.entity.PageWallet;
import com.dotshelf.domain.payment.entity.PaymentOrder;
import com.dotshelf.domain.readingrecord.entity.ReadingRecord;
import com.dotshelf.domain.readingrecord.entity.ReadingRecordComment;
import com.dotshelf.domain.readingrecord.entity.ReadingRecordCommentLike;
import com.dotshelf.domain.readingrecord.entity.ReadingRecordLike;
import com.dotshelf.domain.report.entity.Report;
import com.dotshelf.domain.space.entity.Space;
import com.dotshelf.domain.user.entity.Follow;
import com.dotshelf.domain.user.entity.User;
import com.dotshelf.domain.user.entity.UserBlock;
import com.dotshelf.domain.user.entity.UserFeedSetting;
import com.dotshelf.domain.user.entity.UserNotificationSetting;

public final class TestFixtures {

    private TestFixtures() {
    }

    public static User user(Long id, String nickname) {
        User user = User.builder()
                .email(nickname + "@example.com")
                .nickname(nickname)
                .profileImageUrl("https://example.com/" + nickname + ".png")
                .introduction(nickname + " introduction")
                .build();
        setId(user, id);
        return user;
    }

    public static User user(String nickname) {
        return User.builder()
                .email(nickname + "@example.com")
                .nickname(nickname)
                .build();
    }

    public static UserNotificationSetting notificationSetting(Long userId) {
        return UserNotificationSetting.builder()
                .userId(userId)
                .build();
    }

    public static UserFeedSetting feedSetting(Long userId) {
        return UserFeedSetting.builder()
                .userId(userId)
                .build();
    }

    public static Follow follow(Long followerUserId, Long followingUserId) {
        return Follow.builder()
                .followerUserId(followerUserId)
                .followingUserId(followingUserId)
                .build();
    }

    public static UserBlock userBlock(Long blockerUserId, Long blockedUserId) {
        return UserBlock.builder()
                .blockerUserId(blockerUserId)
                .blockedUserId(blockedUserId)
                .build();
    }

    public static Space space(Long id, Long ownerUserId) {
        Space space = Space.builder()
                .ownerUserId(ownerUserId)
                .spaceName("나의 공간")
                .build();
        setId(space, id);
        return space;
    }

    public static AssetCategory assetCategory(String code) {
        return AssetCategory.builder()
                .code(code)
                .name(code + " category")
                .displayOrder(1)
                .build();
    }

    public static Asset asset(Long id, Long categoryId, String code, int pricePages) {
        Asset asset = Asset.builder()
                .categoryId(categoryId)
                .code(code)
                .name(code + " asset")
                .assetScope("SPACE")
                .assetType("WALLPAPER")
                .pricePages(pricePages)
                .build();
        setId(asset, id);
        return asset;
    }

    public static Asset themeAsset(Long id, Long categoryId, String compatibleTargets, boolean allowsCustomization) {
        Asset asset = Asset.builder()
                .categoryId(categoryId)
                .code("theme-" + id)
                .name("theme")
                .assetScope("SPACE")
                .assetType("THEME")
                .pricePages(100)
                .compatibleTargets(compatibleTargets)
                .allowsCustomization(allowsCustomization)
                .build();
        setId(asset, id);
        return asset;
    }

    public static UserAssetInventory inventory(Long id, Long userId, Long assetId) {
        UserAssetInventory inventory = UserAssetInventory.builder()
                .userId(userId)
                .assetId(assetId)
                .grantType("PURCHASE")
                .build();
        setId(inventory, id);
        return inventory;
    }

    public static AssetWishlist wishlist(Long userId, Long assetId) {
        return AssetWishlist.builder()
                .userId(userId)
                .assetId(assetId)
                .build();
    }

    public static AssetPurchase assetPurchase(Long id, Long userId, Long assetId, Long inventoryId, int pricePages) {
        AssetPurchase purchase = AssetPurchase.builder()
                .userId(userId)
                .assetId(assetId)
                .inventoryId(inventoryId)
                .pricePages(pricePages)
                .build();
        setId(purchase, id);
        return purchase;
    }

    public static PageWallet wallet(Long userId, int balancePages) {
        return PageWallet.builder()
                .userId(userId)
                .balancePages(balancePages)
                .build();
    }

    public static PageTransaction pageTransaction(Long userId, Long paymentOrderId, int pagesDelta, int balanceAfter) {
        return PageTransaction.builder()
                .userId(userId)
                .paymentOrderId(paymentOrderId)
                .type(PageTransaction.TYPE_CHARGE)
                .pagesDelta(pagesDelta)
                .balanceAfter(balanceAfter)
                .reason("TEST")
                .referenceType("PAYMENT_ORDER")
                .referenceId(paymentOrderId)
                .build();
    }

    public static UserSocialAccount socialAccount(Long userId, SocialProvider provider, String providerUserId) {
        return UserSocialAccount.builder()
                .userId(userId)
                .provider(provider)
                .providerUserId(providerUserId)
                .emailAtProvider("social@example.com")
                .build();
    }

    public static AuthRefreshToken refreshToken(Long id, Long userId, String tokenHash) {
        AuthRefreshToken token = AuthRefreshToken.builder()
                .userId(userId)
                .tokenHash(tokenHash)
                .expiresAt(Instant.now().plusSeconds(3600))
                .build();
        setId(token, id);
        return token;
    }

    public static PaymentOrder paymentOrder(String orderId, Long userId, int amountKrw) {
        Instant now = Instant.now();
        return PaymentOrder.builder()
                .userId(userId)
                .orderId(orderId)
                .orderName("DotShelf " + amountKrw + "P 충전 (유효기간 1년/자동갱신)")
                .pagesAmount(amountKrw)
                .amountKrw(amountKrw)
                .buyerName("홍길동")
                .buyerTel("010-1234-5678")
                .buyerEmail("buyer@example.com")
                .requestedAt(now)
                .expiresAt(now.plusSeconds(1800))
                .build();
    }

    public static Report report(Long reporterUserId, String targetType, Long targetId) {
        return Report.builder()
                .reporterUserId(reporterUserId)
                .reportedUserId(2L)
                .targetType(targetType)
                .targetId(targetId)
                .reasonType("SPAM")
                .reasonDetail("spam")
                .build();
    }

    public static Book book(Long id, String title) {
        long isbnSeed = id == null ? Math.abs((long) title.hashCode()) % 1_000_000_000L : id;
        Book book = Book.builder()
                .isbn13("9780" + String.format("%09d", isbnSeed))
                .title(title)
                .author("author")
                .publisher("publisher")
                .build();
        setId(book, id);
        return book;
    }

    public static UserBook userBook(Long id, Long userId, Long bookId) {
        UserBook userBook = UserBook.builder()
                .userId(userId)
                .bookId(bookId)
                .build();
        setId(userBook, id);
        return userBook;
    }

    public static BookReview bookReview(Long id, Long bookId, Long userId) {
        BookReview review = BookReview.builder()
                .bookId(bookId)
                .userId(userId)
                .rating(java.math.BigDecimal.valueOf(4.0))
                .content("review")
                .build();
        setId(review, id);
        return review;
    }

    public static Bookshelf bookshelf(Long id, Long spaceId, int displayOrder) {
        Bookshelf bookshelf = Bookshelf.builder()
                .spaceId(spaceId)
                .name("bookshelf-" + id)
                .visibilityScope("PUBLIC")
                .displayOrder(displayOrder)
                .build();
        setId(bookshelf, id);
        return bookshelf;
    }

    public static BookshelfSection bookshelfSection(Long id, Long bookshelfId, int rowNo, int columnNo) {
        BookshelfSection section = BookshelfSection.builder()
                .bookshelfId(bookshelfId)
                .rowNo(rowNo)
                .columnNo(columnNo)
                .maxItemCount(20)
                .build();
        setId(section, id);
        return section;
    }

    public static BookshelfItem bookshelfItem(Long id, Long bookshelfId, Long sectionId, Long userBookId) {
        BookshelfItem item = BookshelfItem.builder()
                .bookshelfId(bookshelfId)
                .bookshelfSectionId(sectionId)
                .itemType("BOOK")
                .userBookId(userBookId)
                .slotNo(1)
                .facingDirection("FRONT")
                .displayStyle("SPINE")
                .build();
        setId(item, id);
        return item;
    }

    public static BookshelfItem decorItem(Long id, Long bookshelfId, Long sectionId, Long inventoryId) {
        BookshelfItem item = BookshelfItem.builder()
                .bookshelfId(bookshelfId)
                .bookshelfSectionId(sectionId)
                .itemType("DECOR")
                .assetInventoryId(inventoryId)
                .slotNo(1)
                .build();
        setId(item, id);
        return item;
    }

    public static ReadingRecord readingRecord(Long id, Long userBookId) {
        ReadingRecord record = ReadingRecord.builder()
                .userBookId(userBookId)
                .title("record-" + id)
                .content("content")
                .visibilityScope("PUBLIC")
                .build();
        setId(record, id);
        return record;
    }

    public static ReadingRecordComment readingRecordComment(Long id, Long recordId, Long userId) {
        ReadingRecordComment comment = ReadingRecordComment.builder()
                .readingRecordId(recordId)
                .userId(userId)
                .content("comment")
                .depth(0)
                .build();
        setId(comment, id);
        return comment;
    }

    public static ReadingRecordComment replyComment(Long id, Long recordId, Long userId, Long parentCommentId) {
        ReadingRecordComment comment = ReadingRecordComment.builder()
                .readingRecordId(recordId)
                .userId(userId)
                .parentCommentId(parentCommentId)
                .depth(1)
                .content("reply")
                .build();
        setId(comment, id);
        return comment;
    }

    public static ReadingRecordLike readingRecordLike(Long recordId, Long userId) {
        return ReadingRecordLike.builder()
                .readingRecordId(recordId)
                .userId(userId)
                .build();
    }

    public static ReadingRecordCommentLike readingRecordCommentLike(Long commentId, Long userId) {
        return ReadingRecordCommentLike.builder()
                .commentId(commentId)
                .userId(userId)
                .build();
    }

    public static Notification notification(Long id, Long receiverUserId, Long senderUserId) {
        Notification notification = Notification.builder()
                .receiverUserId(receiverUserId)
                .senderUserId(senderUserId)
                .type("BOOK_ADDED")
                .targetType("USER_BOOK")
                .targetId(10L)
                .message("message")
                .build();
        setId(notification, id);
        return notification;
    }

    public static AdminAnnouncement announcement(Long id, String title) {
        AdminAnnouncement announcement = AdminAnnouncement.builder()
                .title(title)
                .body("body")
                .audience("all_users")
                .isUrgent(false)
                .createdByUserId(1L)
                .build();
        setId(announcement, id);
        return announcement;
    }

    public static AdminAuditLog auditLog(Long id, Long actorUserId, String action) {
        AdminAuditLog log = AdminAuditLog.builder()
                .actorUserId(actorUserId)
                .action(action)
                .targetType("USER")
                .targetId(2L)
                .reason("reason")
                .ipAddress("127.0.0.1")
                .build();
        setId(log, id);
        return log;
    }

    public static AdminUserNote adminUserNote(Long id, Long userId, Long authorUserId) {
        AdminUserNote note = AdminUserNote.builder()
                .userId(userId)
                .authorUserId(authorUserId)
                .body("note")
                .build();
        setId(note, id);
        return note;
    }

    public static CreatorProfile creatorProfile(Long id, Long userId) {
        CreatorProfile creator = CreatorProfile.builder()
                .userId(userId)
                .displayName("creator")
                .status("ACTIVE")
                .build();
        setId(creator, id);
        return creator;
    }

    public static MaintenanceSetting maintenanceSetting(Long id, boolean enabled) {
        MaintenanceSetting setting = MaintenanceSetting.builder()
                .enabled(enabled)
                .message("maintenance")
                .updatedByUserId(1L)
                .build();
        setId(setting, id);
        return setting;
    }

    public static PolicyVersion policyVersion(Long id, String version) {
        PolicyVersion policy = PolicyVersion.builder()
                .policyType("terms")
                .version(version)
                .title("policy")
                .body("body")
                .isActive(true)
                .publishedByUserId(1L)
                .build();
        setId(policy, id);
        return policy;
    }

    public static SettlementWithdrawal withdrawal(Long id, Long creatorUserId, int requestedPages, String status) {
        SettlementWithdrawal withdrawal = SettlementWithdrawal.builder()
                .creatorUserId(creatorUserId)
                .requestedPages(requestedPages)
                .status(status)
                .requestedAt(Instant.now())
                .memo("memo")
                .build();
        setId(withdrawal, id);
        return withdrawal;
    }

    public static SettlementPayoutBatch payoutBatch(Long id, String batchCode) {
        SettlementPayoutBatch batch = SettlementPayoutBatch.builder()
                .batchCode(batchCode)
                .totalPages(100)
                .withdrawalCount(2)
                .status("READY")
                .build();
        setId(batch, id);
        return batch;
    }

    public static SettlementAccountChangeRequest accountChange(Long id, Long userId) {
        SettlementAccountChangeRequest request = SettlementAccountChangeRequest.builder()
                .userId(userId)
                .bankName("bank")
                .accountNumberMasked("1234****")
                .accountHolder("holder")
                .status("REQUESTED")
                .requestedAt(Instant.now())
                .build();
        setId(request, id);
        return request;
    }

    public static SettlementAdjustment settlementAdjustment(Long id, Long userId, int deltaPages) {
        SettlementAdjustment adjustment = SettlementAdjustment.builder()
                .userId(userId)
                .deltaPages(deltaPages)
                .reason("reason")
                .memo("memo")
                .adjustedByUserId(1L)
                .build();
        setId(adjustment, id);
        return adjustment;
    }

    public static void setId(Object entity, Long id) {
        ReflectionTestUtils.setField(entity, "id", id);
    }
}
