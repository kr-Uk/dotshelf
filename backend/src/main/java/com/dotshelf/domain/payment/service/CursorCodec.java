package com.dotshelf.domain.payment.service;

import java.nio.charset.StandardCharsets;
import java.time.Instant;
import java.time.LocalDateTime;
import java.time.ZoneOffset;
import java.util.Base64;

import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;

public final class CursorCodec {

    private CursorCodec() {
    }

    public static String encode(long timestampMillis, long id) {
        String raw = timestampMillis + "_" + id;
        return Base64.getUrlEncoder().withoutPadding().encodeToString(raw.getBytes(StandardCharsets.UTF_8));
    }

    public static String encodeFromInstant(Instant timestamp, long id) {
        return encode(timestamp.toEpochMilli(), id);
    }

    public static String encodeFromLocalDateTimeUtc(LocalDateTime timestamp, long id) {
        return encode(timestamp.toInstant(ZoneOffset.UTC).toEpochMilli(), id);
    }

    public static CursorValue decode(String encoded) {
        try {
            String raw = new String(Base64.getUrlDecoder().decode(encoded), StandardCharsets.UTF_8);
            String[] parts = raw.split("_", 2);
            return new CursorValue(Long.parseLong(parts[0]), Long.parseLong(parts[1]));
        } catch (RuntimeException e) {
            throw new CustomException(ErrorCode.INVALID_INPUT, "잘못된 cursor 형식입니다.");
        }
    }

    public record CursorValue(long timestampMillis, long id) {

        public Instant asInstant() {
            return Instant.ofEpochMilli(timestampMillis);
        }

        public LocalDateTime asLocalDateTimeUtc() {
            return LocalDateTime.ofInstant(asInstant(), ZoneOffset.UTC);
        }
    }
}
