package com.dotshelf.global.common;

import java.util.List;

public record CursorResponse<T>(
        List<T> items,
        String nextCursor,
        boolean hasNext
) {

    public static <T> CursorResponse<T> from(List<T> items) {
        return new CursorResponse<>(items, null, false);
    }
}
