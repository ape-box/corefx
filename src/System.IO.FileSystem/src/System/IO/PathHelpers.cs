// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.IO
{
    // Helper methods related to paths.  Some of these are copies of 
    // internal members of System.IO.Path from System.Runtime.Extensions.dll.
    internal static partial class PathHelpers
    {
        // Array of the separator chars
        internal static readonly char[] DirectorySeparatorChars = new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };

        // string-representation of the directory-separator character, used when appending the character to another
        // string so as to avoid the boxing of the character when calling string.Concat(..., object).
        internal static readonly string DirectorySeparatorCharAsString = Path.DirectorySeparatorChar.ToString();

        // System.IO.Path has both public Combine and internal InternalCombine
        // members.  InternalCombine performs these extra validations on the second 
        // argument.  This provides a convenient helper to maintain this extra
        // validation when porting code from Path.InternalCombine to Path.Combine.
        internal static void ThrowIfEmptyOrRootedPath(string path2)
        {
            if (path2 == null)
                throw new ArgumentNullException(nameof(path2));
            if (path2.Length == 0)
                throw new ArgumentException(SR.Argument_PathEmpty, nameof(path2));
            if (Path.IsPathRooted(path2))
                throw new ArgumentException(SR.Arg_Path2IsRooted, nameof(path2));
        }

        internal static bool IsRoot(ReadOnlySpan<char> path)
            => path.Length == PathInternal.GetRootLength(path);

        internal static bool EndsInDirectorySeparator(ReadOnlySpan<char> path)
            => path.Length > 0 && PathInternal.IsDirectorySeparator(path[path.Length - 1]);

        internal static string TrimEndingDirectorySeparator(string path) =>
            EndsInDirectorySeparator(path) && !IsRoot(path) ?
                path.Substring(0, path.Length - 1) :
                path;

        internal static ReadOnlySpan<char> TrimEndingDirectorySeparator(ReadOnlySpan<char> path) =>
            EndsInDirectorySeparator(path) && !IsRoot(path) ?
                path.Slice(0, path.Length - 1) :
                path;

        /// <summary>
        /// Combines two paths. Does no validation of paths, only concatenates the paths
        /// and places a directory separator between them if needed.
        /// </summary>
        internal static string CombineNoChecks(ReadOnlySpan<char> first, ReadOnlySpan<char> second)
        {
            if (first.Length == 0)
                return second.Length == 0
                    ? string.Empty
                    : new string(second);

            if (second.Length == 0)
                return new string(first);

            return CombineNoChecksInternal(first, second);
        }

        /// <summary>
        /// Combines three paths. Does no validation of paths, only concatenates the paths
        /// and places a directory separator between them if needed.
        /// </summary>
        internal static string CombineNoChecks(ReadOnlySpan<char> first, ReadOnlySpan<char> second, ReadOnlySpan<char> third)
        {
            if (first.Length == 0)
                return CombineNoChecks(second, third);

            if (second.Length == 0)
                return CombineNoChecks(first, third);

            if (third.Length == 0)
                return CombineNoChecks(first, second);

            return CombineNoChecksInternal(first, second, third);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe static string CombineNoChecksInternal(ReadOnlySpan<char> first, ReadOnlySpan<char> second)
        {
            Debug.Assert(first.Length > 0 && second.Length > 0, "should have dealt with empty paths");

            bool hasSeparator = PathInternal.IsDirectorySeparator(first[first.Length - 1])
                || PathInternal.IsDirectorySeparator(second[0]);

            fixed (char* f = &MemoryMarshal.GetReference(first), s = &MemoryMarshal.GetReference(second))
            {
                return string.Create(
                    first.Length + second.Length + (hasSeparator ? 0 : 1),
                    (First: (IntPtr)f, FirstLength: first.Length, Second: (IntPtr)s, SecondLength: second.Length, HasSeparator: hasSeparator),
                    (destination, state) =>
                    {
                        new Span<char>((char*)state.First, state.FirstLength).CopyTo(destination);
                        if (!state.HasSeparator)
                            destination[state.FirstLength] = Path.DirectorySeparatorChar;
                        new Span<char>((char*)state.Second, state.SecondLength).CopyTo(destination.Slice(state.FirstLength + (state.HasSeparator ? 0 : 1)));
                    });
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe static string CombineNoChecksInternal(ReadOnlySpan<char> first, ReadOnlySpan<char> second, ReadOnlySpan<char> third)
        {
            Debug.Assert(first.Length > 0 && second.Length > 0 && third.Length > 0, "should have dealt with empty paths");

            bool firstHasSeparator = PathInternal.IsDirectorySeparator(first[first.Length - 1])
                || PathInternal.IsDirectorySeparator(second[0]);
            bool thirdHasSeparator = PathInternal.IsDirectorySeparator(second[second.Length - 1])
                || PathInternal.IsDirectorySeparator(third[0]);

            fixed (char* f = &MemoryMarshal.GetReference(first), s = &MemoryMarshal.GetReference(second), t = &MemoryMarshal.GetReference(third))
            {
                return string.Create(
                    first.Length + second.Length + third.Length + (firstHasSeparator ? 0 : 1) + (thirdHasSeparator ? 0 : 1),
                    (First: (IntPtr)f, FirstLength: first.Length, Second: (IntPtr)s, SecondLength: second.Length,
                        Third: (IntPtr)t, ThirdLength: third.Length, FirstHasSeparator: firstHasSeparator, ThirdHasSeparator: thirdHasSeparator),
                    (destination, state) =>
                    {
                        new Span<char>((char*)state.First, state.FirstLength).CopyTo(destination);
                        if (!state.FirstHasSeparator)
                            destination[state.FirstLength] = Path.DirectorySeparatorChar;
                        new Span<char>((char*)state.Second, state.SecondLength).CopyTo(destination.Slice(state.FirstLength + (state.FirstHasSeparator ? 0 : 1)));
                        if (!state.ThirdHasSeparator)
                            destination[destination.Length - state.ThirdLength - 1] = Path.DirectorySeparatorChar;
                        new Span<char>((char*)state.Third, state.ThirdLength).CopyTo(destination.Slice(destination.Length - state.ThirdLength));
                    });
            }
        }

        public static ReadOnlySpan<char> GetDirectoryNameNoChecks(ReadOnlySpan<char> path)
        {
            if (path.Length == 0)
                return ReadOnlySpan<char>.Empty;

            int root = PathInternal.GetRootLength(path);
            int i = path.Length;
            if (i > root)
            {
                while (i > root && !PathInternal.IsDirectorySeparator(path[--i])) ;
                return path.Slice(0, i);
            }

            return ReadOnlySpan<char>.Empty;
        }
    }
}
