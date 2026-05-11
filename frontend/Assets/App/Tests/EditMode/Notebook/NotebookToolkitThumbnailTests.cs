using System.Reflection;
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
    }
}
