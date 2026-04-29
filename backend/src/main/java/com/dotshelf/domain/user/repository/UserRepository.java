package com.dotshelf.domain.user.repository;

import java.util.Optional;
import java.util.List;

import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.user.entity.User;

public interface UserRepository extends JpaRepository<User, Long> {

    boolean existsByNickname(String nickname);

    boolean existsByEmail(String email);

    Optional<User> findByEmail(String email);

    List<User> findByRoleIgnoreCase(String role);

    long countByRoleIgnoreCase(String role);

    long countByStatus(String status);
}
