namespace NuGetForUnity.Editor {
  using Enumerable = System.Linq.Enumerable;

  /// <summary>
  ///   Represents a .nuspec file used to store metadata for a NuGet package.
  /// </summary>
  /// <remarks>
  ///   At a minimum, Id, Version, Description, and Authors is required.  Everything else is optional.
  ///   See more info here: https://docs.microsoft.com/en-us/nuget/schema/nuspec
  /// </remarks>
  public class NuspecFile {
    /// <summary>
    ///   Gets or sets the source control branch the package is from.
    /// </summary>
    public string _RepositoryBranch;

    /// <summary>
    ///   Gets or sets the source control commit the package is from.
    /// </summary>
    public string _RepositoryCommit;

    /// <summary>
    ///   Gets or sets the type of source control software that the package's source code resides in.
    /// </summary>
    public string _RepositoryType;

    /// <summary>
    ///   Gets or sets the url for the location of the package's source code.
    /// </summary>
    public string _RepositoryUrl;

    public NuspecFile() {
      this.Dependencies = new System.Collections.Generic.List<NugetFrameworkGroup>();
      this.Files = new System.Collections.Generic.List<NuspecContentFile>();
    }

    /// <summary>
    ///   Gets or sets the title of the NuGet package.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    ///   Gets or sets the owners of the NuGet package.
    /// </summary>
    public string Owners { get; set; }

    /// <summary>
    ///   Gets or sets the URL for the location of the license of the NuGet package.
    /// </summary>
    public string LicenseUrl { get; set; }

    /// <summary>
    ///   Gets or sets the URL for the location of the project webpage of the NuGet package.
    /// </summary>
    public string ProjectUrl { get; set; }

    /// <summary>
    ///   Gets or sets the URL for the location of the icon of the NuGet package.
    /// </summary>
    public string IconUrl { get; set; }

    /// <summary>
    ///   Gets or sets a value indicating whether the license of the NuGet package needs to be accepted in order to
    ///   use it.
    /// </summary>
    public bool RequireLicenseAcceptance { get; set; }

    /// <summary>
    ///   Gets or sets the NuGet packages that this NuGet package depends on.
    /// </summary>
    public System.Collections.Generic.List<NugetFrameworkGroup> Dependencies { get; set; }

    /// <summary>
    ///   Gets or sets the release notes of the NuGet package.
    /// </summary>
    public string ReleaseNotes { get; set; }

    /// <summary>
    ///   Gets or sets the copyright of the NuGet package.
    /// </summary>
    public string Copyright { get; set; }

    /// <summary>
    ///   Gets or sets the tags of the NuGet package.
    /// </summary>
    public string Tags { get; set; }

    /// <summary>
    ///   Gets or sets the list of content files listed in the .nuspec file.
    /// </summary>
    public System.Collections.Generic.List<NuspecContentFile> Files { get; set; }

    /// <summary>
    ///   Loads the .nuspec file inside the .nupkg file at the given filepath.
    /// </summary>
    /// <param name="nupkg_filepath">The filepath to the .nupkg file to load.</param>
    /// <returns>The .nuspec file loaded from inside the .nupkg file.</returns>
    public static NuspecFile FromNupkgFile(string nupkg_filepath) {
      var nuspec = new NuspecFile();

      if (System.IO.File.Exists(path : nupkg_filepath)) {
        // get the .nuspec file from inside the .nupkg
        using (var zip = Ionic.Zip.ZipFile.Read(fileName : nupkg_filepath)) {
          //var entry = zip[string.Format("{0}.nuspec", packageId)];
          var entry = Enumerable.First(zip, x => x.FileName.EndsWith(".nuspec"));

          using (var stream = new System.IO.MemoryStream()) {
            entry.Extract(stream : stream);
            stream.Position = 0;

            nuspec = Load(stream : stream);
          }
        }
      } else {
        UnityEngine.Debug.LogErrorFormat("Package could not be read: {0}", nupkg_filepath);

        //nuspec.Id = packageId;
        //nuspec.Version = packageVersion;
        nuspec.Description = string.Format("COULD NOT LOAD {0}", arg0 : nupkg_filepath);
      }

      return nuspec;
    }

