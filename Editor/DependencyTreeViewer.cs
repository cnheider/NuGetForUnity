namespace NuGetForUnity.Editor {
  using Enumerable = System.Linq.Enumerable;

  /// <inheritdoc />
  /// <summary>
  ///   A viewer for all of the packages and their dependencies currently installed in the project.
  /// </summary>
  public class DependencyTreeViewer : UnityEditor.EditorWindow {
    /// <summary>
    ///   The titles of the tabs in the window.
    /// </summary>
    readonly string[] _tab_titles = {"Dependency Tree", "Who Depends on Me?"};

    /// <summary>
    ///   The currently selected tab in the window.
    /// </summary>
    int _current_tab;

    System.Collections.Generic.Dictionary<NugetPackage, bool> _expanded =
        new System.Collections.Generic.Dictionary<NugetPackage, bool>();

    /// <summary>
    ///   The array of currently installed package IDs.
    /// </summary>
    string[] _installed_package_ids;

    /// <summary>
    ///   The list of currently installed packages.
    /// </summary>
    System.Collections.Generic.List<NugetPackage> _installed_packages;

    /// <summary>
    ///   The list of packages that depend on the specified package.
    /// </summary>
    System.Collections.Generic.List<NugetPackage> _parent_packages =
        new System.Collections.Generic.List<NugetPackage>();

    System.Collections.Generic.List<NugetPackage> _roots;

    UnityEngine.Vector2 _scroll_position;

    int _selected_package_index = -1;

    /// <summary>
    ///   Called when enabling the window.
    /// </summary>
    void OnEnable() {
      try {
        // reload the nuget.config file, in case it was changed after Unity opened, but before the manager window opened (now)
        NugetHelper.LoadNugetConfigFile();

        // set the window title
        this.titleContent = new UnityEngine.GUIContent("Dependencies");

        UnityEditor.EditorUtility.DisplayProgressBar("Building Dependency Tree",
                                                     "Reading installed packages...",
                                                     0.5f);

        NugetHelper.UpdateInstalledPackages();
        this._installed_packages = Enumerable.ToList(NugetHelper.InstalledPackages);
        var installed_package_names = new System.Collections.Generic.List<string>();

        foreach (var package in this._installed_packages) {
          if (!this._expanded.ContainsKey(key : package)) {
            this._expanded.Add(key : package, false);
          }

          installed_package_names.Add(item : package._Id);
        }

        this._installed_package_ids = installed_package_names.ToArray();

        this.BuildTree();
      } catch (System.Exception e) {
        UnityEngine.Debug.LogErrorFormat("{0}", e);
      } finally {
        UnityEditor.EditorUtility.ClearProgressBar();
      }
    }

    /// <summary>
    ///   Automatically called by Unity to draw the GUI.
    /// </summary>
    protected void OnGUI() {
      this._current_tab = UnityEngine.GUILayout.Toolbar(selected : this._current_tab, texts : this._tab_titles);

      switch (this._current_tab) {
        case 0:
          this._scroll_position =
              UnityEditor.EditorGUILayout.BeginScrollView(scrollPosition : this._scroll_position);
          foreach (var package in this._roots) {
            this.DrawPackage(package : package);
          }

          UnityEditor.EditorGUILayout.EndScrollView();
          break;
        case 1:
          UnityEditor.EditorStyles.label.fontStyle = UnityEngine.FontStyle.Bold;
          UnityEditor.EditorStyles.label.fontSize = 14;
          UnityEditor.EditorGUILayout.LabelField("Select Dependency:", UnityEngine.GUILayout.Height(20));
          UnityEditor.EditorStyles.label.fontStyle = UnityEngine.FontStyle.Normal;
          UnityEditor.EditorStyles.label.fontSize = 10;
          UnityEditor.EditorGUI.indentLevel++;
          var new_index = UnityEditor.EditorGUILayout.Popup(selectedIndex : this._selected_package_index,
                                                           displayedOptions : this._installed_package_ids);
          UnityEditor.EditorGUI.indentLevel--;

          if (new_index != this._selected_package_index) {
            this._selected_package_index = new_index;

            this._parent_packages.Clear();
            var selected_package = this._installed_packages[index : this._selected_package_index];
            foreach (var package in this._installed_packages) {
              var framework_group =
                  NugetHelper.GetBestDependencyFrameworkGroupForCurrentSettings(package : package);
              foreach (var dependency in framework_group.Dependencies) {
                if (dependency._Id == selected_package._Id) {
                  this._parent_packages.Add(item : package);
                }
              }
            }
          }

          UnityEditor.EditorGUILayout.Space();
          UnityEditor.EditorStyles.label.fontStyle = UnityEngine.FontStyle.Bold;
          UnityEditor.EditorStyles.label.fontSize = 14;
          UnityEditor.EditorGUILayout.LabelField("Packages That Depend on Above:",
                                                 UnityEngine.GUILayout.Height(20));
          UnityEditor.EditorStyles.label.fontStyle = UnityEngine.FontStyle.Normal;
          UnityEditor.EditorStyles.label.fontSize = 10;

          this._scroll_position =
              UnityEditor.EditorGUILayout.BeginScrollView(scrollPosition : this._scroll_position);
          UnityEditor.EditorGUI.indentLevel++;
          if (this._parent_packages.Count <= 0) {
            UnityEditor.EditorGUILayout.LabelField("NONE");
          } else {
            foreach (var parent in this._parent_packages) {
              //EditorGUILayout.LabelField(string.Format("{0} {1}", parent.Id, parent.Version));
              this.DrawPackage(package : parent);
            }
          }

          UnityEditor.EditorGUI.indentLevel--;
          UnityEditor.EditorGUILayout.EndScrollView();
          break;
      }
    }

    /// <summary>
    ///   Opens the NuGet Package Manager Window.
    /// </summary>
    [UnityEditor.MenuItem("NuGet/Show Dependency Tree", false, 5)]
    protected static void DisplayDependencyTree() { GetWindow<DependencyTreeViewer>(); }

    void BuildTree() {
      // default all packages to being roots
      this._roots = new System.Collections.Generic.List<NugetPackage>(collection : this._installed_packages);

      // remove a package as a root if another package is dependent on it
      foreach (var package in this._installed_packages) {
        var framework_group = NugetHelper.GetBestDependencyFrameworkGroupForCurrentSettings(package : package);
        foreach (var dependency in framework_group.Dependencies) {
          this._roots.RemoveAll(p => p._Id == dependency._Id);
        }
      }
    }

    void DrawDepencency(NugetPackageIdentifier dependency) {
      var full_dependency = this._installed_packages.Find(p => p._Id == dependency._Id);
      if (full_dependency != null) {
        this.DrawPackage(package : full_dependency);
      } else {
        UnityEngine.Debug.LogErrorFormat("{0} {1} is not installed!", dependency._Id, dependency._Version);
      }
    }

    void DrawPackage(NugetPackage package) {
      if (package.Dependencies != null && package.Dependencies.Count > 0) {
        this._expanded[key : package] =
            UnityEditor.EditorGUILayout.Foldout(foldout : this._expanded[key : package],
                                                content : string.Format("{0} {1}",
                                                                        arg0 : package._Id,
                                                                        arg1 : package._Version));

        if (this._expanded[key : package]) {
          UnityEditor.EditorGUI.indentLevel++;

          var framework_group =
              NugetHelper.GetBestDependencyFrameworkGroupForCurrentSettings(package : package);
          foreach (var dependency in framework_group.Dependencies) {
            this.DrawDepencency(dependency : dependency);
          }

          UnityEditor.EditorGUI.indentLevel--;
        }
      } else {
        UnityEditor.EditorGUILayout.LabelField(label : string.Format("{0} {1}",
                                                                     arg0 : package._Id,
                                                                     arg1 : package._Version));
      }
    }
  }
}