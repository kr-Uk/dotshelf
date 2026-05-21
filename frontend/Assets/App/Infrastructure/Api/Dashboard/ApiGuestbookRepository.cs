using System;
using App.Application.Dashboard;
using App.Domain.Dashboard;
using App.Infrastructure.Api.Dtos;

namespace App.Infrastructure.Api.Dashboard
{
    public sealed class ApiGuestbookRepository : IGuestbookRepository
    {
        private readonly NotebookApiClient client;

        public ApiGuestbookRepository(NotebookApiClient client)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public void LoadPage(long spaceId, string cursor, int limit, Action<GuestbookListPage> onSuccess, Action<DashboardApiError> onError)
        {
            client.GetGuestbooks(
                spaceId,
                cursor,
                limit,
                onSuccess: dto => onSuccess?.Invoke(DashboardDtoMapper.MapGuestbookPage(dto)),
                onError: error => onError?.Invoke(DashboardDtoMapper.MapError(error)));
        }

        public void Write(WriteGuestbookCommand command, Action<Guestbook> onSuccess, Action<DashboardApiError> onError)
        {
            GuestbookRequestDto request = new GuestbookRequestDto
            {
                PostItInventoryId = command.PostItInventoryId,
                Content = command.Content,
                VisibilityScope = "PUBLIC",
                Anonymous = command.Anonymous
            };

            client.CreateGuestbook(
                command.SpaceId,
                request,
                onSuccess: dto => onSuccess?.Invoke(DashboardDtoMapper.MapGuestbook(dto)),
                onError: error => onError?.Invoke(DashboardDtoMapper.MapError(error)));
        }

        public void WriteReply(WriteGuestbookReplyCommand command, Action<GuestbookReply> onSuccess, Action<DashboardApiError> onError)
        {
            GuestbookReplyRequestDto request = new GuestbookReplyRequestDto
            {
                PostItInventoryId = command.PostItInventoryId,
                Content = command.Content,
                VisibilityScope = "PUBLIC"
            };

            client.ReplyGuestbook(
                command.GuestbookId,
                request,
                onSuccess: dto => onSuccess?.Invoke(DashboardDtoMapper.MapReply(dto)),
                onError: error => onError?.Invoke(DashboardDtoMapper.MapError(error)));
        }

        public void Delete(long guestbookId, Action onSuccess, Action<DashboardApiError> onError)
        {
            client.DeleteGuestbook(
                guestbookId,
                onSuccess: _ => onSuccess?.Invoke(),
                onError: error => onError?.Invoke(DashboardDtoMapper.MapError(error)));
        }

        public void DeleteReply(long guestbookId, long replyId, Action onSuccess, Action<DashboardApiError> onError)
        {
            client.DeleteGuestbookReply(
                guestbookId,
                replyId,
                onSuccess: _ => onSuccess?.Invoke(),
                onError: error => onError?.Invoke(DashboardDtoMapper.MapError(error)));
        }

        public void Report(ReportGuestbookCommand command, Action onSuccess, Action<DashboardApiError> onError)
        {
            ReportRequestDto request = new ReportRequestDto
            {
                TargetType = command.Target == GuestbookReportTarget.Reply ? "GUESTBOOK_REPLY" : "GUESTBOOK",
                TargetId = command.TargetId,
                ReasonType = command.ReasonType,
                ReasonDetail = command.ReasonDetail
            };

            client.ReportContent(
                request,
                onSuccess: _ => onSuccess?.Invoke(),
                onError: error => onError?.Invoke(DashboardDtoMapper.MapError(error)));
        }
    }
}
