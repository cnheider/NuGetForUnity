#pragma warning disable 649 // never assigned
#pragma warning disable 618 // UnityEngine.WWW
#define VERBOSE_NOT

namespace NuGetForUnity.Editor {
  using System;
  using System.Collections.Generic;
  using System.Diagnostics;
  using System.IO;
  using System.Linq;
  using System.Net;
  using System.Security.Cryptography;
  using System.Text;
  using System.Text.RegularExpressions;
  using Ionic.Zip;
  using UnityEditor;
  using UnityEngine;
  using Debug = UnityEngine.Debug;

  /// <summary>
  /// A set of helper methods that act as a wrapper around nuget.exe
  /// 
  /// TIP: It's incredibly useful to associate .nupkg files as compressed folder in Windows (View like .zip files).  To do this:
  ///      1) Open a command prompt as admin (Press Windows key. Type "cmd".  Right click on the icon and choose "Run as Administrator"
  ///      2) Enter this command: cmd /c assoc .nupkg=CompressedFolder
  /// </summary>
  [InitializeOnLoad]
  public static class NugetHelper {
    static bool _inside_initialize_on_load = false;

    /// <summary>
    /// The path to the nuget.config file.
    /// </summary>
    const string _nuget_config_file_path = "nuget.config";

    /// <summary>
    /// The path to the packages.config file.
    /// </summary>
    const string _packages_config_file_path = "packages.config";

    /// <summary>
    /// The path where to put created (packed) and downloaded (not installed yet) .nupkg files.
    /// </summary>
//public static readonly string PackOutputDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Path.Combine("NuGet", "Cache"));
    const string _pack_output_directory = "Libraries";

    const string _tools_packages_folder = "Libraries";

    /// <summary>
    /// The amount of time, in milliseconds, before the nuget.exe process times out and is killed.
    /// </summary>
    const int _time_out = 60000;

    static NugetConfigFile _nuget_config_file;
    
    /// <summary>
    /// The loaded NuGet.config file that holds the settings for NuGet.
    /// </summary>
    public static NugetConfigFile NugetConfigFile {
      get { return _nuget_config_file; }
      private set { _nuget_config_file = value; }
    }

    /// <summary>
    /// Backing field for the packages.config file.
    /// </summary>
    static PackagesConfigFile _packages_config_file;

    /// <summary>
    /// Gets the loaded packages.config file that hold the dependencies for the project.
    /// </summary>
    public static PackagesConfigFile PackagesConfigFile {
      get {
        if (_packages_config_file == null) {
          _packages_config_file = PackagesConfigFile.Load(filepath : PackagesConfigFilePath);
        }

        return _packages_config_file;
      }
    }

    /// <summary>
    /// The list of <see cref="NugetPackageSource"/>s to use.
    /// </summary>
    static List<NugetPackageSource> _package_sources = new List<NugetPackageSource>();

    /// <summary>
    /// The dictionary of currently installed <see cref="NugetPackage"/>s keyed off of their ID string.
    /// </summary>
    static Dictionary<string, NugetPackage> _installed_packages = new Dictionary<string, NugetPackage>();

    /// <summary>
    /// The dictionary of cached credentials retrieved by credential providers, keyed by feed URI.
    /// </summary>
    static Dictionary<Uri, CredentialProviderResponse?> _cached_credentials_by_feed_uri =
        new Dictionary<Uri, CredentialProviderResponse?>();

    /// <summary>
    /// The current .NET version being used (2.0 [actually 3.5], 4.6, etc).
    /// </summary>
    static ApiCompatibilityLevel DotNetVersion {
      get {
        #if UNITY_5_6_OR_NEWER
        return PlayerSettings.GetApiCompatibilityLevel(buildTargetGroup : EditorUserBuildSettings
                                                           .selectedBuildTargetGroup);
        #else
                return PlayerSettings.apiCompatibilityLevel;
        #endif
      }
    }

    /// <summary>
    /// Static constructor used by Unity to initialize NuGet and restore packages defined in packages.config.
    /// </summary>
    static NugetHelper() {
      _inside_initialize_on_load = true;
      try {
        if (EditorApplication.isPlayingOrWillChangePlaymode) {
          // if we are entering playmode, don't do anything
          return;
        }

        LoadNugetConfigFile(); // Load the NuGet.config file

        if (!Directory.Exists(path : PackOutputDirectory)) {
          // create the nupkgs directory, if it doesn't exist
          Directory.CreateDirectory(path : PackOutputDirectory);
        }

        Restore(); // restore packages - this will be called EVERY time the project is loaded or a code-file changes
      } finally {
        _inside_initialize_on_load = false;
      }
    }

    /// <summary>
    /// Loads the NuGet.config file.
    /// </summary>
    public static void LoadNugetConfigFile() {
      if (File.Exists(path : NugetConfigFilePath)) {
        NugetConfigFile = NugetConfigFile.Load(file_path : NugetConfigFilePath);
      } else {
        Debug.LogFormat("No nuget.config file found. Creating default at {0}", NugetConfigFilePath);

        NugetConfigFile = NugetConfigFile.CreateDefaultFile(file_path : NugetConfigFilePath);
        AssetDatabase.Refresh();
      }

      // parse any command line arguments
      _package_sources.Clear();
      var reading_sources = false;
      var use_command_line_sources = false;
      foreach (var arg in Environment.GetCommandLineArgs()) {
        if (reading_sources) {
          if (arg.StartsWith("-")) {
            reading_sources = false;
          } else {
            var source = new NugetPackageSource(name : "CMD_LINE_SRC_" + _package_sources.Count, path : arg);
            LogVerbose("Adding command line package source {0} at {1}",
                       "CMD_LINE_SRC_" + _package_sources.Count,
                       arg);
            _package_sources.Add(item : source);
          }
        }

        if (arg == "-Source") {
          // if the source is being forced, don't install packages from the cache
          NugetConfigFile.InstallFromCache = false;
          reading_sources = true;
          use_command_line_sources = true;
        }
      }


      if (!use_command_line_sources) {       // if there are not command line overrides, use the NuGet.config package sources
        if (NugetConfigFile.ActivePackageSource.ExpandedPath == "(Aggregate source)") {
          _package_sources.AddRange(collection : NugetConfigFile.PackageSources);
        } else {
          _package_sources.Add(item : NugetConfigFile.ActivePackageSource);
        }
      }
    }

    /// <summary>
    /// Runs nuget.exe using the given arguments.
    /// </summary>
    /// <param name="arguments">The arguments to run nuget.exe with.</param>
    /// <param name="verbose">True to output debug information to the Unity console.  Defaults to true.</param>
    /// <returns>The string of text that was output from nuget.exe following its execution.</returns>
    static void RunNugetProcess(string arguments, bool verbose = true) {
      // Try to find any nuget.exe in the package tools installation location

      // create the folder to prevent an exception when getting the files
      Directory.CreateDirectory(path : ToolsPackagesFolder);

      var files = Directory.GetFiles(path : ToolsPackagesFolder,
                                     "NuGet.exe",
                                     searchOption : SearchOption.AllDirectories);
      if (files.Length > 1) {
        Debug.LogWarningFormat("More than one nuget.exe found. Using first one.");
      } else if (files.Length < 1) {
        Debug.LogWarningFormat("No nuget.exe found! Attempting to install the NuGet.CommandLine package.");
        InstallIdentifier(package : new NugetPackageIdentifier("NuGet.CommandLine", "2.8.6"));
        files = Directory.GetFiles(path : ToolsPackagesFolder,
                                   "NuGet.exe",
                                   searchOption : SearchOption.AllDirectories);
        if (files.Length < 1) {
          Debug.LogErrorFormat("nuget.exe still not found. Quiting...");
          return;
        }
      }

      LogVerbose("Running: {0} \nArgs: {1}", files[0], arguments);

      var file_name = string.Empty;
      var command_line = string.Empty;

      #if UNITY_EDITOR_OSX
            // ATTENTION: you must install mono running on your mac, we use this mono to run `nuget.exe`
            fileName = "/Library/Frameworks/Mono.framework/Versions/Current/Commands/mono";
            commandLine = " " + files[0] + " " + arguments;
            LogVerbose("command: " + commandLine);
      #else
      file_name = files[0];
      command_line = arguments;
      #endif
      var process =
          Process.Start(startInfo : new ProcessStartInfo(fileName : file_name, arguments : command_line) {
                                        RedirectStandardError = true,
                                        RedirectStandardOutput = true,
                                        UseShellExecute = false,
                                        CreateNoWindow = true,
                                        // WorkingDirectory = Path.GettargetFramework(files[0]),

                                        // http://stackoverflow.com/questions/16803748/how-to-decode-cmd-output-correctly
                                        // Default = 65533, ASCII = ?, Unicode = nothing works at all, UTF-8 = 65533, UTF-7 = 242 = WORKS!, UTF-32 = nothing works at all
                                        StandardOutputEncoding = Encoding.GetEncoding(850)
                                    });

      if (process != null) {
        if (!process.WaitForExit(milliseconds : _time_out)) {
          Debug.LogWarning("NuGet took too long to finish.  Killing operation.");
          process.Kill();
        }

        var error = process.StandardError.ReadToEnd();
        if (!string.IsNullOrEmpty(value : error)) {
          Debug.LogError(message : error);
        }

        var output = process.StandardOutput.ReadToEnd();
        if (verbose && !string.IsNullOrEmpty(value : output)) {
          Debug.Log(message : output);
        }
      } else {
        Debug.LogError(message : "Process did not start");
      }
    }

