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

        [Test]
        public void MapAssetInventory_PreservesOwnedAssetMetadata()
        {
            var response = new InventoryResponseDto
            {
                Items = new List<InventoryItemResponseDto>
                {
                    new InventoryItemResponseDto
                    {
                        InventoryId = 88L,
                        AssetId = 10L,
                        Asset = new AssetResponseDto
                        {
                            Id = 10L,
                            Code = "desk-01",
                            Name = "Desk",
                            AssetType = "DESK",
                            CollectionCode = "NATURAL",
                            Theme = "NATURAL",
                            Placement = "floor",
                            PreviewImageUrl = "https://cdn.test/desk.png",
                            ContentType = "model/gltf-binary",
                            FileHash = "sha256:test",
                            FileSize = 123L,
                            Version = 4,
                            Owned = true
                        }
                    }
                }
            };

            var method = typeof(ApiNotebookRuntimeRepository).GetMethod(
                "MapAssetInventory",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(method);
            var assets = (IReadOnlyList<NotebookAssetItem>)method.Invoke(null, new object[] { response });

            Assert.AreEqual(1, assets.Count);
            Assert.AreEqual(88L, assets[0].InventoryId);
            Assert.AreEqual("NATURAL", assets[0].CollectionCode);
            Assert.AreEqual("floor", assets[0].Placement);
            Assert.AreEqual("https://cdn.test/desk.png", assets[0].PreviewImageUrl);
            Assert.AreEqual("sha256:test", assets[0].FileHash);
            Assert.AreEqual(4, assets[0].Version);
            Assert.IsTrue(assets[0].Owned);
        }

        [Test]
        public void MapAssetDownloadManifest_PreservesDownloadFiles()
        {
            var response = new DownloadManifestResponseDto
            {
                AssetId = 10L,
                AssetType = "DESK",
                CollectionCode = "NATURAL",
                Version = 4,
                Files = new List<DownloadFileResponseDto>
                {
                    new DownloadFileResponseDto
                    {
                        AssetId = 10L,
                        Code = "desk-01",
                        Name = "Desk",
                        AssetType = "DESK",
                        Role = "DESK",
                        Url = "https://cdn.test/desk.glb",
                        ContentType = "model/gltf-binary",
                        FileHash = "sha256:test",
                        FileSize = 123L,
                        Version = 4,
                        Required = true
                    }
                }
            };

            var method = typeof(ApiNotebookRuntimeRepository).GetMethod(
                "MapAssetDownloadManifest",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(method);
            var manifest = (NotebookAssetDownloadManifestResult)method.Invoke(null, new object[] { response });

            Assert.AreEqual(10L, manifest.AssetId);
            Assert.AreEqual("NATURAL", manifest.CollectionCode);
            Assert.AreEqual(1, manifest.Files.Count);
            Assert.AreEqual("https://cdn.test/desk.glb", manifest.Files[0].Url);
            Assert.AreEqual("sha256:test", manifest.Files[0].FileHash);
        }

        [Test]
        public void MapSpaceAssetDownloadManifests_PreservesSpaceAndManifestMetadata()
        {
            var response = new SpaceAssetManifestListResponseDto
            {
                SpaceId = 42L,
                LayoutVersion = 8L,
                Manifests = new List<DownloadManifestResponseDto>
                {
                    new DownloadManifestResponseDto
                    {
                        AssetId = 10L,
                        AssetType = "DESK",
                        CollectionCode = "NATURAL",
                        Version = 4,
                        Files = new List<DownloadFileResponseDto>()
                    }
                }
            };

            var method = typeof(ApiNotebookRuntimeRepository).GetMethod(
                "MapSpaceAssetDownloadManifests",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(method);
            var result = (NotebookSpaceAssetManifestListResult)method.Invoke(null, new object[] { response });

            Assert.AreEqual(42L, result.SpaceId);
            Assert.AreEqual(8L, result.LayoutVersion);
            Assert.AreEqual(1, result.Manifests.Count);
            Assert.AreEqual(10L, result.Manifests[0].AssetId);
        }
    }
}