    /// <summary>
    ///   Loads a .nuspec file at the given filepath.
    /// </summary>
    /// <param name="file_path">The full filepath to the .nuspec file to load.</param>
    /// <returns>The newly loaded <see cref="NuspecFile" />.</returns>
    public static NuspecFile Load(string file_path) {
      return Load(nuspec_document : System.Xml.Linq.XDocument.Load(uri : file_path));
    }

    /// <summary>
    ///   Loads a .nuspec file inside the given stream.
    /// </summary>
    /// <param name="stream">The stream containing the .nuspec file to load.</param>
    /// <returns>The newly loaded <see cref="NuspecFile" />.</returns>
    public static NuspecFile Load(System.IO.Stream stream) {
      System.Xml.XmlReader reader = new System.Xml.XmlTextReader(input : stream);
      var document = System.Xml.Linq.XDocument.Load(reader : reader);
      return Load(nuspec_document : document);
    }

    /// <summary>
    ///   Loads a .nuspec file inside the given <see cref="System.Xml.Linq.XDocument" />.
    /// </summary>
    /// <param name="nuspec_document">The .nuspec file as an <see cref="System.Xml.Linq.XDocument" />.</param>
    /// <returns>The newly loaded <see cref="NuspecFile" />.</returns>
    public static NuspecFile Load(System.Xml.Linq.XDocument nuspec_document) {
      var nuspec = new NuspecFile();

      var nuspec_namespace = nuspec_document.Root.GetDefaultNamespace().ToString();

      var package =
          nuspec_document.Element(name : System.Xml.Linq.XName.Get("package",
                                                                  namespaceName : nuspec_namespace));
      var metadata =
          package.Element(name : System.Xml.Linq.XName.Get("metadata", namespaceName : nuspec_namespace));

      nuspec.Id =
          (string)metadata.Element(name : System.Xml.Linq.XName.Get("id", namespaceName : nuspec_namespace))
          ?? string.Empty;
      nuspec.Version =
          (string)metadata.Element(name : System.Xml.Linq.XName.Get("version",
                                                                    namespaceName : nuspec_namespace))
          ?? string.Empty;
      nuspec.Title =
          (string)metadata.Element(name : System.Xml.Linq.XName.Get("title", namespaceName : nuspec_namespace))
          ?? string.Empty;
      nuspec.Authors =
          (string)metadata.Element(name : System.Xml.Linq.XName.Get("authors",
                                                                    namespaceName : nuspec_namespace))
          ?? string.Empty;
      nuspec.Owners =
          (string)metadata.Element(name : System.Xml.Linq.XName.Get("owners",
                                                                    namespaceName : nuspec_namespace))
          ?? string.Empty;
      nuspec.LicenseUrl =
          (string)metadata.Element(name : System.Xml.Linq.XName.Get("licenseUrl",
                                                                    namespaceName : nuspec_namespace))
          ?? string.Empty;
      nuspec.ProjectUrl =
          (string)metadata.Element(name : System.Xml.Linq.XName.Get("projectUrl",
                                                                    namespaceName : nuspec_namespace))
          ?? string.Empty;
      nuspec.IconUrl =
          (string)metadata.Element(name : System.Xml.Linq.XName.Get("iconUrl",
                                                                    namespaceName : nuspec_namespace))
          ?? string.Empty;
      nuspec.RequireLicenseAcceptance =
          bool.Parse(value :
                     (string)metadata.Element(name : System.Xml.Linq.XName.Get("requireLicenseAcceptance",
                                                namespaceName : nuspec_namespace))
                     ?? "False");
      nuspec.Description =
          (string)metadata.Element(name : System.Xml.Linq.XName.Get("description",
                                                                    namespaceName : nuspec_namespace))
          ?? string.Empty;
      nuspec.Summary =
          (string)metadata.Element(name : System.Xml.Linq.XName.Get("summary",
                                                                    namespaceName : nuspec_namespace))
          ?? string.Empty;
      nuspec.ReleaseNotes =
          (string)metadata.Element(name : System.Xml.Linq.XName.Get("releaseNotes",
                                                                    namespaceName : nuspec_namespace))
          ?? string.Empty;
      nuspec.Copyright =
          (string)metadata.Element(name : System.Xml.Linq.XName.Get("copyright",
                                                                    namespaceName : nuspec_namespace));
      nuspec.Tags =
          (string)metadata.Element(name : System.Xml.Linq.XName.Get("tags", namespaceName : nuspec_namespace))
          ?? string.Empty;

      var repository_element =
          metadata.Element(name : System.Xml.Linq.XName.Get("repository", namespaceName : nuspec_namespace));

      if (repository_element != null) {
        nuspec._RepositoryType = (string)repository_element.Attribute(name : System.Xml.Linq.XName.Get("type"))
                                ?? string.Empty;
        nuspec._RepositoryUrl = (string)repository_element.Attribute(name : System.Xml.Linq.XName.Get("url"))
                               ?? string.Empty;
        nuspec._RepositoryBranch =
            (string)repository_element.Attribute(name : System.Xml.Linq.XName.Get("branch")) ?? string.Empty;
        nuspec._RepositoryCommit =
            (string)repository_element.Attribute(name : System.Xml.Linq.XName.Get("commit")) ?? string.Empty;
      }

      var dependencies_element =
          metadata.Element(name : System.Xml.Linq.XName.Get("dependencies", namespaceName : nuspec_namespace));
      if (dependencies_element != null) {
        // Dependencies specified for specific target frameworks
        foreach (var framework_group in dependencies_element.Elements(name : System.Xml.Linq.XName.Get("group",
                                                                      namespaceName : nuspec_namespace))) {
          var group = new NugetFrameworkGroup();
          group.TargetFramework =
              ConvertFromNupkgTargetFrameworkName(target_framework :
                                                  (string)framework_group.Attribute("targetFramework")
                                                  ?? string.Empty);

          foreach (var dependency_element in
              framework_group.Elements(name : System.Xml.Linq.XName.Get("dependency",
                                                                       namespaceName : nuspec_namespace))) {
            var dependency = new NugetPackageIdentifier();

            dependency._Id = (string)dependency_element.Attribute("id") ?? string.Empty;
            dependency._Version = (string)dependency_element.Attribute("version") ?? string.Empty;
            group.Dependencies.Add(item : dependency);
          }

          nuspec.Dependencies.Add(item : group);
        }

        // Flat dependency list
        if (nuspec.Dependencies.Count == 0) {
          var group = new NugetFrameworkGroup();
          foreach (var dependency_element in
              dependencies_element.Elements(name : System.Xml.Linq.XName.Get("dependency",
                                             namespaceName : nuspec_namespace))) {
            var dependency = new NugetPackageIdentifier();
            dependency._Id = (string)dependency_element.Attribute("id") ?? string.Empty;
            dependency._Version = (string)dependency_element.Attribute("version") ?? string.Empty;
            group.Dependencies.Add(item : dependency);
          }

          nuspec.Dependencies.Add(item : group);
        }
      }

      var files_element =
          package.Element(name : System.Xml.Linq.XName.Get("files", namespaceName : nuspec_namespace));
      if (files_element != null) {
        //UnityEngine.Debug.Log("Loading files!");
        foreach (var file_element in files_element.Elements(name : System.Xml.Linq.XName.Get("file",
                                                            namespaceName : nuspec_namespace))) {
          var file = new NuspecContentFile();
          file.Source = (string)file_element.Attribute("src") ?? string.Empty;
          file.Target = (string)file_element.Attribute("target") ?? string.Empty;
          nuspec.Files.Add(item : file);
        }
      }

      return nuspec;
    }

