using System.Diagnostics;
using System.Text;
using Notes.Server;

namespace NotesServer.BasicAuth
{
    public interface IBasicAuthService
    {
        public IResult GetUser(string? authTokenHeader, Func<User?, IResult> handleRequest);
        public User? GetUser(string? authTokenHeader);
    }

    public class BasicAuthService(BasicAuthOptions options) : IBasicAuthService
    {
        readonly List<User> users = options.Users;
        readonly bool writeLogs = options.WriteLogs;
        readonly bool give404 = options.Give404;

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
            if (authTokenHeader == null || !authTokenHeader.Contains(' '))
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
    }
}