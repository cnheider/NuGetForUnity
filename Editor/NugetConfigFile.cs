namespace NuGetForUnity.Editor {
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using System.Text;
  using System.Xml.Linq;
  using UnityEditor;

  /// <summary>
  /// Represents a NuGet.config file that stores the NuGet settings.
  /// See here: https://docs.nuget.org/consume/nuget-config-file
  /// </summary>
  public class NugetConfigFile {
    /// <summary>
    /// Gets the list of package sources that are defined in the NuGet.config file.
    /// </summary>
    public List<NugetPackageSource> PackageSources { get; private set; }

    /// <summary>
    /// Gets the currectly active package source that is defined in the NuGet.config file.
    /// Note: If the key/Name is set to "All" and the value/Path is set to "(Aggregate source)", all package sources are used.
    /// </summary>
    public NugetPackageSource ActivePackageSource { get; private set; }

    /// <summary>
    /// Gets the local path where packages are to be installed.  It can be a full path or a relative path.
    /// </summary>
    public string RepositoryPath { get; private set; }

    /// <summary>
    /// Gets the default package source to push NuGet packages to.
    /// </summary>
    public string DefaultPushSource { get; private set; }

    /// <summary>
    /// True to output verbose log messages to the console.  False to output the normal level of messages.
    /// </summary>
    public bool Verbose { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a package is installed from the cache (if present), or if it always downloads the package from the server.
    /// </summary>
    public bool InstallFromCache { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether installed package files are set to read-only.
    /// </summary>
    public bool ReadOnlyPackageFiles { get; set; }

    /// <summary>
    /// The incomplete path that is saved.  The path is expanded and made public via the property above.
    /// </summary>
    string _saved_repository_path;

    /// <summary>
    /// Saves this NuGet.config file to disk.
    /// </summary>
    /// <param name="filepath">The filepath to where this NuGet.config will be saved.</param>
    public void Save(string filepath) {
      var config_file = new XDocument();

      var package_sources = new XElement("packageSources");
      var disabled_package_sources = new XElement("disabledPackageSources");
      var package_source_credentials = new XElement("packageSourceCredentials");

      XElement add_element;

      // save all enabled and disabled package sources 
      foreach (var source in this.PackageSources) {
        add_element = new XElement("add");
        add_element.Add(content : new XAttribute("key", value : source.Name));
        add_element.Add(content : new XAttribute("value", value : source.SavedPath));
        package_sources.Add(content : add_element);

        if (!source.IsEnabled) {
          add_element = new XElement("add");
          add_element.Add(content : new XAttribute("key", value : source.Name));
          add_element.Add(content : new XAttribute("value", "true"));
          disabled_package_sources.Add(content : add_element);
        }

        if (source.HasPassword) {
          var source_element = new XElement(name : source.Name);
          package_source_credentials.Add(content : source_element);

          add_element = new XElement("add");
          add_element.Add(content : new XAttribute("key", "userName"));
          add_element.Add(content : new XAttribute("value", value : source.UserName ?? string.Empty));
          source_element.Add(content : add_element);

          add_element = new XElement("add");
          add_element.Add(content : new XAttribute("key", "clearTextPassword"));
          add_element.Add(content : new XAttribute("value", value : source.SavedPassword));
          source_element.Add(content : add_element);
        }
      }

      // save the active package source (may be an aggregate)
      var active_package_source = new XElement("activePackageSource");
      add_element = new XElement("add");
      add_element.Add(content : new XAttribute("key", "All"));
      add_element.Add(content : new XAttribute("value", "(Aggregate source)"));
      active_package_source.Add(content : add_element);

      var config = new XElement("config");

      // save the un-expanded respository path
      add_element = new XElement("add");
      add_element.Add(content : new XAttribute("key", "repositoryPath"));
      add_element.Add(content : new XAttribute("value", value : this._saved_repository_path));
      config.Add(content : add_element);

      // save the default push source
      if (this.DefaultPushSource != null) {
        add_element = new XElement("add");
        add_element.Add(content : new XAttribute("key", "DefaultPushSource"));
        add_element.Add(content : new XAttribute("value", value : this.DefaultPushSource));
        config.Add(content : add_element);
      }

      if (this.Verbose) {
        add_element = new XElement("add");
        add_element.Add(content : new XAttribute("key", "verbose"));
        add_element.Add(content : new XAttribute("value", value : this.Verbose.ToString().ToLower()));
        config.Add(content : add_element);
      }

      if (!this.InstallFromCache) {
        add_element = new XElement("add");
        add_element.Add(content : new XAttribute("key", "InstallFromCache"));
        add_element.Add(content : new XAttribute("value",
                                                 value : this.InstallFromCache.ToString().ToLower()));
        config.Add(content : add_element);
      }

      if (!this.ReadOnlyPackageFiles) {
        add_element = new XElement("add");
        add_element.Add(content : new XAttribute("key", "ReadOnlyPackageFiles"));
        add_element.Add(content : new XAttribute("value",
                                                 value : this.ReadOnlyPackageFiles.ToString().ToLower()));
        config.Add(content : add_element);
      }

      var configuration = new XElement("configuration");
      configuration.Add(content : package_sources);
      configuration.Add(content : disabled_package_sources);
      configuration.Add(content : package_source_credentials);
      configuration.Add(content : active_package_source);
      configuration.Add(content : config);

      config_file.Add(content : configuration);

      var file_exists = File.Exists(path : filepath);
      // remove the read only flag on the file, if there is one.
      if (file_exists) {
        var attributes = File.GetAttributes(path : filepath);

        if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly) {
          attributes &= ~FileAttributes.ReadOnly;
          File.SetAttributes(path : filepath, fileAttributes : attributes);
        }
      }

      config_file.Save(fileName : filepath);

      NugetHelper.DisableWsapExportSetting(file_path : filepath, notify_of_update : file_exists);
    }

    /// <summary>
    /// Loads a NuGet.config file at the given filepath.
    /// </summary>
    /// <param name="file_path">The full filepath to the NuGet.config file to load.</param>
    /// <returns>The newly loaded <see cref="NugetConfigFile"/>.</returns>
    public static NugetConfigFile Load(string file_path) {
      var config_file = new NugetConfigFile {
                                                PackageSources = new List<NugetPackageSource>(),
                                                InstallFromCache = true,
                                                ReadOnlyPackageFiles = false
                                            };

      var file = XDocument.Load(uri : file_path);

      // Force disable
      NugetHelper.DisableWsapExportSetting(file_path : file_path, false);

      // read the full list of package sources (some may be disabled below)
      var package_sources = file.Root?.Element("packageSources");
      if (package_sources != null) {
        var adds = package_sources.Elements("add");
        foreach (var add in adds) {
          config_file.PackageSources.Add(item : new NugetPackageSource(name : add.Attribute("key").Value,
                                                                       path : add.Attribute("value").Value));
        }
      }

      // read the active package source (may be an aggregate of all enabled sources!)
      var active_package_source = file.Root?.Element("activePackageSource");
      if (active_package_source != null) {
        var add = active_package_source.Element("add");
        config_file.ActivePackageSource =
            new NugetPackageSource(name : add?.Attribute("key").Value, path : add.Attribute("value").Value);
      }

      // disable all listed disabled package sources
      var disabled_package_sources = file.Root?.Element("disabledPackageSources");
      if (disabled_package_sources != null) {
        var adds = disabled_package_sources.Elements("add");
        foreach (var add in adds) {
          var name = add.Attribute("key")?.Value;
          var disabled = add.Attribute("value")?.Value;
          if (string.Equals(a : disabled, "true", comparisonType : StringComparison.OrdinalIgnoreCase)) {
            var source = config_file.PackageSources.FirstOrDefault(p => p.Name == name);
            if (source != null) {
              source.IsEnabled = false;
            }
          }
        }
      }

      // set all listed passwords for package source credentials
      var package_source_credentials = file.Root?.Element("packageSourceCredentials");
      if (package_source_credentials != null) {
        foreach (var source_element in package_source_credentials.Elements()) {
          var name = source_element.Name.LocalName;
          var source = config_file.PackageSources.FirstOrDefault(p => p.Name == name);
          if (source != null) {
            var adds = source_element.Elements("add");
            foreach (var add in adds) {
              if (string.Equals(a : add.Attribute("key")?.Value,
                                "userName",
                                comparisonType : StringComparison.OrdinalIgnoreCase)) {
                var user_name = add.Attribute("value")?.Value;
                source.UserName = user_name;
              }

              if (string.Equals(a : add.Attribute("key")?.Value,
                                "clearTextPassword",
                                comparisonType : StringComparison.OrdinalIgnoreCase)) {
                var password = add.Attribute("value")?.Value;
                source.SavedPassword = password;
              }
            }
          }
        }
      }

      // read the configuration data
      var config = file.Root?.Element("config");
      if (config != null) {
        var adds = config.Elements("add");
        foreach (var add in adds) {
          var key = add.Attribute("key").Value;
          var value = add.Attribute("value").Value;

          if (string.Equals(a : key, "repositoryPath", comparisonType : StringComparison.OrdinalIgnoreCase)) {
            config_file._saved_repository_path = value;
            config_file.RepositoryPath = Environment.ExpandEnvironmentVariables(name : value);

            if (!Path.IsPathRooted(path : config_file.RepositoryPath)) {
              var repository_path = Path.Combine(path1 : UnityEngine.Application.dataPath,
                                                 path2 : NugetPreferences._Base_Path,
                                                 path3 : config_file.RepositoryPath);
              repository_path = Path.GetFullPath(path : repository_path);

              config_file.RepositoryPath = repository_path;
            }
          } else if (string.Equals(a : key,
                                   "DefaultPushSource",
                                   comparisonType : StringComparison.OrdinalIgnoreCase)) {
            config_file.DefaultPushSource = value;
          } else if (string.Equals(a : key, "verbose", comparisonType : StringComparison.OrdinalIgnoreCase)) {
            config_file.Verbose = bool.Parse(value : value);
          } else if (string.Equals(a : key,
                                   "InstallFromCache",
                                   comparisonType : StringComparison.OrdinalIgnoreCase)) {
            config_file.InstallFromCache = bool.Parse(value : value);
          } else if (string.Equals(a : key,
                                   "ReadOnlyPackageFiles",
                                   comparisonType : StringComparison.OrdinalIgnoreCase)) {
            config_file.ReadOnlyPackageFiles = bool.Parse(value : value);
          }
        }
      }

      return config_file;
    }

    /// <summary>
    /// Creates a NuGet.config file with the default settings at the given full filepath.
    /// </summary>
    /// <param name="file_path">The full filepath where to create the NuGet.config file.</param>
    /// <returns>The loaded <see cref="NugetConfigFile"/> loaded off of the newly created default file.</returns>
    public static NugetConfigFile CreateDefaultFile(string file_path) {
      const string contents = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
       <add key=""NuGet"" value=""http://www.nuget.org/api/v2/"" />
    </packageSources>
    <disabledPackageSources />
    <activePackageSource>
       <add key=""All"" value=""(Aggregate source)"" />
    </activePackageSource>
    <config>
       <add key=""repositoryPath"" value=""Libraries"" />
       <add key=""DefaultPushSource"" value=""http://www.nuget.org/api/v2/"" />
    </config>
</configuration>";

      File.WriteAllText(path : file_path, contents : contents, encoding : new UTF8Encoding());

      AssetDatabase.Refresh();

      NugetHelper.DisableWsapExportSetting(file_path : file_path, false);

      return Load(file_path : file_path);
    }
  }
}