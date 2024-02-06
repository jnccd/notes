using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Notes.Interface
{
    public static class Extensions
    {
        public static Random RDM = new Random();

        public static byte[] ToArray(this Stream stream)
        {
            byte[] buffer = new byte[4096];
            MemoryStream memoryStream = new MemoryStream();
            int reader;
            while ((reader = stream.Read(buffer, 0, buffer.Length)) != 0)
                memoryStream.Write(buffer, 0, reader);
            return memoryStream.ToArray();
        }

        public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            using (cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).TrySetResult(true), tcs))
            {
                if (task != await Task.WhenAny(task, tcs.Task))
                {
                    throw new OperationCanceledException(cancellationToken);
                }
            }

            return task.Result;
        }
        public static string Combine(this IEnumerable<string> s, string combinator = "")
        {
            return s.Count() == 0 ? "" : s.Foldl("", (x, y) => x + combinator + y).Remove(0, combinator.Length);
        }
        public static b Foldl<a, b>(this IEnumerable<a> xs, b y, Func<b, a, b> f)
        {
            foreach (a x in xs)
                y = f(y, x);
            return y;
        }
        public static b Foldl<a, b>(this IEnumerable<a> xs, Func<b, a, b> f)
        {
            return xs.Foldl(default, f);
        }
        public static a MaxElement<a>(this IEnumerable<a> xs, Func<a, double> f) { return xs.MaxElement(f, out double _); }
        public static a MaxElement<a>(this IEnumerable<a> xs, Func<a, double> f, out double max)
        {
            max = double.MinValue; a maxE = default;
            foreach (a x in xs)
            {
                double res = f(x);
                if (res > max)
                {
                    max = res;
                    maxE = x;
                }
            }
            return maxE;
        }
        public static a MinElement<a>(this IEnumerable<a> xs, Func<a, double> f) { return xs.MinElement(f, out double _); }
        public static a MinElement<a>(this IEnumerable<a> xs, Func<a, double> f, out double min)
        {
            min = double.MaxValue; a minE = default;
            foreach (a x in xs)
            {
                double res = f(x);
                if (res < min)
                {
                    min = res;
                    minE = x;
                }
            }
            return minE;
        }
        public static bool ContainsAny<a>(this IEnumerable<a> xs, params a[] ys)
        {
            foreach (a y in ys)
                if (xs.Contains(y))
                    return true;
            return false;
        }
        public static a GetRandomValue<a>(this IEnumerable<a> xs)
        {
            a[] arr = xs.ToArray();
            return arr[RDM.Next(arr.Length)];
        }
    }
}
