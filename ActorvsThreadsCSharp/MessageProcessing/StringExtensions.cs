// --------------------------------------------------------------------------------------------------------------------
// <copyright file="StringExtensions.cs" company="Yet another App Factory">
//   @ Matthias Dittrich
// </copyright>
// <summary>
//   Some extensionmethods to make the live easier
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Yaaf.Utils.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Security;

    /// <summary>
    /// Some extensionmethods to make the live easier
    /// </summary>
    public static class StringExtensions
    {
        #region Public Methods

        /// <summary>
        /// The to secure string.
        /// </summary>
        /// <param name="current">
        /// The current.
        /// </param>
        /// <returns>
        /// </returns>
        public static SecureString ToSecureString(this string current)
        {
            if (current == null)
            {
                current = string.Empty;
            }

            using (var str = new SecureString())
            {
                for (int i = 0; i < current.Length; i++)
                {
                    str.AppendChar(current[i]);
                }

                return str.Copy();
            }
        }

        public static IEnumerable<int> AllIndexOf(this string current, string search)
        {
            if (string.IsNullOrEmpty(search))
            {
                throw new ArgumentException("search is null or empty");
            }

            for (int i = 0; i < current.Length - search.Length + 1; i++)
            {
                if (current.Substring(i).StartsWith(search))
                {
                    yield return i;
                }
            }
        }

        /// <summary>
        /// The to unsecure string.
        /// </summary>
        /// <param name="current">
        /// The current.
        /// </param>
        /// <returns>
        /// The to unsecure string.
        /// </returns>
        public static string ToUnsecureString(this SecureString current)
        {
            if (current == null)
            {
                return null;
            }

            IntPtr bstr = Marshal.SecureStringToBSTR(current);

            try
            {
                return Marshal.PtrToStringBSTR(bstr);
            }
            finally
            {
                Marshal.FreeBSTR(bstr);
            }
        }

        #endregion
    }
}