namespace NuGetForUnity.Editor {
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using UnityEditor;
  using UnityEngine;

  /// <summary>
  /// Represents a custom editor inside the Unity editor that allows easy editting of a .nuspec file.
  /// </summary>
  public class NuspecEditor : EditorWindow {
    /// <summary>
    /// The full filepath to the .nuspec file that is being edited.
    /// </summary>
    string filepath;

    /// <summary>
    /// The NuspecFile that was loaded from the .nuspec file.
    /// </summary>
    NuspecFile nuspec;

    /// <summary>
    /// True if the dependencies list is expanded in the GUI.  False if it is collapsed.
    /// </summary>
    bool dependenciesExpanded = true;

    /// <summary>
    /// The API key used to verify an acceptable package being pushed to the server.
    /// </summary>
    string apiKey = string.Empty;

    /// <summary>
    /// Creates a new MyPackage.nuspec file.
    /// </summary>
    [MenuItem("Assets/NuGet/Create Nuspec File", false, 2000)]
    protected static void CreateNuspecFile() {
      var filepath = Application.dataPath;

      if (Selection.activeObject != null && Selection.activeObject != Selection.activeGameObject) {
        var selectedFile = AssetDatabase.GetAssetPath(assetObject : Selection.activeObject);
        filepath = selectedFile.Substring(startIndex : "Assets/".Length);
        filepath = Path.Combine(path1 : Application.dataPath, path2 : filepath);
      }

      if (!string.IsNullOrEmpty(value : Path.GetExtension(path : filepath))) {
        // if it was a file that was selected, replace the filename
        filepath = filepath.Replace(oldValue : Path.GetFileName(path : filepath), newValue : string.Empty);
        filepath += "MyPackage.nuspec";
      } else {
        // if it was a directory that was selected, simply add the filename
        filepath += "/MyPackage.nuspec";
      }

      Debug.LogFormat("Creating: {0}", filepath);

      var file = new NuspecFile();
      file.Id = "MyPackage";
      file.Version = "0.0.1";
      file.Authors = "Your Name";
      file.Owners = "Your Name";
      file.LicenseUrl = "http://your_license_url_here";
      file.ProjectUrl = "http://your_project_url_here";
      file.Description = "A description of what this package is and does.";
      file.Summary = "A brief description of what this package is and does.";
      file.ReleaseNotes = "Notes for this specific release";
      file.Copyright = "Copyright 2017";
      file.IconUrl = "https://www.nuget.org/Content/Images/packageDefaultIcon-50x50.png";
      file.Save(filePath : filepath);

      AssetDatabase.Refresh();

      // select the newly created .nuspec file
      var dataPath =
          Application.dataPath.Substring(0, length : Application.dataPath.Length - "Assets".Length);
      Selection.activeObject =
          AssetDatabase.LoadMainAssetAtPath(assetPath : filepath.Replace(oldValue : dataPath,
                                                                           newValue : string.Empty));

      // automatically display the editor with the newly created .nuspec file
      DisplayNuspecEditor();
    }

    /// <summary>
    /// Opens the .nuspec file editor.
    /// </summary>
    [MenuItem("Assets/NuGet/Open Nuspec Editor", false, 2000)]
    protected static void DisplayNuspecEditor() {
      var nuspecEditor = GetWindow<NuspecEditor>();
      nuspecEditor.Reload();
    }

    /// <summary>
    /// Validates the opening of the .nuspec file editor.
    /// </summary>
    [MenuItem("Assets/NuGet/Open Nuspec Editor", true, 2000)]
    protected static bool DisplayNuspecEditorValidation() {
      var isNuspec = false;

      var defaultAsset = Selection.activeObject as DefaultAsset;
      if (defaultAsset != null) {
        var filepath = AssetDatabase.GetAssetPath(assetObject : defaultAsset);
        var dataPath =
            Application.dataPath.Substring(0, length : Application.dataPath.Length - "Assets".Length);
        filepath = Path.Combine(path1 : dataPath, path2 : filepath);

        isNuspec = Path.GetExtension(path : filepath) == ".nuspec";
      }

      return isNuspec;
    }

    /// <summary>
    /// Called when enabling the window.
    /// </summary>
    void OnFocus() { this.Reload(); }