    /// <summary>
    /// Replace all %20 encodings with a normal space.
    /// </summary>
    /// <param name="directory_path">The path to the directory.</param>
    static void FixSpaces(string directory_path) {
      if (directory_path.Contains("%20")) {
        LogVerbose("Removing %20 from {0}", directory_path);
        Directory.Move(sourceDirName : directory_path, destDirName : directory_path.Replace("%20", " "));
        directory_path = directory_path.Replace("%20", " ");
      }

      var subdirectories = Directory.GetDirectories(path : directory_path);
      foreach (var sub_dir in subdirectories) {
        FixSpaces(directory_path : sub_dir);
      }

      var files = Directory.GetFiles(path : directory_path);
      foreach (var file in files) {
        if (file.Contains("%20")) {
          LogVerbose("Removing %20 from {0}", file);
          File.Move(sourceFileName : file, destFileName : file.Replace("%20", " "));
        }
      }
    }

    /// <summary>
    /// Checks the file importer settings and disables the export to WSA Platform setting.
    /// </summary>
    /// <param name="file_path">The path to the .config file.</param>
    /// <param name="notify_of_update">Whether or not to log a warning of the update.</param>
    public static void DisableWsapExportSetting(string file_path, bool notify_of_update) {
      var unity_version_parts = Application.unityVersion.Split('.');
      int unity_major_version;
      if (int.TryParse(s : unity_version_parts[0], result : out unity_major_version)
          && unity_major_version <= 2017) {
        return;
      }

      file_path = Path.GetFullPath(path : file_path);

      var assets_local_path =
          file_path.Replace(oldValue : Path.GetFullPath(path : Application.dataPath), "Assets");
      var importer = AssetImporter.GetAtPath(path : assets_local_path) as PluginImporter;

      if (importer == null) {
        if (!_inside_initialize_on_load) {
          Debug.LogError(message : string.Format("Couldn't get importer for '{0}'.", arg0 : file_path));
        }

        return;
      }

      if (importer.GetCompatibleWithPlatform(platform : BuildTarget.WSAPlayer)) {
        if (notify_of_update) {
          Debug.LogWarning(message : string.Format("Disabling WSA platform on asset settings for {0}",
                                                   arg0 : file_path));
        } else {
          LogVerbose("Disabling WSA platform on asset settings for {0}", file_path);
        }

        importer.SetCompatibleWithPlatform(platform : BuildTarget.WSAPlayer, false);
      }
    }

    /// <summary>
    /// Cleans up a package after it has been installed.
    /// Since we are in Unity, we can make certain assumptions on which files will NOT be used, so we can delete them.
    /// </summary>
    /// <param name="package">The NugetPackage to clean.</param>
    static void Clean(NugetPackageIdentifier package) {
      var package_install_directory = Path.Combine(path1 : NugetConfigFile.RepositoryPath,
                                                   path2 : string.Format("{0}.{1}",
                                                                           arg0 : package._Id,
                                                                           arg1 : package._Version));

      LogVerbose("Cleaning {0}", package_install_directory);

      FixSpaces(directory_path : package_install_directory);

      // delete a remnant .meta file that may exist from packages created by Unity
      DeleteFile(file_path : package_install_directory + "/" + package._Id + ".nuspec.meta");

      // delete directories & files that NuGet normally deletes, but since we are installing "manually" they exist
      DeleteDirectory(directory_path : package_install_directory + "/_rels");
      DeleteDirectory(directory_path : package_install_directory + "/package");
      DeleteFile(file_path : package_install_directory + "/" + package._Id + ".nuspec");
      DeleteFile(file_path : package_install_directory + "/[Content_Types].xml");

      // Unity has no use for the build directory
      DeleteDirectory(directory_path : package_install_directory + "/build");

      // For now, delete src.  We may use it later...
      DeleteDirectory(directory_path : package_install_directory + "/src");

      // Since we don't automatically fix up the runtime dll platforms, remove them until we improve support
      // for this newer feature of nuget packages.
      DeleteDirectory(directory_path : Path.Combine(path1 : package_install_directory, "runtimes"));

      // Delete documentation folders since they sometimes have HTML docs with JavaScript, which Unity tried to parse as "UnityScript"
      DeleteDirectory(directory_path : package_install_directory + "/docs");

      // Delete ref folder, as it is just used for compile-time reference and does not contain implementations.
      // Leaving it results in "assembly loading" and "multiple pre-compiled assemblies with same name" errors
      DeleteDirectory(directory_path : package_install_directory + "/ref");

      if (Directory.Exists(path : package_install_directory + "/lib")) {
        var selected_directories = new List<string>();

        // go through the library folders in descending order (highest to lowest version)
        var lib_directories = Directory.GetDirectories(path : package_install_directory + "/lib")
                                       .Select(s => new DirectoryInfo(path : s));
        var target_frameworks = lib_directories.Select(x => x.Name.ToLower());

        var best_target_framework =
            TryGetBestTargetFrameworkForCurrentSettings(target_frameworks : target_frameworks);
        if (best_target_framework != null) {
          var best_lib_directory = lib_directories.First(x => x.Name.ToLower() == best_target_framework);

          if (best_target_framework == "unity"
              || best_target_framework == "net35-unity full v3.5"
              || best_target_framework == "net35-unity subset v3.5") {
            selected_directories.Add(item : Path.Combine(path1 : best_lib_directory.Parent.FullName,
                                                         "unity"));
            selected_directories.Add(item : Path.Combine(path1 : best_lib_directory.Parent.FullName,
                                                         "net35-unity full v3.5"));
            selected_directories.Add(item : Path.Combine(path1 : best_lib_directory.Parent.FullName,
                                                         "net35-unity subset v3.5"));
          } else {
            selected_directories.Add(item : best_lib_directory.FullName);
          }
        }

        foreach (var directory in selected_directories) {
          LogVerbose($"Using {0}", directory);
        }

        // delete all of the libraries except for the selected one
        foreach (var directory in lib_directories) {
          var valid_directory = selected_directories
                                .Where(d => string.Compare(strA : d,
                                                           strB : directory.FullName,
                                                           ignoreCase : true)
                                            == 0).Any();

          if (!valid_directory) {
            DeleteDirectory(directory_path : directory.FullName);
          }
        }
      }

      if (Directory.Exists(path : package_install_directory + "/tools")) {
        // Move the tools folder outside of the Unity Assets folder
        var tools_install_directory = Path.Combine(path1 : Application.dataPath,
                                                   path2 : string.Format("../Packages/{0}.{1}/tools",
                                                                           arg0 : package._Id,
                                                                           arg1 : package._Version));

        LogVerbose("Moving {0} to {1}", package_install_directory + "/tools", tools_install_directory);

        // create the directory to create any of the missing folders in the path
        Directory.CreateDirectory(path : tools_install_directory);

        // delete the final directory to prevent the Move operation from throwing exceptions.
        DeleteDirectory(directory_path : tools_install_directory);

        Directory.Move(sourceDirName : package_install_directory + "/tools",
                       destDirName : tools_install_directory);
      }

      // delete all PDB files since Unity uses Mono and requires MDB files, which causes it to output "missing MDB" errors
      DeleteAllFiles(directory_path : package_install_directory, "*.pdb");

      // if there are native DLLs, copy them to the Unity project root (1 up from Assets)
      if (Directory.Exists(path : package_install_directory + "/output")) {
        var files = Directory.GetFiles(path : package_install_directory + "/output");
        foreach (var file in files) {
          var new_file_path = Directory.GetCurrentDirectory() + "/" + Path.GetFileName(path : file);
          LogVerbose("Moving {0} to {1}", file, new_file_path);
          DeleteFile(file_path : new_file_path);
          File.Move(sourceFileName : file, destFileName : new_file_path);
        }

        LogVerbose("Deleting {0}", package_install_directory + "/output");

        DeleteDirectory(directory_path : package_install_directory + "/output");
      }

      // if there are Unity plugin DLLs, copy them to the Unity Plugins folder (Assets/Plugins)
      if (Directory.Exists(path : package_install_directory + "/unityplugin")) {
        var plugins_directory = Application.dataPath + "/Plugins/";

        DirectoryCopy(source_directory_path : package_install_directory + "/unityplugin",
                      dest_directory_path : plugins_directory);

        LogVerbose("Deleting {0}", package_install_directory + "/unityplugin");

        DeleteDirectory(directory_path : package_install_directory + "/unityplugin");
      }

      // if there are Unity StreamingAssets, copy them to the Unity StreamingAssets folder (Assets/StreamingAssets)
      if (Directory.Exists(path : package_install_directory + "/StreamingAssets")) {
        var streaming_assets_directory = Application.dataPath + "/StreamingAssets/";

        if (!Directory.Exists(path : streaming_assets_directory)) {
          Directory.CreateDirectory(path : streaming_assets_directory);
        }

        // move the files
        var files = Directory.GetFiles(path : package_install_directory + "/StreamingAssets");
        foreach (var file in files) {
          var new_file_path = streaming_assets_directory + Path.GetFileName(path : file);

          try {
            LogVerbose("Moving {0} to {1}", file, new_file_path);
            DeleteFile(file_path : new_file_path);
            File.Move(sourceFileName : file, destFileName : new_file_path);
          } catch (Exception e) {
            Debug.LogWarningFormat("{0} couldn't be moved. \n{1}", new_file_path, e.ToString());
          }
        }

        // move the directories
        var directories = Directory.GetDirectories(path : package_install_directory + "/StreamingAssets");
        foreach (var directory in directories) {
          var new_directory_path = streaming_assets_directory + new DirectoryInfo(path : directory).Name;

          try {
            LogVerbose("Moving {0} to {1}", directory, new_directory_path);
            if (Directory.Exists(path : new_directory_path)) {
              DeleteDirectory(directory_path : new_directory_path);
            }

            Directory.Move(sourceDirName : directory, destDirName : new_directory_path);
          } catch (Exception e) {
            Debug.LogWarningFormat("{0} couldn't be moved. \n{1}", new_directory_path, e.ToString());
          }
        }

        // delete the package's StreamingAssets folder and .meta file
        LogVerbose("Deleting {0}", package_install_directory + "/StreamingAssets");
        DeleteDirectory(directory_path : package_install_directory + "/StreamingAssets");
        DeleteFile(file_path : package_install_directory + "/StreamingAssets.meta");
      }
    }

