using System.Diagnostics;
using System.Text;

namespace Notes.Server
{
    public class BasicAuth
    {
        readonly List<User> users;
        readonly bool writeLogs = false;
        readonly bool give404 = false;

        public BasicAuth(List<User> users, bool writeLogs = false, bool give404 = false)
        {
            this.users = users;
            this.writeLogs = writeLogs;
            this.give404 = give404;
        }

        public IResult GetUser(string? authTokenHeader, Func<User?, IResult> handleRequest)
        {
            if (writeLogs)
                Debug.WriteLine("TokenHeader:", authTokenHeader);

            User? u;
            if ((u = GetUser(authTokenHeader)) != null)
                return handleRequest(u);
            else
                return give404 ? Results.NotFound() : new AuthReqResult();
        }

        public User? GetUser(string? authTokenHeader)
        {
            if (authTokenHeader == null || !authTokenHeader.Contains(" "))
                return null;
            var authToken = authTokenHeader?.Split(" ")[1];
            if (string.IsNullOrWhiteSpace(authToken))
                return null;

            string decodedAuthToken = Encoding.UTF8.GetString(Convert.FromBase64String(authToken));
            string[] split = decodedAuthToken.Split(':');
            string user = split[0];
            string pass = split[1];

            if (writeLogs)
                Debug.WriteLine(decodedAuthToken);

            return users.FirstOrDefault(u => u.Username == user && u.Password == pass);
        }

        class AuthReqResult : IResult
        {
            public AuthReqResult() { }

            public Task ExecuteAsync(HttpContext httpContext)
            {
                httpContext.Response.StatusCode = 401;
                httpContext.Response.Headers.Add("Www-Authenticate", "Basic realm=\"private\"");
                httpContext.Response.Headers.Add("Connection", "close");
                return Task.FromResult(httpContext.Response);
            }
        }
    }
}