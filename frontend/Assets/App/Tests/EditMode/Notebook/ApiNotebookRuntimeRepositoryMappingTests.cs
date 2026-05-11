using System.Collections.Generic;
using System.Reflection;
using App.Application.Notebook;
using App.Infrastructure.Api;
using App.Infrastructure.Api.Dtos;
using NUnit.Framework;

namespace App.Tests.EditMode.Notebook
{
    public sealed class ApiNotebookRuntimeRepositoryMappingTests
    {
        [Test]
        public void MapSpaces_PreservesThumbnailUrl()
        {
            var response = new SpaceListResponseDto
            {
                Spaces = new List<SpaceListItemResponseDto>
                {
                    new SpaceListItemResponseDto
                    {
                        SpaceId = 42L,
                        OwnerUserId = 7L,
                        OwnerNickname = "reader",
                        SpaceName = "room",
                        ThumbnailUrl = "https://cdn.test/spaces/42/latest.png"
                    }
                }
            };

            var method = typeof(ApiNotebookRuntimeRepository).GetMethod(
                "MapSpaces",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(method);
            var spaces = (IReadOnlyList<NotebookSpaceItem>)method.Invoke(null, new object[] { response, 10, 1L });

            Assert.AreEqual(1, spaces.Count);
            Assert.AreEqual("https://cdn.test/spaces/42/latest.png", spaces[0].ThumbnailUrl);
        }

        [Test]
        public void MapSpaceThumbnailUpload_PreservesThumbnailUrl()
        {
            var response = new SpaceThumbnailUploadResponseDto
            {
                SpaceId = 42L,
                ThumbnailKey = "spaces/42/thumbnail/latest.png",
                ThumbnailUrl = "https://cdn.test/spaces/42/latest.png",
                LayoutVersion = 9L
            };

            var method = typeof(ApiNotebookRuntimeRepository).GetMethod(
                "MapSpaceThumbnailUpload",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(method);
            var result = (NotebookSpaceThumbnailUploadResult)method.Invoke(null, new object[] { response });

            Assert.AreEqual(42L, result.SpaceId);
            Assert.AreEqual("spaces/42/thumbnail/latest.png", result.ThumbnailKey);
            Assert.AreEqual("https://cdn.test/spaces/42/latest.png", result.ThumbnailUrl);
            Assert.AreEqual(9L, result.LayoutVersion);
        }
    }
}
