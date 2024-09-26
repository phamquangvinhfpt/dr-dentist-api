using Microsoft.AspNetCore.Http;

namespace FSH.WebApi.Application.Common.FileStorage;
public class MultipleFileUploadRequest : IRequest<string[]>
{
    // [AllowedExtensions(FileType.Document)]
    // or
    // [AllowedExtensions(".jpg", ".png")]
    // [MaxFileSize(5 * 1024 * 1024)]
    public IFormFile[] Files { get; set; }
}

public class MultipleFileUploadRequestHandler : IRequestHandler<MultipleFileUploadRequest, string[]>
{
    private readonly IFileStorageService _fileStorageService;

    public MultipleFileUploadRequestHandler(IFileStorageService fileStorageService) => _fileStorageService = fileStorageService;

    public Task<string[]> Handle(MultipleFileUploadRequest request, CancellationToken cancellationToken)
    {
        return _fileStorageService.SaveFilesAsync(request.Files, cancellationToken);
    }
}

