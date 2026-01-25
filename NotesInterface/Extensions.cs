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
            using (cancellationToken.Register(s => ((TaskCompletionSource<bool>)s!).TrySetResult(true), tcs))
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
        public static B Foldl<A, B>(this IEnumerable<A> xs, B y, Func<B, A, B> f)
        {
            foreach (A x in xs)
                y = f(y, x);
            return y;
        }
        public static B? Foldl<A, B>(this IEnumerable<A> xs, Func<B, A, B> f)
        {
#pragma warning disable CS8620 // Argument cannot be used for parameter due to differences in the nullability of reference types.
            return xs.Foldl(default, f);
#pragma warning restore CS8620 // Argument cannot be used for parameter due to differences in the nullability of reference types.
        }
        public static A? MaxElement<A>(this IEnumerable<A> xs, Func<A, double> f) { return xs.MaxElement(f, out double _); }
        public static A? MaxElement<A>(this IEnumerable<A> xs, Func<A, double> f, out double max)
        {
            max = double.MinValue; A? maxE = default;
            foreach (A x in xs)
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
        public static A? MinElement<A>(this IEnumerable<A> xs, Func<A, double> f) { return xs.MinElement(f, out double _); }
        public static A? MinElement<A>(this IEnumerable<A> xs, Func<A, double> f, out double min)
        {
            min = double.MaxValue; A? minE = default;
            foreach (A x in xs)
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
        public static bool ContainsAny<A>(this IEnumerable<A> xs, params A[] ys)
        {
            foreach (A y in ys)
                if (xs.Contains(y))
                    return true;
            return false;
        }
        public static A GetRandomValue<A>(this IEnumerable<A> xs)
        {
            A[] arr = xs.ToArray();
            return arr[RDM.Next(arr.Length)];
        }
    }
}
