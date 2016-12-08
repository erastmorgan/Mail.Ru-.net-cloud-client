using System;
using System.Text;

namespace MailRuCloudApi
{
    public static class Extensions
    {
        public static long BytesCount(this string value)
        {
            return Encoding.UTF8.GetByteCount(value);
        }


        /// <summary>
        /// Finds the first exception of the requested type.
        /// </summary>
        /// <typeparam name="T">
        /// The type of exception to return
        /// </typeparam>
        /// <param name="ex">
        /// The exception to look in.
        /// </param>
        /// <returns>
        /// The exception or the first inner exception that matches the
        /// given type; null if not found.
        /// </returns>
        public static T InnerOf<T>(this Exception ex)
            where T : Exception
        {
            return (T)InnerOf(ex, typeof(T));
        }

        /// <summary>
        /// Finds the first exception of the requested type.
        /// </summary>
        /// <param name="ex">
        /// The exception to look in.
        /// </param>
        /// <param name="t">
        /// The type of exception to return
        /// </param>
        /// <returns>
        /// The exception or the first inner exception that matches the
        /// given type; null if not found.
        /// </returns>
        public static Exception InnerOf(this Exception ex, Type t)
        {
            if (ex == null || t.IsInstanceOfType(ex))
            {
                return ex;
            }

            var ae = ex as AggregateException;
            if (ae != null)
            {
                foreach (var e in ae.InnerExceptions)
                {
                    var ret = InnerOf(e, t);
                    if (ret != null)
                    {
                        return ret;
                    }
                }
            }

            return InnerOf(ex.InnerException, t);
        }
    }
}