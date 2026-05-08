package com.dotshelf.global.storage;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.mockito.Mockito.mock;

import java.io.IOException;
import java.net.URI;
import java.time.Duration;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.ArgumentCaptor;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;
import org.springframework.mock.web.MockMultipartFile;
import org.springframework.web.multipart.MultipartFile;

import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;
import com.dotshelf.global.storage.S3StorageService.ObjectMetadata;

import software.amazon.awssdk.core.ResponseBytes;
import software.amazon.awssdk.core.exception.SdkClientException;
import software.amazon.awssdk.core.sync.RequestBody;
import software.amazon.awssdk.services.s3.S3Client;
import software.amazon.awssdk.services.s3.model.GetObjectRequest;
import software.amazon.awssdk.services.s3.model.GetObjectResponse;
import software.amazon.awssdk.services.s3.model.HeadObjectRequest;
import software.amazon.awssdk.services.s3.model.HeadObjectResponse;
import software.amazon.awssdk.services.s3.model.NoSuchKeyException;
import software.amazon.awssdk.services.s3.model.PutObjectRequest;
import software.amazon.awssdk.services.s3.presigner.S3Presigner;
import software.amazon.awssdk.services.s3.presigner.model.PresignedPutObjectRequest;
import software.amazon.awssdk.services.s3.presigner.model.PutObjectPresignRequest;

@ExtendWith(MockitoExtension.class)
class S3StorageServiceTest {

    @Mock
    private S3Client s3Client;

    @Mock
    private S3Presigner s3Presigner;

    private S3StorageService s3StorageService;

    @BeforeEach
    void setUp() {
        s3StorageService = new S3StorageService(
                s3Client,
                s3Presigner,
                new S3StorageProperties(
                        "dotshelf-bucket",
                        "ap-northeast-2",
                        "test-access",
                        "test-secret",
                        "https://cdn.example.com/root/",
                        300,
                        1800));
    }

    @Test
    @DisplayName("putObject는 bucket/key/contentType/contentLength를 S3 요청에 반영한다")
    void putObject_validFile_sendsS3PutRequest() {
        MockMultipartFile file = new MockMultipartFile("file", "thumbnail.png", "image/png", new byte[] {1, 2, 3});
        ArgumentCaptor<PutObjectRequest> requestCaptor = ArgumentCaptor.forClass(PutObjectRequest.class);

        s3StorageService.putObject("spaces/1/thumbnail-1.png", file, "image/png");

        then(s3Client).should().putObject(requestCaptor.capture(), any(RequestBody.class));
        PutObjectRequest request = requestCaptor.getValue();
        assertThat(request.bucket()).isEqualTo("dotshelf-bucket");
        assertThat(request.key()).isEqualTo("spaces/1/thumbnail-1.png");
        assertThat(request.contentType()).isEqualTo("image/png");
        assertThat(request.contentLength()).isEqualTo(3L);
    }

