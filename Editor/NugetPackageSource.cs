namespace NuGetForUnity.Editor {
  using Enumerable = System.Linq.Enumerable;

  /// <summary>
  ///   Represents a NuGet Package Source (a "server").
  /// </summary>
  public class NugetPackageSource {
    /// <summary>
    ///   Initializes a new instance of the <see cref="NugetPackageSource" /> class.
    /// </summary>
    /// <param name="name">The name of the package source.</param>
    /// <param name="path">The path to the package source.</param>
    public NugetPackageSource(string name, string path) {
      this.Name = name;
      this.SavedPath = path;
      this.IsLocalPath = !this.ExpandedPath.StartsWith("http");
      this.IsEnabled = true;
    }

    /// <summary>
    ///   Gets or sets the name of the package source.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    ///   Gets or sets the path of the package source.
    /// </summary>
    public string SavedPath { get; set; }

    /// <summary>
    ///   Gets path, with the values of environment variables expanded.
    /// </summary>
    public string ExpandedPath {
      get {
        var path = System.Environment.ExpandEnvironmentVariables(name : this.SavedPath);
        if (!path.StartsWith("http")
            && path != "(Aggregate source)"
            && !System.IO.Path.IsPathRooted(path : path)) {
          path =
              System.IO.Path.Combine(path1 : System.IO.Path.GetDirectoryName(path : NugetHelper
                                         .NugetConfigFilePath),
                                     path2 : path);
        }

        return path;
      }
    }

    public string UserName { get; set; }

    /// <summary>
    ///   Gets or sets the password used to access the feed. Null indicates that no password is used.
    /// </summary>
    public string SavedPassword { get; set; }

    /// <summary>
    ///   Gets password, with the values of environment variables expanded.
    /// </summary>
    public string ExpandedPassword {
      get {
        return this.SavedPassword != null
                   ? System.Environment.ExpandEnvironmentVariables(name : this.SavedPassword)
                   : null;
      }
    }

    public bool HasPassword {
      get { return this.SavedPassword != null; }
      set {
        if (value) {
          if (this.SavedPassword == null) {
            this.SavedPassword = string.Empty; // Initialize newly-enabled password to empty string.
          }
        } else {
          this.SavedPassword = null; // Clear password to null when disabled.
        }
      }
    }

    /// <summary>
    ///   Gets or sets a value indicated whether the path is a local path or a remote path.
    /// </summary>
    public bool IsLocalPath { get; }

    /// <summary>
    ///   Gets or sets a value indicated whether this source is enabled or not.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    ///   Gets a NugetPackage from the NuGet server that matches (or is in range of) the
    ///   <see cref="NugetPackageIdentifier" /> given.
    /// </summary>
    /// <param name="package">
    ///   The <see cref="NugetPackageIdentifier" /> containing the ID and Version of the package
    ///   to get.
    /// </param>
    /// <returns>The retrieved package, if there is one.  Null if no matching package was found.</returns>
    public System.Collections.Generic.List<NugetPackage> FindPackagesById(NugetPackageIdentifier package) {
      System.Collections.Generic.List<NugetPackage> found_packages;

      if (this.IsLocalPath) {
        if (!package.HasVersionRange) {
          var local_package_path = System.IO.Path.Combine(path1 : this.ExpandedPath,
                                                          path2 : string.Format("./{0}.{1}.nupkg",
                                                            arg0 : package._Id,
                                                            arg1 : package._Version));
          if (System.IO.File.Exists(path : local_package_path)) {
            var local_package = NugetPackage.FromNupkgFile(nupkgFilepath : local_package_path);
            found_packages = new System.Collections.Generic.List<NugetPackage> {local_package};
          } else {
            found_packages = new System.Collections.Generic.List<NugetPackage>();
          }
        } else {
          // TODO: Optimize to no longer use GetLocalPackages, since that loads the .nupkg itself
          found_packages = this.GetLocalPackages(search_term : package._Id, true, true);
        }
      } else {
        // See here: http://www.odata.org/documentation/odata-version-2-0/uri-conventions/
        // Note: without $orderby=Version, the Version filter below will not work
        var url = $"{this.ExpandedPath}FindPackagesById()?id='{package._Id}'&$orderby=Version asc";

        // Are we looking for a specific package?
        if (!package.HasVersionRange) {
          url = $"{url}&$filter=Version eq '{package._Version}'";
        }

        try {
          found_packages =
              this.GetPackagesFromUrl(url : url, username : this.UserName, password : this.ExpandedPassword);
        } catch (System.Exception e) {
          found_packages = new System.Collections.Generic.List<NugetPackage>();
          UnityEngine.Debug.LogErrorFormat("Unable to retrieve package list from {0}\n{1}", url, e);
        }
      }

      if (found_packages != null) {
        // Return all the packages in the range of versions specified by 'package'.
        found_packages.RemoveAll(p => !package.InRange(other_package : p));
        found_packages.Sort();

        foreach (var found_package in found_packages) {
          found_package.PackageSource = this;
        }
      }

      return found_packages;
    }

    /// <summary>
    ///   Gets a NugetPackage from the NuGet server that matches (or is in range of) the
    ///   <see cref="NugetPackageIdentifier" /> given.
    /// </summary>
    /// <param name="package">
    ///   The <see cref="NugetPackageIdentifier" /> containing the ID and Version of the package
    ///   to get.
    /// </param>
    /// <returns>The retrieved package, if there is one.  Null if no matching package was found.</returns>
    public NugetPackage GetSpecificPackage(NugetPackageIdentifier package) {
      return Enumerable.FirstOrDefault(this.FindPackagesById(package : package));
    }

    /// <summary>
    ///   Gets a list of NuGetPackages from this package source.
    ///   This allows searching for partial IDs or even the empty string (the default) to list ALL packages.
    ///   NOTE: See the functions and parameters defined here: https://www.nuget.org/api/v2/$metadata
    /// </summary>
    /// <param name="search_term">The search term to use to filter packages. Defaults to the empty string.</param>
    /// <param name="include_all_versions">True to include older versions that are not the latest version.</param>
    /// <param name="include_prerelease">True to include prerelease packages (alpha, beta, etc).</param>
    /// <param name="number_to_get">The number of packages to fetch.</param>
    /// <param name="number_to_skip">The number of packages to skip before fetching.</param>
    /// <returns>The list of available packages.</returns>
    public System.Collections.Generic.List<NugetPackage> Search(string search_term = "",
                                                                bool include_all_versions = false,
                                                                bool include_prerelease = false,
                                                                int number_to_get = 15,
                                                                int number_to_skip = 0) {
      if (this.IsLocalPath) {
        return this.GetLocalPackages(search_term : search_term,
                                     include_all_versions : include_all_versions,
                                     include_prerelease : include_prerelease,
                                     number_to_get : number_to_get,
                                     number_to_skip : number_to_skip);
      }

      //Example URL: "http://www.nuget.org/api/v2/Search()?$filter=IsLatestVersion&$orderby=Id&$skip=0&$top=30&searchTerm='newtonsoft'&targetFramework=''&includePrerelease=false";

      var url = this.ExpandedPath;

      // call the search method
      url += "Search()?";

      // filter results
      if (!include_all_versions) {
        if (!include_prerelease) {
          url += "$filter=IsLatestVersion&";
        } else {
          url += "$filter=IsAbsoluteLatestVersion&";
        }
      }

      // order results
      //url += "$orderby=Id&";
      //url += "$orderby=LastUpdated&";
      url += "$orderby=DownloadCount desc&";

      // skip a certain number of entries
      url += string.Format("$skip={0}&", arg0 : number_to_skip);

      // show a certain number of entries
      url += string.Format("$top={0}&", arg0 : number_to_get);

      // apply the search term
      url += string.Format("searchTerm='{0}'&", arg0 : search_term);

      // apply the target framework filters
      url += "targetFramework=''&";

      // should we include prerelease packages?
      url += string.Format("includePrerelease={0}", arg0 : include_prerelease.ToString().ToLower());

      try {
        return this.GetPackagesFromUrl(url : url, username : this.UserName, password : this.ExpandedPassword);
      } catch (System.Exception e) {
        UnityEngine.Debug.LogErrorFormat("Unable to retrieve package list from {0}\n{1}", url, e);
        return new System.Collections.Generic.List<NugetPackage>();
      }
    }

    /// <summary>
    ///   Gets a list of all available packages from a local source (not a web server) that match the given filters.
    /// </summary>
    /// <param name="search_term">The search term to use to filter packages. Defaults to the empty string.</param>
    /// <param name="include_all_versions">True to include older versions that are not the latest version.</param>
    /// <param name="include_prerelease">True to include prerelease packages (alpha, beta, etc).</param>
    /// <param name="number_to_get">The number of packages to fetch.</param>
    /// <param name="number_to_skip">The number of packages to skip before fetching.</param>
    /// <returns>The list of available packages.</returns>
    System.Collections.Generic.List<NugetPackage> GetLocalPackages(string search_term = "",
                                                                   bool include_all_versions = false,
                                                                   bool include_prerelease = false,
                                                                   int number_to_get = 15,
                                                                   int number_to_skip = 0) {
      var local_packages = new System.Collections.Generic.List<NugetPackage>();

      if (number_to_skip != 0) {
        // we return the entire list the first time, so no more to add
        return local_packages;
      }

      var path = this.ExpandedPath;

      if (System.IO.Directory.Exists(path : path)) {
        var package_paths = System.IO.Directory.GetFiles(path : path,
                                                         searchPattern : string.Format("*{0}*.nupkg",
                                                           arg0 : search_term));

        foreach (var package_path in package_paths) {
          var package = NugetPackage.FromNupkgFile(nupkgFilepath : package_path);
          package.PackageSource = this;

          if (package.IsPreRelease && !include_prerelease) {
            // if it's a prerelease package and we aren't supposed to return prerelease packages, just skip it
            continue;
          }

          if (include_all_versions) {
            // if all versions are being included, simply add it and move on
            local_packages.Add(item : package);
            //LogVerbose("Adding {0} {1}", package.Id, package.Version);
            continue;
          }

          var existing_package = Enumerable.FirstOrDefault(local_packages, x => x._Id == package._Id);
          if (existing_package != null) {
            // there is already a package with the same ID in the list
            if (existing_package < package) {
              // if the current package is newer than the existing package, swap them
              local_packages.Remove(item : existing_package);
              local_packages.Add(item : package);
            }
          } else {
            // there is no package with the same ID in the list yet
            local_packages.Add(item : package);
          }
        }
      } else {
        UnityEngine.Debug.LogErrorFormat("Local folder not found: {0}", path);
      }

      return local_packages;
    }

    /// <summary>
    ///   Builds a list of NugetPackages from the XML returned from the HTTP GET request issued at the given URL.
    ///   Note that NuGet uses an Atom-feed (XML Syndicaton) superset called OData.
    ///   See here http://www.odata.org/documentation/odata-version-2-0/uri-conventions/
    /// </summary>
    /// <param name="url"></param>
    /// <param name="password"></param>
    /// <returns></returns>
    System.Collections.Generic.List<NugetPackage> GetPackagesFromUrl(
        string url,
        string username,
        string password) {
      NugetHelper.LogVerbose("Getting packages from: {0}", url);

      var stopwatch = new System.Diagnostics.Stopwatch();
      stopwatch.Start();

      var packages = new System.Collections.Generic.List<NugetPackage>();

      // Mono doesn't have a Certificate Authority, so we have to provide all validation manually.  Currently just accept anything.
      // See here: http://stackoverflow.com/questions/4926676/mono-webrequest-fails-with-https

      // remove all handlers
      System.Net.ServicePointManager.ServerCertificateValidationCallback = null;

      // add anonymous handler
      System.Net.ServicePointManager.ServerCertificateValidationCallback +=
          (sender, certificate, chain, policy_errors) => true;

      using (var response_stream =
          NugetHelper.RequestUrl(url : url,
                                 user_name : username,
                                 password : password,
                                 5000)) {
        using (var stream_reader = new System.IO.StreamReader(stream : response_stream)) {
          packages =
              NugetODataResponse.Parse(document : System.Xml.Linq.XDocument.Load(textReader : stream_reader));
          foreach (var package in packages) {
            package.PackageSource = this;
          }
        }
      }

      stopwatch.Stop();
      NugetHelper.LogVerbose("Retreived {0} packages in {1} ms",
                             packages.Count,
                             stopwatch.ElapsedMilliseconds);

      return packages;
    }

    /// <summary>
    ///   Gets a list of available packages from a local source (not a web server) that are upgrades for the given
    ///   list of installed packages.
    /// </summary>
    /// <param name="installed_packages">The list of currently installed packages to use to find updates.</param>
    /// <param name="include_prerelease">True to include prerelease packages (alpha, beta, etc).</param>
    /// <param name="include_all_versions">True to include older versions that are not the latest version.</param>
    /// <returns>A list of all updates available.</returns>
    System.Collections.Generic.List<NugetPackage> GetLocalUpdates(
        System.Collections.Generic.IEnumerable<NugetPackage> installed_packages,
        bool include_prerelease = false,
        bool include_all_versions = false) {
      var updates = new System.Collections.Generic.List<NugetPackage>();

      var available_packages = this.GetLocalPackages(search_term : string.Empty,
                                                     include_all_versions : include_all_versions,
                                                     include_prerelease : include_prerelease);
      foreach (var installed_package in installed_packages) {
        foreach (var available_package in available_packages) {
          if (installed_package._Id == available_package._Id) {
            if (installed_package < available_package) {
              updates.Add(item : available_package);
            }
          }
        }
      }

      return updates;
    }

    /// <summary>
    ///   Queries the source with the given list of installed packages to get any updates that are available.
    /// </summary>
    /// <param name="installed_packages">The list of currently installed packages.</param>
    /// <param name="include_prerelease">True to include prerelease packages (alpha, beta, etc).</param>
    /// <param name="include_all_versions">True to include older versions that are not the latest version.</param>
    /// <param name="target_frameworks">The specific frameworks to target?</param>
    /// <param name="version_contraints">The version constraints?</param>
    /// <returns>A list of all updates available.</returns>
    public System.Collections.Generic.List<NugetPackage> GetUpdates(
        System.Collections.Generic.IEnumerable<NugetPackage> installed_packages,
        bool include_prerelease = false,
        bool include_all_versions = false,
        string target_frameworks = "",
        string version_contraints = "") {
      if (this.IsLocalPath) {
        return this.GetLocalUpdates(installed_packages : installed_packages,
                                    include_prerelease : include_prerelease,
                                    include_all_versions : include_all_versions);
      }

      var updates = new System.Collections.Generic.List<NugetPackage>();

      // check for updates in groups of 10 instead of all of them, since that causes servers to throw errors for queries that are too long
      for (var i = 0; i < Enumerable.Count(installed_packages); i += 10) {
        var package_group = Enumerable.Take(Enumerable.Skip(installed_packages, count : i), 10);

        var package_ids = string.Empty;
        var versions = string.Empty;

        foreach (var package in package_group) {
          if (string.IsNullOrEmpty(value : package_ids)) {
            package_ids += package._Id;
          } else {
            package_ids += "|" + package._Id;
          }

          if (string.IsNullOrEmpty(value : versions)) {
            versions += package._Version;
          } else {
            versions += "|" + package._Version;
          }
        }

        var url =
            string.Format("{0}GetUpdates()?packageIds='{1}'&versions='{2}'&includePrerelease={3}&includeAllVersions={4}&targetFrameworks='{5}'&versionConstraints='{6}'",
                          this.ExpandedPath,
                          package_ids,
                          versions,
                          include_prerelease.ToString().ToLower(),
                          include_all_versions.ToString().ToLower(),
                          target_frameworks,
                          version_contraints);

        try {
          var new_packages =
              this.GetPackagesFromUrl(url : url, username : this.UserName, password : this.ExpandedPassword);
          updates.AddRange(collection : new_packages);
        } catch (System.Exception e) {
          var web_exception = e as System.Net.WebException;
          var web_response = web_exception != null
                                 ? web_exception.Response as System.Net.HttpWebResponse
                                 : null;
          if (web_response != null && web_response.StatusCode == System.Net.HttpStatusCode.NotFound) {
            // Some web services, such as VSTS don't support the GetUpdates API. Attempt to retrieve updates via FindPackagesById.
            NugetHelper.LogVerbose("{0} not found. Falling back to FindPackagesById.", url);
            return this.GetUpdatesFallback(installed_packages : installed_packages,
                                           include_prerelease : include_prerelease,
                                           include_all_versions : include_all_versions,
                                           target_frameworks : target_frameworks,
                                           version_contraints : version_contraints);
          }

          UnityEngine.Debug.LogErrorFormat("Unable to retrieve package list from {0}\n{1}", url, e);
        }
      }

      // sort alphabetically, then by version descending
      updates.Sort(delegate(NugetPackage x, NugetPackage y) {
                     if (x._Id == y._Id) {
                       return -1 * x.CompareVersion(other_version : y._Version);
                     }

                     return x._Id.CompareTo(strB : y._Id);
                   });

      #if TEST_GET_UPDATES_FALLBACK
            // Enable this define in order to test that GetUpdatesFallback is working as intended. This tests that it returns the same set of packages
            // that are returned by the GetUpdates API. Since GetUpdates isn't available when using a Visual Studio Team Services feed, the intention
            // is that this test would be conducted by using nuget.org's feed where both paths can be compared.
            List<NugetPackage> updatesReplacement =
 GetUpdatesFallback(installedPackages, includePrerelease, includeAllVersions, targetFrameworks, versionContraints);
            ComparePackageLists(updates, updatesReplacement, "GetUpdatesFallback doesn't match GetUpdates API");
      #endif

      return updates;
    }

    static void ComparePackageLists(System.Collections.Generic.List<NugetPackage> updates,
                                    System.Collections.Generic.List<NugetPackage> updates_replacement,
                                    string error_message_to_display_if_lists_do_not_match) {
      var matching_comparison = new System.Text.StringBuilder();
      var missing_comparison = new System.Text.StringBuilder();
      foreach (var package in updates) {
        if (updates_replacement.Contains(item : package)) {
          matching_comparison.Append(value : matching_comparison.Length == 0 ? "Matching: " : ", ");
          matching_comparison.Append(value : package);
        } else {
          missing_comparison.Append(value : missing_comparison.Length == 0 ? "Missing: " : ", ");
          missing_comparison.Append(value : package);
        }
      }

      var extra_comparison = new System.Text.StringBuilder();
      foreach (var package in updates_replacement) {
        if (!updates.Contains(item : package)) {
          extra_comparison.Append(value : extra_comparison.Length == 0 ? "Extra: " : ", ");
          extra_comparison.Append(value : package);
        }
      }

      if (missing_comparison.Length > 0 || extra_comparison.Length > 0) {
        UnityEngine.Debug.LogWarningFormat("{0}\n{1}\n{2}\n{3}",
                                           error_message_to_display_if_lists_do_not_match,
                                           matching_comparison,
                                           missing_comparison,
                                           extra_comparison);
      }
    }

    /// <summary>
    ///   Some NuGet feeds such as Visual Studio Team Services do not implement the GetUpdates function.
    ///   In that case this fallback function can be used to retrieve updates by using the FindPackagesById function.
    /// </summary>
    /// <param name="installed_packages">The list of currently installed packages.</param>
    /// <param name="include_prerelease">True to include prerelease packages (alpha, beta, etc).</param>
    /// <param name="include_all_versions">True to include older versions that are not the latest version.</param>
    /// <param name="target_frameworks">The specific frameworks to target?</param>
    /// <param name="version_contraints">The version constraints?</param>
    /// <returns>A list of all updates available.</returns>
    System.Collections.Generic.List<NugetPackage> GetUpdatesFallback(
        System.Collections.Generic.IEnumerable<NugetPackage> installed_packages,
        bool include_prerelease = false,
        bool include_all_versions = false,
        string target_frameworks = "",
        string version_contraints = "") {
      var stopwatch = new System.Diagnostics.Stopwatch();
      stopwatch.Start();
      UnityEngine.Debug.Assert(condition : string.IsNullOrEmpty(value : target_frameworks)
                                           && string
                                               .IsNullOrEmpty(value :
                                                              version_contraints)); // These features are not supported by this version of GetUpdates.

      var updates = new System.Collections.Generic.List<NugetPackage>();
      foreach (var installed_package in installed_packages) {
        var version_range =
            string.Format("({0},)",
                          arg0 : installed_package
                              ._Version); // Minimum of Current ID (exclusive) with no maximum (exclusive).
        var id = new NugetPackageIdentifier(id : installed_package._Id, version : version_range);
        var package_updates = this.FindPackagesById(package : id);

        if (!include_prerelease) {
          package_updates.RemoveAll(p => p.IsPreRelease);
        }

        if (package_updates.Count == 0) {
          continue;
        }

        var skip = include_all_versions ? 0 : package_updates.Count - 1;
        updates.AddRange(collection : Enumerable.Skip(package_updates, count : skip));
      }

      NugetHelper.LogVerbose("NugetPackageSource.GetUpdatesFallback took {0} ms",
                             stopwatch.ElapsedMilliseconds);
      return updates;
    }
  }
}