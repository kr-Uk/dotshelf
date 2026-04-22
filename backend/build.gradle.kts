plugins {
    java
    id("org.springframework.boot") version "4.0.5"
    id("io.spring.dependency-management") version "1.1.7"
}

group = "com.dotshelf"
version = "0.0.1-SNAPSHOT"

java {
    toolchain {
        languageVersion.set(JavaLanguageVersion.of(21))
    }
}

configurations {
    compileOnly {
        extendsFrom(configurations.annotationProcessor.get())
    }
}

repositories {
    mavenCentral()
}

dependencies {
    // Spring Boot starters
    implementation("org.springframework.boot:spring-boot-starter-web")
    implementation("org.springframework.boot:spring-boot-starter-data-jpa")
    implementation("org.springframework.boot:spring-boot-starter-security")
    implementation("org.springframework.boot:spring-boot-starter-oauth2-client")
    implementation("org.springframework.boot:spring-boot-starter-validation")
    implementation("org.springframework.boot:spring-boot-starter-data-redis")

    // OpenAPI / Swagger
    implementation("org.springdoc:springdoc-openapi-starter-webmvc-ui:2.8.0")

    // JWT (jjwt)
    implementation("io.jsonwebtoken:jjwt-api:0.12.6")
    runtimeOnly("io.jsonwebtoken:jjwt-impl:0.12.6")
    runtimeOnly("io.jsonwebtoken:jjwt-jackson:0.12.6")

    // PostgreSQL
    runtimeOnly("org.postgresql:postgresql")

    // Flyway (DB 스키마 마이그레이션)
    // Spring Boot 4 부터 Flyway autoconfig 가 별도 모듈(spring-boot-flyway)로 분리됨 → 반드시 함께 포함
    implementation("org.springframework.boot:spring-boot-flyway")
    implementation("org.flywaydb:flyway-core")
    implementation("org.flywaydb:flyway-database-postgresql")

    // Lombok
    compileOnly("org.projectlombok:lombok")
    annotationProcessor("org.projectlombok:lombok")

    // QueryDSL (openfeign fork, Jakarta-compatible)
    implementation("io.github.openfeign.querydsl:querydsl-jpa:7.1")
    annotationProcessor("io.github.openfeign.querydsl:querydsl-apt:7.1:jakarta")
    annotationProcessor("jakarta.annotation:jakarta.annotation-api")
    annotationProcessor("jakarta.persistence:jakarta.persistence-api")

    // Test
    testImplementation("org.springframework.boot:spring-boot-starter-test")
    testImplementation("org.springframework.security:spring-security-test")
    testRuntimeOnly("org.junit.platform:junit-platform-launcher")
}

// QueryDSL generated sources directory
val querydslDir = layout.buildDirectory.dir("generated/querydsl")

sourceSets {
    main {
        java {
            srcDir(querydslDir)
        }
    }
}

tasks.withType<JavaCompile>().configureEach {
    options.generatedSourceOutputDirectory.set(querydslDir.get().asFile)
}

tasks.named<Delete>("clean") {
    delete(querydslDir)
}

tasks.withType<Test> {
    useJUnitPlatform()
}
