# DotShelf API

## 프로젝트 개요
도서 소장 플랫폼 백엔드 API. 감성적인 서재 꾸미기를 핵심으로, 독서 기록이 소셜 콘텐츠로 이어지는 서비스.

## 기술 스택
- Java 21, Spring Boot 4.0.5, Gradle Kotlin DSL
- PostgreSQL 17, Redis 7.4
- Spring Data JPA (Hibernate 7.0) + QueryDSL 7.1 (OpenFeign)
- Spring Security + OAuth2 소셜 로그인
- Nginx 1.28.3 (리버스 프록시)

## 아키텍처
- 레이어드 아키텍처: Controller → Service → Repository
- 도메인별 패키지 분리: user, book, bookshelf, review, follow

## 브랜치 전략
- master: 프로덕션 (현재 미사용)
- dev: 개발 통합 + CD 배포
- feature/be-*, feature/aos-*, feature/ios-*

## 로컬 실행
1. docker compose up -d
2. IDE에서 local 프로필로 Spring Boot 실행