    public static NugetFrameworkGroup
        GetBestDependencyFrameworkGroupForCurrentSettings(NugetPackage package) {
      var target_frameworks = package.Dependencies.Select(x => x.TargetFramework);

      var best_target_framework =
          TryGetBestTargetFrameworkForCurrentSettings(target_frameworks : target_frameworks);
      return package.Dependencies.FirstOrDefault(x => x.TargetFramework == best_target_framework)
             ?? new NugetFrameworkGroup();
    }

    public static NugetFrameworkGroup GetBestDependencyFrameworkGroupForCurrentSettings(NuspecFile nuspec) {
      var target_frameworks = nuspec.Dependencies.Select(x => x.TargetFramework);

      var best_target_framework =
          TryGetBestTargetFrameworkForCurrentSettings(target_frameworks : target_frameworks);
      return nuspec.Dependencies.FirstOrDefault(x => x.TargetFramework == best_target_framework)
             ?? new NugetFrameworkGroup();
    }

    struct UnityVersion : IComparable<UnityVersion> {
      public int _Major;
      public int _Minor;
      public int _Revision;
      public char _Release;
      public int _Build;

      public static UnityVersion _Current = new UnityVersion(version : Application.unityVersion);

      public UnityVersion(string version) {
        var match = Regex.Match(input : version, @"(\d+)\.(\d+)\.(\d+)([fpba])(\d+)");
        if (!match.Success) {
          throw new ArgumentException("Invalid unity version");
        }

        this._Major = int.Parse(s : match.Groups[1].Value);
        this._Minor = int.Parse(s : match.Groups[2].Value);
        this._Revision = int.Parse(s : match.Groups[3].Value);
        this._Release = match.Groups[4].Value[0];
        this._Build = int.Parse(s : match.Groups[5].Value);
      }

      public static int Compare(UnityVersion a, UnityVersion b) {
        if (a._Major < b._Major) {
          return -1;
        }

        if (a._Major > b._Major) {
          return 1;
        }

        if (a._Minor < b._Minor) {
          return -1;
        }

        if (a._Minor > b._Minor) {
          return 1;
        }

        if (a._Revision < b._Revision) {
          return -1;
        }

        if (a._Revision > b._Revision) {
          return 1;
        }

        if (a._Release < b._Release) {
          return -1;
        }

        if (a._Release > b._Release) {
          return 1;
        }

        if (a._Build < b._Build) {
          return -1;
        }

        if (a._Build > b._Build) {
          return 1;
        }

        return 0;
      }

      public int CompareTo(UnityVersion other) { return Compare(a : this, b : other); }
    }

    struct PriorityFramework {
      public int _Priority;
      public string _Framework;
    }

    static readonly string[] _unity_frameworks = new string[] {"unity"};

    static readonly string[] _net_standard_frameworks = new string[] {
                                                                         "netstandard2.0",
                                                                         "netstandard1.6",
                                                                         "netstandard1.5",
                                                                         "netstandard1.4",
                                                                         "netstandard1.3",
                                                                         "netstandard1.2",
                                                                         "netstandard1.1",
                                                                         "netstandard1.0"
                                                                     };

    static readonly string[] _net4_unity2018_frameworks = new string[] {"net471", "net47"};

    static readonly string[] _net4_unity2017_frameworks = new string[] {
                                                                           "net462",
                                                                           "net461",
                                                                           "net46",
                                                                           "net452",
                                                                           "net451",
                                                                           "net45",
                                                                           "net403",
                                                                           "net40",
                                                                           "net4"
                                                                       };

    static readonly string[] _net3_frameworks =
        new string[] {
                         "net35-unity full v3.5",
                         "net35-unity subset v3.5",
                         "net35",
                         "net20",
                         "net11"
                     };

    static readonly string[] _default_frameworks = new string[] {string.Empty};

    public static string TryGetBestTargetFrameworkForCurrentSettings(IEnumerable<string> target_frameworks) {
      var int_dot_net_version = (int)DotNetVersion; // c
      //bool using46 = DotNetVersion == ApiCompatibilityLevel.NET_4_6; // NET_4_6 option was added in Unity 5.6
      var using46 =
          int_dot_net_version
          == 3; // NET_4_6 = 3 in Unity 5.6 and Unity 2017.1 - use the hard-coded int value to ensure it works in earlier versions of Unity
      var using_standard2 = int_dot_net_version == 6; // using .net standard 2.0

      var framework_groups = new List<string[]> {_unity_frameworks};

      if (using_standard2) {
        framework_groups.Add(item : _net_standard_frameworks);
      } else if (using46) {
        if (UnityVersion._Current._Major >= 2018) {
          framework_groups.Add(item : _net4_unity2018_frameworks);
        }

        if (UnityVersion._Current._Major >= 2017) {
          framework_groups.Add(item : _net4_unity2017_frameworks);
        }

        framework_groups.Add(item : _net3_frameworks);
        framework_groups.Add(item : _net_standard_frameworks);
      } else {
        framework_groups.Add(item : _net3_frameworks);
      }

      framework_groups.Add(item : _default_frameworks);

      Func<string, int> get_tfm_priority = (string tfm) => {
                                             for (var i = 0; i < framework_groups.Count; ++i) {
                                               var index =
                                                   Array.IndexOf(array : framework_groups[index : i],
                                                                 value : tfm);
                                               if (index >= 0) {
                                                 return i * 1000 + index;
                                               }
                                             }

                                             return int.MaxValue;
                                           };

      // Select the highest .NET library available that is supported
      // See here: https://docs.nuget.org/ndocs/schema/target-frameworks
      var result = target_frameworks
                   .Select(tfm => new PriorityFramework {
                                                            _Priority = get_tfm_priority(arg : tfm),
                                                            _Framework = tfm
                                                        }).Where(pfm => pfm._Priority != int.MaxValue)
                   .ToArray() // Ensure we don't search for priorities again when sorting
                   .OrderBy(pfm => pfm._Priority).Select(pfm => pfm._Framework).FirstOrDefault();

      LogVerbose("Selecting {0} as the best target framework for current settings", result ?? "(null)");
      return result;
    }

    /// <summary>
    /// Calls "nuget.exe pack" to create a .nupkg file based on the given .nuspec file.
    /// </summary>
    /// <param name="nuspec_file_path">The full filepath to the .nuspec file to use.</param>
    public static void Pack(string nuspec_file_path) {
      if (!Directory.Exists(path : PackOutputDirectory)) {
        Directory.CreateDirectory(path : PackOutputDirectory);
      }

      // Use -NoDefaultExcludes to allow files and folders that start with a . to be packed into the package
      // This is done because if you want a file/folder in a Unity project, but you want Unity to ignore it, it must start with a .
      // This is especially useful for .cs and .js files that you don't want Unity to compile as game scripts
      var arguments = string.Format("pack \"{0}\" -OutputDirectory \"{1}\" -NoDefaultExcludes",
                                    arg0 : nuspec_file_path,
                                    arg1 : PackOutputDirectory);

      RunNugetProcess(arguments : arguments);
    }

