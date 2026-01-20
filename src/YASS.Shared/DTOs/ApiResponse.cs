namespace YASS.Shared.DTOs;

/// <summary>
/// 通用 API 响应
/// </summary>
/// <typeparam name="T">数据类型</typeparam>
public class ApiResponse<T>
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 响应数据
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// 错误代码
    /// </summary>
    public string? ErrorCode { get; set; }

    public static ApiResponse<T> Ok(T data) => new()
    {
        Success = true,
        Data = data
    };

    public static ApiResponse<T> Fail(string error, string? errorCode = null) => new()
    {
        Success = false,
        Error = error,
        ErrorCode = errorCode
    };
}

/// <summary>
/// 无数据的 API 响应
/// </summary>
public class ApiResponse : ApiResponse<object>
{
    public static ApiResponse Ok() => new()
    {
        Success = true
    };

    public new static ApiResponse Fail(string error, string? errorCode = null) => new()
    {
        Success = false,
        Error = error,
        ErrorCode = errorCode
    };
}
