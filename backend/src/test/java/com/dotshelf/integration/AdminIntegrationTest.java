package com.dotshelf.integration;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.delete;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.put;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.test.web.servlet.MockMvc;

import com.dotshelf.domain.admin.repository.AdminAnnouncementRepository;
import com.dotshelf.domain.admin.repository.AdminAuditLogRepository;
import com.dotshelf.domain.admin.repository.MaintenanceSettingRepository;
import com.dotshelf.domain.admin.repository.SettlementAdjustmentRepository;
import com.dotshelf.domain.asset.repository.AssetPurchaseRepository;
import com.dotshelf.domain.payment.entity.PageWallet;
import com.dotshelf.domain.payment.repository.PageWalletRepository;
import com.dotshelf.domain.user.entity.User;
import com.dotshelf.domain.user.repository.UserRepository;
import com.dotshelf.support.IntegrationTestSupport;
import com.dotshelf.support.TestFixtures;

class AdminIntegrationTest extends IntegrationTestSupport {

    @Autowired
    private MockMvc mockMvc;

    @Autowired
    private UserRepository userRepository;

    @Autowired
    private PageWalletRepository walletRepository;

    @Autowired
    private AssetPurchaseRepository purchaseRepository;

    @Autowired
    private SettlementAdjustmentRepository settlementAdjustmentRepository;

    @Autowired
    private AdminAuditLogRepository auditLogRepository;

    @Autowired
    private AdminAnnouncementRepository announcementRepository;

    @Autowired
    private MaintenanceSettingRepository maintenanceSettingRepository;

    @Test
    @DisplayName("일반 사용자는 실제 Security/Service 경계를 거쳐 관리자 API에서 거부된다")
    void nonAdminUser_adminApiReturnsForbidden() throws Exception {
        User user = userRepository.saveAndFlush(TestFixtures.user("plain-user"));

        mockMvc.perform(get("/v1/admin/me")
                        .header(TEST_USER_HEADER, user.getId().toString()))
                .andExpect(status().isForbidden())
                .andExpect(jsonPath("$.code").value("A002"));
    }

    @Test
    @DisplayName("관리자 잔액 조정 API는 wallet, settlement_adjustments, audit log를 함께 저장한다")
    void adjustBalance_persistsWalletAdjustmentAndAuditLog() throws Exception {
        User admin = adminUser("admin-adjust");
        User target = userRepository.saveAndFlush(TestFixtures.user("balance-target"));

        mockMvc.perform(post("/v1/admin/users/{targetUserId}/adjust-balance", target.getId())
                        .header(TEST_USER_HEADER, admin.getId().toString())
                        .header("X-Forwarded-For", "203.0.113.10, 10.0.0.1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"deltaPages":120,"reason":"EVENT_REWARD","memo":"이벤트 지급"}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.userId").value(target.getId().toString()))
                .andExpect(jsonPath("$.result.balancePages").value(120))
                .andExpect(jsonPath("$.result.adjustment.deltaPages").value(120));

        assertThat(walletRepository.findByUserId(target.getId()).orElseThrow().getBalancePages()).isEqualTo(120);
        assertThat(settlementAdjustmentRepository.findAll())
                .singleElement()
                .satisfies(adjustment -> {
                    assertThat(adjustment.getUserId()).isEqualTo(target.getId());
                    assertThat(adjustment.getDeltaPages()).isEqualTo(120);
                    assertThat(adjustment.getAdjustedByUserId()).isEqualTo(admin.getId());
                });
        assertThat(auditLogRepository.findAll())
                .singleElement()
                .satisfies(log -> {
                    assertThat(log.getAction()).isEqualTo("BALANCE_ADJUST");
                    assertThat(log.getActorUserId()).isEqualTo(admin.getId());
                    assertThat(log.getTargetId()).isEqualTo(target.getId());
                    assertThat(log.getIpAddress()).isEqualTo("203.0.113.10");
                });
    }

