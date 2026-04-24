package com.dotshelf.global.common;

import java.time.Instant;
import java.util.LinkedHashMap;
import java.util.Map;

import org.springframework.boot.health.actuate.endpoint.HealthDescriptor;
import org.springframework.boot.health.actuate.endpoint.HealthEndpoint;
import org.springframework.boot.health.contributor.Status;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.security.SecurityRequirements;
import io.swagger.v3.oas.annotations.tags.Tag;
import lombok.RequiredArgsConstructor;

@Tag(name = "Health", description = "서비스 상태 확인")
@RestController
@RequestMapping("/api")
@RequiredArgsConstructor
public class HealthController {

    private final HealthEndpoint healthEndpoint;

    @Operation(summary = "헬스체크", description = "배포 파이프라인/컨테이너 healthcheck 가 사용. DB/Redis/Flyway 등 의존성까지 실제 상태를 반영한다.")
    @SecurityRequirements({})
    @GetMapping("/health")
    public ResponseEntity<ApiResponse<Map<String, Object>>> health() {
        HealthDescriptor descriptor = healthEndpoint.health();
        Status status = descriptor.getStatus();

        Map<String, Object> body = new LinkedHashMap<>();
        body.put("status", status.getCode());
        body.put("service", "dotshelf-api");
        body.put("timestamp", Instant.now().toString());
        body.put("details", descriptor);

        if (Status.UP.equals(status)) {
            return ResponseEntity.ok(ApiResponse.success(body));
        }
        // 한 개 이상의 의존성 비정상 → 503 으로 CI/컨테이너 헬스체크에 장애 신호 전달
        return ResponseEntity.status(503).body(ApiResponse.success(body));
    }
}
