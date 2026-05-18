using System.Net;

namespace Woola.PhotoManager.Shared.Models;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? ErrorMessage { get; set; }
    public int StatusCode { get; set; }

    public static ApiResponse<T> Ok(T data) => new()
    {
        Success = true, Data = data, StatusCode = (int)HttpStatusCode.OK
    };

    public static ApiResponse<T> Fail(string error, int statusCode = 400) => new()
    {
        Success = false, ErrorMessage = error, StatusCode = statusCode
    };
}

public class PagedApiResponse<T>
{
    public bool Success { get; set; }
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public bool HasNextPage { get; set; }
    public string? ErrorMessage { get; set; }

    public static PagedApiResponse<T> Ok(List<T> items, int totalCount, int page, int pageSize) => new()
    {
        Success = true, Items = items, TotalCount = totalCount,
        Page = page, PageSize = pageSize,
        TotalPages = (int)Math.Ceiling((double)totalCount / pageSize),
        HasNextPage = page * pageSize < totalCount
    };
}

public class ErrorResponse
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Status { get; set; }
    public string Detail { get; set; } = string.Empty;
    public string? TraceId { get; set; }
}
