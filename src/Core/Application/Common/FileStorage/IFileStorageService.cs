using Microsoft.AspNetCore.Http;

namespace FSH.WebApi.Application.Common.FileStorage;

public interface IFileStorageService : ITransientService
{
    public Task<string> UploadAsync<T>(FileUploadRequest? request, FileType supportedFileType, CancellationToken cancellationToken = default)
    where T : class;
    public Task<string> SaveFileAsync(IFormFile file, CancellationToken cancellationToken);
    public Task<string[]> SaveFilesAsync(IFormFile[] files, CancellationToken cancellationToken);
    public void Remove(string? path);
    public void RemoveAll(string[] paths);
}