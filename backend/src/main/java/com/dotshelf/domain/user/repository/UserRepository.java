package com.dotshelf.domain.user.repository;

import java.time.Instant;
import java.util.List;
import java.util.Optional;

import org.springframework.data.domain.Page;
import org.springframework.data.domain.Pageable;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.JpaSpecificationExecutor;

import com.dotshelf.domain.user.entity.User;

public interface UserRepository extends JpaRepository<User, Long>, JpaSpecificationExecutor<User> {

    boolean existsByNickname(String nickname);

    boolean existsByEmail(String email);

    Optional<User> findByEmail(String email);

    List<User> findByRoleIgnoreCase(String role);

    Page<User> findByRoleIgnoreCase(String role, Pageable pageable);

    long countByRoleIgnoreCase(String role);

    long countByStatus(String status);

    long countByLastLoginAtGreaterThanEqual(Instant since);

    long countByCreatedAtGreaterThanEqual(Instant since);

    long countByCreatedAtBetween(Instant start, Instant end);
}
