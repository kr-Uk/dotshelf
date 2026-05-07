package com.dotshelf.domain.report.repository;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.dao.DataIntegrityViolationException;
import org.springframework.test.context.ActiveProfiles;

import com.dotshelf.support.TestFixtures;

@DataJpaTest
@ActiveProfiles("test")
class ReportRepositoryTest {

    @Autowired
    private ReportRepository reportRepository;

    @Test
    @DisplayName("reporterUserId, targetType, targetId 조합으로 중복 신고 여부를 확인한다")
    void existsByReporterUserIdAndTargetTypeAndTargetId() {
        reportRepository.saveAndFlush(TestFixtures.report(1L, "BOOK", 10L));

        assertThat(reportRepository.existsByReporterUserIdAndTargetTypeAndTargetId(1L, "BOOK", 10L)).isTrue();
        assertThat(reportRepository.existsByReporterUserIdAndTargetTypeAndTargetId(1L, "BOOK", 11L)).isFalse();
    }

    @Test
    @DisplayName("같은 사용자와 대상에 대한 중복 신고는 unique 제약으로 막는다")
    void uniqueReporterTarget() {
        reportRepository.saveAndFlush(TestFixtures.report(1L, "BOOK", 10L));

        assertThatThrownBy(() -> reportRepository.saveAndFlush(TestFixtures.report(1L, "BOOK", 10L)))
                .isInstanceOf(DataIntegrityViolationException.class);
    }
}
