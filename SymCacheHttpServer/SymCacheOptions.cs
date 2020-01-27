// © Microsoft Corporation. All rights reserved.

using System;

public class SymCacheOptions
{
    public Uri SymbolServer { get; set; }

    public string SymCacheDirectory { get; set; }

    public string TranscoderPath { get; set; }

    public SemanticVersion TranscoderVersion { get; set; }
}
