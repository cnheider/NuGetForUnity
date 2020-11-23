namespace NuGetForUnity.Editor {
  using System.Collections.Generic;
  using System.Linq;
  using UnityEditor;
  using UnityEngine;

  /// <summary>
  /// A viewer for all of the packages and their dependencies currently installed in the project.
  /// </summary>
  public class DependencyTreeViewer : EditorWindow {
    /// <summary>
    /// Opens the NuGet Package Manager Window.
    /// </summary>
    [MenuItem("NuGet/Show Dependency Tree", false, 5)]
    protected static void DisplayDependencyTree() { GetWindow<DependencyTreeViewer>(); }

    /// <summary>
    /// The titles of the tabs in the window.
    /// </summary>
    readonly string[] tabTitles = {"Dependency Tree", "Who Depends on Me?"};

    /// <summary>
    /// The currently selected tab in the window.
    /// </summary>
    int currentTab;

    int selectedPackageIndex = -1;

    /// <summary>
    /// The list of packages that depend on the specified package.
    /// </summary>
    List<NugetPackage> parentPackages = new List<NugetPackage>();

    /// <summary>
    /// The list of currently installed packages.
    /// </summary>
    List<NugetPackage> installedPackages;

    /// <summary>
    /// The array of currently installed package IDs.
    /// </summary>
    string[] installedPackageIds;

    Dictionary<NugetPackage, bool> expanded = new Dictionary<NugetPackage, bool>();

    List<NugetPackage> roots;

    Vector2 scrollPosition;

    /// <summary>
    /// Called when enabling the window.
    /// </summary>
    void OnEnable() {
      try {
        // reload the NuGet.config file, in case it was changed after Unity opened, but before the manager window opened (now)
        NugetHelper.LoadNugetConfigFile();

        // set the window title
        this.titleContent = new GUIContent("Dependencies");

        EditorUtility.DisplayProgressBar("Building Dependency Tree", "Reading installed packages...", 0.5f);

        NugetHelper.UpdateInstalledPackages();
        this.installedPackages = NugetHelper.InstalledPackages.ToList();
        var installedPackageNames = new List<string>();

        foreach (var package in this.installedPackages) {
          if (!this.expanded.ContainsKey(key : package)) {
            this.expanded.Add(key : package, false);
          } else {
            //Debug.LogErrorFormat("Expanded already contains {0} {1}", package.Id, package.Version);
          }

          installedPackageNames.Add(item : package._Id);
        }

        this.installedPackageIds = installedPackageNames.ToArray();

        this.BuildTree();
      } catch (System.Exception e) {
        Debug.LogErrorFormat("{0}", e.ToString());
      } finally {
        EditorUtility.ClearProgressBar();
      }
    }

    void BuildTree() {
      // default all packages to being roots
      this.roots = new List<NugetPackage>(collection : this.installedPackages);

      // remove a package as a root if another package is dependent on it
      foreach (var package in this.installedPackages) {
        var frameworkGroup = NugetHelper.GetBestDependencyFrameworkGroupForCurrentSettings(package : package);
        foreach (var dependency in frameworkGroup.Dependencies) {
          this.roots.RemoveAll(p => p._Id == dependency._Id);
        }
      }
    }

    /// <summary>
    /// Automatically called by Unity to draw the GUI.
    /// </summary>
    protected void OnGUI() {
      this.currentTab = GUILayout.Toolbar(selected : this.currentTab, texts : this.tabTitles);

      switch (this.currentTab) {
        case 0:
          this.scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition : this.scrollPosition);
          foreach (var package in this.roots) {
            this.DrawPackage(package : package);
          }

          EditorGUILayout.EndScrollView();
          break;
        case 1:
          EditorStyles.label.fontStyle = FontStyle.Bold;
          EditorStyles.label.fontSize = 14;
          EditorGUILayout.LabelField("Select Dependency:", GUILayout.Height(20));
          EditorStyles.label.fontStyle = FontStyle.Normal;
          EditorStyles.label.fontSize = 10;
          EditorGUI.indentLevel++;
          var newIndex = EditorGUILayout.Popup(selectedIndex : this.selectedPackageIndex,
                                               displayedOptions : this.installedPackageIds);
          EditorGUI.indentLevel--;

          if (newIndex != this.selectedPackageIndex) {
            this.selectedPackageIndex = newIndex;

            this.parentPackages.Clear();
            var selectedPackage = this.installedPackages[index : this.selectedPackageIndex];
            foreach (var package in this.installedPackages) {
              var frameworkGroup =
                  NugetHelper.GetBestDependencyFrameworkGroupForCurrentSettings(package : package);
              foreach (var dependency in frameworkGroup.Dependencies) {
                if (dependency._Id == selectedPackage._Id) {
                  this.parentPackages.Add(item : package);
                }
              }
            }
          }

          EditorGUILayout.Space();
          EditorStyles.label.fontStyle = FontStyle.Bold;
          EditorStyles.label.fontSize = 14;
          EditorGUILayout.LabelField("Packages That Depend on Above:", GUILayout.Height(20));
          EditorStyles.label.fontStyle = FontStyle.Normal;
          EditorStyles.label.fontSize = 10;

          this.scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition : this.scrollPosition);
          EditorGUI.indentLevel++;
          if (this.parentPackages.Count <= 0) {
            EditorGUILayout.LabelField("NONE");
          } else {
            foreach (var parent in this.parentPackages) {
              //EditorGUILayout.LabelField(string.Format("{0} {1}", parent.Id, parent.Version));
              this.DrawPackage(package : parent);
            }
          }

          EditorGUI.indentLevel--;
          EditorGUILayout.EndScrollView();
          break;
      }
    }

    void DrawDepencency(NugetPackageIdentifier dependency) {
      var fullDependency = this.installedPackages.Find(p => p._Id == dependency._Id);
      if (fullDependency != null) {
        this.DrawPackage(package : fullDependency);
      } else {
        Debug.LogErrorFormat("{0} {1} is not installed!", dependency._Id, dependency._Version);
      }
    }

    void DrawPackage(NugetPackage package) {
      if (package.Dependencies != null && package.Dependencies.Count > 0) {
        this.expanded[key : package] = EditorGUILayout.Foldout(foldout : this.expanded[key : package],
                                                               content : string.Format("{0} {1}",
                                                                 arg0 : package._Id,
                                                                 arg1 : package._Version));

        if (this.expanded[key : package]) {
          EditorGUI.indentLevel++;

          var frameworkGroup =
              NugetHelper.GetBestDependencyFrameworkGroupForCurrentSettings(package : package);
          foreach (var dependency in frameworkGroup.Dependencies) {
            this.DrawDepencency(dependency : dependency);
          }

          EditorGUI.indentLevel--;
        }
      } else {
        EditorGUILayout.LabelField(label : string.Format("{0} {1}",
                                                         arg0 : package._Id,
                                                         arg1 : package._Version));
      }
    }
  }
}