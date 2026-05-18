using System;
using System.IO;
using UnityEngine;

namespace App.Bootstrap
{
    internal static class ProfileImageSelectionUtility
    {
        internal const long MaxProfileImageBytes = 5L * 1024L * 1024L;
        internal const int MaxProfileImageDimension = 4096;

        internal static bool TryLoadPreviewTexture(
            string imagePath,
            string textureName,
            out byte[] imageBytes,
            out Texture2D texture,
            out string contentType,
            out string errorMessage)
        {
            imageBytes = null;
            texture = null;
            contentType = null;
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                errorMessage = "Image file was not found.";
                return false;
            }

            var extension = Path.GetExtension(imagePath);
            if (!IsSupportedImageExtension(extension))
            {
                errorMessage = "Only PNG and JPEG profile images are supported.";
                return false;
            }

            var fileInfo = new FileInfo(imagePath);
            if (fileInfo.Length <= 0)
            {
                errorMessage = "Selected image file is empty.";
                return false;
            }

            if (fileInfo.Length > MaxProfileImageBytes)
            {
                errorMessage = "Profile image must be 5 MB or smaller.";
                return false;
            }

            try
            {
                imageBytes = File.ReadAllBytes(imagePath);
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.name = textureName;
                texture.hideFlags = HideFlags.HideAndDontSave;

                if (!texture.LoadImage(imageBytes))
                {
                    UnityObjectSafeDestroy.Destroy(texture);
                    texture = null;
                    errorMessage = "Failed to decode the selected image.";
                    return false;
                }

                if (texture.width > MaxProfileImageDimension || texture.height > MaxProfileImageDimension)
                {
                    UnityObjectSafeDestroy.Destroy(texture);
                    texture = null;
                    errorMessage = $"Profile image dimensions must be {MaxProfileImageDimension}px or smaller.";
                    return false;
                }

                contentType = GetImageContentType(extension);
                return true;
            }
            catch (Exception exception)
            {
                if (texture != null)
                {
                    UnityObjectSafeDestroy.Destroy(texture);
                    texture = null;
                }

                imageBytes = null;
                contentType = null;
                errorMessage = $"Failed to read selected image: {exception.Message}";
                return false;
            }
        }

        internal static bool IsSupportedImageExtension(string extension)
        {
            return string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase);
        }

        internal static string GetImageContentType(string extension)
        {
            if (string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase))
            {
                return "image/png";
            }

            return "image/jpeg";
        }
    }
}
