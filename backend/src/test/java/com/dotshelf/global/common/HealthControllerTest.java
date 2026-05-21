package com.dotshelf.global.common;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.BDDMockito.given;

import java.util.Map;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;
import org.springframework.boot.health.actuate.endpoint.HealthDescriptor;
import org.springframework.boot.health.actuate.endpoint.HealthEndpoint;
import org.springframework.boot.health.contributor.Health;
import org.springframework.boot.health.contributor.Status;

@ExtendWith(MockitoExtension.class)
class HealthControllerTest {

    @Mock
    private HealthEndpoint healthEndpoint;

    @Test
    @DisplayName("Health status가 UP이면 200과 공통 envelope를 반환한다")
    void health_up_returnsOkEnvelope() {
        HealthController controller = new HealthController(healthEndpoint);
        HealthDescriptor descriptor = descriptor(Status.UP);
        given(healthEndpoint.health()).willReturn(descriptor);

        var response = controller.health();

        assertThat(response.getStatusCode().value()).isEqualTo(200);
        assertThat(response.getBody()).isNotNull();
        assertThat(response.getBody().isSuccess()).isTrue();
        Map<String, Object> result = response.getBody().getResult();
        assertThat(result)
                .containsEntry("status", "UP")
                .containsEntry("service", "dotshelf-api")
                .containsEntry("details", descriptor);
        assertThat(result.get("timestamp")).isInstanceOf(String.class);
    }

    @Test
    @DisplayName("Health status가 DOWN이면 503으로 컨테이너/CI에 장애 신호를 준다")
    void health_down_returnsServiceUnavailableEnvelope() {
        HealthController controller = new HealthController(healthEndpoint);
        given(healthEndpoint.health()).willReturn(descriptor(Status.DOWN));

        var response = controller.health();

        assertThat(response.getStatusCode().value()).isEqualTo(503);
        assertThat(response.getBody()).isNotNull();
        assertThat(response.getBody().getResult()).containsEntry("status", "DOWN");
    }

    private HealthDescriptor descriptor(Status status) {
        try {
            Class<?> type = Class.forName("org.springframework.boot.health.actuate.endpoint.IndicatedHealthDescriptor");
            var constructor = type.getDeclaredConstructor(Health.class);
            constructor.setAccessible(true);
            return (HealthDescriptor) constructor.newInstance(Health.status(status).build());
        } catch (ReflectiveOperationException e) {
            throw new AssertionError("HealthDescriptor test fixture 생성 실패", e);
        }
    }
}
