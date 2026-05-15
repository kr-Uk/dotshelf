package com.dotshelf.global.error;

import org.springframework.http.HttpStatus;

import lombok.Getter;
import lombok.RequiredArgsConstructor;

@Getter
@RequiredArgsConstructor
public enum ErrorCode {

    // Common
    INVALID_INPUT(HttpStatus.BAD_REQUEST, "C001", "잘못된 입력 값입니다."),
    METHOD_NOT_ALLOWED(HttpStatus.METHOD_NOT_ALLOWED, "C002", "허용되지 않은 메서드입니다."),
    INTERNAL_SERVER_ERROR(HttpStatus.INTERNAL_SERVER_ERROR, "C003", "서버 오류가 발생했습니다."),
    RESOURCE_NOT_FOUND(HttpStatus.NOT_FOUND, "C004", "리소스를 찾을 수 없습니다."),

    // Auth
    UNAUTHORIZED(HttpStatus.UNAUTHORIZED, "A001", "인증이 필요합니다."),
    FORBIDDEN(HttpStatus.FORBIDDEN, "A002", "접근 권한이 없습니다."),
    INVALID_TOKEN(HttpStatus.UNAUTHORIZED, "A003", "유효하지 않은 토큰입니다."),

    // User
    USER_NOT_FOUND(HttpStatus.NOT_FOUND, "U001", "사용자를 찾을 수 없습니다."),
    USER_ALREADY_EXISTS(HttpStatus.CONFLICT, "U002", "이미 존재하는 사용자입니다."),
    DUPLICATE_NICKNAME(HttpStatus.CONFLICT, "U003", "이미 사용 중인 닉네임입니다."),

    // Book
    BOOK_NOT_FOUND(HttpStatus.NOT_FOUND, "B001", "도서를 찾을 수 없습니다."),
    USER_BOOK_NOT_FOUND(HttpStatus.NOT_FOUND, "B002", "내 책을 찾을 수 없습니다."),
    BOOK_SEARCH_UNAVAILABLE(HttpStatus.BAD_GATEWAY, "B003", "도서 검색 서비스를 사용할 수 없습니다."),

    // Bookshelf
    BOOKSHELF_NOT_FOUND(HttpStatus.NOT_FOUND, "S001", "책장을 찾을 수 없습니다."),
    BOOKSHELF_ITEM_NOT_FOUND(HttpStatus.NOT_FOUND, "S002", "책장 아이템을 찾을 수 없습니다."),
    SPACE_NOT_FOUND(HttpStatus.NOT_FOUND, "S003", "공간을 찾을 수 없습니다."),
    SPACE_OWNER_CANNOT_POST(HttpStatus.CONFLICT, "S004", "본인 공간에는 방명록을 작성할 수 없습니다."),
    SPACE_ACCESS_BLOCKED(HttpStatus.FORBIDDEN, "S005", "차단된 사용자의 공간에 접근할 수 없습니다."),
    THEME_NOT_OWNED(HttpStatus.FORBIDDEN, "S006", "보유하지 않은 테마/꾸미기 항목입니다."),
    THEME_INVALID_MODE(HttpStatus.BAD_REQUEST, "S007", "테마 적용 모드가 올바르지 않습니다."),
    CATEGORY_INVENTORY_TYPE_MISMATCH(HttpStatus.CONFLICT, "S008", "카테고리와 자산 타입이 일치하지 않습니다."),
    THEME_MODE_NOT_IMPLEMENTED(HttpStatus.NOT_IMPLEMENTED, "S009", "해당 테마 적용 모드는 아직 구현되지 않았습니다."),

    // Review
    REVIEW_NOT_FOUND(HttpStatus.NOT_FOUND, "R001", "리뷰를 찾을 수 없습니다."),
    READING_RECORD_NOT_FOUND(HttpStatus.NOT_FOUND, "R002", "독서기록을 찾을 수 없습니다."),
    COMMENT_NOT_FOUND(HttpStatus.NOT_FOUND, "R003", "댓글을 찾을 수 없습니다."),

    // Follow
    FOLLOW_NOT_FOUND(HttpStatus.NOT_FOUND, "F001", "팔로우 관계를 찾을 수 없습니다."),
    BLOCKED_RELATION(HttpStatus.FORBIDDEN, "F002", "차단된 사용자와 관련된 요청은 허용되지 않습니다."),

