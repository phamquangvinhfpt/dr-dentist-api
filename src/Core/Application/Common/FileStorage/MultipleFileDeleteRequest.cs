using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Common.FileStorage;
public class MultipleFileDeleteRequest : IRequest<string>
{
    public string[] Paths { get; set; }
}

public class MultipleFileDeleteRequestHandler : IRequestHandler<MultipleFileDeleteRequest, string>
{
    private readonly IFileStorageService _fileStorageService;

    public MultipleFileDeleteRequestHandler(IFileStorageService fileStorageService) => _fileStorageService = fileStorageService;

    public async Task<string> Handle(MultipleFileDeleteRequest request, CancellationToken cancellationToken)
    {
        _fileStorageService.RemoveAll(request.Paths);
        return "Files deleted successfully.";
    }
}
