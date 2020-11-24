namespace NuGetForUnity.Editor {
  /// <summary>
  ///   Represents a package.config file that holds the NuGet package dependencies for the project.
  /// </summary>
  public class PackagesConfigFile {
    /// <summary>
    ///   Gets the <see cref="NugetPackageIdentifier" />s contained in the package.config file.
    /// </summary>
    public System.Collections.Generic.List<NugetPackageIdentifier> Packages { get; private set; }

    /// <summary>
    ///   Adds a package to the packages.config file.
    /// </summary>
    /// <param name="package">The NugetPackage to add to the packages.config file.</param>
    public void AddPackage(NugetPackageIdentifier package) {
      var existing_package = this.Packages.Find(p => p._Id.ToLower() == package._Id.ToLower());
      if (existing_package != null) {
        if (existing_package < package) {
          UnityEngine.Debug
                     .LogWarningFormat("{0} {1} is already listed in the packages.config file.  Updating to {2}",
                                       existing_package._Id,
                                       existing_package._Version,
                                       package._Version);
          this.Packages.Remove(item : existing_package);
          this.Packages.Add(item : package);
        } else if (existing_package > package) {
          UnityEngine.Debug
                     .LogWarningFormat("Trying to add {0} {1} to the packages.config file.  {2} is already listed, so using that.",
                                       package._Id,
                                       package._Version,
                                       existing_package._Version);
        }
      } else {
        this.Packages.Add(item : package);
      }
    }

    /// <summary>
    ///   Removes a package from the packages.config file.
    /// </summary>
    /// <param name="package">The NugetPackage to remove from the packages.config file.</param>
    public void RemovePackage(NugetPackageIdentifier package) { this.Packages.Remove(item : package); }

    /// <summary>
    ///   Loads a list of all currently installed packages by reading the packages.config file.
    /// </summary>
    /// <returns>A newly created <see cref="PackagesConfigFile" />.</returns>
    public static PackagesConfigFile Load(string filepath) {
      var config_file =
          new PackagesConfigFile {Packages = new System.Collections.Generic.List<NugetPackageIdentifier>()};

      // Create a package.config file, if there isn't already one in the project
      if (!System.IO.File.Exists(path : filepath)) {
        UnityEngine.Debug.LogFormat("No packages.config file found. Creating default at {0}", filepath);

        config_file.Save(filepath : filepath);

        UnityEditor.AssetDatabase.Refresh();
      }

      var packages_file = System.Xml.Linq.XDocument.Load(uri : filepath);

      // Force disable
      NugetHelper.DisableWsapExportSetting(file_path : filepath, false);

      foreach (var package_element in packages_file.Root?.Elements()) {
        var package = new NugetPackage {
                                           _Id = package_element.Attribute("id")?.Value,
                                           _Version = package_element.Attribute("version")?.Value
                                       };
        config_file.Packages.Add(item : package);
      }

      return config_file;
    }

    /// <summary>
    ///   Saves the packages.config file and populates it with given installed NugetPackages.
    /// </summary>
    /// <param name="filepath">The filepath to where this packages.config will be saved.</param>
    public void Save(string filepath) {
      this.Packages.Sort(delegate(NugetPackageIdentifier x, NugetPackageIdentifier y) {
                           if (x._Id == null && y._Id == null) {
                             return 0;
                           }

                           if (x._Id == null) {
                             return -1;
                           }

                           if (y._Id == null) {
                             return 1;
                           }

                           if (x._Id == y._Id) {
                             return x._Version.CompareTo(strB : y._Version);
                           }

                           return x._Id.CompareTo(strB : y._Id);
                         });

      var packages_file = new System.Xml.Linq.XDocument();
      packages_file.Add(content : new System.Xml.Linq.XElement("packages"));
      foreach (var package in this.Packages) {
        var package_element = new System.Xml.Linq.XElement("package");
        package_element.Add(content : new System.Xml.Linq.XAttribute("id", value : package._Id));
        package_element.Add(content : new System.Xml.Linq.XAttribute("version", value : package._Version));
        packages_file.Root?.Add(content : package_element);
      }

      // remove the read only flag on the file, if there is one.
      var package_exists = System.IO.File.Exists(path : filepath);
      if (package_exists) {
        var attributes = System.IO.File.GetAttributes(path : filepath);

        if ((attributes & System.IO.FileAttributes.ReadOnly) == System.IO.FileAttributes.ReadOnly) {
          attributes &= ~System.IO.FileAttributes.ReadOnly;
          System.IO.File.SetAttributes(path : filepath, fileAttributes : attributes);
        }
      }

      packages_file.Save(fileName : filepath);

      NugetHelper.DisableWsapExportSetting(file_path : filepath, notify_of_update : package_exists);
    }
  }
}