#nullable enable
/*
 * Copyright (C) 2026 The Libphonenumber Authors
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 */

using System.IO;
using System.Linq;
using System.Reflection;

namespace PhoneNumbers
{
    /// <summary>
    /// Resolves a logical metadata file name (e.g. <c>PhoneNumberMetadata_TW</c>) into a stream
    /// containing that file's binary contents. Implement this to load metadata from a custom
    /// location — disk, a CDN, a trimmed resource bundle, etc. The default
    /// <see cref="EmbeddedResourceMetadataLoader"/> reads from the assembly's manifest resources.
    /// </summary>
    /// <remarks>Mirrors <c>com.google.i18n.phonenumbers.MetadataLoader</c> in Java.</remarks>
    public interface IMetadataLoader
    {
        /// <summary>
        /// Returns an open stream for the given file, or <c>null</c> if it does not exist. The
        /// caller takes ownership and must dispose the returned stream.
        /// </summary>
        Stream? LoadMetadata(string fileName);
    }

    /// <summary>
    /// In-memory <see cref="IMetadataLoader"/> backed by a dictionary of pre-serialized binary
    /// metadata. Used by <see cref="PhoneNumberUtil"/>'s legacy Stream constructor: the parsed XML
    /// is round-tripped through <see cref="BuildMetadataFromBin"/> once at construction so the
    /// rest of the library can use the same lazy <c>MetadataSource</c> path as production code.
    /// </summary>
    internal sealed class InMemoryMetadataLoader : IMetadataLoader
    {
        private readonly System.Collections.Generic.Dictionary<string, byte[]> data;

        public InMemoryMetadataLoader(System.Collections.Generic.Dictionary<string, byte[]> data)
        {
            this.data = data;
        }

        public Stream? LoadMetadata(string fileName)
            => data.TryGetValue(fileName, out var bytes) ? new MemoryStream(bytes, writable: false) : null;
    }

    /// <summary>
    /// Default <see cref="IMetadataLoader"/> implementation that reads metadata from a .NET
    /// assembly's embedded manifest resources. Resource names are matched by suffix, so the loader
    /// is agnostic to the exact namespace prefix the build assigns.
    /// </summary>
    public sealed class EmbeddedResourceMetadataLoader : IMetadataLoader
    {
        private readonly Assembly assembly;

        public EmbeddedResourceMetadataLoader() : this(typeof(PhoneNumberUtil).Assembly) { }

        public EmbeddedResourceMetadataLoader(Assembly assembly)
        {
            this.assembly = assembly ?? throw new System.ArgumentNullException(nameof(assembly));
        }

        public Stream? LoadMetadata(string fileName)
        {
            // Manifest resource names are typically prefixed with a default namespace + linkBase
            // (e.g. "PhoneNumbers.metadata.PhoneNumberMetadata_TW"). Suffix-matching keeps the
            // contract simple for callers without coupling them to the build system's choices.
            var resourceName = assembly
                .GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(fileName, System.StringComparison.Ordinal));
            return resourceName == null ? null : assembly.GetManifestResourceStream(resourceName);
        }
    }
}