    /// <summary>
    /// Calls "nuget.exe push" to push a .nupkf file to the the server location defined in the NuGet.config file.
    /// Note: This differs slightly from NuGet's Push command by automatically calling Pack if the .nupkg doesn't already exist.
    /// </summary>
    /// <param name="nuspec">The NuspecFile which defines the package to push.  Only the ID and Version are used.</param>
    /// <param name="nuspec_file_path">The full filepath to the .nuspec file to use.  This is required by NuGet's Push command.</param>
    /// /// <param name="api_key">The API key to use when pushing a package to the server.  This is optional.</param>
    public static void Push(NuspecFile nuspec, string nuspec_file_path, string api_key = "") {
      var package_path = Path.Combine(path1 : PackOutputDirectory,
                                      path2 : string.Format("{0}.{1}.nupkg",
                                                            arg0 : nuspec.Id,
                                                            arg1 : nuspec.Version));
      if (!File.Exists(path : package_path)) {
        LogVerbose("Attempting to Pack.");
        Pack(nuspec_file_path : nuspec_file_path);

        if (!File.Exists(path : package_path)) {
          Debug.LogErrorFormat("NuGet package not found: {0}", package_path);
          return;
        }
      }

      var arguments = $"push \"{package_path}\" {api_key} -configfile \"{NugetConfigFilePath}\"";

      RunNugetProcess(arguments : arguments);
    }

    /// <summary>
    /// Recursively copies all files and sub-directories from one directory to another.
    /// </summary>
    /// <param name="source_directory_path">The filepath to the folder to copy from.</param>
    /// <param name="dest_directory_path">The filepath to the folder to copy to.</param>
    static void DirectoryCopy(string source_directory_path, string dest_directory_path) {
      var dir = new DirectoryInfo(path : source_directory_path);
      if (!dir.Exists) {
        throw new DirectoryNotFoundException(message :
                                             "Source directory does not exist or could not be found: "
                                             + source_directory_path);
      }

      // if the destination directory doesn't exist, create it
      if (!Directory.Exists(path : dest_directory_path)) {
        LogVerbose("Creating new directory: {0}", dest_directory_path);
        Directory.CreateDirectory(path : dest_directory_path);
      }

      // get the files in the directory and copy them to the new location
      var files = dir.GetFiles();
      foreach (var file in files) {
        var new_file_path = Path.Combine(path1 : dest_directory_path, path2 : file.Name);

        try {
          LogVerbose("Moving {0} to {1}", file.ToString(), new_file_path);
          file.CopyTo(destFileName : new_file_path, true);
        } catch (Exception e) {
          Debug.LogWarningFormat("{0} couldn't be moved to {1}. It may be a native plugin already locked by Unity. Please trying closing Unity and manually moving it. \n{2}",
                                 file.ToString(),
                                 new_file_path,
                                 e.ToString());
        }
      }

      // copy sub-directories and their contents to new location
      var dirs = dir.GetDirectories();
      foreach (var sub_dir in dirs) {
        var temp_path = Path.Combine(path1 : dest_directory_path, path2 : sub_dir.Name);
        DirectoryCopy(source_directory_path : sub_dir.FullName, dest_directory_path : temp_path);
      }
    }

    /// <summary>
    /// Recursively deletes the folder at the given path.
    /// NOTE: Directory.Delete() doesn't delete Read-Only files, whereas this does.
    /// </summary>
    /// <param name="directory_path">The path of the folder to delete.</param>
    static void DeleteDirectory(string directory_path) {
      if (!Directory.Exists(path : directory_path)) {
        return;
      }

      var directory_info = new DirectoryInfo(path : directory_path);

      // delete any sub-folders first
      foreach (var child_info in directory_info.GetFileSystemInfos()) {
        DeleteDirectory(directory_path : child_info.FullName);
      }

      // remove the read-only flag on all files
      var files = directory_info.GetFiles();
      foreach (var file in files) {
        file.Attributes = FileAttributes.Normal;
      }

      // remove the read-only flag on the directory
      directory_info.Attributes = FileAttributes.Normal;

      // recursively delete the directory
      directory_info.Delete(true);
    }

    /// <summary>
    /// Deletes a file at the given filepath.
    /// </summary>
    /// <param name="file_path">The filepath to the file to delete.</param>
    static void DeleteFile(string file_path) {
      if (File.Exists(path : file_path)) {
        File.SetAttributes(path : file_path, fileAttributes : FileAttributes.Normal);
        File.Delete(path : file_path);
      }
    }

    /// <summary>
    /// Deletes all files in the given directory or in any sub-directory, with the given extension.
    /// </summary>
    /// <param name="directory_path">The path to the directory to delete all files of the given extension from.</param>
    /// <param name="extension">The extension of the files to delete, in the form "*.ext"</param>
    static void DeleteAllFiles(string directory_path, string extension) {
      var files = Directory.GetFiles(path : directory_path,
                                     searchPattern : extension,
                                     searchOption : SearchOption.AllDirectories);
      foreach (var file in files) {
        DeleteFile(file_path : file);
      }
    }

    /// <summary>
    /// Uninstalls all of the currently installed packages.
    /// </summary>
    internal static void UninstallAll() {
      foreach (var package in _installed_packages.Values.ToList()) {
        Uninstall(package : package);
      }
    }

    /// <summary>
    /// "Uninstalls" the given package by simply deleting its folder.
    /// </summary>
    /// <param name="package">The NugetPackage to uninstall.</param>
    /// <param name="refresh_assets">True to force Unity to refesh its Assets folder.  False to temporarily ignore the change.  Defaults to true.</param>
    public static void Uninstall(NugetPackageIdentifier package, bool refresh_assets = true) {
      LogVerbose("Uninstalling: {0} {1}", package._Id, package._Version);

      // update the package.config file
      PackagesConfigFile.RemovePackage(package : package);
      PackagesConfigFile.Save(filepath : PackagesConfigFilePath);

      var package_install_directory = Path.Combine(path1 : NugetConfigFile.RepositoryPath,
                                                   path2 : string.Format("{0}.{1}",
                                                                           arg0 : package._Id,
                                                                           arg1 : package._Version));
      DeleteDirectory(directory_path : package_install_directory);

      var meta_file = Path.Combine(path1 : NugetConfigFile.RepositoryPath,
                                   path2 : string.Format("{0}.{1}.meta",
                                                         arg0 : package._Id,
                                                         arg1 : package._Version));
      DeleteFile(file_path : meta_file);

      var tools_install_directory = Path.Combine(path1 : Application.dataPath,
                                                 path2 : string.Format("../Packages/{0}.{1}",
                                                                       arg0 : package._Id,
                                                                       arg1 : package._Version));
      DeleteDirectory(directory_path : tools_install_directory);

      _installed_packages.Remove(key : package._Id);

      if (refresh_assets) {
        AssetDatabase.Refresh();
      }
    }

    /// <summary>
    /// Updates a package by uninstalling the currently installed version and installing the "new" version.
    /// </summary>
    /// <param name="current_version">The current package to uninstall.</param>
    /// <param name="new_version">The package to install.</param>
    /// <param name="refresh_assets">True to refresh the assets inside Unity.  False to ignore them (for now).  Defaults to true.</param>
    public static bool Update(NugetPackageIdentifier current_version,
                              NugetPackage new_version,
                              bool refresh_assets = true) {
      LogVerbose("Updating {0} {1} to {2}",
                 current_version._Id,
                 current_version._Version,
                 new_version._Version);
      Uninstall(package : current_version, false);
      return InstallIdentifier(package : new_version, refresh_assets : refresh_assets);
    }

    /// <summary>
    /// Installs all of the given updates, and uninstalls the corresponding package that is already installed.
    /// </summary>
    /// <param name="updates">The list of all updates to install.</param>
    /// <param name="packages_to_update">The list of all packages currently installed.</param>
    public static void UpdateAll(IEnumerable<NugetPackage> updates,
                                 IEnumerable<NugetPackage> packages_to_update) {
      var progress_step = 1.0f / updates.Count();
      float current_progress = 0;

      foreach (var update in updates) {
        EditorUtility.DisplayProgressBar(title : string.Format("Updating to {0} {1}",
                                                               arg0 : update._Id,
                                                               arg1 : update._Version),
                                         "Installing All Updates",
                                         progress : current_progress);

        var installed_package = packages_to_update.FirstOrDefault(p => p._Id == update._Id);
        if (installed_package != null) {
          Update(current_version : installed_package, new_version : update, false);
        } else {
          Debug.LogErrorFormat("Trying to update {0} to {1}, but no version is installed!",
                               update._Id,
                               update._Version);
        }

        current_progress += progress_step;
      }

      AssetDatabase.Refresh();

      EditorUtility.ClearProgressBar();
    }

    /// <summary>
    /// Gets the dictionary of packages that are actually installed in the project, keyed off of the ID.
    /// </summary>
    /// <returns>A dictionary of installed <see cref="NugetPackage"/>s.</returns>
    public static IEnumerable<NugetPackage> InstalledPackages { get { return _installed_packages.Values; } }

    /// <summary>
    /// The path to the nuget.config file.
    /// </summary>
    public static String NugetConfigFilePath {
      get { return Path.Combine(path1 : BasePath, path2 : _nuget_config_file_path); }
    }

    /// <summary>
    /// The path to the packages.config file.
    /// </summary>
    public static String PackagesConfigFilePath {
      get { return Path.Combine(path1 : BasePath, path2 : _packages_config_file_path); }
    }

