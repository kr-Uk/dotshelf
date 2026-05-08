package com.dotshelf.global.storage;

import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.validation.annotation.Validated;

import jakarta.validation.constraints.NotBlank;

/**
 * S3 upload configuration. Bucket and region are required so storage
 * misconfiguration fails during application startup instead of first upload.
 *
 * <p>If access-key and secret-key are blank, the AWS default credentials chain
 * can resolve credentials from environment variables, profiles, or IAM roles.
 */
@Validated
@ConfigurationProperties(prefix = "app.storage.s3")
public record S3StorageProperties(
        @NotBlank(message = "app.storage.s3.bucket is required") String bucket,
        @NotBlank(message = "app.storage.s3.region is required") String region,
        String accessKey,
        String secretKey,
        String publicBaseUrl,
        long presignTtlSeconds,
        long presignBundleTtlSeconds
) {
}
