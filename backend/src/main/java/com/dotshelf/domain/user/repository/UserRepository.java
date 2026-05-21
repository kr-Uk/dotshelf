package com.dotshelf.domain.user.repository;

import java.time.Instant;
import java.util.List;
import java.util.Optional;

import org.springframework.data.domain.Page;
import org.springframework.data.domain.Pageable;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.JpaSpecificationExecutor;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import com.dotshelf.domain.user.entity.User;

public interface UserRepository extends JpaRepository<User, Long>, JpaSpecificationExecutor<User> {

    interface SessionInvalidationView {
        Long getId();

        String getStatus();

        Instant getSessionInvalidatedAt();

        Instant getDeletedAt();
    }

    boolean existsByNickname(String nickname);

    boolean existsByEmail(String email);

    Optional<User> findByEmail(String email);

    @Query("""
            select u.id as id,
                   u.status as status,
                   u.sessionInvalidatedAt as sessionInvalidatedAt,
                   u.deletedAt as deletedAt
            from User u
            where u.id = :id
            """)
    Optional<SessionInvalidationView> findSessionInvalidationById(@Param("id") Long id);

    List<User> findByRoleIgnoreCase(String role);

    Page<User> findByRoleIgnoreCase(String role, Pageable pageable);

    long countByRoleIgnoreCase(String role);

    long countByStatus(String status);

    long countByLastLoginAtGreaterThanEqual(Instant since);

    long countByCreatedAtGreaterThanEqual(Instant since);

    long countByCreatedAtBetween(Instant start, Instant end);
}
