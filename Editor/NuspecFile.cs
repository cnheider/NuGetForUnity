namespace NuGetForUnity.Editor {
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using System.Xml;
  using System.Xml.Linq;
  using Ionic.Zip;

  /// <summary>
  /// Represents a .nuspec file used to store metadata for a NuGet package.
  /// </summary>
  /// <remarks>
  /// At a minumum, Id, Version, Description, and Authors is required.  Everything else is optional.
  /// See more info here: https://docs.microsoft.com/en-us/nuget/schema/nuspec
  /// </remarks>
  public class NuspecFile {
    public NuspecFile() {
      this.Dependencies = new List<NugetFrameworkGroup>();
      this.Files = new List<NuspecContentFile>();
    }

    #region Required

    /// <summary>
    /// Gets or sets the ID of the NuGet package.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the version number of the NuGet package.
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    /// Gets or sets the description of the NuGet package.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the description of the NuGet package.
    /// </summary>
    public string Summary { get; set; }

    /// <summary>
    /// Gets or sets the authors of the NuGet package.
    /// </summary>
    public string Authors { get; set; }

    #endregion

    /// <summary>
    /// Gets or sets the title of the NuGet package.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the owners of the NuGet package.
    /// </summary>
    public string Owners { get; set; }

    /// <summary>
    /// Gets or sets the URL for the location of the license of the NuGet package.
    /// </summary>
    public string LicenseUrl { get; set; }

    /// <summary>
    /// Gets or sets the URL for the location of the project webpage of the NuGet package.
    /// </summary>
    public string ProjectUrl { get; set; }

    /// <summary>
    /// Gets or sets the URL for the location of the icon of the NuGet package.
    /// </summary>
    public string IconUrl { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the license of the NuGet package needs to be accepted in order to use it.
    /// </summary>
    public bool RequireLicenseAcceptance { get; set; }

    /// <summary>
    /// Gets or sets the NuGet packages that this NuGet package depends on.
    /// </summary>
    public List<NugetFrameworkGroup> Dependencies { get; set; }

    /// <summary>
    /// Gets or sets the release notes of the NuGet package.
    /// </summary>
    public string ReleaseNotes { get; set; }

    /// <summary>
    /// Gets or sets the copyright of the NuGet package.
    /// </summary>
    public string Copyright { get; set; }

    /// <summary>
    /// Gets or sets the tags of the NuGet package.
    /// </summary>
    public string Tags { get; set; }

    /// <summary>
    /// Gets or sets the url for the location of the package's source code.
    /// </summary>
    public string RepositoryUrl;

    /// <summary>
    /// Gets or sets the type of source control software that the package's source code resides in.
    /// </summary>
    public string RepositoryType;

    /// <summary>
    /// Gets or sets the source control branch the package is from.
    /// </summary>
    public string RepositoryBranch;

    /// <summary>
    /// Gets or sets the source control commit the package is from.
    /// </summary>
    public string RepositoryCommit;

    /// <summary>
    /// Gets or sets the list of content files listed in the .nuspec file.
    /// </summary>
    public List<NuspecContentFile> Files { get; set; }

    /// <summary>
    /// Loads the .nuspec file inside the .nupkg file at the given filepath.
    /// </summary>
    /// <param name="nupkgFilepath">The filepath to the .nupkg file to load.</param>
    /// <returns>The .nuspec file loaded from inside the .nupkg file.</returns>
    public static NuspecFile FromNupkgFile(string nupkgFilepath) {
      var nuspec = new NuspecFile();

      if (File.Exists(path : nupkgFilepath)) {
        // get the .nuspec file from inside the .nupkg
        using (var zip = ZipFile.Read(fileName : nupkgFilepath)) {
          //var entry = zip[string.Format("{0}.nuspec", packageId)];
          var entry = zip.First(x => x.FileName.EndsWith(".nuspec"));

          using (var stream = new MemoryStream()) {
            entry.Extract(stream : stream);
            stream.Position = 0;

            nuspec = Load(stream : stream);
          }
        }
      } else {
        UnityEngine.Debug.LogErrorFormat("Package could not be read: {0}", nupkgFilepath);

        //nuspec.Id = packageId;
        //nuspec.Version = packageVersion;
        nuspec.Description = string.Format("COULD NOT LOAD {0}", arg0 : nupkgFilepath);
      }

      return nuspec;
    }

    /// <summary>
    /// Loads a .nuspec file at the given filepath.
    /// </summary>
    /// <param name="filePath">The full filepath to the .nuspec file to load.</param>
    /// <returns>The newly loaded <see cref="NuspecFile"/>.</returns>
    public static NuspecFile Load(string filePath) {
      return Load(nuspecDocument : XDocument.Load(uri : filePath));
    }

    /// <summary>
    /// Loads a .nuspec file inside the given stream.
    /// </summary>
    /// <param name="stream">The stream containing the .nuspec file to load.</param>
    /// <returns>The newly loaded <see cref="NuspecFile"/>.</returns>
    public static NuspecFile Load(Stream stream) {
      XmlReader reader = new XmlTextReader(input : stream);
      var document = XDocument.Load(reader : reader);
      return Load(nuspecDocument : document);
    }

    /// <summary>
    /// Loads a .nuspec file inside the given <see cref="XDocument"/>.
    /// </summary>
    /// <param name="nuspecDocument">The .nuspec file as an <see cref="XDocument"/>.</param>
    /// <returns>The newly loaded <see cref="NuspecFile"/>.</returns>
    public static NuspecFile Load(XDocument nuspecDocument) {
      var nuspec = new NuspecFile();

      var nuspecNamespace = nuspecDocument.Root.GetDefaultNamespace().ToString();

      var package = nuspecDocument.Element(name : XName.Get("package", namespaceName : nuspecNamespace));
      var metadata = package.Element(name : XName.Get("metadata", namespaceName : nuspecNamespace));

      nuspec.Id = (string)metadata.Element(name : XName.Get("id", namespaceName : nuspecNamespace))
                  ?? string.Empty;
      nuspec.Version = (string)metadata.Element(name : XName.Get("version", namespaceName : nuspecNamespace))
                       ?? string.Empty;
      nuspec.Title = (string)metadata.Element(name : XName.Get("title", namespaceName : nuspecNamespace))
                     ?? string.Empty;
      nuspec.Authors = (string)metadata.Element(name : XName.Get("authors", namespaceName : nuspecNamespace))
                       ?? string.Empty;
      nuspec.Owners = (string)metadata.Element(name : XName.Get("owners", namespaceName : nuspecNamespace))
                      ?? string.Empty;
      nuspec.LicenseUrl =
          (string)metadata.Element(name : XName.Get("licenseUrl", namespaceName : nuspecNamespace))
          ?? string.Empty;
      nuspec.ProjectUrl =
          (string)metadata.Element(name : XName.Get("projectUrl", namespaceName : nuspecNamespace))
          ?? string.Empty;
      nuspec.IconUrl = (string)metadata.Element(name : XName.Get("iconUrl", namespaceName : nuspecNamespace))
                       ?? string.Empty;
      nuspec.RequireLicenseAcceptance =
          bool.Parse(value : (string)metadata.Element(name : XName.Get("requireLicenseAcceptance",
                                                                       namespaceName : nuspecNamespace))
                             ?? "False");
      nuspec.Description =
          (string)metadata.Element(name : XName.Get("description", namespaceName : nuspecNamespace))
          ?? string.Empty;
      nuspec.Summary = (string)metadata.Element(name : XName.Get("summary", namespaceName : nuspecNamespace))
                       ?? string.Empty;
      nuspec.ReleaseNotes =
          (string)metadata.Element(name : XName.Get("releaseNotes", namespaceName : nuspecNamespace))
          ?? string.Empty;
      nuspec.Copyright =
          (string)metadata.Element(name : XName.Get("copyright", namespaceName : nuspecNamespace));
      nuspec.Tags = (string)metadata.Element(name : XName.Get("tags", namespaceName : nuspecNamespace))
                    ?? string.Empty;

      var repositoryElement =
          metadata.Element(name : XName.Get("repository", namespaceName : nuspecNamespace));

      if (repositoryElement != null) {
        nuspec.RepositoryType = (string)repositoryElement.Attribute(name : XName.Get("type")) ?? string.Empty;
        nuspec.RepositoryUrl = (string)repositoryElement.Attribute(name : XName.Get("url")) ?? string.Empty;
        nuspec.RepositoryBranch =
            (string)repositoryElement.Attribute(name : XName.Get("branch")) ?? string.Empty;
        nuspec.RepositoryCommit =
            (string)repositoryElement.Attribute(name : XName.Get("commit")) ?? string.Empty;
      }

      var dependenciesElement =
          metadata.Element(name : XName.Get("dependencies", namespaceName : nuspecNamespace));
      if (dependenciesElement != null) {
        // Dependencies specified for specific target frameworks
        foreach (var frameworkGroup in dependenciesElement.Elements(name : XName.Get("group",
                                                                      namespaceName : nuspecNamespace))) {
          var group = new NugetFrameworkGroup();
          group.TargetFramework =
              ConvertFromNupkgTargetFrameworkName(targetFramework :
                                                  (string)frameworkGroup.Attribute("targetFramework")
                                                  ?? string.Empty);

          foreach (var dependencyElement in frameworkGroup.Elements(name : XName.Get("dependency",
                                                                      namespaceName : nuspecNamespace))) {
            var dependency = new NugetPackageIdentifier();

            dependency._Id = (string)dependencyElement.Attribute("id") ?? string.Empty;
            dependency._Version = (string)dependencyElement.Attribute("version") ?? string.Empty;
            group.Dependencies.Add(item : dependency);
          }

          nuspec.Dependencies.Add(item : group);
        }

        // Flat dependency list
        if (nuspec.Dependencies.Count == 0) {
          var group = new NugetFrameworkGroup();
          foreach (var dependencyElement in dependenciesElement.Elements(name : XName.Get("dependency",
                                                                           namespaceName :
                                                                           nuspecNamespace))) {
            var dependency = new NugetPackageIdentifier();
            dependency._Id = (string)dependencyElement.Attribute("id") ?? string.Empty;
            dependency._Version = (string)dependencyElement.Attribute("version") ?? string.Empty;
            group.Dependencies.Add(item : dependency);
          }

          nuspec.Dependencies.Add(item : group);
        }
      }

      var filesElement = package.Element(name : XName.Get("files", namespaceName : nuspecNamespace));
      if (filesElement != null) {
        //UnityEngine.Debug.Log("Loading files!");
        foreach (var fileElement in filesElement.Elements(name : XName.Get("file",
                                                            namespaceName : nuspecNamespace))) {
          var file = new NuspecContentFile();
          file.Source = (string)fileElement.Attribute("src") ?? string.Empty;
          file.Target = (string)fileElement.Attribute("target") ?? string.Empty;
          nuspec.Files.Add(item : file);
        }
      }

      return nuspec;
    }

    /// <summary>
    /// Saves a <see cref="NuspecFile"/> to the given filepath, automatically overwriting.
    /// </summary>
    /// <param name="filePath">The full filepath to the .nuspec file to save.</param>
    public void Save(string filePath) {
      // TODO: Set a namespace when saving

      var file = new XDocument();
      var packageElement = new XElement("package");
      file.Add(content : packageElement);
      var metadata = new XElement("metadata");

      // required
      metadata.Add(content : new XElement("id", content : this.Id));
      metadata.Add(content : new XElement("version", content : this.Version));
      metadata.Add(content : new XElement("description", content : this.Description));
      metadata.Add(content : new XElement("authors", content : this.Authors));

      if (!string.IsNullOrEmpty(value : this.Title)) {
        metadata.Add(content : new XElement("title", content : this.Title));
      }

      if (!string.IsNullOrEmpty(value : this.Owners)) {
        metadata.Add(content : new XElement("owners", content : this.Owners));
      }

      if (!string.IsNullOrEmpty(value : this.LicenseUrl)) {
        metadata.Add(content : new XElement("licenseUrl", content : this.LicenseUrl));
      }

      if (!string.IsNullOrEmpty(value : this.ProjectUrl)) {
        metadata.Add(content : new XElement("projectUrl", content : this.ProjectUrl));
      }

      if (!string.IsNullOrEmpty(value : this.IconUrl)) {
        metadata.Add(content : new XElement("iconUrl", content : this.IconUrl));
      }

      if (this.RequireLicenseAcceptance) {
        metadata.Add(content : new XElement("requireLicenseAcceptance",
                                            content : this.RequireLicenseAcceptance));
      }

      if (!string.IsNullOrEmpty(value : this.ReleaseNotes)) {
        metadata.Add(content : new XElement("releaseNotes", content : this.ReleaseNotes));
      }

      if (!string.IsNullOrEmpty(value : this.Copyright)) {
        metadata.Add(content : new XElement("copyright", content : this.Copyright));
      }

      if (!string.IsNullOrEmpty(value : this.Tags)) {
        metadata.Add(content : new XElement("tags", content : this.Tags));
      }

      if (this.Dependencies.Count > 0) {
        //UnityEngine.Debug.Log("Saving dependencies!");
        var dependenciesElement = new XElement("dependencies");
        foreach (var frameworkGroup in this.Dependencies) {
          var group = new XElement("group");
          if (!string.IsNullOrEmpty(value : frameworkGroup.TargetFramework)) {
            group.Add(content : new XAttribute("targetFramework", value : frameworkGroup.TargetFramework));
          }

          foreach (var dependency in frameworkGroup.Dependencies) {
            var dependencyElement = new XElement("dependency");
            dependencyElement.Add(content : new XAttribute("id", value : dependency._Id));
            dependencyElement.Add(content : new XAttribute("version", value : dependency._Version));
            dependenciesElement.Add(content : dependencyElement);
          }
        }

        metadata.Add(content : dependenciesElement);
      }

      file.Root.Add(content : metadata);

      if (this.Files.Count > 0) {
        //UnityEngine.Debug.Log("Saving files!");
        var filesElement = new XElement("files");
        foreach (var contentFile in this.Files) {
          var fileElement = new XElement("file");
          fileElement.Add(content : new XAttribute("src", value : contentFile.Source));
          fileElement.Add(content : new XAttribute("target", value : contentFile.Target));
          filesElement.Add(content : fileElement);
        }

        packageElement.Add(content : filesElement);
      }

      // remove the read only flag on the file, if there is one.
      if (File.Exists(path : filePath)) {
        var attributes = File.GetAttributes(path : filePath);

        if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly) {
          attributes &= ~FileAttributes.ReadOnly;
          File.SetAttributes(path : filePath, fileAttributes : attributes);
        }
      }

      file.Save(fileName : filePath);
    }

    static string ConvertFromNupkgTargetFrameworkName(string targetFramework) {
      var convertedTargetFramework = targetFramework.ToLower().Replace(".netstandard", "netstandard")
                                                    .Replace("native0.0", "native");

      convertedTargetFramework = convertedTargetFramework.StartsWith(".netframework")
                                     ? convertedTargetFramework.Replace(".netframework", "net")
                                                               .Replace(".", "")
                                     : convertedTargetFramework;

      return convertedTargetFramework;
    }
  }
}