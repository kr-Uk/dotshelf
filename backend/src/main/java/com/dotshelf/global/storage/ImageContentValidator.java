package com.dotshelf.global.storage;

import java.io.IOException;
import java.io.InputStream;

import org.springframework.web.multipart.MultipartFile;

import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;

/**
 * 이미지 파일의 매직 바이트(file signature) 와 클라이언트가 신고한 contentType 일치를 검증.
 * 클라가 어떤 contentType 을 신고하더라도 실제 파일이 그 포맷이 아니면 거부한다 — public URL 로
 * 노출되는 이미지에 비-이미지 파일이 섞여 들어오는 1차 방어.
 *
 * <p>presigned PUT 흐름은 BE 가 파일 본체를 보지 못해 여기서 검증할 수 없다. 그쪽은
 * S3StorageService.headObject 로 contentType / contentLength 만 재검증한다.
 */
public final class ImageContentValidator {

    private ImageContentValidator() {
    }

    private static final byte[] JPEG_SIG = {(byte) 0xFF, (byte) 0xD8, (byte) 0xFF};
    private static final byte[] PNG_SIG = {(byte) 0x89, 'P', 'N', 'G', 0x0D, 0x0A, 0x1A, 0x0A};

    public static void verifyMagicBytes(MultipartFile file, String contentType) {
        try (InputStream in = file.getInputStream()) {
            byte[] header = in.readNBytes(12);
            boolean ok = switch (contentType) {
                case "image/jpeg" -> startsWith(header, JPEG_SIG);
                case "image/png" -> startsWith(header, PNG_SIG);
                case "image/webp" -> isWebp(header);
                default -> false;
            };
            if (!ok) {
                throw new CustomException(ErrorCode.STORAGE_INVALID_CONTENT_TYPE);
            }
        } catch (IOException e) {
            throw new CustomException(ErrorCode.STORAGE_INTERNAL_ERROR);
        }
    }

    private static boolean startsWith(byte[] data, byte[] sig) {
        if (data.length < sig.length) {
            return false;
        }
        for (int i = 0; i < sig.length; i++) {
            if (data[i] != sig[i]) {
                return false;
            }
        }
        return true;
    }

    /** WebP 컨테이너: 0..3 "RIFF", 4..7 file size, 8..11 "WEBP". */
    private static boolean isWebp(byte[] header) {
        return header.length >= 12
                && header[0] == 'R' && header[1] == 'I' && header[2] == 'F' && header[3] == 'F'
                && header[8] == 'W' && header[9] == 'E' && header[10] == 'B' && header[11] == 'P';
    }
}