    // Asset
    ASSET_NOT_FOUND(HttpStatus.NOT_FOUND, "AS001", "에셋을 찾을 수 없습니다."),
    ASSET_ALREADY_OWNED(HttpStatus.CONFLICT, "AS002", "이미 보유한 에셋입니다."),
    INSUFFICIENT_PAGES(HttpStatus.UNPROCESSABLE_ENTITY, "AS003", "페이지 잔액이 부족합니다."),
    ASSET_CATEGORY_NOT_FOUND(HttpStatus.NOT_FOUND, "AS004", "에셋 카테고리를 찾을 수 없습니다."),
    ASSET_CODE_ALREADY_EXISTS(HttpStatus.CONFLICT, "AS005", "이미 존재하는 에셋 코드입니다."),
    ASSET_NOT_OWNED(HttpStatus.FORBIDDEN, "AS006", "보유하지 않은 에셋입니다."),
    ASSET_FILE_NOT_READY(HttpStatus.CONFLICT, "AS007", "다운로드할 에셋 파일이 준비되지 않았습니다."),
    ASSET_PUBLISH_NOT_READY(HttpStatus.CONFLICT, "AS008", "에셋 본체와 썸네일을 모두 업로드해야 공개할 수 있습니다."),
    ASSET_VERSION_DOWNGRADE(HttpStatus.CONFLICT, "AS009", "에셋 버전은 현재 버전보다 커야 합니다."),
    ASSET_CODE_FORMAT_INVALID(HttpStatus.BAD_REQUEST, "AS010", "에셋 코드 형식이 올바르지 않습니다. 예: FANTASY_CHAIR_001"),

    // Payment
    PAYMENT_ORDER_NOT_FOUND(HttpStatus.NOT_FOUND, "P001", "결제 주문을 찾을 수 없습니다."),
    PAYMENT_AMOUNT_MISMATCH(HttpStatus.BAD_REQUEST, "P002", "결제 금액이 일치하지 않습니다."),
    PAYMENT_ORDER_INVALID_STATE(HttpStatus.CONFLICT, "P003", "처리할 수 없는 결제 주문 상태입니다."),
    PAYMENT_PROVIDER_UNAVAILABLE(HttpStatus.BAD_GATEWAY, "P004", "결제 서비스를 사용할 수 없습니다."),
    PAYMENT_APPROVAL_FAILED(HttpStatus.BAD_GATEWAY, "P005", "결제 승인에 실패했습니다."),
    PAYMENT_DIRECT_CHARGE_DISABLED(HttpStatus.BAD_REQUEST, "P006", "직접 페이지 충전은 지원하지 않습니다."),
    PAYMENT_CONFIGURATION_MISSING(HttpStatus.INTERNAL_SERVER_ERROR, "P007", "결제 설정이 누락되었습니다."),
    PAYMENT_AUTHENTICATION_FAILED(HttpStatus.BAD_REQUEST, "P008", "결제 인증에 실패했습니다."),
    PAYMENT_AMOUNT_EXCEEDS_LIMIT(HttpStatus.BAD_REQUEST, "P009", "1회 결제 한도를 초과했습니다."),

    // Guestbook
    GUESTBOOK_NOT_FOUND(HttpStatus.NOT_FOUND, "G001", "방명록을 찾을 수 없습니다."),
    GUESTBOOK_REPLY_NOT_FOUND(HttpStatus.NOT_FOUND, "G002", "방명록 답장을 찾을 수 없습니다."),
    GUESTBOOK_REPLY_FORBIDDEN(HttpStatus.FORBIDDEN, "G003", "답장에 대한 권한이 없습니다."),
    GUESTBOOK_REPLY_ALREADY_EXISTS(HttpStatus.CONFLICT, "G004", "이미 답장이 작성된 방명록입니다."),
    GUESTBOOK_POSTIT_NOT_ALLOWED(HttpStatus.CONFLICT, "G005", "공간 주인이 허용하지 않은 포스트잇 스타일입니다."),

    // Notification / Report
    NOTIFICATION_NOT_FOUND(HttpStatus.NOT_FOUND, "N001", "알림을 찾을 수 없습니다."),
    REPORT_ALREADY_EXISTS(HttpStatus.CONFLICT, "RP001", "이미 신고한 대상입니다."),

    // Storage / 업로드
    STORAGE_INVALID_CONTENT_TYPE(HttpStatus.UNSUPPORTED_MEDIA_TYPE, "ST001", "허용되지 않은 파일 형식입니다."),
    STORAGE_FILE_TOO_LARGE(HttpStatus.PAYLOAD_TOO_LARGE, "ST002", "파일 크기가 허용 범위를 초과했습니다."),
    STORAGE_OBJECT_NOT_UPLOADED(HttpStatus.NOT_FOUND, "ST003", "업로드된 객체를 찾을 수 없습니다."),
    STORAGE_INTERNAL_ERROR(HttpStatus.INTERNAL_SERVER_ERROR, "ST004", "스토리지 처리에 실패했습니다."),
    STORAGE_INVALID_KEY(HttpStatus.BAD_REQUEST, "ST005", "유효하지 않은 객체 키입니다.");

    private final HttpStatus status;
    private final String code;
    private final String message;
}
