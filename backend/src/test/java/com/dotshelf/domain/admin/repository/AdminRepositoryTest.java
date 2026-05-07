package com.dotshelf.domain.admin.repository;

import static org.assertj.core.api.Assertions.assertThat;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.data.domain.PageRequest;
import org.springframework.test.context.ActiveProfiles;

import com.dotshelf.support.TestFixtures;

@DataJpaTest
@ActiveProfiles("test")
class AdminRepositoryTest {

    @Autowired
    private AdminAnnouncementRepository announcementRepository;

    @Autowired
    private AdminUserNoteRepository userNoteRepository;

    @Autowired
    private MaintenanceSettingRepository maintenanceSettingRepository;

    @Autowired
    private PolicyVersionRepository policyVersionRepository;

    @Autowired
    private SettlementWithdrawalRepository withdrawalRepository;

    @Autowired
    private CreatorProfileRepository creatorProfileRepository;

    @Autowired
    private AdminAuditLogRepository auditLogRepository;

    @Test
    @DisplayName("공지와 정책은 id 역순 페이지 조회를 제공한다")
    void announcementAndPolicy_orderByIdDesc() {
        var old = announcementRepository.save(TestFixtures.announcement(null, "old"));
        var recent = announcementRepository.save(TestFixtures.announcement(null, "recent"));
        policyVersionRepository.save(TestFixtures.policyVersion(null, "v1"));
        policyVersionRepository.save(TestFixtures.policyVersion(null, "v2"));

        assertThat(announcementRepository.findAllByOrderByIdDesc())
                .extracting("id")
                .containsExactly(recent.getId(), old.getId());
        assertThat(policyVersionRepository.findAllByOrderByIdDesc(PageRequest.of(0, 1)).getContent())
                .hasSize(1);
    }

    @Test
    @DisplayName("관리자 사용자 메모는 userId 기준 id 역순으로 조회한다")
    void adminUserNote_findByUserIdOrderByIdDesc() {
        var old = userNoteRepository.save(TestFixtures.adminUserNote(null, 2L, 1L));
        var recent = userNoteRepository.save(TestFixtures.adminUserNote(null, 2L, 1L));
        userNoteRepository.save(TestFixtures.adminUserNote(null, 3L, 1L));

        assertThat(userNoteRepository.findByUserIdOrderByIdDesc(2L))
                .extracting("id")
                .containsExactly(recent.getId(), old.getId());
    }

    @Test
    @DisplayName("점검 설정은 가장 최근 row를 조회한다")
    void maintenance_findLatest() {
        maintenanceSettingRepository.save(TestFixtures.maintenanceSetting(null, false));
        var latest = maintenanceSettingRepository.save(TestFixtures.maintenanceSetting(null, true));

        assertThat(maintenanceSettingRepository.findFirstByOrderByIdDesc())
                .get()
                .extracting("id")
                .isEqualTo(latest.getId());
    }

    @Test
    @DisplayName("정산 출금 요청은 상태별 요청 페이지 합계를 계산한다")
    void withdrawal_sumByStatus() {
        withdrawalRepository.save(TestFixtures.withdrawal(null, 10L, 100, "REQUESTED"));
        withdrawalRepository.save(TestFixtures.withdrawal(null, 11L, 200, "requested"));
        withdrawalRepository.save(TestFixtures.withdrawal(null, 12L, 300, "DONE"));

        assertThat(withdrawalRepository.sumRequestedPagesByStatus("REQUESTED")).isEqualTo(300);
    }

    @Test
    @DisplayName("크리에이터 프로필은 userId로 조회하고 id 역순 페이지를 제공한다")
    void creatorProfile_findByUserIdAndOrder() {
        creatorProfileRepository.save(TestFixtures.creatorProfile(null, 10L));
        var second = creatorProfileRepository.save(TestFixtures.creatorProfile(null, 11L));

        assertThat(creatorProfileRepository.findByUserId(10L)).isPresent();
        assertThat(creatorProfileRepository.findAllByOrderByIdDesc(PageRequest.of(0, 1)).getContent())
                .extracting("id")
                .containsExactly(second.getId());
    }

    @Test
    @DisplayName("감사 로그는 JpaSpecificationExecutor로 조건 조회가 가능하다")
    void auditLog_specification() {
        auditLogRepository.save(TestFixtures.auditLog(null, 1L, "USER_SUSPEND"));
        auditLogRepository.save(TestFixtures.auditLog(null, 2L, "REPORT_DISMISS"));

        var result = auditLogRepository.findAll((root, query, cb) ->
                cb.equal(root.get("actorUserId"), 1L));

        assertThat(result).hasSize(1);
        assertThat(result.get(0).getAction()).isEqualTo("USER_SUSPEND");
    }
}
