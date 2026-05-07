package com.dotshelf.global.storage;

import org.springframework.boot.context.properties.ConfigurationProperties;

@ConfigurationProperties(prefix = "app.storage.s3")
public record S3StorageProperties(
        String bucket,
        String region,
        String accessKey,
        String secretKey,
        String publicBaseUrl,
        long presignTtlSeconds,
        long presignBundleTtlSeconds
) {
}
