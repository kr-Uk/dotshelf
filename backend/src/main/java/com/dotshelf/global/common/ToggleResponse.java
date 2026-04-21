package com.dotshelf.global.common;

public record ToggleResponse(
        boolean active,
        long count
) {
}
