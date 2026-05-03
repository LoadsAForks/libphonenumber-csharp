/*
 * Copyright (C) 2026 The Libphonenumber Authors
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 */

using System;
using System.Globalization;
using System.IO;

namespace PhoneNumbers.MetadataBuilder;

/// <summary>
/// Build-time tool that converts the XML metadata files in <c>resources/</c> into per-region
/// binary files consumed at runtime. Mirrors the Java upstream's
/// <c>BuildMetadataProtoFromXml</c>: one file per region (or per country-calling-code for
/// non-geographical entities and alternate formats), serialized via
/// <see cref="BuildMetadataFromBin"/>.
/// </summary>
internal static class Program
{
    private const string PhoneMetadataPrefix = "PhoneNumberMetadata";
    private const string ShortMetadataPrefix = "ShortNumberMetadata";
    private const string AlternateFormatsPrefix = "PhoneNumberAlternateFormats";
    private const string TestMetadataPrefix = "PhoneNumberMetadataForTesting";

    private const string NonGeoEntityRegionCode = "001";

    public static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"PhoneNumbers.MetadataBuilder failed: {ex.Message}");
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static int Run(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine(
                "Usage: PhoneNumbers.MetadataBuilder <kind> <input-xml-path> <output-dir>");
            Console.Error.WriteLine(
                "  kind: phone | short | alternate | test");
            return 2;
        }

        var kind = args[0];
        var inputXml = args[1];
        var outputDir = args[2];

        if (!File.Exists(inputXml))
            throw new FileNotFoundException($"Input XML not found: {inputXml}", inputXml);

        Directory.CreateDirectory(outputDir);

        return kind switch
        {
            "phone" => BuildPerRegion(inputXml, outputDir, PhoneMetadataPrefix,
                isShortNumberMetadata: false, isAlternateFormatsMetadata: false),
            "short" => BuildPerRegion(inputXml, outputDir, ShortMetadataPrefix,
                isShortNumberMetadata: true, isAlternateFormatsMetadata: false),
            "alternate" => BuildPerRegion(inputXml, outputDir, AlternateFormatsPrefix,
                isShortNumberMetadata: false, isAlternateFormatsMetadata: true),
            "test" => BuildPerRegion(inputXml, outputDir, TestMetadataPrefix,
                isShortNumberMetadata: false, isAlternateFormatsMetadata: false),
            _ => UnknownKind(kind),
        };
    }

    private static int UnknownKind(string kind)
    {
        Console.Error.WriteLine($"Unknown kind '{kind}'. Expected one of: phone, short, alternate, test.");
        return 2;
    }

    private static int BuildPerRegion(
        string inputXml,
        string outputDir,
        string filePrefix,
        bool isShortNumberMetadata,
        bool isAlternateFormatsMetadata)
    {
        using var input = File.OpenRead(inputXml);
        var metadataList = BuildMetadataFromXml.BuildPhoneMetadataFromStream(
            input,
            liteBuild: false,
            specialBuild: false,
            isShortNumberMetadata: isShortNumberMetadata,
            isAlternateFormatsMetadata: isAlternateFormatsMetadata);

        var written = 0;
        foreach (var metadata in metadataList)
        {
            var key = MakeFileNameKey(metadata, isAlternateFormatsMetadata);
            var path = Path.Combine(outputDir, $"{filePrefix}_{key}");
            using var fs = File.Create(path);
            BuildMetadataFromBin.WriteMetadata(fs, metadata);
            written++;
        }

        Console.Out.WriteLine($"PhoneNumbers.MetadataBuilder: wrote {written} {filePrefix}_* file(s) to {outputDir}");
        return 0;
    }

    /// <summary>
    /// Builds the per-file suffix Java's <c>MultiFileModeFileNameProvider</c> would: region code
    /// for geographical entries, country-calling-code for non-geographical / alternate-format
    /// entries (which don't have a meaningful region code).
    /// </summary>
    private static string MakeFileNameKey(PhoneMetadata metadata, bool isAlternateFormatsMetadata)
    {
        if (isAlternateFormatsMetadata)
            return metadata.CountryCode.ToString(CultureInfo.InvariantCulture);
        if (string.IsNullOrEmpty(metadata.Id) || metadata.Id == NonGeoEntityRegionCode)
            return metadata.CountryCode.ToString(CultureInfo.InvariantCulture);
        return metadata.Id;
    }
}
