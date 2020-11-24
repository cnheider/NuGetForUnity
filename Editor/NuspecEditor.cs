namespace NuGetForUnity.Editor {
  using Enumerable = System.Linq.Enumerable;

  /// <summary>
  ///   Represents a custom editor inside the Unity editor that allows easy editting of a .nuspec file.
  /// </summary>
  public class NuspecEditor : UnityEditor.EditorWindow {
    /// <summary>
    ///   The API key used to verify an acceptable package being pushed to the server.
    /// </summary>
    string _api_key = string.Empty;

    /// <summary>
    ///   True if the dependencies list is expanded in the GUI.  False if it is collapsed.
    /// </summary>
    bool _dependencies_expanded = true;

    /// <summary>
    ///   The full filepath to the .nuspec file that is being edited.
    /// </summary>
    string _filepath;

    /// <summary>
    ///   The NuspecFile that was loaded from the .nuspec file.
    /// </summary>
    NuspecFile _nuspec;

    /// <summary>
    ///   Use the Unity GUI to draw the controls.
    /// </summary>
    protected void OnGUI() {
      if (this._nuspec == null) {
        this.Reload();
      }

      if (this._nuspec == null) {
        this.titleContent = new UnityEngine.GUIContent("[NO NUSPEC]");
        UnityEditor.EditorGUILayout.LabelField("There is no .nuspec file selected.");
      } else {
        UnityEditor.EditorGUIUtility.labelWidth = 100;
        this._nuspec.Id =
            UnityEditor.EditorGUILayout.TextField(label : new UnityEngine.GUIContent("ID",
                                                    "The name of the package."),
                                                  text : this._nuspec.Id);
        this._nuspec.Version =
            UnityEditor.EditorGUILayout.TextField(label : new UnityEngine.GUIContent("Version",
                                                    "The semantic version of the package."),
                                                  text : this._nuspec.Version);
        this._nuspec.Authors =
            UnityEditor.EditorGUILayout.TextField(label : new UnityEngine.GUIContent("Authors",
                                                    "The authors of the package."),
                                                  text : this._nuspec.Authors);
        this._nuspec.Owners =
            UnityEditor.EditorGUILayout.TextField(label : new UnityEngine.GUIContent("Owners",
                                                    "The owners of the package."),
                                                  text : this._nuspec.Owners);
        this._nuspec.LicenseUrl =
            UnityEditor.EditorGUILayout.TextField(label : new UnityEngine.GUIContent("License URL",
                                                    "The URL for the license of the package."),
                                                  text : this._nuspec.LicenseUrl);
        this._nuspec.ProjectUrl =
            UnityEditor.EditorGUILayout.TextField(label : new UnityEngine.GUIContent("Project URL",
                                                    "The URL of the package project."),
                                                  text : this._nuspec.ProjectUrl);
        this._nuspec.IconUrl =
            UnityEditor.EditorGUILayout.TextField(label : new UnityEngine.GUIContent("Icon URL",
                                                    "The URL for the icon of the package."),
                                                  text : this._nuspec.IconUrl);
        this._nuspec.RequireLicenseAcceptance =
            UnityEditor.EditorGUILayout.Toggle(label : new
                                                   UnityEngine.GUIContent("Require License Acceptance",
                                                     "Does the package license need to be accepted before use?"),
                                               value : this._nuspec.RequireLicenseAcceptance);
        this._nuspec.Description =
            UnityEditor.EditorGUILayout.TextField(label : new UnityEngine.GUIContent("Description",
                                                    "The description of the package."),
                                                  text : this._nuspec.Description);
        this._nuspec.Summary =
            UnityEditor.EditorGUILayout.TextField(label : new UnityEngine.GUIContent("Summary",
                                                    "The brief description of the package."),
                                                  text : this._nuspec.Summary);
        this._nuspec.ReleaseNotes =
            UnityEditor.EditorGUILayout.TextField(label : new UnityEngine.GUIContent("Release Notes",
                                                    "The release notes for this specific version of the package."),
                                                  text : this._nuspec.ReleaseNotes);
        this._nuspec.Copyright =
            UnityEditor.EditorGUILayout.TextField(label : new UnityEngine.GUIContent("Copyright",
                                                    "The copyright details for the package."),
                                                  text : this._nuspec.Copyright);
        this._nuspec.Tags =
            UnityEditor.EditorGUILayout.TextField(label : new UnityEngine.GUIContent("Tags",
                                                    "The space-delimited list of tags and keywords that describe the package and aid discoverability of packages through search and filtering."),
                                                  text : this._nuspec.Tags);

        this._dependencies_expanded = UnityEditor.EditorGUILayout.Foldout(foldout : this._dependencies_expanded,
                                                                        content : new
                                                                            UnityEngine.
                                                                            GUIContent("Dependencies",
                                                                              "The list of NuGet packages that this packages depends on."));

        if (this._dependencies_expanded) {
          UnityEditor.EditorGUILayout.BeginHorizontal();
          {
            UnityEngine.GUILayout.Space(50);

            // automatically fill in the dependencies based upon the "root" packages currently installed in the project
            if (UnityEngine.GUILayout.Button(content : new
                                                 UnityEngine.GUIContent("Automatically Fill Dependencies",
                                                                        "Populates the list of dependencies with the \"root\" NuGet packages currently installed in the project."))
            ) {
              NugetHelper.UpdateInstalledPackages();
              var installed_packages = Enumerable.ToList(NugetHelper.InstalledPackages);

              // default all packages to being roots
              var roots = new System.Collections.Generic.List<NugetPackage>(collection : installed_packages);

              // remove a package as a root if another package is dependent on it
              foreach (var package in installed_packages) {
                var package_framework_group =
                    NugetHelper.GetBestDependencyFrameworkGroupForCurrentSettings(package : package);
                foreach (var dependency in package_framework_group.Dependencies) {
                  roots.RemoveAll(p => p._Id == dependency._Id);
                }
              }

              // remove all existing dependencies from the .nuspec
              this._nuspec.Dependencies.Clear();

              this._nuspec.Dependencies.Add(item : new NugetFrameworkGroup());
              this._nuspec.Dependencies[0].Dependencies =
                  Enumerable.ToList(Enumerable.Cast<NugetPackageIdentifier>(roots));
            }
          }
          UnityEditor.EditorGUILayout.EndHorizontal();

          // display the dependencies
          NugetPackageIdentifier to_delete = null;
          var nuspec_framework_group =
              NugetHelper.GetBestDependencyFrameworkGroupForCurrentSettings(nuspec : this._nuspec);
          foreach (var dependency in nuspec_framework_group.Dependencies) {
            UnityEditor.EditorGUILayout.BeginHorizontal();
            UnityEngine.GUILayout.Space(75);
            var prev_label_width = UnityEditor.EditorGUIUtility.labelWidth;
            UnityEditor.EditorGUIUtility.labelWidth = 50;
            dependency._Id =
                UnityEditor.EditorGUILayout.TextField(label : new UnityEngine.GUIContent("ID",
                                                        "The ID of the dependency package."),
                                                      text : dependency._Id);
            UnityEditor.EditorGUILayout.EndHorizontal();

            //int oldSeletedIndex = IndexOf(ref existingComponents, dependency.Id);
            //int newSelectIndex = EditorGUILayout.Popup("Name", oldSeletedIndex, existingComponents);
            //if (oldSeletedIndex != newSelectIndex)
            //{
            //    dependency.Name = existingComponents[newSelectIndex];
            //}

            UnityEditor.EditorGUILayout.BeginHorizontal();
            UnityEngine.GUILayout.Space(75);
            dependency._Version =
                UnityEditor.EditorGUILayout.TextField(label : new UnityEngine.GUIContent("Version",
                                                        "The version number of the dependency package. (specify ranges with =><)"),
                                                      text : dependency._Version);
            UnityEditor.EditorGUILayout.EndHorizontal();

            UnityEditor.EditorGUILayout.BeginHorizontal();
            {
              UnityEngine.GUILayout.Space(75);

              if (UnityEngine.GUILayout.Button(text : "Remove " + dependency._Id)) {
                to_delete = dependency;
              }
            }
            UnityEditor.EditorGUILayout.EndHorizontal();

            UnityEditor.EditorGUILayout.Separator();

            UnityEditor.EditorGUIUtility.labelWidth = prev_label_width;
          }

          if (to_delete != null) {
            nuspec_framework_group.Dependencies.Remove(item : to_delete);
          }

          UnityEditor.EditorGUILayout.BeginHorizontal();
          {
            UnityEngine.GUILayout.Space(50);

            if (UnityEngine.GUILayout.Button("Add Dependency")) {
              nuspec_framework_group.Dependencies.Add(item : new NugetPackageIdentifier());
            }
          }
          UnityEditor.EditorGUILayout.EndHorizontal();
        }

        UnityEditor.EditorGUILayout.Separator();

        if (UnityEngine.GUILayout.Button(text : string.Format("Save {0}",
                                                              arg0 : System.IO.Path.GetFileName(path : this
                                                                  ._filepath)))) {
          this._nuspec.Save(file_path : this._filepath);
        }

        UnityEditor.EditorGUILayout.Separator();

        if (UnityEngine.GUILayout.Button(text : string.Format("Pack {0}.nupkg",
                                                              arg0 : System.IO.Path
                                                                  .GetFileNameWithoutExtension(path : this
                                                                      ._filepath)))) {
          NugetHelper.Pack(nuspec_file_path : this._filepath);
        }

        UnityEditor.EditorGUILayout.Separator();

        this._api_key =
            UnityEditor.EditorGUILayout.TextField(label : new UnityEngine.GUIContent("API Key",
                                                    "The API key to use when pushing the package to the server"),
                                                  text : this._api_key);

        if (UnityEngine.GUILayout.Button("Push to Server")) {
          NugetHelper.Push(nuspec : this._nuspec, nuspec_file_path : this._filepath, api_key : this._api_key);
        }
      }
    }

    /// <summary>
    ///   Called when enabling the window.
    /// </summary>
    void OnFocus() { this.Reload(); }

    /// <summary>
    ///   Reloads the .nuspec file when the selection changes.
    /// </summary>
    void OnSelectionChange() { this.Reload(); }

    /// <summary>
    ///   Creates a new MyPackage.nuspec file.
    /// </summary>
    [UnityEditor.MenuItem("Assets/NuGet/Create Nuspec File", false, 2000)]
    protected static void CreateNuspecFile() {
      var filepath = UnityEngine.Application.dataPath;

      if (UnityEditor.Selection.activeObject != null
          && UnityEditor.Selection.activeObject != UnityEditor.Selection.activeGameObject) {
        var selected_file =
            UnityEditor.AssetDatabase.GetAssetPath(assetObject : UnityEditor.Selection.activeObject);
        filepath = selected_file.Substring(startIndex : "Assets/".Length);
        filepath = System.IO.Path.Combine(path1 : UnityEngine.Application.dataPath, path2 : filepath);
      }

      if (!string.IsNullOrEmpty(value : System.IO.Path.GetExtension(path : filepath))) {
        // if it was a file that was selected, replace the filename
        filepath = filepath.Replace(oldValue : System.IO.Path.GetFileName(path : filepath),
                                    newValue : string.Empty);
        filepath += "MyPackage.nuspec";
      } else {
        // if it was a directory that was selected, simply add the filename
        filepath += "/MyPackage.nuspec";
      }

      UnityEngine.Debug.LogFormat("Creating: {0}", filepath);

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
      file.Save(file_path : filepath);

      UnityEditor.AssetDatabase.Refresh();

      // select the newly created .nuspec file
      var data_path =
          UnityEngine.Application.dataPath.Substring(0,
                                                     length : UnityEngine.Application.dataPath.Length
                                                              - "Assets".Length);
      UnityEditor.Selection.activeObject =
          UnityEditor.AssetDatabase.LoadMainAssetAtPath(assetPath : filepath.Replace(oldValue : data_path,
                                                          newValue : string.Empty));

      // automatically display the editor with the newly created .nuspec file
      DisplayNuspecEditor();
    }

    /// <summary>
    ///   Opens the .nuspec file editor.
    /// </summary>
    [UnityEditor.MenuItem("Assets/NuGet/Open Nuspec Editor", false, 2000)]
    protected static void DisplayNuspecEditor() {
      var nuspec_editor = GetWindow<NuspecEditor>();
      nuspec_editor.Reload();
    }

    /// <summary>
    ///   Validates the opening of the .nuspec file editor.
    /// </summary>
    [UnityEditor.MenuItem("Assets/NuGet/Open Nuspec Editor", true, 2000)]
    protected static bool DisplayNuspecEditorValidation() {
      var is_nuspec = false;

      var default_asset = UnityEditor.Selection.activeObject as UnityEditor.DefaultAsset;
      if (default_asset != null) {
        var filepath = UnityEditor.AssetDatabase.GetAssetPath(assetObject : default_asset);
        var data_path =
            UnityEngine.Application.dataPath.Substring(0,
                                                       length : UnityEngine.Application.dataPath.Length
                                                                - "Assets".Length);
        filepath = System.IO.Path.Combine(path1 : data_path, path2 : filepath);

        is_nuspec = System.IO.Path.GetExtension(path : filepath) == ".nuspec";
      }

      return is_nuspec;
    }

    /// <summary>
    ///   Reload the currently selected asset as a .nuspec file.
    /// </summary>
    protected void Reload() {
      var default_asset = UnityEditor.Selection.activeObject as UnityEditor.DefaultAsset;
      if (default_asset != null) {
        var asset_filepath = UnityEditor.AssetDatabase.GetAssetPath(assetObject : default_asset);
        var data_path =
            UnityEngine.Application.dataPath.Substring(0,
                                                       length : UnityEngine.Application.dataPath.Length
                                                                - "Assets".Length);
        asset_filepath = System.IO.Path.Combine(path1 : data_path, path2 : asset_filepath);

        var is_nuspec = System.IO.Path.GetExtension(path : asset_filepath) == ".nuspec";

        if (is_nuspec) {
          this._filepath = asset_filepath;
          this._nuspec = NuspecFile.Load(file_path : this._filepath);
          this.titleContent =
              new UnityEngine.GUIContent(text : System.IO.Path.GetFileNameWithoutExtension(path : this
                                             ._filepath));

          // force a repaint
          this.Repaint();
        }
      }
    }
  }
}