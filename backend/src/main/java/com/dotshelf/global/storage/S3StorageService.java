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
import software.amazon.awssdk.services.s3.model.HeadObjectResponse;
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
     * presigned PUT 의 confirm 단계에서 호출. 객체가 실제로 올라갔는지 + S3 가 받은 contentType /
     * contentLength 가 도메인 화이트리스트에 맞는지 재검증하는 입구. 1단계 발급 때 신고한 값을 클라가
     * 변조하면 PUT 서명이 깨져 막히지만, layer-of-defense 로 confirm 시점에 한 번 더 본다.
     */
    public ObjectMetadata headObject(String key) {
        try {
            HeadObjectResponse response = s3Client.headObject(HeadObjectRequest.builder()
                    .bucket(properties.bucket())
                    .key(key)
                    .build());
            return new ObjectMetadata(response.contentType(), response.contentLength());
        } catch (NoSuchKeyException e) {
            throw new CustomException(ErrorCode.STORAGE_OBJECT_NOT_UPLOADED);
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

    public record ObjectMetadata(String contentType, long contentLength) {
    }
}
