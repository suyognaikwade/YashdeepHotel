using System;
using System.Collections.Generic;

namespace Yashdeep.Shared.Contracts;

/// <summary>
/// Standard pagination request contract.
/// </summary>
public class PagedRequest
{
    private int _pageNumber = 1;
    private int _pageSize = 20;

    public int PageNumber
    {
        get => _pageNumber;
        set => _pageNumber = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value < 1 ? 1 : (value > 100 ? 100 : value);
    }

    public string? SortBy { get; set; }
    public bool SortDescending { get; set; }
    public string? SearchTerm { get; set; }
}

/// <summary>
/// Standard paginated response envelope contract.
/// </summary>
/// <typeparam name="T">The item type contained in the paged collection.</typeparam>
public class PagedResponse<T>
{
    public IReadOnlyCollection<T> Items { get; set; } = Array.Empty<T>();
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public long TotalCount { get; set; }
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;

    public PagedResponse()
    {
    }

    public PagedResponse(IReadOnlyCollection<T> items, int pageNumber, int pageSize, long totalCount)
    {
        Items = items;
        PageNumber = pageNumber;
        PageSize = pageSize;
        TotalCount = totalCount;
        TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
    }
}
