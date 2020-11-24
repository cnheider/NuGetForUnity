namespace NuGetForUnity.Editor {
  /// <summary>
  ///   Represents an identifier for a NuGet package.  It contains only an ID and a Version number.
  /// </summary>
  public class NugetPackageIdentifier : System.IEquatable<NugetPackageIdentifier>,
                                        System.IComparable<NugetPackage> {
    /// <summary>
    ///   Gets or sets the ID of the NuGet package.
    /// </summary>
    public string _Id;

    /// <summary>
    ///   Gets or sets the version number of the NuGet package.
    /// </summary>
    public string _Version;

    /// <summary>
    ///   Initializes a new instance of a <see cref="NugetPackageIdentifider" /> with empty ID and Version.
    /// </summary>
    public NugetPackageIdentifier() {
      this._Id = string.Empty;
      this._Version = string.Empty;
    }

    /// <summary>
    ///   Initializes a new instance of a <see cref="NugetPackageIdentifider" /> with the given ID and Version.
    /// </summary>
    /// <param name="id">The ID of the package.</param>
    /// <param name="version">The version number of the package.</param>
    public NugetPackageIdentifier(string id, string version) {
      this._Id = id;
      this._Version = version;
    }

    /// <summary>
    ///   Gets a value indicating whether this is a pre-release package or an official release package.
    /// </summary>
    public bool IsPreRelease { get { return this._Version.Contains("-"); } }

    /// <summary>
    ///   Gets a value indicating whether the version number specified is a range of values.
    /// </summary>
    public bool HasVersionRange {
      get { return this._Version.StartsWith("(") || this._Version.StartsWith("["); }
    }

    /// <summary>
    ///   Gets a value indicating whether the minimum version number (only valid when HasVersionRange is true) is
    ///   inclusive (true) or exclusive (false).
    /// </summary>
    public bool IsMinInclusive { get { return this._Version.StartsWith("["); } }

    /// <summary>
    ///   Gets a value indicating whether the maximum version number (only valid when HasVersionRange is true) is
    ///   inclusive (true) or exclusive (false).
    /// </summary>
    public bool IsMaxInclusive { get { return this._Version.EndsWith("]"); } }

    /// <summary>
    ///   Gets the minimum version number of the NuGet package. Only valid when HasVersionRange is true.
    /// </summary>
    public string MinimumVersion {
      get {
        return this._Version.TrimStart(trimChars : new[] {'[', '('}).TrimEnd(trimChars : new[] {']', ')'})
                   .Split(separator : new[] {','})[0].Trim();
      }
    }

    /// <summary>
    ///   Gets the maximum version number of the NuGet package. Only valid when HasVersionRange is true.
    /// </summary>
    public string MaximumVersion {
      get {
        // if there is no MaxVersion specified, but the Max is Inclusive, then it is an EXACT version match with the stored MINIMUM
        var min_max = this._Version.TrimStart(trimChars : new[] {'[', '('})
                          .TrimEnd(trimChars : new[] {']', ')'}).Split(separator : new[] {','});
        return min_max.Length == 2 ? min_max[1].Trim() : null;
      }
    }

    #region IComparable<NugetPackage> Members

    public int CompareTo(NugetPackage other) {
      if (this._Id != other._Id) {
        return string.Compare(strA : this._Id, strB : other._Id);
      }

      return CompareVersions(version_a : this._Version, version_b : other._Version);
    }

    #endregion

    #region IEquatable<NugetPackageIdentifier> Members

    /// <summary>
    ///   Checks to see if this <see cref="NugetPackageIdentifier" /> is equal to the given one.
    /// </summary>
    /// <param name="other">The other <see cref="NugetPackageIdentifier" /> to check equality with.</param>
    /// <returns>True if the package identifiers are equal, otherwise false.</returns>
    public bool Equals(NugetPackageIdentifier other) {
      return other != null && other._Id == this._Id && other._Version == this._Version;
    }

    #endregion

    /// <summary>
    ///   Checks to see if the first <see cref="NugetPackageIdentifier" /> is less than the second.
    /// </summary>
    /// <param name="first">The first to compare.</param>
    /// <param name="second">The second to compare.</param>
    /// <returns>True if the first is less than the second.</returns>
    public static bool operator<(NugetPackageIdentifier first, NugetPackageIdentifier second) {
      if (first._Id != second._Id) {
        return string.Compare(strA : first._Id, strB : second._Id) < 0;
      }

      return CompareVersions(version_a : first._Version, version_b : second._Version) < 0;
    }

    /// <summary>
    ///   Checks to see if the first <see cref="NugetPackageIdentifier" /> is greater than the second.
    /// </summary>
    /// <param name="first">The first to compare.</param>
    /// <param name="second">The second to compare.</param>
    /// <returns>True if the first is greater than the second.</returns>
    public static bool operator>(NugetPackageIdentifier first, NugetPackageIdentifier second) {
      if (first._Id != second._Id) {
        return string.Compare(strA : first._Id, strB : second._Id) > 0;
      }

      return CompareVersions(version_a : first._Version, version_b : second._Version) > 0;
    }

    /// <summary>
    ///   Checks to see if the first <see cref="NugetPackageIdentifier" /> is less than or equal to the second.
    /// </summary>
    /// <param name="first">The first to compare.</param>
    /// <param name="second">The second to compare.</param>
    /// <returns>True if the first is less than or equal to the second.</returns>
    public static bool operator<=(NugetPackageIdentifier first, NugetPackageIdentifier second) {
      if (first._Id != second._Id) {
        return string.Compare(strA : first._Id, strB : second._Id) <= 0;
      }

      return CompareVersions(version_a : first._Version, version_b : second._Version) <= 0;
    }

    /// <summary>
    ///   Checks to see if the first <see cref="NugetPackageIdentifier" /> is greater than or equal to the second.
    /// </summary>
    /// <param name="first">The first to compare.</param>
    /// <param name="second">The second to compare.</param>
    /// <returns>True if the first is greater than or equal to the second.</returns>
    public static bool operator>=(NugetPackageIdentifier first, NugetPackageIdentifier second) {
      if (first._Id != second._Id) {
        return string.Compare(strA : first._Id, strB : second._Id) >= 0;
      }

      return CompareVersions(version_a : first._Version, version_b : second._Version) >= 0;
    }

    /// <summary>
    ///   Checks to see if the first <see cref="NugetPackageIdentifier" /> is equal to the second.
    ///   They are equal if the Id and the Version match.
    /// </summary>
    /// <param name="first">The first to compare.</param>
    /// <param name="second">The second to compare.</param>
    /// <returns>True if the first is equal to the second.</returns>
    public static bool operator==(NugetPackageIdentifier first, NugetPackageIdentifier second) {
      if (ReferenceEquals(objA : first, null)) {
        return ReferenceEquals(objA : second, null);
      }

      return first.Equals(other : second);
    }

    /// <summary>
    ///   Checks to see if the first <see cref="NugetPackageIdentifier" /> is not equal to the second.
    ///   They are not equal if the Id or the Version differ.
    /// </summary>
    /// <param name="first">The first to compare.</param>
    /// <param name="second">The second to compare.</param>
    /// <returns>True if the first is not equal to the second.</returns>
    public static bool operator!=(NugetPackageIdentifier first, NugetPackageIdentifier second) {
      if (ReferenceEquals(objA : first, null)) {
        return !ReferenceEquals(objA : second, null);
      }

      return !first.Equals(other : second);
    }

    /// <summary>
    ///   Determines if a given object is equal to this <see cref="NugetPackageIdentifier" />.
    /// </summary>
    /// <param name="obj">The object to check.</param>
    /// <returns>True if the given object is equal to this <see cref="NugetPackageIdentifier" />, otherwise false.</returns>
    public override bool Equals(object obj) {
      // If parameter is null return false.
      if (obj == null) {
        return false;
      }

      // If parameter cannot be cast to NugetPackageIdentifier return false.
      var p = obj as NugetPackageIdentifier;
      if ((object)p == null) {
        return false;
      }

      // Return true if the fields match:
      return this._Id == p._Id && this._Version == p._Version;
    }

    /// <summary>
    ///   Gets the hashcode for this <see cref="NugetPackageIdentifier" />.
    /// </summary>
    /// <returns>The hashcode for this instance.</returns>
    public override int GetHashCode() { return this._Id.GetHashCode() ^ this._Version.GetHashCode(); }

    /// <summary>
    ///   Returns the string representation of this <see cref="NugetPackageIdentifer" /> in the form
    ///   "{ID}.{Version}".
    /// </summary>
    /// <returns>A string in the form "{ID}.{Version}".</returns>
    public override string ToString() {
      return string.Format("{0}.{1}", arg0 : this._Id, arg1 : this._Version);
    }

    /// <summary>
    ///   Determines if the given <see cref="NugetPackageIdentifier" />'s version is in the version range of this
    ///   <see cref="NugetPackageIdentifier" />.
    ///   See here: https://docs.nuget.org/ndocs/create-packages/dependency-versions
    /// </summary>
    /// <param name="otherVersion">
    ///   The <see cref="NugetPackageIdentifier" /> whose version to check if is in the
    ///   range.
    /// </param>
    /// <returns>True if the given version is in the range, otherwise false.</returns>
    public bool InRange(NugetPackageIdentifier other_package) {
      return this.InRange(other_version : other_package._Version);
    }

    /// <summary>
    ///   Determines if the given version is in the version range of this <see cref="NugetPackageIdentifier" />.
    ///   See here: https://docs.nuget.org/ndocs/create-packages/dependency-versions
    /// </summary>
    /// <param name="other_version">The version to check if is in the range.</param>
    /// <returns>True if the given version is in the range, otherwise false.</returns>
    public bool InRange(string other_version) {
      return this.CompareVersion(other_version : other_version) == 0;
    }

    /// <summary>
    ///   Compares the given version string with the version range of this <see cref="NugetPackageIdentifier" />.
    ///   See here: https://docs.nuget.org/ndocs/create-packages/dependency-versions
    /// </summary>
    /// <param name="other_version">The version to check if is in the range.</param>
    /// <returns>
    ///   -1 if otherVersion is less than the version range. 0 if otherVersion is inside the version range. +1
    ///   if otherVersion is greater than the version range.
    /// </returns>
    public int CompareVersion(string other_version) {
      if (!this.HasVersionRange) {
        // if it has no version range specified (ie only a single version number) NuGet's specs state that that is the minimum version number, inclusive
        var compare = CompareVersions(version_a : this._Version, version_b : other_version);
        return compare <= 0 ? 0 : compare;
      }

      if (!string.IsNullOrEmpty(value : this.MinimumVersion)) {
        var compare = CompareVersions(version_a : this.MinimumVersion, version_b : other_version);
        // -1 = Min < other <-- Inclusive & Exclusive
        //  0 = Min = other <-- Inclusive Only
        // +1 = Min > other <-- OUT OF RANGE

        if (this.IsMinInclusive) {
          if (compare > 0) {
            return -1;
          }
        } else {
          if (compare >= 0) {
            return -1;
          }
        }
      }

      if (!string.IsNullOrEmpty(value : this.MaximumVersion)) {
        var compare = CompareVersions(version_a : this.MaximumVersion, version_b : other_version);
        // -1 = Max < other <-- OUT OF RANGE
        //  0 = Max = other <-- Inclusive Only
        // +1 = Max > other <-- Inclusive & Exclusive

        if (this.IsMaxInclusive) {
          if (compare < 0) {
            return 1;
          }
        } else {
          if (compare <= 0) {
            return 1;
          }
        }
      } else {
        if (this.IsMaxInclusive) {
          // if there is no MaxVersion specified, but the Max is Inclusive, then it is an EXACT version match with the stored MINIMUM
          return CompareVersions(version_a : this.MinimumVersion, version_b : other_version);
        }
      }

      return 0;
    }

    /// <summary>
    ///   Compares two version numbers in the form "1.2". Also supports an optional 3rd and 4th number as well as a
    ///   prerelease tag, such as "1.3.0.1-alpha2".
    ///   Returns:
    ///   -1 if versionA is less than versionB
    ///   0 if versionA is equal to versionB
    ///   +1 if versionA is greater than versionB
    /// </summary>
    /// <param name="version_a">The first version number to compare.</param>
    /// <param name="version_b">The second version number to compare.</param>
    /// <returns>
    ///   -1 if versionA is less than versionB. 0 if versionA is equal to versionB. +1 if versionA is greater
    ///   than versionB
    /// </returns>
    static int CompareVersions(string version_a, string version_b) {
      try {
        var split_strings_a = version_a.Split('-');
        version_a = split_strings_a[0];
        var prerelease_a = "\uFFFF";

        if (split_strings_a.Length > 1) {
          prerelease_a = split_strings_a[1];
          for (var i = 2; i < split_strings_a.Length; i++) {
            prerelease_a += "-" + split_strings_a[i];
          }
        }

        var split_a = version_a.Split('.');
        var major_a = int.Parse(s : split_a[0]);
        var minor_a = int.Parse(s : split_a[1]);
        var patch_a = 0;
        if (split_a.Length >= 3) {
          patch_a = int.Parse(s : split_a[2]);
        }

        var build_a = 0;
        if (split_a.Length >= 4) {
          build_a = int.Parse(s : split_a[3]);
        }

        var split_strings_b = version_b.Split('-');
        version_b = split_strings_b[0];
        var prerelease_b = "\uFFFF";

        if (split_strings_b.Length > 1) {
          prerelease_b = split_strings_b[1];
          for (var i = 2; i < split_strings_b.Length; i++) {
            prerelease_b += "-" + split_strings_b[i];
          }
        }

        var split_b = version_b.Split('.');
        var major_b = int.Parse(s : split_b[0]);
        var minor_b = int.Parse(s : split_b[1]);
        var patch_b = 0;
        if (split_b.Length >= 3) {
          patch_b = int.Parse(s : split_b[2]);
        }

        var build_b = 0;
        if (split_b.Length >= 4) {
          build_b = int.Parse(s : split_b[3]);
        }

        var major = major_a < major_b ? -1 :
                    major_a > major_b ? 1 : 0;
        var minor = minor_a < minor_b ? -1 :
                    minor_a > minor_b ? 1 : 0;
        var patch = patch_a < patch_b ? -1 :
                    patch_a > patch_b ? 1 : 0;
        var build = build_a < build_b ? -1 :
                    build_a > build_b ? 1 : 0;
        var prerelease = string.Compare(strA : prerelease_a,
                                        strB : prerelease_b,
                                        comparisonType : System.StringComparison.Ordinal);

        if (major == 0) {
          // if major versions are equal, compare minor versions
          if (minor == 0) {
            if (patch == 0) {
              // if patch versions are equal, compare build versions
              if (build == 0) {
                // if the build versions are equal, just return the prerelease version comparison
                return prerelease;
              }

              // the build versions are different, so use them
              return build;
            }

            // the patch versions are different, so use them
            return patch;
          }

          // the minor versions are different, so use them
          return minor;
        }

        // the major versions are different, so use them
        return major;
      } catch (System.Exception) {
        UnityEngine.Debug.LogErrorFormat("Compare Error: {0} {1}", version_a, version_b);
        return -1;
      }
    }
  }
}