#pragma warning disable 618

namespace NuGetForUnity.Editor {
  /// <summary>
  ///   Handles the displaying, editing, and saving of the preferences for NuGet For Unity.
  /// </summary>
  public static class NugetPreferences {
    /// <summary>
    ///   The current position of the scroll bar in the GUI.
    /// </summary>
    static UnityEngine.Vector2 _scroll_position;

    public static string _Base_Path = "NuGetForUnity";
    static bool _prefs_loaded = false;

    /// <summary>
    ///   Draws the preferences GUI inside the Unity preferences window in the Editor.
    /// </summary>
    [UnityEditor.PreferenceItem("NuGet For Unity")]
    public static void PreferencesGUI() {
      if (!_prefs_loaded) {
        if (UnityEditor.EditorPrefs.HasKey("NugetProjectBasePath")) {
          _Base_Path = UnityEditor.EditorPrefs.GetString("NugetProjectBasePath", defaultValue : _Base_Path);
        }

        _prefs_loaded = true;
      }

      if (NugetHelper.NugetConfigFile == null) {
        NugetHelper.LoadNugetConfigFile();
      }

      var preferences_changed_this_frame = false;

      UnityEditor.EditorGUILayout.LabelField(label : $"Version: {NugetConstants._NuGetForUnityVersion}");

      UnityEditor.EditorGUILayout.LabelField("Project Base Path:");
      _Base_Path = UnityEditor.EditorGUILayout.TextField(text : _Base_Path).Trim();

      if (NugetHelper.NugetConfigFile != null) {
        var install_from_cache =
            UnityEditor.EditorGUILayout.Toggle("Install From the Cache",
                                               value : NugetHelper.NugetConfigFile.InstallFromCache);
        if (install_from_cache != NugetHelper.NugetConfigFile.InstallFromCache) {
          preferences_changed_this_frame = true;
          NugetHelper.NugetConfigFile.InstallFromCache = install_from_cache;
        }

        var read_only_package_files = UnityEditor.EditorGUILayout.Toggle("Read-Only Package Files",
                                                                           value : NugetHelper.NugetConfigFile
                                                                               .ReadOnlyPackageFiles);
        if (read_only_package_files != NugetHelper.NugetConfigFile.ReadOnlyPackageFiles) {
          preferences_changed_this_frame = true;
          NugetHelper.NugetConfigFile.ReadOnlyPackageFiles = read_only_package_files;
        }

        var verbose =
            UnityEditor.EditorGUILayout.Toggle("Use Verbose Logging",
                                               value : NugetHelper.NugetConfigFile.Verbose);
        if (verbose != NugetHelper.NugetConfigFile.Verbose) {
          preferences_changed_this_frame = true;
          NugetHelper.NugetConfigFile.Verbose = verbose;
        }

        UnityEditor.EditorGUILayout.LabelField("Package Sources:");
        _scroll_position = UnityEditor.EditorGUILayout.BeginScrollView(scrollPosition : _scroll_position);
        {
          NugetPackageSource source_to_move_up = null;
          NugetPackageSource source_to_move_down = null;
          NugetPackageSource source_to_remove = null;

          foreach (var source in NugetHelper.NugetConfigFile.PackageSources) {
            UnityEditor.EditorGUILayout.BeginVertical();
            {
              UnityEditor.EditorGUILayout.BeginHorizontal();
              {
                UnityEditor.EditorGUILayout.BeginVertical(UnityEngine.GUILayout.Width(20));
                {
                  UnityEngine.GUILayout.Space(10);
                  var is_enabled =
                      UnityEditor.EditorGUILayout.Toggle(value : source.IsEnabled,
                                                         UnityEngine.GUILayout.Width(20));
                  if (is_enabled != source.IsEnabled) {
                    preferences_changed_this_frame = true;
                    source.IsEnabled = is_enabled;
                  }
                }
                UnityEditor.EditorGUILayout.EndVertical();

                UnityEditor.EditorGUILayout.BeginVertical();
                {
                  var name = UnityEditor.EditorGUILayout.TextField(text : source.Name);
                  if (name != source.Name) {
                    preferences_changed_this_frame = true;
                    source.Name = name;
                  }

                  var saved_path = UnityEditor.EditorGUILayout.TextField(text : source.SavedPath).Trim();
                  if (saved_path != source.SavedPath) {
                    preferences_changed_this_frame = true;
                    source.SavedPath = saved_path;
                  }
                }
                UnityEditor.EditorGUILayout.EndVertical();
              }
              UnityEditor.EditorGUILayout.EndHorizontal();

              UnityEditor.EditorGUILayout.BeginHorizontal();
              {
                UnityEngine.GUILayout.Space(29);
                UnityEditor.EditorGUIUtility.labelWidth = 75;
                UnityEditor.EditorGUILayout.BeginVertical();

                var has_password =
                    UnityEditor.EditorGUILayout.Toggle("Credentials", value : source.HasPassword);
                if (has_password != source.HasPassword) {
                  preferences_changed_this_frame = true;
                  source.HasPassword = has_password;
                }

                if (source.HasPassword) {
                  var user_name = UnityEditor.EditorGUILayout.TextField("User Name", text : source.UserName);
                  if (user_name != source.UserName) {
                    preferences_changed_this_frame = true;
                    source.UserName = user_name;
                  }

                  var saved_password =
                      UnityEditor.EditorGUILayout.PasswordField("Password", password : source.SavedPassword);
                  if (saved_password != source.SavedPassword) {
                    preferences_changed_this_frame = true;
                    source.SavedPassword = saved_password;
                  }
                } else {
                  source.UserName = null;
                }

                UnityEditor.EditorGUIUtility.labelWidth = 0;
                UnityEditor.EditorGUILayout.EndVertical();
              }
              UnityEditor.EditorGUILayout.EndHorizontal();

              UnityEditor.EditorGUILayout.BeginHorizontal();
              {
                if (UnityEngine.GUILayout.Button("Move Up")) {
                  source_to_move_up = source;
                }

                if (UnityEngine.GUILayout.Button("Move Down")) {
                  source_to_move_down = source;
                }

                if (UnityEngine.GUILayout.Button("Remove")) {
                  source_to_remove = source;
                }
              }
              UnityEditor.EditorGUILayout.EndHorizontal();
            }
            UnityEditor.EditorGUILayout.EndVertical();
          }

          if (source_to_move_up != null) {
            var index = NugetHelper.NugetConfigFile.PackageSources.IndexOf(item : source_to_move_up);
            if (index > 0) {
              NugetHelper.NugetConfigFile.PackageSources[index : index] =
                  NugetHelper.NugetConfigFile.PackageSources[index : index - 1];
              NugetHelper.NugetConfigFile.PackageSources[index : index - 1] = source_to_move_up;
            }

            preferences_changed_this_frame = true;
          }

          if (source_to_move_down != null) {
            var index = NugetHelper.NugetConfigFile.PackageSources.IndexOf(item : source_to_move_down);
            if (index < NugetHelper.NugetConfigFile.PackageSources.Count - 1) {
              NugetHelper.NugetConfigFile.PackageSources[index : index] =
                  NugetHelper.NugetConfigFile.PackageSources[index : index + 1];
              NugetHelper.NugetConfigFile.PackageSources[index : index + 1] = source_to_move_down;
            }

            preferences_changed_this_frame = true;
          }

          if (source_to_remove != null) {
            NugetHelper.NugetConfigFile.PackageSources.Remove(item : source_to_remove);
            preferences_changed_this_frame = true;
          }

          if (UnityEngine.GUILayout.Button("Add New Source")) {
            NugetHelper.NugetConfigFile.PackageSources.Add(item : new NugetPackageSource("New Source",
                                                             "source_path"));
            preferences_changed_this_frame = true;
          }

          UnityEditor.EditorGUILayout.EndScrollView();

          if (UnityEngine.GUILayout.Button("Reset To Default")) {
            NugetConfigFile.CreateDefaultFile(file_path : NugetHelper.NugetConfigFilePath);
            NugetHelper.LoadNugetConfigFile();
            preferences_changed_this_frame = true;
          }

          if (preferences_changed_this_frame) {
            NugetHelper.NugetConfigFile.Save(filepath : NugetHelper.NugetConfigFilePath);
          }
        }
      }

      if (UnityEngine.GUI.changed) {
        UnityEditor.EditorPrefs.SetString("NugetProjectBasePath", value : _Base_Path);
      }
    }
  }
}