    /// <summary>
    /// The path where to put created (packed) and downloaded (not installed yet) .nupkg files.
    /// </summary>
    public static String PackOutputDirectory {
      get { return Path.Combine(path1 : BasePath, path2 : _pack_output_directory); }
    }

    static string BasePath {
      get { return Path.Combine(path1 : Application.dataPath, path2 : NugetPreferences._Base_Path); }
    }

    public static String ToolsPackagesFolder {
      get { return Path.Combine(path1 : BasePath, path2 : _tools_packages_folder); }
    }

    /// <summary>
    /// Updates the dictionary of packages that are actually installed in the project based on the files that are currently installed.
    /// </summary>
    public static void UpdateInstalledPackages() {
      LoadNugetConfigFile();

      var stopwatch = new Stopwatch();
      stopwatch.Start();

      _installed_packages.Clear();

      // loops through the packages that are actually installed in the project
      if (Directory.Exists(path : NugetConfigFile.RepositoryPath)) {
        // a package that was installed via NuGet will have the .nupkg it came from inside the folder
        var nupkg_files = Directory.GetFiles(path : NugetConfigFile.RepositoryPath,
                                             "*.nupkg",
                                             searchOption : SearchOption.AllDirectories);
        foreach (var nupkg_file in nupkg_files) {
          var package = NugetPackage.FromNupkgFile(nupkgFilepath : nupkg_file);
          if (!_installed_packages.ContainsKey(key : package._Id)) {
            _installed_packages.Add(key : package._Id, value : package);
          } else {
            #if VERBOSE
            Debug.LogErrorFormat("Package is already in installed list: {0}", package._Id);
            #endif
          }
        }

        // if the source code & assets for a package are pulled directly into the project (ex: via a symlink/junction) it should have a .nuspec defining the package
        var nuspec_files = Directory.GetFiles(path : NugetConfigFile.RepositoryPath,
                                              "*.nuspec",
                                              searchOption : SearchOption.AllDirectories);
        foreach (var nuspec_file in nuspec_files) {
          var package = NugetPackage.FromNuspec(nuspec : NuspecFile.Load(filePath : nuspec_file));
          if (!_installed_packages.ContainsKey(key : package._Id)) {
            _installed_packages.Add(key : package._Id, value : package);
          } else {
            #if VERBOSE
            Debug.LogErrorFormat("Package is already in installed list: {0}", package._Id);
            #endif
          }
        }
      }

      stopwatch.Stop();
      LogVerbose("Getting installed packages took {0} ms", stopwatch.ElapsedMilliseconds);
    }

    /// <summary>
    /// Gets a list of NuGetPackages via the HTTP Search() function defined by NuGet.Server and NuGet Gallery.
    /// This allows searching for partial IDs or even the empty string (the default) to list ALL packages.
    /// 
    /// NOTE: See the functions and parameters defined here: https://www.nuget.org/api/v2/$metadata
    /// </summary>
    /// <param name="search_term">The search term to use to filter packages. Defaults to the empty string.</param>
    /// <param name="include_all_versions">True to include older versions that are not the latest version.</param>
    /// <param name="include_prerelease">True to include prerelease packages (alpha, beta, etc).</param>
    /// <param name="number_to_get">The number of packages to fetch.</param>
    /// <param name="number_to_skip">The number of packages to skip before fetching.</param>
    /// <returns>The list of available packages.</returns>
    public static List<NugetPackage> Search(string search_term = "",
                                            bool include_all_versions = false,
                                            bool include_prerelease = false,
                                            int number_to_get = 15,
                                            int number_to_skip = 0) {
      var packages = new List<NugetPackage>();

      // Loop through all active sources and combine them into a single list
      foreach (var source in _package_sources.Where(s => s.IsEnabled)) {
        var new_packages = source.Search(search_term : search_term,
                                         include_all_versions : include_all_versions,
                                         include_prerelease : include_prerelease,
                                         number_to_get : number_to_get,
                                         number_to_skip : number_to_skip);
        packages.AddRange(collection : new_packages);
        packages = packages.Distinct().ToList();
      }

      return packages;
    }

    /// <summary>
    /// Queries the server with the given list of installed packages to get any updates that are available.
    /// </summary>
    /// <param name="packages_to_update">The list of currently installed packages.</param>
    /// <param name="include_prerelease">True to include prerelease packages (alpha, beta, etc).</param>
    /// <param name="include_all_versions">True to include older versions that are not the latest version.</param>
    /// <param name="target_frameworks">The specific frameworks to target?</param>
    /// <param name="version_contraints">The version constraints?</param>
    /// <returns>A list of all updates available.</returns>
    public static List<NugetPackage> GetUpdates(IEnumerable<NugetPackage> packages_to_update,
                                                bool include_prerelease = false,
                                                bool include_all_versions = false,
                                                string target_frameworks = "",
                                                string version_contraints = "") {
      var packages = new List<NugetPackage>();

      // Loop through all active sources and combine them into a single list
      foreach (var source in _package_sources.Where(s => s.IsEnabled)) {
        var new_packages = source.GetUpdates(installed_packages : packages_to_update,
                                             include_prerelease : include_prerelease,
                                             include_all_versions : include_all_versions,
                                             target_frameworks : target_frameworks,
                                             version_contraints : version_contraints);
        packages.AddRange(collection : new_packages);
        packages = packages.Distinct().ToList();
      }

      return packages;
    }

    /// <summary>
    /// Gets a NugetPackage from the NuGet server with the exact ID and Version given.
    /// </summary>
    /// <param name="package_id">The <see cref="NugetPackageIdentifier"/> containing the ID and Version of the package to get.</param>
    /// <returns>The retrieved package, if there is one.  Null if no matching package was found.</returns>
    static NugetPackage GetSpecificPackage(NugetPackageIdentifier package_id) {
      // First look to see if the package is already installed
      var package = GetInstalledPackage(package_id : package_id);

      if (package == null) {
        // That package isn't installed yet, so look in the cache next
        package = GetCachedPackage(package_id : package_id);
      }

      if (package == null) {
        // It's not in the cache, so we need to look in the active sources
        package = GetOnlinePackage(package_id : package_id);
      }

      return package;
    }

    /// <summary>
    /// Tries to find an already installed package that matches (or is in the range of) the given package ID.
    /// </summary>
    /// <param name="package_id">The <see cref="NugetPackageIdentifier"/> of the <see cref="NugetPackage"/> to find.</param>
    /// <returns>The best <see cref="NugetPackage"/> match, if there is one, otherwise null.</returns>
    static NugetPackage GetInstalledPackage(NugetPackageIdentifier package_id) {
      NugetPackage installed_package = null;

      if (_installed_packages.TryGetValue(key : package_id._Id, value : out installed_package)) {
        if (package_id._Version != installed_package._Version) {
          if (package_id.InRange(other_package : installed_package)) {
            LogVerbose("Requested {0} {1}, but {2} is already installed, so using that.",
                       package_id._Id,
                       package_id._Version,
                       installed_package._Version);
          } else {
            LogVerbose("Requested {0} {1}. {2} is already installed, but it is out of range.",
                       package_id._Id,
                       package_id._Version,
                       installed_package._Version);
            installed_package = null;
          }
        } else {
          LogVerbose("Found exact package already installed: {0} {1}",
                     installed_package._Id,
                     installed_package._Version);
        }
      }

      return installed_package;
    }

    /// <summary>
    /// Tries to find an already cached package that matches the given package ID.
    /// </summary>
    /// <param name="package_id">The <see cref="NugetPackageIdentifier"/> of the <see cref="NugetPackage"/> to find.</param>
    /// <returns>The best <see cref="NugetPackage"/> match, if there is one, otherwise null.</returns>
    static NugetPackage GetCachedPackage(NugetPackageIdentifier package_id) {
      NugetPackage package = null;

      if (NugetConfigFile.InstallFromCache) {
        var cached_package_path = System.IO.Path.Combine(path1 : PackOutputDirectory,
                                                         path2 :
                                                         $"./{package_id._Id}.{package_id._Version}.nupkg");

        if (File.Exists(path : cached_package_path)) {
          LogVerbose("Found exact package in the cache: {0}", cached_package_path);
          package = NugetPackage.FromNupkgFile(nupkgFilepath : cached_package_path);
        }
      }

      return package;
    }

    /// <summary>
    /// Tries to find an "online" (in the package sources - which could be local) package that matches (or is in the range of) the given package ID.
    /// </summary>
    /// <param name="package_id">The <see cref="NugetPackageIdentifier"/> of the <see cref="NugetPackage"/> to find.</param>
    /// <returns>The best <see cref="NugetPackage"/> match, if there is one, otherwise null.</returns>
    static NugetPackage GetOnlinePackage(NugetPackageIdentifier package_id) {
      NugetPackage package = null;

      // Loop through all active sources and stop once the package is found
      foreach (var source in _package_sources.Where(s => s.IsEnabled)) {
        var found_package = source.GetSpecificPackage(package : package_id);
        if (found_package == null) {
          continue;
        }

        if (found_package._Version == package_id._Version) {
          LogVerbose("{0} {1} was found in {2}",
                     found_package._Id,
                     found_package._Version,
                     source.Name);
          return found_package;
        }

        LogVerbose("{0} {1} was found in {2}, but wanted {3}",
                   found_package._Id,
                   found_package._Version,
                   source.Name,
                   package_id._Version);
        if (package == null) {
          // if another package hasn't been found yet, use the current found one
          package = found_package;
        }
        // another package has been found previously, but neither match identically
        else if (found_package > package) {
          // use the new package if it's closer to the desired version
          package = found_package;
        }
      }

      if (package != null) {
        LogVerbose("{0} {1} not found, using {2}",
                   package_id._Id,
                   package_id._Version,
                   package._Version);
      } else {
        LogVerbose("Failed to find {0} {1}", package_id._Id, package_id._Version);
      }

      return package;
    }