    /// <summary>
    ///   Saves a <see cref="NuspecFile" /> to the given filepath, automatically overwriting.
    /// </summary>
    /// <param name="file_path">The full filepath to the .nuspec file to save.</param>
    public void Save(string file_path) {
      // TODO: Set a namespace when saving

      var file = new System.Xml.Linq.XDocument();
      var package_element = new System.Xml.Linq.XElement("package");
      file.Add(content : package_element);
      var metadata = new System.Xml.Linq.XElement("metadata");

      // required
      metadata.Add(content : new System.Xml.Linq.XElement("id", content : this.Id));
      metadata.Add(content : new System.Xml.Linq.XElement("version", content : this.Version));
      metadata.Add(content : new System.Xml.Linq.XElement("description", content : this.Description));
      metadata.Add(content : new System.Xml.Linq.XElement("authors", content : this.Authors));

      if (!string.IsNullOrEmpty(value : this.Title)) {
        metadata.Add(content : new System.Xml.Linq.XElement("title", content : this.Title));
      }

      if (!string.IsNullOrEmpty(value : this.Owners)) {
        metadata.Add(content : new System.Xml.Linq.XElement("owners", content : this.Owners));
      }

      if (!string.IsNullOrEmpty(value : this.LicenseUrl)) {
        metadata.Add(content : new System.Xml.Linq.XElement("licenseUrl", content : this.LicenseUrl));
      }

      if (!string.IsNullOrEmpty(value : this.ProjectUrl)) {
        metadata.Add(content : new System.Xml.Linq.XElement("projectUrl", content : this.ProjectUrl));
      }

      if (!string.IsNullOrEmpty(value : this.IconUrl)) {
        metadata.Add(content : new System.Xml.Linq.XElement("iconUrl", content : this.IconUrl));
      }

      if (this.RequireLicenseAcceptance) {
        metadata.Add(content : new System.Xml.Linq.XElement("requireLicenseAcceptance",
                                                            content : this.RequireLicenseAcceptance));
      }

      if (!string.IsNullOrEmpty(value : this.ReleaseNotes)) {
        metadata.Add(content : new System.Xml.Linq.XElement("releaseNotes", content : this.ReleaseNotes));
      }

      if (!string.IsNullOrEmpty(value : this.Copyright)) {
        metadata.Add(content : new System.Xml.Linq.XElement("copyright", content : this.Copyright));
      }

      if (!string.IsNullOrEmpty(value : this.Tags)) {
        metadata.Add(content : new System.Xml.Linq.XElement("tags", content : this.Tags));
      }

      if (this.Dependencies.Count > 0) {
        //UnityEngine.Debug.Log("Saving dependencies!");
        var dependencies_element = new System.Xml.Linq.XElement("dependencies");
        foreach (var framework_group in this.Dependencies) {
          var group = new System.Xml.Linq.XElement("group");
          if (!string.IsNullOrEmpty(value : framework_group.TargetFramework)) {
            group.Add(content : new System.Xml.Linq.XAttribute("targetFramework",
                                                               value : framework_group.TargetFramework));
          }

          foreach (var dependency in framework_group.Dependencies) {
            var dependency_element = new System.Xml.Linq.XElement("dependency");
            dependency_element.Add(content : new System.Xml.Linq.XAttribute("id", value : dependency._Id));
            dependency_element.Add(content : new System.Xml.Linq.XAttribute("version",
                                    value : dependency._Version));
            dependencies_element.Add(content : dependency_element);
          }
        }

        metadata.Add(content : dependencies_element);
      }

      file.Root.Add(content : metadata);

      if (this.Files.Count > 0) {
        //UnityEngine.Debug.Log("Saving files!");
        var files_element = new System.Xml.Linq.XElement("files");
        foreach (var content_file in this.Files) {
          var file_element = new System.Xml.Linq.XElement("file");
          file_element.Add(content : new System.Xml.Linq.XAttribute("src", value : content_file.Source));
          file_element.Add(content : new System.Xml.Linq.XAttribute("target", value : content_file.Target));
          files_element.Add(content : file_element);
        }

        package_element.Add(content : files_element);
      }

      // remove the read only flag on the file, if there is one.
      if (System.IO.File.Exists(path : file_path)) {
        var attributes = System.IO.File.GetAttributes(path : file_path);

        if ((attributes & System.IO.FileAttributes.ReadOnly) == System.IO.FileAttributes.ReadOnly) {
          attributes &= ~System.IO.FileAttributes.ReadOnly;
          System.IO.File.SetAttributes(path : file_path, fileAttributes : attributes);
        }
      }

      file.Save(fileName : file_path);
    }

    static string ConvertFromNupkgTargetFrameworkName(string target_framework) {
      var converted_target_framework = target_framework.ToLower().Replace(".netstandard", "netstandard")
                                                    .Replace("native0.0", "native");

      converted_target_framework = converted_target_framework.StartsWith(".netframework")
                                     ? converted_target_framework.Replace(".netframework", "net")
                                                               .Replace(".", "")
                                     : converted_target_framework;

      return converted_target_framework;
    }

    #region Required

    /// <summary>
    ///   Gets or sets the ID of the NuGet package.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    ///   Gets or sets the version number of the NuGet package.
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    ///   Gets or sets the description of the NuGet package.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    ///   Gets or sets the description of the NuGet package.
    /// </summary>
    public string Summary { get; set; }

    /// <summary>
    ///   Gets or sets the authors of the NuGet package.
    /// </summary>
    public string Authors { get; set; }

    #endregion
  }
}