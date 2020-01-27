// © Microsoft Corporation. All rights reserved.

using System;
using System.Diagnostics;
using System.Text.RegularExpressions;

/// <summary>Represents a semantic version.</summary>
/// <remarks>This class corresponds to Semantic Versioning 1.0.0 from semver.org.</remarks>
public class SemanticVersion : IEquatable<SemanticVersion>, IComparable<SemanticVersion>, IComparable
{
    static Regex regex = new Regex(
        @"(?<Major>[\d]+)\.(?<Minor>[\d]+)\.(?<Patch>[\d]+)(?:-(?<Prerelease>[0-9A-Za-z-]+))?");

    /// <summary>
    /// Initializes a new instance of the <see cref="SemanticVersion"/> class with the specified values.
    /// </summary>
    /// <param name="major">The major version number.</param>
    /// <param name="minor">The minor version number.</param>
    /// <param name="patch">The patch version number.</param>
    public SemanticVersion(ushort major, byte minor, byte patch) : this(major, minor, patch, prerelease: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SemanticVersion"/> class with the specified values.
    /// </summary>
    /// <param name="major">The major version number.</param>
    /// <param name="minor">The minor version number.</param>
    /// <param name="patch">The patch version number.</param>
    /// <param name="prerelease">The pre-release version number, if any.</param>
    public SemanticVersion(ushort major, byte minor, byte patch, string prerelease)
    {
        if (prerelease != null && prerelease.Length == 0)
        {
            throw new ArgumentException("Prerelease must not be an empty string.", nameof(prerelease));
        }

        Major = major;
        Minor = minor;
        Patch = patch;
        Prerelease = prerelease;
    }

    /// <summary>Gets the major version number.</summary>
    public ushort Major { get; }

    /// <summary>Gets the minor version number.</summary>
    public byte Minor { get; }

    /// <summary>Gets the patch version number.</summary>
    public byte Patch { get; }

    /// <summary>Gets the pre-release version number, if any.</summary>
    public string Prerelease { get; }

    /// <summary>
    /// Compares the current version with another version and returns an integer that indicates whether the current
    /// version precedes, follows, or occurs in the same position in the sort order as the other version.
    /// </summary>
    /// <param name="other">The version to compare with this version.</param>
    /// <returns>
    /// A value that indicates the relative order of the versions being compared. The return value has these
    /// meanings:
    /// <list type="table">
    /// <listheader>
    /// <term>Value</term>
    /// <description>Meaning</description>
    /// </listheader>
    /// <item>
    /// <term>Less than zero</term>
    /// <description>This version precedes <paramref name="other"/> in the sort order.</description>
    /// </item>
    /// <item>
    /// <term>Zero</term>
    /// <description>
    /// This version occurs in the same position in the sort order as <paramref name="other"/>.
    /// </description>
    /// </item>
    /// <item>
    /// <term>Greater than zero</term>
    /// <description>This version follows <paramref name="other"/> in the sort order.</description>
    /// </item>
    /// </list>
    /// </returns>
    public int CompareTo(SemanticVersion other)
    {
        return Compare(this, other);
    }

    /// <summary>
    /// Compares the current version with another object and returns an integer that indicates whether the current
    /// version precedes, follows, or occurs in the same position in the sort order as the other object.
    /// </summary>
    /// <param name="obj">The object to compare with this version.</param>
    /// <returns>
    /// A value that indicates the relative order of the objects being compared. The return value has these
    /// meanings:
    /// <list type="table">
    /// <listheader>
    /// <term>Value</term>
    /// <description>Meaning</description>
    /// </listheader>
    /// <item>
    /// <term>Less than zero</term>
    /// <description>This version precedes <paramref name="obj"/> in the sort order.</description>
    /// </item>
    /// <item>
    /// <term>Zero</term>
    /// <description>
    /// This version occurs in the same position in the sort order as <paramref name="obj"/>.
    /// </description>
    /// </item>
    /// <item>
    /// <term>Greater than zero</term>
    /// <description>This version follows <paramref name="obj"/> in the sort order.</description>
    /// </item>
    /// </list>
    /// </returns>
    public int CompareTo(object obj)
    {
        return CompareTo(obj as SemanticVersion);
    }

    /// <summary>Determines whether the current version is equal to another version.</summary>
    /// <param name="other">An version to compare with this version.</param>
    /// <returns>
    /// <see langword="true"/> if the current version is equal to the <paramref name="other"/> parameter; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    public bool Equals(SemanticVersion other)
    {
        return this == other;
    }

    /// <summary>Determines whether the current version is equal to another object.</summary>
    /// <param name="obj">An object to compare with this version.</param>
    /// <returns>
    /// <see langword="true"/> if the current version is equal to the <paramref name="obj"/> parameter; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    public override bool Equals(object obj)
    {
        return Equals(obj as SemanticVersion);
    }

    /// <summary>Returns the hash code for this version.</summary>
    /// <returns>A hash code.</returns>
    public override int GetHashCode()
    {
        // Pack Major & Minor & Patch into a single uint, and use its hash code.
        return ((uint)Major << 16 | (uint)Minor << 8 | Patch).GetHashCode();
    }

    /// <summary>Converts this version to its equivalent string representation.</summary>
    /// <returns>The string representation of this version.</returns>
    public override string ToString()
    {
        string result = $"{Major}.{Minor}.{Patch}";

        if (Prerelease != null)
        {
            result += $"-{Prerelease}";
        }

        return result;
    }

    /// <summary>Parses a <see cref="SemanticVersion"/> from a string representation.</summary>
    /// <param name="input">The text to parse.</param>
    /// <returns>The parsed value.</returns>
    public static SemanticVersion Parse(string input)
    {
        SemanticVersion result;

        if (!TryParse(input, out result))
        {
            throw new FormatException("Input is not a valid semantic version.");
        }

        return result;
    }

    /// <summary>Attempts to parse a <see cref="SemanticVersion"/> from a string representation.</summary>
    /// <param name="input">The text to parse.</param>
    /// <param name="version">The parsed value, when successful.</param>
    /// <returns>
    /// <see langword="true"/> if the string could be parsed to a <see cref="SemanticVersion"/>; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    public static bool TryParse(string input, out SemanticVersion version)
    {
        if (string.IsNullOrEmpty(input))
        {
            version = default(SemanticVersion);
            return false;
        }

        Match match = regex.Match(input);

        if (!match.Success)
        {
            version = default(SemanticVersion);
            return false;
        }

        ushort major;
        byte minor;
        byte patch;

        if (!ushort.TryParse(match.Groups["Major"].Value, out major) ||
            !byte.TryParse(match.Groups["Minor"].Value, out minor) ||
            !byte.TryParse(match.Groups["Patch"].Value, out patch))
        {
            version = default(SemanticVersion);
            return false;
        }

        string prerelease = NullIfEmpty(match.Groups["Prerelease"]);

        version = new SemanticVersion(major, minor, patch, prerelease);
        return true;
    }

    /// <summary>Determines whether two versions are equal.</summary>
    /// <param name="left">The first version.</param>
    /// <param name="right">The second version.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="left"/> equals <paramref name="right"/>; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    public static bool operator ==(SemanticVersion left, SemanticVersion right)
    {
        if (object.ReferenceEquals(left, null))
        {
            return object.ReferenceEquals(right, null);
        }
        else if (object.ReferenceEquals(right, null))
        {
            return false;
        }

        return left.Major == right.Major && left.Minor == right.Minor && left.Patch == right.Patch &&
            left.Prerelease == right.Prerelease;
    }

    /// <summary>Determines whether two versions are not equal.</summary>
    /// <param name="left">The first version.</param>
    /// <param name="right">The second version.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="left"/> does not equal <paramref name="right"/>; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    public static bool operator !=(SemanticVersion left, SemanticVersion right)
    {
        return !(left == right);
    }

    /// <summary>Determines whether the first specified version is less than the second specified version.</summary>
    /// <param name="left">The first version.</param>
    /// <param name="right">The second version.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="left"/> is less than <paramref name="right"/>; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    public static bool operator <(SemanticVersion left, SemanticVersion right)
    {
        return Compare(left, right) == CompareResult.LessThan;
    }

    /// <summary>
    /// Determines whether the first specified version is greater than the second specified version.
    /// </summary>
    /// <param name="left">The first version.</param>
    /// <param name="right">The second version.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="left"/> is greater than <paramref name="right"/>; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    public static bool operator >(SemanticVersion left, SemanticVersion right)
    {
        return Compare(left, right) == CompareResult.GreaterThan;
    }

    /// <summary>
    /// Determines whether the first specified version is less than or equal to the second specified version.
    /// </summary>
    /// <param name="left">The first version.</param>
    /// <param name="right">The second version.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="left"/> is less than or equal to <paramref name="right"/>;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public static bool operator <=(SemanticVersion left, SemanticVersion right)
    {
        return Compare(left, right) != CompareResult.GreaterThan;
    }

    /// <summary>
    /// Determines whether the first specified version is greater than or equal to the second specified version.
    /// </summary>
    /// <param name="left">The first version.</param>
    /// <param name="right">The second version.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="left"/> is greater than or equal to <paramref name="right"/>;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public static bool operator >=(SemanticVersion left, SemanticVersion right)
    {
        return Compare(left, right) != CompareResult.LessThan;
    }

    static string NullIfEmpty(Group group)
    {
        Debug.Assert(group != null);

        if (group.Length == 0)
        {
            return null;
        }
        else
        {
            Debug.Assert(group.Value != null);
            return group.Value;
        }
    }

    static int Compare(SemanticVersion left, SemanticVersion right)
    {
        if (object.ReferenceEquals(left, null))
        {
            if (object.ReferenceEquals(right, null))
            {
                return CompareResult.LessThan;
            }
            else
            {
                return CompareResult.Equal;
            }
        }
        else if (object.ReferenceEquals(right, null))
        {
            return CompareResult.GreaterThan;
        }

        // If Major versions are different, nothing else matters.
        if (left.Major < right.Major)
        {
            return CompareResult.LessThan;
        }
        else if (left.Major > right.Major)
        {
            return CompareResult.GreaterThan;
        }
        // else Major versions are equal, so check Minor versions ...

        // If Major versions are the same and Minor versions are different, nothing else matters.
        if (left.Minor < right.Minor)
        {
            return CompareResult.LessThan;
        }
        else if (left.Minor > right.Minor)
        {
            return CompareResult.GreaterThan;
        }
        // else Major and Minor versions are equal, so check Patch versions ...

        if (left.Patch < right.Patch)
        {
            return CompareResult.LessThan;
        }
        else if (left.Patch > right.Patch)
        {
            return CompareResult.GreaterThan;
        }

        // else Major, Minor and Patch are equal, so check pre-release version.
        if (left.Prerelease != null && right.Prerelease == null)
        {
            // Any pre-release version less than the non-pre-release version.
            return CompareResult.LessThan;
        }
        else if (left.Prerelease == null && right.Prerelease != null)
        {
            return CompareResult.GreaterThan;
        }
        else
        {
            return string.CompareOrdinal(left.Prerelease, right.Prerelease);
        }
    }

    static class CompareResult
    {
        public const int LessThan = -1;
        public const int Equal = 0;
        public const int GreaterThan = 1;
    }
}
