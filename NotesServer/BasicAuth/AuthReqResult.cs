namespace NotesServer.BasicAuth
{
    public class AuthReqResult : IResult
    {
        public AuthReqResult() { }

        public Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.StatusCode = 401;
#pragma warning disable ASP0019 // There should not be a duplicate key, so let it crash in that case
            httpContext.Response.Headers.Add("Www-Authenticate", "Basic realm=\"private\"");
            httpContext.Response.Headers.Add("Connection", "close");
#pragma warning restore ASP0019
            return Task.FromResult(httpContext.Response);
        }
    }
}
