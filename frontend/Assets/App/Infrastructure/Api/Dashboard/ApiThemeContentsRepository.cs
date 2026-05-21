using System;
using System.Collections.Generic;
using App.Application.Dashboard;
using App.Infrastructure.Api.Dtos;

namespace App.Infrastructure.Api.Dashboard
{
    public sealed class ApiThemeContentsRepository : IThemeContentsRepository
    {
        private readonly NotebookApiClient client;

        public ApiThemeContentsRepository(NotebookApiClient client)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public void LoadThemeContents(long themeAssetId, Action<ThemeContents> onSuccess, Action<DashboardApiError> onError)
        {
            client.GetThemeContents(
                themeAssetId,
                dto => onSuccess?.Invoke(Map(dto)),
                error => onError?.Invoke(DashboardDtoMapper.MapError(error)));
        }

        private static ThemeContents Map(ThemeContentsResponseDto dto)
        {
            if (dto == null)
            {
                return null;
            }

            return new ThemeContents
            {
                ThemeAssetId = dto.ThemeAssetId,
                Wallpaper = dto.Wallpaper,
                Floor = dto.Floor,
                Desk = dto.Desk,
                Dashboard = dto.Dashboard,
                InteriorLight = dto.InteriorLight,
                ExteriorLight = dto.ExteriorLight,
                BookshelfSkinDefault = dto.BookshelfSkinDefault,
                Children = MapChildren(dto.Children)
            };
        }

        private static IReadOnlyList<ThemeContentItem> MapChildren(List<ThemeContentItemResponseDto> dtoChildren)
        {
            if (dtoChildren == null || dtoChildren.Count == 0)
            {
                return Array.Empty<ThemeContentItem>();
            }

            var items = new List<ThemeContentItem>(dtoChildren.Count);
            for (var i = 0; i < dtoChildren.Count; i++)
            {
                var child = dtoChildren[i];
                if (child == null)
                {
                    continue;
                }

                items.Add(new ThemeContentItem
                {
                    AssetId = child.AssetId,
                    AssetType = child.AssetType,
                    Role = child.Role,
                    Required = child.Required,
                    DefaultTransform = child.DefaultTransform
                });
            }

            return items;
        }
    }
}
