package com.dotshelf.domain.user.repository;

import java.util.Optional;

import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.user.entity.UserFeedSetting;

public interface UserFeedSettingRepository extends JpaRepository<UserFeedSetting, Long> {

    Optional<UserFeedSetting> findByUserId(Long userId);
}
