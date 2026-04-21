package com.dotshelf.global.common;

import java.time.Instant;
import java.util.Map;

import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.security.SecurityRequirements;
import io.swagger.v3.oas.annotations.tags.Tag;

@Tag(name = "Health", description = "서비스 상태 확인")
@RestController
@RequestMapping("/api")
public class HealthController {

    @Operation(summary = "헬스체크", description = "배포 파이프라인과 컨테이너 healthcheck 가 사용하는 엔드포인트")
    @SecurityRequirements({})
    @GetMapping("/health")
    public ApiResponse<Map<String, Object>> health() {
        return ApiResponse.success(Map.of(
                "status", "UP",
                "service", "dotshelf-api",
                "timestamp", Instant.now().toString()
        ));
    }
}
