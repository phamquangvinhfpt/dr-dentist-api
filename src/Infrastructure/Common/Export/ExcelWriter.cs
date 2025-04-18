﻿using ClosedXML.Excel;
using FSH.WebApi.Application.Common.Exporters;
using System.ComponentModel;
using System.Data;

namespace FSH.WebApi.Infrastructure.Common.Export;

public class ExcelWriter : IExcelWriter
{
    public Stream WriteToStream<T>(IList<T> data)
    {
        PropertyDescriptorCollection properties = TypeDescriptor.GetProperties(typeof(T));
        DataTable table = new DataTable("table", "table");
        foreach (PropertyDescriptor prop in properties)
            table.Columns.Add(prop.Name, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType);
        foreach (T item in data)
        {
            DataRow row = table.NewRow();
            foreach (PropertyDescriptor prop in properties)
                row[prop.Name] = prop.GetValue(item) ?? DBNull.Value;
            table.Rows.Add(row);
        }

        using XLWorkbook wb = new XLWorkbook();
        wb.Worksheets.Add(table).Columns().AdjustToContents();
        Stream stream = new MemoryStream();
        wb.SaveAs(stream);
        stream.Seek(0, SeekOrigin.Begin);
        return stream;
    }

    public Stream WriteToStreamWithMultipleSheets<T>(Dictionary<string, List<T>> sheetData)
    {
        using XLWorkbook wb = new XLWorkbook();

        foreach (var sheet in sheetData)
        {
            PropertyDescriptorCollection properties = TypeDescriptor.GetProperties(typeof(T));
            DataTable table = new DataTable(sheet.Key);

            foreach (PropertyDescriptor prop in properties)
            {
                table.Columns.Add(prop.Name, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType);
            }

            foreach (T item in sheet.Value)
            {
                DataRow row = table.NewRow();
                foreach (PropertyDescriptor prop in properties)
                {
                    row[prop.Name] = prop.GetValue(item) ?? DBNull.Value;
                }
                table.Rows.Add(row);
            }

            wb.Worksheets.Add(table).Columns().AdjustToContents();
        }

        Stream stream = new MemoryStream();
        wb.SaveAs(stream);
        stream.Seek(0, SeekOrigin.Begin);
        return stream;
    }
}
