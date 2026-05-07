package com.dotshelf.support;

import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc;
import org.springframework.test.context.ActiveProfiles;
import org.springframework.transaction.annotation.Transactional;

/**
 * Rollback 기반 통합 테스트 공통 베이스.
 *
 * <p>{@code @TransactionalEventListener(phase = AFTER_COMMIT)}처럼 실제 commit이 필요한 흐름은
 * 이 베이스를 상속하지 말고 테스트별 cleanup으로 격리한다.
 */
@SpringBootTest
@AutoConfigureMockMvc
@ActiveProfiles("test")
@Transactional
public abstract class IntegrationTestSupport {

    protected static final String TEST_USER_HEADER = "X-Test-Admin-User-Id";
}