    /// <summary>
    /// Copies the contents of input to output. Doesn't close either stream.
    /// </summary>
    static void CopyStream(Stream input, Stream output) {
      var buffer = new byte[8 * 1024];
      int len;
      while ((len = input.Read(buffer : buffer, 0, count : buffer.Length)) > 0) {
        output.Write(buffer : buffer, 0, count : len);
      }
    }

    /// <summary>
    /// Installs the package given by the identifer.  It fetches the appropriate full package from the installed packages, package cache, or package sources and installs it.
    /// </summary>
    /// <param name="package">The identifer of the package to install.</param>
    /// <param name="refresh_assets">True to refresh the Unity asset database.  False to ignore the changes (temporarily).</param>
    internal static bool InstallIdentifier(NugetPackageIdentifier package, bool refresh_assets = true) {
      var found_package = GetSpecificPackage(package_id : package);

      if (found_package != null) {
        return Install(package : found_package, refresh_assets : refresh_assets);
      } else {
        Debug.LogErrorFormat("Could not find {0} {1} or greater.", package._Id, package._Version);
        return false;
      }
    }

    /// <summary>
    /// Outputs the given message to the log only if verbose mode is active.  Otherwise it does nothing.
    /// </summary>
    /// <param name="format">The formatted message string.</param>
    /// <param name="args">The arguments for the formattted message string.</param>
    public static void LogVerbose(string format, params object[] args) {
      if (NugetConfigFile == null || NugetConfigFile.Verbose) {
        #if UNITY_5_4_OR_NEWER
        var stack_trace_log_type = Application.GetStackTraceLogType(logType : LogType.Log);
        Application.SetStackTraceLogType(logType : LogType.Log, stackTraceType : StackTraceLogType.None);
        #else
                var stackTraceLogType = Application.stackTraceLogType;
                Application.stackTraceLogType = StackTraceLogType.None;
        #endif
        Debug.LogFormat(format : format, args : args);

        #if UNITY_5_4_OR_NEWER
        Application.SetStackTraceLogType(logType : LogType.Log, stackTraceType : stack_trace_log_type);
        #else
                Application.stackTraceLogType = stackTraceLogType;
        #endif
      }
    }

    /// <summary>
    /// Installs the given package.
    /// </summary>
    /// <param name="package">The package to install.</param>
    /// <param name="refresh_assets">True to refresh the Unity asset database.  False to ignore the changes (temporarily).</param>
    public static bool Install(NugetPackage package, bool refresh_assets = true) {
      NugetPackage installed_package = null;
      if (_installed_packages.TryGetValue(key : package._Id, value : out installed_package)) {
        if (installed_package < package) {
          LogVerbose("{0} {1} is installed, but need {2} or greater. Updating to {3}",
                     installed_package._Id,
                     installed_package._Version,
                     package._Version,
                     package._Version);
          return Update(current_version : installed_package, new_version : package, false);
        } else if (installed_package > package) {
          LogVerbose("{0} {1} is installed. {2} or greater is needed, so using installed version.",
                     installed_package._Id,
                     installed_package._Version,
                     package._Version);
        } else {
          LogVerbose("Already installed: {0} {1}", package._Id, package._Version);
        }

        return true;
      }

      var install_success = false;
      try {
        LogVerbose("Installing: {0} {1}", package._Id, package._Version);

        // look to see if the package (any version) is already installed

        if (refresh_assets) {
          EditorUtility.DisplayProgressBar(title : string.Format("Installing {0} {1}",
                                                                 arg0 : package._Id,
                                                                 arg1 : package._Version),
                                           "Installing Dependencies",
                                           0.1f);
        }

        // install all dependencies for target framework
        var framework_group = GetBestDependencyFrameworkGroupForCurrentSettings(package : package);

        LogVerbose("Installing dependencies for TargetFramework: {0}", framework_group.TargetFramework);
        foreach (var dependency in framework_group.Dependencies) {
          LogVerbose("Installing Dependency: {0} {1}", dependency._Id, dependency._Version);
          var installed = InstallIdentifier(package : dependency);
          if (!installed) {
            throw new Exception(message :
                                $"Failed to install dependency: {dependency._Id} {dependency._Version}.");
          }
        }

        // update packages.config
        PackagesConfigFile.AddPackage(package : package);
        PackagesConfigFile.Save(filepath : PackagesConfigFilePath);

        var cached_package_path = Path.Combine(path1 : PackOutputDirectory,
                                               path2 : $"./{package._Id}.{package._Version}.nupkg");
        if (NugetConfigFile.InstallFromCache && File.Exists(path : cached_package_path)) {
          LogVerbose("Cached package found for {0} {1}", package._Id, package._Version);
        } else {
          if (package.PackageSource.IsLocalPath) {
            LogVerbose("Caching local package {0} {1}", package._Id, package._Version);

            // copy the .nupkg from the local path to the cache
            File.Copy(sourceFileName : Path.Combine(path1 : package.PackageSource.ExpandedPath,
                                                    path2 : $"./{package._Id}.{package._Version}.nupkg"),
                      destFileName : cached_package_path,
                      true);
          } else {
            // Mono doesn't have a Certificate Authority, so we have to provide all validation manually.  Currently just accept anything.
            // See here: http://stackoverflow.com/questions/4926676/mono-webrequest-fails-with-https

            // remove all handlers
            //if (ServicePointManager.ServerCertificateValidationCallback != null)
            //    foreach (var d in ServicePointManager.ServerCertificateValidationCallback.GetInvocationList())
            //        ServicePointManager.ServerCertificateValidationCallback -= (d as System.Net.Security.RemoteCertificateValidationCallback);
            ServicePointManager.ServerCertificateValidationCallback = null;

            // add anonymous handler
            ServicePointManager.ServerCertificateValidationCallback +=
                (sender, certificate, chain, policy_errors) => true;

            LogVerbose("Downloading package {0} {1}", package._Id, package._Version);

            if (refresh_assets) {
              EditorUtility.DisplayProgressBar(title : $"Installing {package._Id} {package._Version}",
                                               "Downloading Package",
                                               0.3f);
            }

            var obj_stream = RequestUrl(url : package.DownloadUrl,
                                        user_name : package.PackageSource.UserName,
                                        password : package.PackageSource.ExpandedPassword,
                                        time_out : null);
            using (Stream file = File.Create(path : cached_package_path)) {
              CopyStream(input : obj_stream, output : file);
            }
          }
        }

        if (refresh_assets) {
          EditorUtility.DisplayProgressBar(title : $"Installing {package._Id} {package._Version}",
                                           "Extracting Package",
                                           0.6f);
        }

        if (File.Exists(path : cached_package_path)) {
          var base_directory = Path.Combine(path1 : NugetConfigFile.RepositoryPath,
                                            path2 : $"{package._Id}.{package._Version}");

          // unzip the package
          using (var zip = ZipFile.Read(fileName : cached_package_path)) {
            foreach (var entry in zip) {
              entry.Extract(baseDirectory : base_directory,
                            extractExistingFile : ExtractExistingFileAction.OverwriteSilently);
              if (NugetConfigFile.ReadOnlyPackageFiles) {
                var extracted_file =
                    new FileInfo(fileName : Path.Combine(path1 : base_directory, path2 : entry.FileName));
                extracted_file.Attributes |= FileAttributes.ReadOnly;
              }
            }
          }

          // copy the .nupkg inside the Unity project
          File.Copy(sourceFileName : cached_package_path,
                    destFileName : Path.Combine(path1 : NugetConfigFile.RepositoryPath,
                                                path2 :
                                                $"{package._Id}.{package._Version}/{package._Id}.{package._Version}.nupkg"),
                    true);
        } else {
          Debug.LogErrorFormat("File not found: {0}", cached_package_path);
        }

        if (refresh_assets) {
          EditorUtility.DisplayProgressBar(title : $"Installing {package._Id} {package._Version}",
                                           "Cleaning Package",
                                           0.9f);
        }

        // clean
        Clean(package : package);

        // update the installed packages list
        _installed_packages.Add(key : package._Id, value : package);
        install_success = true;
      } catch (Exception e) {
        WarnIfDotNetAuthenticationIssue(e : e);
        Debug.LogErrorFormat("Unable to install package {0} {1}\n{2}",
                             package._Id,
                             package._Version,
                             e.ToString());
        install_success = false;
      } finally {
        if (refresh_assets) {
          EditorUtility.DisplayProgressBar(title : $"Installing {package._Id} {package._Version}",
                                           "Importing Package",
                                           0.95f);
          AssetDatabase.Refresh();
          EditorUtility.ClearProgressBar();
        }
      }

      return install_success;
    }

