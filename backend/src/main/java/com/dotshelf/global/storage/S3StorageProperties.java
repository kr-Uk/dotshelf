package com.dotshelf.global.storage;

import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.validation.annotation.Validated;

import jakarta.validation.constraints.NotBlank;

/**
 * S3 업로드 관련 설정. 부팅 시 bucket/region 이 비면 ApplicationContext 로딩이 즉시 실패해
 * "앱은 떠있는데 첫 업로드에서 500" 같은 deferred error 를 막는다.
 *
 * <p>access-key / secret-key 는 비어있어도 허용 — DefaultCredentialsProvider 가 환경변수 /
 * profile / EC2 IAM Role 순으로 자동 해석한다 (운영 EC2 Role 모드).
 */
@Validated
@ConfigurationProperties(prefix = "app.storage.s3")
public record S3StorageProperties(
        @NotBlank(message = "app.storage.s3.bucket 은 필수입니다") String bucket,
        @NotBlank(message = "app.storage.s3.region 은 필수입니다") String region,
        String accessKey,
        String secretKey,
        String publicBaseUrl,
        long presignTtlSeconds,
        long presignBundleTtlSeconds
) {
}
