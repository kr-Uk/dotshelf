package com.dotshelf.domain.payment.service;

import java.nio.charset.StandardCharsets;
import java.time.Instant;
import java.time.LocalDateTime;
import java.time.ZoneOffset;
import java.util.Base64;

import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;

/**
 * Cursor paging 용 (timestamp, id) keyset 인코딩.
 * <p>
 * <b>정밀도</b>: timestamp 는 {@link Instant#toString()} ISO-8601 nanosecond 까지 직렬화한다.
 * DB 컬럼이 {@code timestamp(6)} (microsecond) 이상의 정밀도를 가질 수 있으므로
 * {@code toEpochMilli()} 로 자르면 같은 millisecond 안의 microsecond row 가 누락될 수 있어 금지.
 */
public final class CursorCodec {

    private static final String SEPARATOR = "|";

    private CursorCodec() {
    }

    public static String encodeFromInstant(Instant timestamp, long id) {
        String raw = timestamp.toString() + SEPARATOR + id;
        return Base64.getUrlEncoder().withoutPadding().encodeToString(raw.getBytes(StandardCharsets.UTF_8));
    }

    public static String encodeFromLocalDateTimeUtc(LocalDateTime timestamp, long id) {
        return encodeFromInstant(timestamp.toInstant(ZoneOffset.UTC), id);
    }

    public static CursorValue decode(String encoded) {
        try {
            String raw = new String(Base64.getUrlDecoder().decode(encoded), StandardCharsets.UTF_8);
            int sep = raw.lastIndexOf(SEPARATOR);
            if (sep < 0) {
                throw new IllegalArgumentException("missing separator");
            }
            Instant timestamp = Instant.parse(raw.substring(0, sep));
            long id = Long.parseLong(raw.substring(sep + 1));
            return new CursorValue(timestamp, id);
        } catch (RuntimeException e) {
            throw new CustomException(ErrorCode.INVALID_INPUT, "잘못된 cursor 형식입니다.");
        }
    }

    public record CursorValue(Instant timestamp, long id) {

        public LocalDateTime asLocalDateTimeUtc() {
            return LocalDateTime.ofInstant(timestamp, ZoneOffset.UTC);
        }
    }
}