    @Test
    @DisplayName("putObject에서 파일 스트림을 읽지 못하면 STORAGE_INTERNAL_ERROR를 던진다")
    void putObject_ioFailure_throwsStorageInternalError() throws Exception {
        MultipartFile file = mock(MultipartFile.class);
        given(file.getSize()).willReturn(10L);
        given(file.getInputStream()).willThrow(new IOException("broken"));

        assertThatThrownBy(() -> s3StorageService.putObject("spaces/1/a.png", file, "image/png"))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.STORAGE_INTERNAL_ERROR);
    }

    @Test
    @DisplayName("readObjectHeadBytes는 S3 range GET으로 앞부분 바이트만 읽는다")
    void readObjectHeadBytes_readsRange() {
        given(s3Client.getObjectAsBytes(any(GetObjectRequest.class)))
                .willReturn(ResponseBytes.fromByteArray(
                        GetObjectResponse.builder().build(),
                        new byte[] {1, 2, 3}));
        ArgumentCaptor<GetObjectRequest> requestCaptor = ArgumentCaptor.forClass(GetObjectRequest.class);

        byte[] bytes = s3StorageService.readObjectHeadBytes("spaces/10/thumbnail/a.png", 12);

        assertThat(bytes).containsExactly(1, 2, 3);
        then(s3Client).should().getObjectAsBytes(requestCaptor.capture());
        GetObjectRequest request = requestCaptor.getValue();
        assertThat(request.bucket()).isEqualTo("dotshelf-bucket");
        assertThat(request.key()).isEqualTo("spaces/10/thumbnail/a.png");
        assertThat(request.range()).isEqualTo("bytes=0-11");
    }

    @Test
    @DisplayName("putObject에서 AWS SDK 예외가 나면 STORAGE_INTERNAL_ERROR로 감싼다")
    void putObject_sdkFailure_throwsStorageInternalError() {
        MockMultipartFile file = new MockMultipartFile("file", "a.png", "image/png", new byte[] {1});
        given(s3Client.putObject(any(PutObjectRequest.class), any(RequestBody.class)))
                .willThrow(SdkClientException.create("s3 down"));

        assertThatThrownBy(() -> s3StorageService.putObject("spaces/1/a.png", file, "image/png"))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.STORAGE_INTERNAL_ERROR);
    }

    @Test
    @DisplayName("presignPut은 presigner 요청을 만들고 uploadUrl/key/ttl을 반환한다")
    void presignPut_validRequest_returnsUploadUrl() throws Exception {
        PresignedPutObjectRequest presigned = mock(PresignedPutObjectRequest.class);
        given(presigned.url()).willReturn(URI.create("https://upload.example.com/object").toURL());
        given(s3Presigner.presignPutObject(any(PutObjectPresignRequest.class))).willReturn(presigned);
        ArgumentCaptor<PutObjectPresignRequest> requestCaptor = ArgumentCaptor.forClass(PutObjectPresignRequest.class);

        var response = s3StorageService.presignPut(
                "users/1/profile/image.png", "image/png", 1024L, Duration.ofSeconds(300));

        assertThat(response.uploadUrl()).isEqualTo("https://upload.example.com/object");
        assertThat(response.key()).isEqualTo("users/1/profile/image.png");
        assertThat(response.expiresInSeconds()).isEqualTo(300L);
        then(s3Presigner).should().presignPutObject(requestCaptor.capture());
        PutObjectPresignRequest request = requestCaptor.getValue();
        assertThat(request.signatureDuration()).isEqualTo(Duration.ofSeconds(300));
        assertThat(request.putObjectRequest().bucket()).isEqualTo("dotshelf-bucket");
        assertThat(request.putObjectRequest().key()).isEqualTo("users/1/profile/image.png");
        assertThat(request.putObjectRequest().contentType()).isEqualTo("image/png");
        assertThat(request.putObjectRequest().contentLength()).isEqualTo(1024L);
    }

    @Test
    @DisplayName("presignPut에서 AWS SDK 예외가 나면 STORAGE_INTERNAL_ERROR로 감싼다")
    void presignPut_sdkFailure_throwsStorageInternalError() {
        given(s3Presigner.presignPutObject(any(PutObjectPresignRequest.class)))
                .willThrow(SdkClientException.create("presign failed"));

        assertThatThrownBy(() -> s3StorageService.presignPut(
                "users/1/profile/a.png", "image/png", 1L, Duration.ofSeconds(300)))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.STORAGE_INTERNAL_ERROR);
    }

    @Test
    @DisplayName("headObject는 S3 metadata를 ObjectMetadata로 변환한다")
    void headObject_existingObject_returnsMetadata() {
        given(s3Client.headObject(any(HeadObjectRequest.class))).willReturn(HeadObjectResponse.builder()
                .contentType("image/webp")
                .contentLength(2048L)
                .build());
        ArgumentCaptor<HeadObjectRequest> requestCaptor = ArgumentCaptor.forClass(HeadObjectRequest.class);

        ObjectMetadata metadata = s3StorageService.headObject("users/1/profile/a.webp");

        assertThat(metadata.contentType()).isEqualTo("image/webp");
        assertThat(metadata.contentLength()).isEqualTo(2048L);
        then(s3Client).should().headObject(requestCaptor.capture());
        assertThat(requestCaptor.getValue().bucket()).isEqualTo("dotshelf-bucket");
        assertThat(requestCaptor.getValue().key()).isEqualTo("users/1/profile/a.webp");
    }

    @Test
    @DisplayName("headObject에서 객체가 없으면 STORAGE_OBJECT_NOT_UPLOADED를 던진다")
    void headObject_missingObject_throwsStorageObjectNotUploaded() {
        given(s3Client.headObject(any(HeadObjectRequest.class)))
                .willThrow(NoSuchKeyException.builder().message("missing").build());

        assertThatThrownBy(() -> s3StorageService.headObject("missing.png"))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.STORAGE_OBJECT_NOT_UPLOADED);
    }

    @Test
    @DisplayName("headObject에서 AWS SDK 예외가 나면 STORAGE_INTERNAL_ERROR로 감싼다")
    void headObject_sdkFailure_throwsStorageInternalError() {
        given(s3Client.headObject(any(HeadObjectRequest.class)))
                .willThrow(SdkClientException.create("head failed"));

        assertThatThrownBy(() -> s3StorageService.headObject("broken.png"))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.STORAGE_INTERNAL_ERROR);
    }

    @Test
    @DisplayName("publicUrl은 publicBaseUrl이 있으면 trailing slash를 제거하고 key를 붙인다")
    void publicUrl_withPublicBaseUrl_usesConfiguredBase() {
        assertThat(s3StorageService.publicUrl("users/1/profile/a.png"))
                .isEqualTo("https://cdn.example.com/root/users/1/profile/a.png");
    }

    @Test
    @DisplayName("publicUrl은 publicBaseUrl이 없으면 S3 virtual-hosted URL을 만든다")
    void publicUrl_withoutPublicBaseUrl_usesS3Url() {
        S3StorageService fallbackService = new S3StorageService(
                s3Client,
                s3Presigner,
                new S3StorageProperties("bucket", "ap-northeast-2", "", "", "", 300, 1800));

        assertThat(fallbackService.publicUrl("assets/a.bundle"))
                .isEqualTo("https://bucket.s3.ap-northeast-2.amazonaws.com/assets/a.bundle");
    }
}
