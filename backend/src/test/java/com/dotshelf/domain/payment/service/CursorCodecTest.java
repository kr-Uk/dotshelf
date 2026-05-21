package com.dotshelf.domain.payment.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import java.time.Instant;
import java.time.LocalDateTime;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;

class CursorCodecTest {

    @Test
    @DisplayName("encodeFromInstant 라운드트립은 동일한 Instant 와 id 를 복원한다")
    void encodeFromInstant_roundTrip() {
        Instant timestamp = Instant.parse("2026-05-15T10:00:00.123Z");

        CursorCodec.CursorValue value = CursorCodec.decode(CursorCodec.encodeFromInstant(timestamp, 42L));

        assertThat(value.timestamp()).isEqualTo(timestamp);
        assertThat(value.id()).isEqualTo(42L);
    }

    @Test
    @DisplayName("encodeFromInstant 는 nanosecond 정밀도까지 보존한다 (DB timestamp(6) 의 microsecond row 누락 방지)")
    void encodeFromInstant_preservesNanosecondPrecision() {
        Instant timestamp = Instant.parse("2026-05-15T10:00:00.123456789Z");

        CursorCodec.CursorValue value = CursorCodec.decode(CursorCodec.encodeFromInstant(timestamp, 101L));

        assertThat(value.timestamp()).isEqualTo(timestamp);
        assertThat(value.timestamp().getNano()).isEqualTo(123_456_789);
        assertThat(value.id()).isEqualTo(101L);
    }

    @Test
    @DisplayName("encodeFromLocalDateTimeUtc 는 LocalDateTime 의 nanosecond 정밀도를 UTC Instant 로 보존한다")
    void encodeFromLocalDateTimeUtc_preservesNanosecondPrecision() {
        LocalDateTime original = LocalDateTime.parse("2026-05-15T10:00:00.123456789");

        CursorCodec.CursorValue value = CursorCodec.decode(CursorCodec.encodeFromLocalDateTimeUtc(original, 7L));

        assertThat(value.asLocalDateTimeUtc()).isEqualTo(original);
        assertThat(value.asLocalDateTimeUtc().getNano()).isEqualTo(123_456_789);
    }

    @Test
    @DisplayName("잘못된 base64 cursor 는 INVALID_INPUT 예외를 던진다")
    void decode_invalidBase64_throwsInvalidInput() {
        assertThatThrownBy(() -> CursorCodec.decode("not!base64@@"))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.INVALID_INPUT);
    }

    @Test
    @DisplayName("separator 가 빠진 cursor 는 INVALID_INPUT 예외를 던진다")
    void decode_missingSeparator_throwsInvalidInput() {
        // base64 로 인코딩됐지만 "|" 가 없는 raw 문자열
        String encoded = java.util.Base64.getUrlEncoder().withoutPadding()
                .encodeToString("no-separator".getBytes(java.nio.charset.StandardCharsets.UTF_8));

        assertThatThrownBy(() -> CursorCodec.decode(encoded))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.INVALID_INPUT);
    }
}
