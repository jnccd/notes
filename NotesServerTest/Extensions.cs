using Microsoft.AspNetCore.Http;
using Notes.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace NotesServer.UnitTests
{
    public static class Extensions
    {
        public static string ToAuthHeader(this User user) =>
            "Authorization: " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user.Username}:{user.Password}"));
    }
}
