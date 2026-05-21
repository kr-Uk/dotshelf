package com.dotshelf.support;

import org.junit.jupiter.api.condition.EnabledIfSystemProperty;
import org.springframework.test.context.DynamicPropertyRegistry;
import org.springframework.test.context.DynamicPropertySource;
import org.testcontainers.containers.PostgreSQLContainer;
import org.testcontainers.junit.jupiter.Container;
import org.testcontainers.junit.jupiter.Testcontainers;

/**
 * PostgreSQL 전용 Repository/통합 테스트가 필요할 때 확장한다.
 * 기본 테스트 스위트는 로컬 실행 안정성을 위해 H2를 사용하고, 이 베이스는
 * `-DrunPostgresTests=true`가 명시된 환경에서만 활성화하는 테스트에 붙인다.
 */
@Testcontainers(disabledWithoutDocker = true)
@EnabledIfSystemProperty(named = "runPostgresTests", matches = "true")
public abstract class PostgresTestContainerSupport {

    @Container
    protected static final PostgreSQLContainer<?> POSTGRES = new PostgreSQLContainer<>("postgres:16-alpine")
            .withDatabaseName("dotshelf_test")
            .withUsername("dotshelf_test")
            .withPassword("dotshelf_test");

    @DynamicPropertySource
    static void postgresProperties(DynamicPropertyRegistry registry) {
        registry.add("spring.datasource.url", POSTGRES::getJdbcUrl);
        registry.add("spring.datasource.username", POSTGRES::getUsername);
        registry.add("spring.datasource.password", POSTGRES::getPassword);
        registry.add("spring.datasource.driver-class-name", POSTGRES::getDriverClassName);
    }
}
