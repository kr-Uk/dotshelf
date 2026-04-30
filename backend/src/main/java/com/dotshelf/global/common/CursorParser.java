package com.dotshelf.global.common;

import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;

/**
 * cursor 페이지네이션 공통 helper.
 * MR #61 리뷰 #9 — SpaceService/ReadingRecordService에 동일 메서드가 복붙돼 있던 것 통합.
 *
 * <p>잘못된 cursor 형식은 silent fallback (null 반환) 대신 명시적 INVALID_INPUT throw —
 * 디버깅 시 클라이언트가 잘못된 cursor 보낸 것을 즉시 알 수 있도록.
 */
public final class CursorParser {

    private CursorParser() {
    }

    public static Long parseId(String cursor) {
        if (cursor == null || cursor.isBlank()) {
            return null;
        }
        try {
            return Long.parseLong(cursor);
        } catch (NumberFormatException e) {
            throw new CustomException(ErrorCode.INVALID_INPUT, "잘못된 cursor 형식입니다.");
        }
    }
}
