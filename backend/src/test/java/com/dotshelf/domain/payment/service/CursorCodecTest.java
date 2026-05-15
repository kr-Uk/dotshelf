package com.dotshelf.domain.payment.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;

class CursorCodecTest {

    @Test
    @DisplayName("encode 한 cursor 를 decode 하면 동일한 timestamp 와 id 가 복원된다")
    void encodeDecode_roundTrip() {
        String encoded = CursorCodec.encode(1_734_152_000_000L, 42L);

        CursorCodec.CursorValue value = CursorCodec.decode(encoded);

        assertThat(value.timestampMillis()).isEqualTo(1_734_152_000_000L);
        assertThat(value.id()).isEqualTo(42L);
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
    @DisplayName("decode 결과는 epoch millis 와 LocalDateTime UTC 변환 모두 제공한다")
    void cursorValue_providesInstantAndLocalDateTime() {
        String encoded = CursorCodec.encode(0L, 1L);

        CursorCodec.CursorValue value = CursorCodec.decode(encoded);

        assertThat(value.asInstant().toEpochMilli()).isEqualTo(0L);
        assertThat(value.asLocalDateTimeUtc().getYear()).isEqualTo(1970);
    }
}