    static void WarnIfDotNetAuthenticationIssue(Exception e) {
      #if !NET_4_6
            WebException webException = e as WebException;
            HttpWebResponse webResponse =
 webException != null ? webException.Response as HttpWebResponse : null;
            if (webResponse != null && webResponse.StatusCode == HttpStatusCode.BadRequest && webException.Message.Contains("Authentication information is not given in the correct format"))
            {
                // This error occurs when downloading a package with authentication using .NET 3.5, but seems to be fixed by the new .NET 4.6 runtime.
                // Inform users when this occurs.
                Debug.LogError("Authentication failed. This can occur due to a known issue in .NET 3.5. This can be fixed by changing Scripting Runtime to Experimental (.NET 4.6 Equivalent) in Player Settings.");
            }
      #endif
    }

    struct AuthenticatedFeed {
      public string _AccountUrlPattern;
      public string _ProviderUrlTemplate;

      public string GetAccount(string url) {
        var match = Regex.Match(input : url,
                                pattern : this._AccountUrlPattern,
                                options : RegexOptions.IgnoreCase);
        if (!match.Success) {
          return null;
        }

        return match.Groups["account"].Value;
      }

      public string GetProviderUrl(string account) {
        return this._ProviderUrlTemplate.Replace("{account}", newValue : account);
      }
    }

    // TODO: Move to ScriptableObjet
    static List<AuthenticatedFeed> _known_authenticated_feeds =
        new List<AuthenticatedFeed>() {
                                          new AuthenticatedFeed() {
                                                                      _AccountUrlPattern =
                                                                          @"^https:\/\/(?<account>[a-zA-z0-9]+).pkgs.visualstudio.com",
                                                                      _ProviderUrlTemplate =
                                                                          "https://{account}.pkgs.visualstudio.com/_apis/public/nuget/client/CredentialProviderBundle.zip"
                                                                  },
                                          new AuthenticatedFeed() {
                                                                      _AccountUrlPattern =
                                                                          @"^https:\/\/pkgs.dev.azure.com\/(?<account>[a-zA-z0-9]+)\/",
                                                                      _ProviderUrlTemplate =
                                                                          "https://pkgs.dev.azure.com/{account}/_apis/public/nuget/client/CredentialProviderBundle.zip"
                                                                  }
                                      };



    /// <summary>
    /// Get the specified URL from the web. Throws exceptions if the request fails.
    /// </summary>
    /// <param name="url">URL that will be loaded.</param>
    /// <param name="password">Password that will be passed in the Authorization header or the request. If null, authorization is omitted.</param>
    /// <param name="time_out">Timeout in milliseconds or null to use the default timeout values of HttpWebRequest.</param>
    /// <returns>Stream containing the result.</returns>
    public static Stream RequestUrl(string url, string user_name, string password, int? time_out) {
      var get_request = (HttpWebRequest)WebRequest.Create(requestUriString : url);
      if (time_out.HasValue) {
        get_request.Timeout = time_out.Value;
        get_request.ReadWriteTimeout = time_out.Value;
      }

      if (string.IsNullOrEmpty(value : password)) {
        var creds =
            GetCredentialFromProvider(feed_uri : GetTruncatedFeedUri(method_uri : get_request.RequestUri));
        if (creds.HasValue) {
          user_name = creds.Value.Username;
          password = creds.Value.Password;
        }
      }

      if (password != null) {
        // Send password as described by https://docs.microsoft.com/en-us/vsts/integrate/get-started/rest/basics.
        // This works with Visual Studio Team Services, but hasn't been tested with other authentication schemes so there may be additional work needed if there
        // are different kinds of authentication.
        get_request.Headers.Add("Authorization",
                                value :
                                $"Basic {Convert.ToBase64String(inArray : System.Text.Encoding.ASCII.GetBytes(s : string.Format("{0}:{1}", arg0 : user_name, arg1 : password)))}");
      }

      LogVerbose("HTTP GET {0}", url);
      var obj_stream = get_request.GetResponse().GetResponseStream();
      return obj_stream;
    }

    /// <summary>
    /// Restores all packages defined in packages.config.
    /// </summary>
    public static void Restore() {
      UpdateInstalledPackages();

      var stopwatch = new Stopwatch();
      stopwatch.Start();

      try {
        var progress_step = 1.0f / PackagesConfigFile.Packages.Count;
        float current_progress = 0;

        // copy the list since the InstallIdentifier operation below changes the actual installed packages list
        var packages_to_install = new List<NugetPackageIdentifier>(collection : PackagesConfigFile.Packages);

        LogVerbose("Restoring {0} packages.", packages_to_install.Count);

        foreach (var package in packages_to_install) {
          if (package != null) {
            EditorUtility.DisplayProgressBar("Restoring NuGet Packages",
                                             info : $"Restoring {package._Id} {package._Version}",
                                             progress : current_progress);

            if (!IsInstalled(package : package)) {
              LogVerbose("---Restoring {0} {1}", package._Id, package._Version);
              InstallIdentifier(package : package);
            } else {
              LogVerbose("---Already installed: {0} {1}", package._Id, package._Version);
            }
          }

          current_progress += progress_step;
        }

        CheckForUnnecessaryPackages();
      } catch (Exception e) {
        Debug.LogErrorFormat("{0}", e.ToString());
      } finally {
        stopwatch.Stop();
        LogVerbose("Restoring packages took {0} ms", stopwatch.ElapsedMilliseconds);

        AssetDatabase.Refresh();
        EditorUtility.ClearProgressBar();
      }
    }

    internal static void CheckForUnnecessaryPackages() {
      if (!Directory.Exists(path : NugetConfigFile.RepositoryPath)) {
        return;
      }

      var directories = Directory.GetDirectories(path : NugetConfigFile.RepositoryPath,
                                                 "*",
                                                 searchOption : SearchOption.TopDirectoryOnly);
      foreach (var folder in directories) {
        var name = Path.GetFileName(path : folder);
        var installed = false;
        foreach (var package in PackagesConfigFile.Packages) {
          var package_name = string.Format("{0}.{1}", arg0 : package._Id, arg1 : package._Version);
          if (name == package_name) {
            installed = true;
            break;
          }
        }

        if (!installed) {
          LogVerbose("---DELETE unnecessary package {0}", name);

          DeleteDirectory(directory_path : folder);
          DeleteFile(file_path : folder + ".meta");
        }
      }
    }

    /// <summary>
    /// Checks if a given package is installed.
    /// </summary>
    /// <param name="package">The package to check if is installed.</param>
    /// <returns>True if the given package is installed.  False if it is not.</returns>
    internal static bool IsInstalled(NugetPackageIdentifier package) {
      var is_installed = false;
      NugetPackage installed_package = null;

      if (_installed_packages.TryGetValue(key : package._Id, value : out installed_package)) {
        is_installed = package._Version == installed_package._Version;
      }

      return is_installed;
    }

    /// <summary>
    /// Downloads an image at the given URL and converts it to a Unity Texture2D.
    /// </summary>
    /// <param name="url">The URL of the image to download.</param>
    /// <returns>The image as a Unity Texture2D object.</returns>
    public static Texture2D DownloadImage(string url) {
      var timedout = false;
      var stopwatch = new Stopwatch();
      stopwatch.Start();

      var from_cache = false;
      if (ExistsInDiskCache(url : url)) {
        url = "file:///" + GetFilePath(url : url);
        from_cache = true;
      }

      var request = new WWW(url : url);
      while (!request.isDone) {
        if (stopwatch.ElapsedMilliseconds >= 750) {
          request.Dispose();
          timedout = true;
          break;
        }
      }

      Texture2D result = null;

      if (timedout) {
        LogVerbose("Downloading image {0} timed out! Took more than 750ms.", url);
      } else {
        if (string.IsNullOrEmpty(value : request.error)) {
          result = request.textureNonReadable;
          LogVerbose("Downloading image {0} took {1} ms", url, stopwatch.ElapsedMilliseconds);
        } else {
          LogVerbose(format : "Request error: " + request.error);
        }
      }

      if (result != null && !from_cache) {
        CacheTextureOnDisk(url : url, bytes : request.bytes);
      }

      request.Dispose();
      return result;
    }

    static void CacheTextureOnDisk(string url, byte[] bytes) {
      var disk_path = GetFilePath(url : url);
      File.WriteAllBytes(path : disk_path, bytes : bytes);
    }

    static bool ExistsInDiskCache(string url) { return File.Exists(path : GetFilePath(url : url)); }

    static string GetFilePath(string url) {
      return Path.Combine(path1 : Application.temporaryCachePath, path2 : GetHash(s : url));
    }

