namespace FSH.WebApi.Application.Common.Exporters;

public interface IExcelWriter : ITransientService
{
    Stream WriteToStream<T>(IList<T> data);
    public Stream WriteToStreamWithMultipleSheets<T>(Dictionary<string, List<T>> sheetData);
}