    @Test
    @DisplayName("관리자 환불 API는 구매 상태를 REFUNDED로 바꾸고 지갑과 감사로그를 반영한다")
    void refundPurchase_persistsRefundWalletAdjustmentAndAuditLog() throws Exception {
        User admin = adminUser("admin-refund");
        User target = userRepository.saveAndFlush(TestFixtures.user("refund-target"));
        var purchase = purchaseRepository.saveAndFlush(TestFixtures.assetPurchase(null, target.getId(), 99L, 199L, 70));
        walletRepository.saveAndFlush(PageWallet.builder()
                .userId(target.getId())
                .balancePages(5)
                .build());

        mockMvc.perform(post("/v1/admin/users/{targetUserId}/refund", target.getId())
                        .header(TEST_USER_HEADER, admin.getId().toString())
                        .header("X-Real-IP", "198.51.100.7")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"purchaseId":"%s","reason":"CUSTOMER_REQUEST","comment":"환불 처리"}
                                """.formatted(purchase.getId())))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.purchaseId").value(purchase.getId().toString()))
                .andExpect(jsonPath("$.result.refundedPages").value(70))
                .andExpect(jsonPath("$.result.balancePages").value(75));

        assertThat(purchaseRepository.findById(purchase.getId()).orElseThrow().getPurchaseStatus()).isEqualTo("REFUNDED");
        assertThat(walletRepository.findByUserId(target.getId()).orElseThrow().getBalancePages()).isEqualTo(75);
        assertThat(settlementAdjustmentRepository.findAll())
                .singleElement()
                .satisfies(adjustment -> {
                    assertThat(adjustment.getDeltaPages()).isEqualTo(70);
                    assertThat(adjustment.getReason()).isEqualTo("CUSTOMER_REQUEST");
                });
        assertThat(auditLogRepository.findAll())
                .singleElement()
                .satisfies(log -> {
                    assertThat(log.getAction()).isEqualTo("USER_REFUND");
                    assertThat(log.getTargetType()).isEqualTo("ASSET_PURCHASE");
                    assertThat(log.getTargetId()).isEqualTo(purchase.getId());
                    assertThat(log.getIpAddress()).isEqualTo("198.51.100.7");
                });
    }

    @Test
    @DisplayName("공지 생성/수정/삭제와 점검 변경은 실제 admin schema row와 감사 로그를 남긴다")
    void operationsLifecycle_persistsAnnouncementMaintenanceAndAuditLogs() throws Exception {
        User admin = adminUser("admin-ops");

        mockMvc.perform(post("/v1/admin/operations/announcements")
                        .header(TEST_USER_HEADER, admin.getId().toString())
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"title":"공지","body":"첫 본문","audience":"all_users","urgent":false}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.title").value("공지"));
        var announcement = announcementRepository.findAllByOrderByIdDesc().get(0);

        mockMvc.perform(put("/v1/admin/operations/announcements/{announcementId}", announcement.getId())
                        .header(TEST_USER_HEADER, admin.getId().toString())
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"title":"수정 공지","body":"수정 본문","audience":"all_users","urgent":true}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.title").value("수정 공지"))
                .andExpect(jsonPath("$.result.urgent").value(true));

        mockMvc.perform(delete("/v1/admin/operations/announcements/{announcementId}", announcement.getId())
                        .header(TEST_USER_HEADER, admin.getId().toString()))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.success").value(true));

        mockMvc.perform(post("/v1/admin/operations/maintenance")
                        .header(TEST_USER_HEADER, admin.getId().toString())
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"enabled":true,"message":"정기 점검"}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.enabled").value(true));

        assertThat(announcementRepository.findById(announcement.getId())).isEmpty();
        assertThat(maintenanceSettingRepository.findFirstByOrderByIdDesc())
                .get()
                .satisfies(setting -> {
                    assertThat(setting.isEnabled()).isTrue();
                    assertThat(setting.getMessage()).isEqualTo("정기 점검");
                    assertThat(setting.getUpdatedByUserId()).isEqualTo(admin.getId());
                });
        assertThat(auditLogRepository.findAll())
                .extracting("action")
                .containsExactlyInAnyOrder(
                        "ANNOUNCEMENT_CREATE",
                        "ANNOUNCEMENT_UPDATE",
                        "ANNOUNCEMENT_DELETE",
                        "MAINTENANCE_UPDATE"
                );
    }

    private User adminUser(String nickname) {
        User admin = TestFixtures.user(nickname);
        admin.changeRole("ADMIN");
        return userRepository.saveAndFlush(admin);
    }
}
