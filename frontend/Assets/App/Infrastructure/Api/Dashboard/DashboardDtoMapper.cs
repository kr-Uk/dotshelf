using System;
using System.Collections.Generic;
using System.Globalization;
using App.Application.Dashboard;
using App.Domain.Dashboard;
using App.Infrastructure.Api.Dtos;

namespace App.Infrastructure.Api.Dashboard
{
    internal static class DashboardDtoMapper
    {
        public static DashboardApiError MapError(ApiErrorResponseDto error)
        {
            return new DashboardApiError
            {
                Code = error?.Code,
                Message = error?.Message,
                Error = error?.Error
            };
        }

        public static VisibilityScope ParseVisibility(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return VisibilityScope.Public;
            }

            switch (raw.ToUpperInvariant())
            {
                case "FOLLOWERS_ONLY":
                case "FOLLOWERS":
                    return VisibilityScope.FollowersOnly;
                case "PRIVATE":
                    return VisibilityScope.Private;
                default:
                    return VisibilityScope.Public;
            }
        }

        public static string FormatVisibility(VisibilityScope scope)
        {
            switch (scope)
            {
                case VisibilityScope.FollowersOnly:
                    return "FOLLOWERS_ONLY";
                case VisibilityScope.Private:
                    return "PRIVATE";
                default:
                    return "PUBLIC";
            }
        }

        public static DateTimeOffset ParseTimestamp(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return DateTimeOffset.MinValue;
            }

            if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset parsed))
            {
                return parsed;
            }

            return DateTimeOffset.MinValue;
        }

        public static Space MapSpace(SpaceResponseDto dto)
        {
            if (dto == null)
            {
                return null;
            }

            return new Space(
                id: dto.Id,
                ownerUserId: dto.OwnerUserId,
                visibility: ParseVisibility(dto.VisibilityScope),
                guestbookVisibility: ParseVisibility(dto.GuestbookVisibilityScope),
                visitCount: dto.VisitCount,
                likeCount: dto.LikeCount,
                liked: dto.Liked,
                layoutVersion: dto.LayoutVersion,
                currentThemeInventoryId: dto.CurrentThemeInventoryId);
        }

        public static DashboardSnapshot MapSnapshot(DashboardResponseDto dto)
        {
            if (dto == null)
            {
                return null;
            }

            return new DashboardSnapshot(
                space: MapSpace(dto.Space),
                ownerNickname: dto.OwnerNickname,
                ownerProfileImageUrl: dto.OwnerProfileImageUrl,
                ownerIntroduction: dto.OwnerIntroduction,
                followerCount: dto.FollowerCount,
                followingCount: dto.FollowingCount,
                bookshelfCount: dto.BookshelfCount,
                bookCount: dto.BookCount,
                readingRecordCount: dto.ReadingRecordCount,
                weeklyVisitorCount: dto.WeeklyVisitorCount);
        }

        public static GuestbookReply MapReply(GuestbookReplyResponseDto dto)
        {
            if (dto == null)
            {
                return null;
            }

            return new GuestbookReply(
                id: dto.Id,
                guestbookId: dto.GuestbookId,
                writerUserId: dto.WriterUserId,
                writerNickname: dto.WriterNickname,
                postItInventoryId: dto.PostItInventoryId.GetValueOrDefault(),
                content: dto.Content,
                createdAt: ParseTimestamp(dto.CreatedAt));
        }

        public static Guestbook MapGuestbook(GuestbookResponseDto dto)
        {
            if (dto == null)
            {
                return null;
            }

            return new Guestbook(
                id: dto.Id,
                spaceId: dto.SpaceId,
                writerUserId: dto.WriterUserId,
                displayedNickname: dto.WriterNickname,
                anonymous: dto.Anonymous,
                postItInventoryId: dto.PostItInventoryId.GetValueOrDefault(),
                content: dto.Content,
                visibility: ParseVisibility(dto.VisibilityScope),
                createdAt: ParseTimestamp(dto.CreatedAt),
                reply: MapReply(dto.Reply));
        }

        public static GuestbookListPage MapGuestbookPage(GuestbookListResponseDto dto)
        {
            List<Guestbook> items = new List<Guestbook>();
            if (dto?.Guestbooks != null)
            {
                for (int i = 0; i < dto.Guestbooks.Count; i++)
                {
                    Guestbook gb = MapGuestbook(dto.Guestbooks[i]);
                    if (gb != null)
                    {
                        items.Add(gb);
                    }
                }
            }
            return new GuestbookListPage(items, dto?.NextCursor);
        }

        public static UserSummary MapUserSummary(UserSummaryResponseDto dto)
        {
            if (dto == null)
            {
                return null;
            }

            return new UserSummary(
                id: dto.Id,
                nickname: dto.Nickname,
                profileImageUrl: dto.ProfileImageUrl,
                mutual: dto.Mutual,
                following: dto.Following,
                blocked: dto.Blocked);
        }

        public static UserListPage MapUserListPage(UserListResponseDto dto)
        {
            List<UserSummary> items = new List<UserSummary>();
            if (dto?.Users != null)
            {
                for (int i = 0; i < dto.Users.Count; i++)
                {
                    UserSummary u = MapUserSummary(dto.Users[i]);
                    if (u != null)
                    {
                        items.Add(u);
                    }
                }
            }
            return new UserListPage(items, dto?.NextCursor);
        }

        public static GuestbookPostItWhitelist MapWhitelist(GuestbookPostItWhitelistResponseDto dto)
        {
            if (dto == null)
            {
                return new GuestbookPostItWhitelist(true, null);
            }

            return new GuestbookPostItWhitelist(dto.AllowAll, dto.AllowedAssetIds ?? new List<long>());
        }
    }
}
