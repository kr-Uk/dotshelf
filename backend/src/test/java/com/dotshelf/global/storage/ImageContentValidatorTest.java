package com.dotshelf.global.storage;

import static org.assertj.core.api.Assertions.assertThatCode;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.BDDMockito.given;
import static org.mockito.Mockito.mock;

import java.io.IOException;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.mock.web.MockMultipartFile;
import org.springframework.web.multipart.MultipartFile;

import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;

class ImageContentValidatorTest {

    @Test
    @DisplayName("JPEG/PNG/WebP 매직 바이트가 contentType과 일치하면 통과한다")
    void verifyMagicBytes_supportedImageSignatures_pass() {
        MockMultipartFile jpeg = new MockMultipartFile(
                "file", "a.jpg", "image/jpeg", new byte[] {(byte) 0xFF, (byte) 0xD8, (byte) 0xFF, 0x00});
        MockMultipartFile png = new MockMultipartFile(
                "file", "a.png", "image/png",
                new byte[] {(byte) 0x89, 'P', 'N', 'G', 0x0D, 0x0A, 0x1A, 0x0A, 0x00});
        MockMultipartFile webp = new MockMultipartFile(
                "file", "a.webp", "image/webp",
                new byte[] {'R', 'I', 'F', 'F', 0x01, 0x00, 0x00, 0x00, 'W', 'E', 'B', 'P'});

        assertThatCode(() -> ImageContentValidator.verifyMagicBytes(jpeg, "image/jpeg")).doesNotThrowAnyException();
        assertThatCode(() -> ImageContentValidator.verifyMagicBytes(png, "image/png")).doesNotThrowAnyException();
        assertThatCode(() -> ImageContentValidator.verifyMagicBytes(webp, "image/webp")).doesNotThrowAnyException();
    }

    @Test
    @DisplayName("contentType이 이미지여도 실제 매직 바이트가 다르면 STORAGE_INVALID_CONTENT_TYPE을 던진다")
    void verifyMagicBytes_mismatchedSignature_throwsInvalidContentType() {
        MockMultipartFile file = new MockMultipartFile(
                "file", "fake.jpg", "image/jpeg", new byte[] {'N', 'O', 'T', '_', 'I', 'M', 'G'});

        assertThatThrownBy(() -> ImageContentValidator.verifyMagicBytes(file, "image/jpeg"))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.STORAGE_INVALID_CONTENT_TYPE);
    }

    @Test
    @DisplayName("지원하지 않는 contentType이면 STORAGE_INVALID_CONTENT_TYPE을 던진다")
    void verifyMagicBytes_unsupportedContentType_throwsInvalidContentType() {
        MockMultipartFile file = new MockMultipartFile("file", "a.gif", "image/gif", new byte[] {'G', 'I', 'F'});

        assertThatThrownBy(() -> ImageContentValidator.verifyMagicBytes(file, "image/gif"))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.STORAGE_INVALID_CONTENT_TYPE);
    }

    @Test
    @DisplayName("파일 스트림을 읽지 못하면 STORAGE_INTERNAL_ERROR를 던진다")
    void verifyMagicBytes_ioFailure_throwsStorageInternalError() throws Exception {
        MultipartFile file = mock(MultipartFile.class);
        given(file.getInputStream()).willThrow(new IOException("broken stream"));

        assertThatThrownBy(() -> ImageContentValidator.verifyMagicBytes(file, "image/png"))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.STORAGE_INTERNAL_ERROR);
    }
}
