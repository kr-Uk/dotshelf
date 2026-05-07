package com.dotshelf.support;

import com.dotshelf.domain.user.entity.Follow;
import com.dotshelf.domain.user.entity.User;
import com.dotshelf.domain.user.entity.UserBlock;

public class TestUsers {

    public User user(Long id, String nickname) {
        return TestFixtures.user(id, nickname);
    }

    public User user(String nickname) {
        return TestFixtures.user(nickname);
    }

    public Follow follow(Long followerUserId, Long followingUserId) {
        return TestFixtures.follow(followerUserId, followingUserId);
    }

    public UserBlock block(Long blockerUserId, Long blockedUserId) {
        return TestFixtures.userBlock(blockerUserId, blockedUserId);
    }
}