    /// <summary>
    /// Reloads the .nuspec file when the selection changes.
    /// </summary>
    void OnSelectionChange() { this.Reload(); }

    /// <summary>
    /// Reload the currently selected asset as a .nuspec file.
    /// </summary>
    protected void Reload() {
      var defaultAsset = Selection.activeObject as DefaultAsset;
      if (defaultAsset != null) {
        var assetFilepath = AssetDatabase.GetAssetPath(assetObject : defaultAsset);
        var dataPath =
            Application.dataPath.Substring(0, length : Application.dataPath.Length - "Assets".Length);
        assetFilepath = Path.Combine(path1 : dataPath, path2 : assetFilepath);

        var isNuspec = Path.GetExtension(path : assetFilepath) == ".nuspec";

        if (isNuspec) {
          this.filepath = assetFilepath;
          this.nuspec = NuspecFile.Load(filePath : this.filepath);
          this.titleContent = new GUIContent(text : Path.GetFileNameWithoutExtension(path : this.filepath));

          // force a repaint
          this.Repaint();
        }
      }
    }

    /// <summary>
    /// Use the Unity GUI to draw the controls.
    /// </summary>
    protected void OnGUI() {
      if (this.nuspec == null) {
        this.Reload();
      }

      if (this.nuspec == null) {
        this.titleContent = new GUIContent("[NO NUSPEC]");
        EditorGUILayout.LabelField("There is no .nuspec file selected.");
      } else {
        EditorGUIUtility.labelWidth = 100;
        this.nuspec.Id = EditorGUILayout.TextField(label : new GUIContent("ID", "The name of the package."),
                                                   text : this.nuspec.Id);
        this.nuspec.Version =
            EditorGUILayout.TextField(label : new GUIContent("Version",
                                                             "The semantic version of the package."),
                                      text : this.nuspec.Version);
        this.nuspec.Authors =
            EditorGUILayout.TextField(label : new GUIContent("Authors", "The authors of the package."),
                                      text : this.nuspec.Authors);
        this.nuspec.Owners =
            EditorGUILayout.TextField(label : new GUIContent("Owners", "The owners of the package."),
                                      text : this.nuspec.Owners);
        this.nuspec.LicenseUrl =
            EditorGUILayout.TextField(label : new GUIContent("License URL",
                                                             "The URL for the license of the package."),
                                      text : this.nuspec.LicenseUrl);
        this.nuspec.ProjectUrl =
            EditorGUILayout.TextField(label : new GUIContent("Project URL",
                                                             "The URL of the package project."),
                                      text : this.nuspec.ProjectUrl);
        this.nuspec.IconUrl =
            EditorGUILayout.TextField(label : new GUIContent("Icon URL",
                                                             "The URL for the icon of the package."),
                                      text : this.nuspec.IconUrl);
        this.nuspec.RequireLicenseAcceptance =
            EditorGUILayout.Toggle(label : new GUIContent("Require License Acceptance",
                                                          "Does the package license need to be accepted before use?"),
                                   value : this.nuspec.RequireLicenseAcceptance);
        this.nuspec.Description =
            EditorGUILayout.TextField(label : new GUIContent("Description",
                                                             "The description of the package."),
                                      text : this.nuspec.Description);
        this.nuspec.Summary =
            EditorGUILayout.TextField(label : new GUIContent("Summary",
                                                             "The brief description of the package."),
                                      text : this.nuspec.Summary);
        this.nuspec.ReleaseNotes =
            EditorGUILayout.TextField(label : new GUIContent("Release Notes",
                                                             "The release notes for this specific version of the package."),
                                      text : this.nuspec.ReleaseNotes);
        this.nuspec.Copyright =
            EditorGUILayout.TextField(label : new GUIContent("Copyright",
                                                             "The copyright details for the package."),
                                      text : this.nuspec.Copyright);
        this.nuspec.Tags =
            EditorGUILayout.TextField(label : new GUIContent("Tags",
                                                             "The space-delimited list of tags and keywords that describe the package and aid discoverability of packages through search and filtering."),
                                      text : this.nuspec.Tags);

        this.dependenciesExpanded = EditorGUILayout.Foldout(foldout : this.dependenciesExpanded,
                                                            content : new GUIContent("Dependencies",
                                                              "The list of NuGet packages that this packages depends on."));

        if (this.dependenciesExpanded) {
          EditorGUILayout.BeginHorizontal();
          {
            GUILayout.Space(50);

            // automatically fill in the dependencies based upon the "root" packages currently installed in the project
            if (GUILayout.Button(content : new GUIContent("Automatically Fill Dependencies",
                                                          "Populates the list of dependencies with the \"root\" NuGet packages currently installed in the project."))
            ) {
              NugetHelper.UpdateInstalledPackages();
              var installedPackages = NugetHelper.InstalledPackages.ToList();

              // default all packages to being roots
              var roots = new List<NugetPackage>(collection : installedPackages);

              // remove a package as a root if another package is dependent on it
              foreach (var package in installedPackages) {
                var packageFrameworkGroup =
                    NugetHelper.GetBestDependencyFrameworkGroupForCurrentSettings(package : package);
                foreach (var dependency in packageFrameworkGroup.Dependencies) {
                  roots.RemoveAll(p => p._Id == dependency._Id);
                }
              }

              // remove all existing dependencies from the .nuspec
              this.nuspec.Dependencies.Clear();

              this.nuspec.Dependencies.Add(item : new NugetFrameworkGroup());
              this.nuspec.Dependencies[0].Dependencies = roots.Cast<NugetPackageIdentifier>().ToList();
            }
          }
          EditorGUILayout.EndHorizontal();

          // display the dependencies
          NugetPackageIdentifier toDelete = null;
          var nuspecFrameworkGroup =
              NugetHelper.GetBestDependencyFrameworkGroupForCurrentSettings(nuspec : this.nuspec);
          foreach (var dependency in nuspecFrameworkGroup.Dependencies) {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(75);
            var prevLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 50;
            dependency._Id =
                EditorGUILayout.TextField(label : new GUIContent("ID", "The ID of the dependency package."),
                                          text : dependency._Id);
            EditorGUILayout.EndHorizontal();

            //int oldSeletedIndex = IndexOf(ref existingComponents, dependency.Id);
            //int newSelectIndex = EditorGUILayout.Popup("Name", oldSeletedIndex, existingComponents);
            //if (oldSeletedIndex != newSelectIndex)
            //{
            //    dependency.Name = existingComponents[newSelectIndex];
            //}

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(75);
            dependency._Version =
                EditorGUILayout.TextField(label : new GUIContent("Version",
                                                                 "The version number of the dependency package. (specify ranges with =><)"),
                                          text : dependency._Version);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            {
              GUILayout.Space(75);

              if (GUILayout.Button(text : "Remove " + dependency._Id)) {
                toDelete = dependency;
              }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Separator();

            EditorGUIUtility.labelWidth = prevLabelWidth;
          }

          if (toDelete != null) {
            nuspecFrameworkGroup.Dependencies.Remove(item : toDelete);
          }

          EditorGUILayout.BeginHorizontal();
          {
            GUILayout.Space(50);

            if (GUILayout.Button("Add Dependency")) {
              nuspecFrameworkGroup.Dependencies.Add(item : new NugetPackageIdentifier());
            }
          }
          EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Separator();

        if (GUILayout.Button(text : string.Format("Save {0}", arg0 : Path.GetFileName(path : this.filepath)))
        ) {
          this.nuspec.Save(filePath : this.filepath);
        }

        EditorGUILayout.Separator();

        if (GUILayout.Button(text : string.Format("Pack {0}.nupkg",
                                                  arg0 : Path
                                                      .GetFileNameWithoutExtension(path : this.filepath)))) {
          NugetHelper.Pack(nuspec_file_path : this.filepath);
        }

        EditorGUILayout.Separator();

        this.apiKey =
            EditorGUILayout.TextField(label : new GUIContent("API Key",
                                                             "The API key to use when pushing the package to the server"),
                                      text : this.apiKey);

        if (GUILayout.Button(text : "Push to Server")) {
          NugetHelper.Push(nuspec : this.nuspec, nuspec_file_path : this.filepath, api_key : this.apiKey);
        }
      }
    }
  }
}