    static string GetHash(string s) {
      if (string.IsNullOrEmpty(value : s)) {
        return null;
      }

      var md5 = new MD5CryptoServiceProvider();
      var data = md5.ComputeHash(buffer : Encoding.Default.GetBytes(s : s));
      var s_builder = new StringBuilder();
      for (var i = 0; i < data.Length; i++) {
        s_builder.Append(value : data[i].ToString("x2"));
      }

      return s_builder.ToString();
    }

    /// <summary>
    /// Data class returned from nuget credential providers in a JSON format. As described here:
    /// https://docs.microsoft.com/en-us/nuget/reference/extensibility/nuget-exe-credential-providers#creating-a-nugetexe-credential-provider
    /// </summary>
    [System.Serializable]
    struct CredentialProviderResponse {
      public string Username;
      public string Password;
    }

    /// <summary>
    /// Possible response codes returned by a Nuget credential provider as described here:
    /// https://docs.microsoft.com/en-us/nuget/reference/extensibility/nuget-exe-credential-providers#creating-a-nugetexe-credential-provider
    /// </summary>
    enum CredentialProviderExitCode {
      Success_ = 0,
      Provider_not_applicable_ = 1,
      Failure_ = 2
    }

    static void DownloadCredentialProviders(Uri feed_uri) {
      foreach (var feed in _known_authenticated_feeds) {
        var account = feed.GetAccount(url : feed_uri.ToString());
        if (string.IsNullOrEmpty(value : account)) {
          continue;
        }

        var provider_url = feed.GetProviderUrl(account : account);

        var credential_provider_request = (HttpWebRequest)WebRequest.Create(requestUriString : provider_url);

        try {
          var credential_provider_download_stream =
              credential_provider_request.GetResponse().GetResponseStream();

          var temp_file_name = Path.GetTempFileName();
          LogVerbose("Writing {0} to {1}", provider_url, temp_file_name);

          using (var file = File.Create(path : temp_file_name)) {
            CopyStream(input : credential_provider_download_stream, output : file);
          }

          var provider_destination = Environment.GetEnvironmentVariable("NUGET_CREDENTIALPROVIDERS_PATH");
          if (string.IsNullOrEmpty(value : provider_destination)) {
            provider_destination =
                Path.Combine(path1 : Environment.GetFolderPath(folder : Environment.SpecialFolder
                                                                   .LocalApplicationData),
                             "Nuget/CredentialProviders");
          }

          // Unzip the bundle and extract any credential provider exes
          using (var zip = ZipFile.Read(fileName : temp_file_name)) {
            foreach (var entry in zip) {
              if (Regex.IsMatch(input : entry.FileName,
                                @"^credentialprovider.+\.exe$",
                                options : RegexOptions.IgnoreCase)) {
                LogVerbose("Extracting {0} to {1}", entry.FileName, provider_destination);
                entry.Extract(baseDirectory : provider_destination,
                              extractExistingFile : ExtractExistingFileAction.OverwriteSilently);
              }
            }
          }

          // Delete the bundle
          File.Delete(path : temp_file_name);
        } catch (Exception e) {
          Debug.LogErrorFormat("Failed to download credential provider from {0}: {1}",
                               credential_provider_request.Address,
                               e.Message);
        }
      }
    }

    /// <summary>
    /// Helper function to acquire a token to access VSTS hosted nuget feeds by using the CredentialProvider.VSS.exe
    /// tool. Downloading it from the VSTS instance if needed.
    /// See here for more info on nuget Credential Providers:
    /// https://docs.microsoft.com/en-us/nuget/reference/extensibility/nuget-exe-credential-providers
    /// </summary>
    /// <param name="feed_uri">The hostname where the VSTS instance is hosted (such as microsoft.pkgs.visualsudio.com.</param>
    /// <returns>The password in the form of a token, or null if the password could not be acquired</returns>
    static CredentialProviderResponse? GetCredentialFromProvider(Uri feed_uri) {
      CredentialProviderResponse? response;
      if (!_cached_credentials_by_feed_uri.TryGetValue(key : feed_uri, value : out response)) {
        response = GetCredentialFromProvider_Uncached(feed_uri : feed_uri, true);
        _cached_credentials_by_feed_uri[key : feed_uri] = response;
      }

      return response;
    }

    /// <summary>
    /// Given the URI of a nuget method, returns the URI of the feed itself without the method and query parameters.
    /// </summary>
    /// <param name="method_uri">URI of nuget method.</param>
    /// <returns>URI of the feed without the method and query parameters.</returns>
    static Uri GetTruncatedFeedUri(Uri method_uri) {
      var truncated_uri_string = method_uri.GetLeftPart(part : UriPartial.Path);

      // Pull off the function if there is one
      if (truncated_uri_string.EndsWith(")")) {
        var last_separator_index = truncated_uri_string.LastIndexOf('/');
        if (last_separator_index != -1) {
          truncated_uri_string = truncated_uri_string.Substring(0, length : last_separator_index);
        }
      }

      var truncated_uri = new Uri(uriString : truncated_uri_string);
      return truncated_uri;
    }

    /// <summary>
    /// Clears static credentials previously cached by GetCredentialFromProvider.
    /// </summary>
    public static void ClearCachedCredentials() { _cached_credentials_by_feed_uri.Clear(); }

    /// <summary>
    /// Internal function called by GetCredentialFromProvider to implement retrieving credentials. For performance reasons,
    /// most functions should call GetCredentialFromProvider in order to take advantage of cached credentials.
    /// </summary>
    static CredentialProviderResponse? GetCredentialFromProvider_Uncached(
        Uri feed_uri,
        bool download_if_missing) {
      LogVerbose("Getting credential for {0}", feed_uri);

      // Build the list of possible locations to find the credential provider. In order it should be local app data, paths set on the
      // environment variable, and lastly look at the root of the packages save location.
      var possible_credential_provider_paths = new List<string> {
                                                                    Path.Combine(path1 :
                                                                      Path.Combine(path1 : Environment
                                                                            .GetFolderPath(folder :
                                                                              Environment
                                                                                  .SpecialFolder
                                                                                  .LocalApplicationData),
                                                                        "Nuget"),
                                                                      "CredentialProviders")
                                                                };

      var environment_credential_provider_paths =
          Environment.GetEnvironmentVariable("NUGET_CREDENTIALPROVIDERS_PATH");
      if (!string.IsNullOrEmpty(value : environment_credential_provider_paths)) {
        possible_credential_provider_paths.AddRange(collection :
                                                    environment_credential_provider_paths.Split(separator :
                                                      new string[] {";"},
                                                      options : StringSplitOptions.RemoveEmptyEntries)
                                                    ?? Enumerable.Empty<string>());
      }

      // Try to find any nuget.exe in the package tools installation location
      possible_credential_provider_paths.Add(item : ToolsPackagesFolder);

      // Search through all possible paths to find the credential provider.
      var provider_paths = new List<string>();
      foreach (var possible_path in possible_credential_provider_paths) {
        if (Directory.Exists(path : possible_path)) {
          provider_paths.AddRange(collection : Directory.GetFiles(path : possible_path,
                                                                  "credentialprovider*.exe",
                                                                  searchOption : SearchOption
                                                                      .AllDirectories));
        }
      }

      foreach (var provider_path in provider_paths.Distinct()) {
        // Launch the credential provider executable and get the json encoded response from the std output
        var process = new Process {
                                      StartInfo = {
                                                      UseShellExecute = false,
                                                      CreateNoWindow = true,
                                                      RedirectStandardOutput = true,
                                                      RedirectStandardError = true,
                                                      FileName = provider_path,
                                                      Arguments =
                                                          string.Format("-uri \"{0}\"",
                                                                        arg0 : feed_uri.ToString()),
                                                      StandardOutputEncoding = Encoding.GetEncoding(850)
                                                  }
                                  };

        // http://stackoverflow.com/questions/16803748/how-to-decode-cmd-output-correctly
        // Default = 65533, ASCII = ?, Unicode = nothing works at all, UTF-8 = 65533, UTF-7 = 242 = WORKS!, UTF-32 = nothing works at all
        process.Start();
        process.WaitForExit();

        var output = process.StandardOutput.ReadToEnd();
        var errors = process.StandardError.ReadToEnd();

        switch ((CredentialProviderExitCode)process.ExitCode) {
          case CredentialProviderExitCode.Provider_not_applicable_: break; // Not the right provider
          case CredentialProviderExitCode.Failure_: // Right provider, failure to get creds
          {
            Debug.LogErrorFormat("Failed to get credentials from {0}!\n\tOutput\n\t{1}\n\tErrors\n\t{2}",
                                 provider_path,
                                 output,
                                 errors);
            return null;
          }
          case CredentialProviderExitCode.Success_: {
            return JsonUtility.FromJson<CredentialProviderResponse>(json : output);
          }
          default: {
            Debug.LogWarningFormat("Unrecognized exit code {0} from {1} {2}",
                                   process.ExitCode,
                                   provider_path,
                                   process.StartInfo.Arguments);
            break;
          }
        }
      }

      if (download_if_missing) {
        DownloadCredentialProviders(feed_uri : feed_uri);
        return GetCredentialFromProvider_Uncached(feed_uri : feed_uri, false);
      }

      return null;
    }
  }
}