package com.dotshelf.domain.notification.repository;

import java.util.List;

import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.notification.entity.Notification;

public interface NotificationRepository extends JpaRepository<Notification, Long> {

    List<Notification> findByReceiverUserIdOrderByIdDesc(Long receiverUserId);

    List<Notification> findByReceiverUserIdAndIsReadFalse(Long receiverUserId);
}
