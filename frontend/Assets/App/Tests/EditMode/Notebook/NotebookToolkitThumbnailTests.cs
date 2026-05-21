using System.Reflection;
using App.Application.Notebook;
using App.Features.Notebook.Presentation;
using NUnit.Framework;

namespace App.Tests.EditMode.Notebook
{
    public sealed class NotebookToolkitThumbnailTests
    {
        [Test]
        public void RemoteSpaceThumbnailLimit_AcceptsGeneratedRoomCaptureWidth()
        {
            var field = typeof(NotebookToolkitController).GetField(
                "MaxRemoteSpaceThumbnailWidth",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(field);
            Assert.AreEqual(2048, field.GetRawConstantValue());
        }

        [Test]
        public void RemoteSpaceThumbnailLimit_AcceptsGeneratedRoomCaptureHeight()
        {
            var field = typeof(NotebookToolkitController).GetField(
                "MaxRemoteSpaceThumbnailHeight",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(field);
            Assert.AreEqual(1152, field.GetRawConstantValue());
        }

        [Test]
        public void GeneratedRoomCapture_UsesCardAspectRatio()
        {
            var apiSyncType = FindType("BookshelfPorting.Runtime.BookshelfApiSyncController");
            Assert.IsNotNull(apiSyncType);

            var width = GetPrivateConstant<int>(apiSyncType, "RoomThumbnailCaptureWidth");
            var height = GetPrivateConstant<int>(apiSyncType, "RoomThumbnailCaptureHeight");

            Assert.AreEqual(2048, width);
            Assert.AreEqual(1152, height);
            Assert.AreEqual(16f / 9f, width / (float)height, 0.001f);
        }

        [Test]
        public void ThumbnailUploadAuthRetry_TrimsBackendErrorCode()
        {
            var apiSyncType = FindType("BookshelfPorting.Runtime.BookshelfApiSyncController");
            Assert.IsNotNull(apiSyncType);

            var method = apiSyncType.GetMethod(
                "IsRefreshableThumbnailUploadAuthError",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(method);
            var result = (bool)method.Invoke(
                null,
                new object[] { new NotebookApiError { Code = " A003 " } });

            Assert.IsTrue(result);
        }

        private static T GetPrivateConstant<T>(System.Type type, string fieldName)
        {
            var field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field);
            return (T)field.GetRawConstantValue();
        }

        private static System.Type FindType(string fullName)
        {
            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                var type = assemblies[i].GetType(fullName);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }
    }
}
