using FSH.WebApi.Application.Common.FileStorage;
using FSH.WebApi.Domain.Common;
using FSH.WebApi.Infrastructure.Common.Extensions;
using Microsoft.AspNetCore.Http;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace FSH.WebApi.Infrastructure.FileStorage;

public class LocalFileStorageService : IFileStorageService
{
    private const int TARGET_FILE_SIZE_KB = 100;
    private const int INITIAL_QUALITY = 90;
    private const int MIN_QUALITY = 30;
    private const int QUALITY_STEP = 10;

    public async Task<string> UploadAsync<T>(FileUploadRequest? request, FileType supportedFileType, CancellationToken cancellationToken = default)
    where T : class
    {
        if (request == null || request.Data == null)
        {
            return string.Empty;
        }

        if (request.Extension is null || !supportedFileType.GetExtentionList().Contains(request.Extension.ToLower()))
            throw new InvalidOperationException("File Format Not Supported.");
        if (request.Name is null)
            throw new InvalidOperationException("Name is required.");

        string base64Data = Regex.Match(request.Data, "data:image/(?<type>.+?),(?<data>.+)").Groups["data"].Value;

        var streamData = new MemoryStream(Convert.FromBase64String(base64Data));
        if (streamData.Length > 0)
        {
            string folder = typeof(T).Name;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                folder = folder.Replace(@"\", "/");
            }

            string folderName = supportedFileType switch
            {
                FileType.Image => Path.Combine("Files", "Images", folder),
                _ => Path.Combine("Files", "Others", folder),
            };
            string pathToSave = Path.Combine(Directory.GetCurrentDirectory(), folderName);
            Directory.CreateDirectory(pathToSave);

            string fileName = request.Name.Trim('"');
            fileName = RemoveSpecialCharacters(fileName);
            fileName = fileName.ReplaceWhitespace("-");
            fileName += request.Extension.Trim();
            string fullPath = Path.Combine(pathToSave, fileName);
            string dbPath = Path.Combine(folderName, fileName);
            if (File.Exists(dbPath))
            {
                dbPath = NextAvailableFilename(dbPath);
                fullPath = NextAvailableFilename(fullPath);
            }

            using var stream = new FileStream(fullPath, FileMode.Create);
            await streamData.CopyToAsync(stream, cancellationToken);
            return dbPath.Replace("\\", "/");
        }
        else
        {
            return string.Empty;
        }
    }

    public async Task<string> SaveFileAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null)
        {
            return string.Empty;
        }

        string folderName = GetDirectoryFromExtension(Path.GetExtension(file.FileName));
        string pathToSave = Path.Combine(Directory.GetCurrentDirectory(), folderName);

        string fileName = RemoveSpecialCharactersInFileName(file.FileName);

        fileName = $"{DateTime.Now:yyyyMMddHHmmssfff}_{fileName}";

        string dbPath = Path.Combine(folderName, fileName);
        string fullPath = Path.Combine(pathToSave, fileName);

        if (File.Exists(dbPath))
        {
            dbPath = NextAvailableFilename(dbPath);
            fullPath = NextAvailableFilename(fullPath);
        }

        Directory.CreateDirectory(pathToSave);

        // using var stream = new FileStream(fullPath, FileMode.Create);
        // await file.CopyToAsync(stream, cancellationToken);
        await CompressAndSaveImageAsync(file, fullPath, cancellationToken);

        return dbPath.Replace("\\", "/");
    }

    public async Task<string[]> SaveFilesAsync(IFormFile[] files, CancellationToken cancellationToken)
    {
        if (files == null || files.Length == 0)
        {
            return Array.Empty<string>();
        }

        List<string> dbPaths = new List<string>();
        foreach (IFormFile file in files)
        {
            dbPaths.Add(await SaveFileAsync(file, cancellationToken));
        }

        return dbPaths.ToArray();
    }

    public static string RemoveSpecialCharacters(string str)
    {
        return Regex.Replace(str, "[^a-zA-Z0-9_.]+", string.Empty, RegexOptions.Compiled);
    }

    public void Remove(string? path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private const string NumberPattern = "-{0}";

    private static string NextAvailableFilename(string path)
    {
        if (!File.Exists(path))
        {
            return path;
        }

        if (Path.HasExtension(path))
        {
            return GetNextFilename(path.Insert(path.LastIndexOf(Path.GetExtension(path), StringComparison.Ordinal), NumberPattern));
        }

        return GetNextFilename(path + NumberPattern);
    }

    private static string GetNextFilename(string pattern)
    {
        string tmp = string.Format(pattern, 1);

        if (!File.Exists(tmp))
        {
            return tmp;
        }

        int min = 1, max = 2;

        while (File.Exists(string.Format(pattern, max)))
        {
            min = max;
            max *= 2;
        }

        while (max != min + 1)
        {
            int pivot = (max + min) / 2;
            if (File.Exists(string.Format(pattern, pivot)))
            {
                min = pivot;
            }
            else
            {
                max = pivot;
            }
        }

        return string.Format(pattern, max);
    }

    private static string GetDirectoryFromExtension(string extension)
    {
        string path = string.Empty;
        foreach (var fileType in (FileType[])Enum.GetValues(typeof(FileType)))
        {
            if (fileType.GetExtentionList().Contains(extension))
            {
                path = Path.Combine(fileType.ToString(), extension.Replace(".", string.Empty));
                break;
            }
        }

        if (string.IsNullOrEmpty(path))
        {
            path = Path.Combine("Others", extension.Replace(".", string.Empty));
        }
        return Path.Combine("Files", path);
    }

    private static string RemoveSpecialCharactersInFileName(string fileName)
    {
        string fileExtension = Path.GetExtension(fileName);
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        string fileNameWithoutSpecialCharacters = RemoveSpecialCharacters(fileNameWithoutExtension);

        return fileNameWithoutSpecialCharacters + fileExtension;
    }

    public void RemoveAll(string[] paths)
    {
        if (paths != null && paths.Length > 0)
        {
            foreach (string path in paths)
            {
                Remove(path);
            }
        }
    }

    private async Task CompressAndSaveImageAsync(IFormFile file, string outputPath, CancellationToken cancellationToken)
    {
        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;

        using var image = new ImageMagick.MagickImage(memoryStream);

        // Start with high quality
        int quality = INITIAL_QUALITY;

        while (quality >= MIN_QUALITY)
        {
            image.Quality = (uint)quality;

            using var tempStream = new MemoryStream();
            await image.WriteAsync(tempStream, cancellationToken);

            // Check if file size is under target
            if (tempStream.Length / 1024 <= TARGET_FILE_SIZE_KB)
            {
                await File.WriteAllBytesAsync(outputPath, tempStream.ToArray(), cancellationToken);
                return;
            }

            quality -= QUALITY_STEP;
        }

        image.Quality = MIN_QUALITY;
        await image.WriteAsync(outputPath, cancellationToken);
    }
}