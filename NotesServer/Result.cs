public class Result<T> : IResult
{
    public T? Value { get; }
    public IResult? HttpResult { get; }
    public bool IsSuccess { get; }

    public Result(T value)
    {
        Value = value;
        IsSuccess = true;
    }

    public Result(IResult? result)
    {
        HttpResult = result;
        IsSuccess = false;
    }

    public Task ExecuteAsync(HttpContext httpContext)
    {
        if (HttpResult != null)
            return HttpResult.ExecuteAsync(httpContext);
        else
        {
            httpContext.Response.StatusCode = 200;
            return Task.CompletedTask;
        }
    }
}