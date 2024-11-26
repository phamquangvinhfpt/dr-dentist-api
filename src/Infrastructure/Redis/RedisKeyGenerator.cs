﻿using FSH.WebApi.Application.Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Infrastructure.Redis;
public static class RedisKeyGenerator
{
    private const string Separator = ":";

    public static string GenerateAppointmentKey(string userId, PaginationFilter filter, DateOnly date, string prefix)
    {
        // Tạo một StringBuilder để xây dựng key hiệu quả
        var keyBuilder = new StringBuilder();

        // Thêm prefix
        keyBuilder.Append(prefix.ToLower());
        keyBuilder.Append(Separator);

        // Thêm userId
        if (!string.IsNullOrEmpty(userId))
        {
            keyBuilder.Append("user");
            keyBuilder.Append(Separator);
            keyBuilder.Append(userId);
            keyBuilder.Append(Separator);
        }

        // Thêm date
        keyBuilder.Append("date");
        keyBuilder.Append(Separator);
        keyBuilder.Append(date.ToString("yyyyMMdd"));
        keyBuilder.Append(Separator);

        // Thêm pagination info
        if (filter != null)
        {
            // Thêm basic pagination
            keyBuilder.Append("page");
            keyBuilder.Append(Separator);
            keyBuilder.Append(filter.PageNumber);
            keyBuilder.Append(Separator);
            keyBuilder.Append("size");
            keyBuilder.Append(Separator);
            keyBuilder.Append(filter.PageSize);
            keyBuilder.Append(Separator);

            // Thêm order by
            if (filter.OrderBy?.Any() == true)
            {
                keyBuilder.Append("order");
                keyBuilder.Append(Separator);
                keyBuilder.Append(string.Join(",", filter.OrderBy));
                keyBuilder.Append(Separator);
            }

            // Thêm keyword
            if (!string.IsNullOrEmpty(filter.Keyword))
            {
                keyBuilder.Append("keyword");
                keyBuilder.Append(Separator);
                keyBuilder.Append(filter.Keyword);
                keyBuilder.Append(Separator);
            }

            // Thêm advanced search
            if (filter.AdvancedSearch != null)
            {
                keyBuilder.Append("advsearch");
                keyBuilder.Append(Separator);
                if (filter.AdvancedSearch.Fields?.Any() == true)
                {
                    keyBuilder.Append(string.Join(",", filter.AdvancedSearch.Fields));
                    keyBuilder.Append(Separator);
                }
                if (!string.IsNullOrEmpty(filter.AdvancedSearch.Keyword))
                {
                    keyBuilder.Append(filter.AdvancedSearch.Keyword);
                    keyBuilder.Append(Separator);
                }
            }

            // Thêm advanced filter
            if (filter.AdvancedFilter != null)
            {
                keyBuilder.Append("advfilter");
                keyBuilder.Append(Separator);
                keyBuilder.Append(SerializeFilter(filter.AdvancedFilter));
                keyBuilder.Append(Separator);
            }
        }

        // Tạo hash từ key để đảm bảo độ dài phù hợp và tránh ký tự đặc biệt
        using (var sha256 = SHA256.Create())
        {
            var bytes = Encoding.UTF8.GetBytes(keyBuilder.ToString());
            var hash = sha256.ComputeHash(bytes);
            var hashString = BitConverter.ToString(hash).Replace("-", "").ToLower();

            // Trả về key cuối cùng với format: prefix:hash
            return $"{prefix}{Separator}{hashString}";
        }
    }

    private static string SerializeFilter(Filter filter)
    {
        var components = new List<string>();

        // Thêm logic operator nếu có
        if (!string.IsNullOrEmpty(filter.Logic))
        {
            components.Add($"l:{filter.Logic}");
        }

        // Thêm field, operator và value nếu có
        if (!string.IsNullOrEmpty(filter.Field))
        {
            components.Add($"f:{filter.Field}");
        }
        if (!string.IsNullOrEmpty(filter.Operator))
        {
            components.Add($"o:{filter.Operator}");
        }
        if (filter.Value != null)
        {
            components.Add($"v:{filter.Value}");
        }

        // Đệ quy serialize các filter con
        if (filter.Filters?.Any() == true)
        {
            var subFilters = filter.Filters.Select(SerializeFilter);
            components.Add($"sf:[{string.Join("|", subFilters)}]");
        }

        return string.Join("_", components);
    }

    public static string GenerateListKey(string prefix)
    {
        return $"{prefix}{Separator}list";
    }
}