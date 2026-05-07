package com.dotshelf.global.storage;

import java.io.IOException;
import java.time.Duration;

import org.springframework.stereotype.Service;
import org.springframework.web.multipart.MultipartFile;

import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;

import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import software.amazon.awssdk.core.exception.SdkException;
import software.amazon.awssdk.core.sync.RequestBody;
import software.amazon.awssdk.services.s3.S3Client;
import software.amazon.awssdk.services.s3.model.HeadObjectRequest;
import software.amazon.awssdk.services.s3.model.NoSuchKeyException;
import software.amazon.awssdk.services.s3.model.PutObjectRequest;
import software.amazon.awssdk.services.s3.presigner.S3Presigner;
import software.amazon.awssdk.services.s3.presigner.model.PresignedPutObjectRequest;
import software.amazon.awssdk.services.s3.presigner.model.PutObjectPresignRequest;

/**
 * S3 객체 입출력 캡슐화. 도메인 서비스는 contentType/size 화이트리스트를 자체 강제하고,
 * 이 클래스에는 검증 통과한 요청만 위임한다.
 *
 * <p>두 업로드 패턴:
 * <ul>
 *   <li>{@link #putObject} — 서버 경유 업로드 (Unity 방 썸네일 자동 캡처처럼 BE 가 받아 즉시 저장)</li>
 *   <li>{@link #presignPut} — 클라 직업로드용 PUT URL 발급 (대용량 에셋, 프로필 이미지)</li>
 * </ul>
 */
@Slf4j
@Service
@RequiredArgsConstructor
public class S3StorageService {

    private final S3Client s3Client;
    private final S3Presigner s3Presigner;
    private final S3StorageProperties properties;

    public void putObject(String key, MultipartFile file, String contentType) {
        try {
            PutObjectRequest request = PutObjectRequest.builder()
                    .bucket(properties.bucket())
                    .key(key)
                    .contentType(contentType)
                    .contentLength(file.getSize())
                    .build();
            s3Client.putObject(request, RequestBody.fromInputStream(file.getInputStream(), file.getSize()));
        } catch (IOException e) {
            log.error("[S3] putObject IO 실패 key={} size={}", key, file.getSize(), e);
            throw new CustomException(ErrorCode.STORAGE_INTERNAL_ERROR);
        } catch (SdkException e) {
            log.error("[S3] putObject SDK 실패 key={}", key, e);
            throw new CustomException(ErrorCode.STORAGE_INTERNAL_ERROR);
        }
    }

    public PresignedPut presignPut(String key, String contentType, long byteLength, Duration ttl) {
        try {
            PutObjectRequest objectRequest = PutObjectRequest.builder()
                    .bucket(properties.bucket())
                    .key(key)
                    .contentType(contentType)
                    .contentLength(byteLength)
                    .build();
            PutObjectPresignRequest presignRequest = PutObjectPresignRequest.builder()
                    .signatureDuration(ttl)
                    .putObjectRequest(objectRequest)
                    .build();
            PresignedPutObjectRequest presigned = s3Presigner.presignPutObject(presignRequest);
            return new PresignedPut(presigned.url().toString(), key, ttl.getSeconds());
        } catch (SdkException e) {
            log.error("[S3] presignPut 실패 key={}", key, e);
            throw new CustomException(ErrorCode.STORAGE_INTERNAL_ERROR);
        }
    }

    /**
     * presigned PUT 의 confirm 단계에서 호출. 클라가 실제로 객체를 올렸는지 headObject 로 확인한다.
     * 없으면 STORAGE_OBJECT_NOT_UPLOADED, S3 자체 오류면 STORAGE_INTERNAL_ERROR.
     */
    public boolean objectExists(String key) {
        try {
            s3Client.headObject(HeadObjectRequest.builder()
                    .bucket(properties.bucket())
                    .key(key)
                    .build());
            return true;
        } catch (NoSuchKeyException e) {
            return false;
        } catch (SdkException e) {
            log.error("[S3] headObject 실패 key={}", key, e);
            throw new CustomException(ErrorCode.STORAGE_INTERNAL_ERROR);
        }
    }

    /**
     * DB 에 저장할 공개 URL. public-base-url 이 설정돼 있으면 그쪽(CDN)을, 없으면 S3 가상 호스트 URL 을 쓴다.
     */
    public String publicUrl(String key) {
        String base = properties.publicBaseUrl();
        if (base != null && !base.isBlank()) {
            return trimTrailingSlash(base) + "/" + key;
        }
        return "https://" + properties.bucket() + ".s3." + properties.region() + ".amazonaws.com/" + key;
    }

    private String trimTrailingSlash(String value) {
        return value.endsWith("/") ? value.substring(0, value.length() - 1) : value;
    }

    public record PresignedPut(String uploadUrl, String key, long expiresInSeconds) {
    }
}
