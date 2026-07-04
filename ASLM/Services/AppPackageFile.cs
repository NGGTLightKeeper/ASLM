// Copyright NGGT.LightKeeper. All Rights Reserved.

namespace ASLM.Services
{
    /// <summary>
    /// Opens files packaged as MauiAsset, resolving them across platform bundle layouts.
    /// </summary>
    /// <remarks>
    /// On macOS the MauiAsset bundle placement can differ from the path
    /// <see cref="FileSystem.OpenAppPackageFileAsync"/> expects, so a bundle-resource
    /// search is used as a fallback.
    /// </remarks>
    public static class AppPackageFile
    {
        /// <summary>
        /// Opens a bundled package file for reading by its logical name.
        /// </summary>
        public static async Task<Stream> OpenReadAsync(string fileName)
        {
            try
            {
                return await FileSystem.OpenAppPackageFileAsync(fileName);
            }
            catch (FileNotFoundException)
            {
                if (TryOpenFromBundle(fileName, out var stream))
                {
                    return stream;
                }

                throw;
            }
        }

        /// <summary>
        /// Finds a packaged file inside the macOS app bundle resources, searching nested folders.
        /// </summary>
        private static bool TryOpenFromBundle(string fileName, out Stream stream)
        {
            stream = Stream.Null;

#if MACCATALYST
            var resourcePath = Foundation.NSBundle.MainBundle.ResourcePath;
            if (string.IsNullOrEmpty(resourcePath) || !Directory.Exists(resourcePath))
            {
                return false;
            }

            var match = Directory.EnumerateFiles(resourcePath, fileName, SearchOption.AllDirectories).FirstOrDefault();
            if (match == null)
            {
                return false;
            }

            stream = File.OpenRead(match);
            return true;
#else
            return false;
#endif
        }
    }
}
