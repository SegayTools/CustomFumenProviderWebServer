namespace CustomFumenProviderWebServer.Models
{
    public record Result(bool IsSuccess, string Message = default);
    public record Result<T>(bool IsSuccess, string Message = default, T Data = default) : Result(IsSuccess, Message);
}
