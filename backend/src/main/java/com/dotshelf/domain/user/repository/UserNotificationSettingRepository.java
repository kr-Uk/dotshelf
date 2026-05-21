package com.dotshelf.domain.user.repository;

import java.util.Collection;
import java.util.List;
import java.util.Optional;

import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.user.entity.UserNotificationSetting;

public interface UserNotificationSettingRepository extends JpaRepository<UserNotificationSetting, Long> {

    Optional<UserNotificationSetting> findByUserId(Long userId);

    List<UserNotificationSetting> findByUserIdIn(Collection<Long> userIds);
}
