﻿using System.Diagnostics;
using System.Net.Mime;
using System.Security.Cryptography;
using System.Text;

namespace NotesServer;

public static class Extensions
{
    public static string GetStringHash(this string unhashed) => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(unhashed)));

    public static void PrintReqHeaders(HttpRequest request)
    {
        Debug.WriteLine("Req: " + request.ToString());
        for (int i = 0; i < request.Headers.Keys.Count; i++)
            Debug.WriteLine(request.Headers.Keys.ElementAt(i).ToString() + ": " +
                request.Headers.GetCommaSeparatedValues(request.Headers.Keys.ElementAt(i)).Aggregate((x, y) => x + "~~" + y));
    }

    public static IResult Html(this IResultExtensions resultExtensions, string html)
    {
        ArgumentNullException.ThrowIfNull(resultExtensions);

        return new HtmlResult(html);
    }

    class HtmlResult(string html) : IResult
    {
        private readonly string _html = html;

        public Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.ContentType = MediaTypeNames.Text.Html;
            httpContext.Response.ContentLength = Encoding.UTF8.GetByteCount(_html);
            return httpContext.Response.WriteAsync(_html);
        }
    }
}
