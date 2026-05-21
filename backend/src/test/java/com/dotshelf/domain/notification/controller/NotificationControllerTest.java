package com.dotshelf.domain.notification.controller;

import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.patch;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import java.util.List;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.setup.MockMvcBuilders;

import com.dotshelf.domain.notification.dto.NotificationDtos.NotificationListResponse;
import com.dotshelf.domain.notification.dto.NotificationDtos.NotificationResponse;
import com.dotshelf.domain.notification.service.NotificationService;
import com.dotshelf.global.common.CurrentUserArgumentResolver;
import com.dotshelf.global.error.GlobalExceptionHandler;

@ExtendWith(MockitoExtension.class)
class NotificationControllerTest {

    @Mock
    private NotificationService notificationService;

    private MockMvc mockMvc;

    @BeforeEach
    void setUp() {
        mockMvc = MockMvcBuilders
                .standaloneSetup(new NotificationController(notificationService))
                .setCustomArgumentResolvers(new CurrentUserArgumentResolver(true, "X-Test-Admin-User-Id"))
                .setControllerAdvice(new GlobalExceptionHandler())
                .build();
    }

    @Test
    @DisplayName("알림 목록 조회는 current userId를 전달한다")
    void getNotifications_passesCurrentUser() throws Exception {
        given(notificationService.getNotifications(1L))
                .willReturn(new NotificationListResponse(List.of(notificationResponse(10L, false))));

        mockMvc.perform(get("/v1/notifications").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.notifications[0].id").value(10))
                .andExpect(jsonPath("$.result.notifications[0].read").value(false));
    }

    @Test
    @DisplayName("알림 읽음 처리는 notificationId와 current userId를 전달한다")
    void read_passesIds() throws Exception {
        given(notificationService.read(1L, 10L)).willReturn(notificationResponse(10L, true));

        mockMvc.perform(patch("/v1/notifications/10/read").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.read").value(true));
    }

    @Test
    @DisplayName("전체 읽음 처리는 성공 envelope를 반환한다")
    void readAll_returnsSuccessEnvelope() throws Exception {
        mockMvc.perform(patch("/v1/notifications/read-all").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.success").value(true));

        then(notificationService).should().readAll(1L);
    }

    private NotificationResponse notificationResponse(Long id, boolean read) {
        return new NotificationResponse(id, 2L, "BOOK_ADDED", "USER_BOOK", 3L, "message", read);
    }
}
