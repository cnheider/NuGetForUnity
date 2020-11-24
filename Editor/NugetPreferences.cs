#pragma warning disable 618

namespace NuGetForUnity.Editor {
  using UnityEditor;
  using UnityEngine;

  /// <summary>
  /// Handles the displaying, editing, and saving of the preferences for NuGet For Unity.
  /// </summary>
  public static class NugetPreferences {
    /// <summary>
    /// The current version of NuGet for Unity.
    /// </summary>
    public const string _NuGetForUnityVersion = "3.0.0";

    /// <summary>
    /// The current position of the scroll bar in the GUI.
    /// </summary>
    static Vector2 _scroll_position;

    public static string _Base_Path = "NuGetForUnity";
    static bool _prefs_loaded = false;

    /// <summary>
    /// Draws the preferences GUI inside the Unity preferences window in the Editor.
    /// </summary>
    [PreferenceItem("NuGet For Unity")]
    public static void PreferencesGUI() {
      if (!_prefs_loaded) {
        if (EditorPrefs.HasKey("NugetProjectBasePath")) {
          _Base_Path = EditorPrefs.GetString("NugetProjectBasePath", defaultValue : _Base_Path);
        }

        _prefs_loaded = true;
      }

      if (NugetHelper.NugetConfigFile == null) {
        NugetHelper.LoadNugetConfigFile();
      }
      
      var preferences_changed_this_frame = false;

      EditorGUILayout.LabelField(label : $"Version: {_NuGetForUnityVersion}");

      EditorGUILayout.LabelField("Project Base Path:");
      _Base_Path = EditorGUILayout.TextField(text : _Base_Path).Trim();


      if (NugetHelper.NugetConfigFile != null) {
        var install_from_cache =
            EditorGUILayout.Toggle("Install From the Cache",
                                   value : NugetHelper.NugetConfigFile.InstallFromCache);
        if (install_from_cache != NugetHelper.NugetConfigFile.InstallFromCache) {
          preferences_changed_this_frame = true;
          NugetHelper.NugetConfigFile.InstallFromCache = install_from_cache;
        }

        var read_only_package_files = EditorGUILayout.Toggle("Read-Only Package Files",
                                                             value : NugetHelper.NugetConfigFile
                                                                 .ReadOnlyPackageFiles);
        if (read_only_package_files != NugetHelper.NugetConfigFile.ReadOnlyPackageFiles) {
          preferences_changed_this_frame = true;
          NugetHelper.NugetConfigFile.ReadOnlyPackageFiles = read_only_package_files;
        }

        var verbose =
            EditorGUILayout.Toggle("Use Verbose Logging", value : NugetHelper.NugetConfigFile.Verbose);
        if (verbose != NugetHelper.NugetConfigFile.Verbose) {
          preferences_changed_this_frame = true;
          NugetHelper.NugetConfigFile.Verbose = verbose;
        }



        EditorGUILayout.LabelField("Package Sources:");
        _scroll_position = EditorGUILayout.BeginScrollView(scrollPosition : _scroll_position);
        {
          NugetPackageSource source_to_move_up = null;
          NugetPackageSource source_to_move_down = null;
          NugetPackageSource source_to_remove = null;

          foreach (var source in NugetHelper.NugetConfigFile.PackageSources) {
            EditorGUILayout.BeginVertical();
            {
              EditorGUILayout.BeginHorizontal();
              {
                EditorGUILayout.BeginVertical(GUILayout.Width(20));
                {
                  GUILayout.Space(10);
                  var is_enabled = EditorGUILayout.Toggle(value : source.IsEnabled, GUILayout.Width(20));
                  if (is_enabled != source.IsEnabled) {
                    preferences_changed_this_frame = true;
                    source.IsEnabled = is_enabled;
                  }
                }
                EditorGUILayout.EndVertical();

                EditorGUILayout.BeginVertical();
                {
                  var name = EditorGUILayout.TextField(text : source.Name);
                  if (name != source.Name) {
                    preferences_changed_this_frame = true;
                    source.Name = name;
                  }

                  var saved_path = EditorGUILayout.TextField(text : source.SavedPath).Trim();
                  if (saved_path != source.SavedPath) {
                    preferences_changed_this_frame = true;
                    source.SavedPath = saved_path;
                  }
                }
                EditorGUILayout.EndVertical();
              }
              EditorGUILayout.EndHorizontal();

              EditorGUILayout.BeginHorizontal();
              {
                GUILayout.Space(29);
                EditorGUIUtility.labelWidth = 75;
                EditorGUILayout.BeginVertical();

                var has_password = EditorGUILayout.Toggle("Credentials", value : source.HasPassword);
                if (has_password != source.HasPassword) {
                  preferences_changed_this_frame = true;
                  source.HasPassword = has_password;
                }

                if (source.HasPassword) {
                  var user_name = EditorGUILayout.TextField("User Name", text : source.UserName);
                  if (user_name != source.UserName) {
                    preferences_changed_this_frame = true;
                    source.UserName = user_name;
                  }

                  var saved_password =
                      EditorGUILayout.PasswordField("Password", password : source.SavedPassword);
                  if (saved_password != source.SavedPassword) {
                    preferences_changed_this_frame = true;
                    source.SavedPassword = saved_password;
                  }
                } else {
                  source.UserName = null;
                }

                EditorGUIUtility.labelWidth = 0;
                EditorGUILayout.EndVertical();
              }
              EditorGUILayout.EndHorizontal();

              EditorGUILayout.BeginHorizontal();
              {
                if (GUILayout.Button(text : "Move Up")) {
                  source_to_move_up = source;
                }

                if (GUILayout.Button(text : "Move Down")) {
                  source_to_move_down = source;
                }

                if (GUILayout.Button(text : "Remove")) {
                  source_to_remove = source;
                }
              }
              EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
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

          if (GUILayout.Button(text : "Add New Source")) {
            NugetHelper.NugetConfigFile.PackageSources.Add(item : new NugetPackageSource("New Source",
                                                             "source_path"));
            preferences_changed_this_frame = true;
          }

          EditorGUILayout.EndScrollView();

          if (GUILayout.Button(text : "Reset To Default")) {
            NugetConfigFile.CreateDefaultFile(file_path : NugetHelper.NugetConfigFilePath);
            NugetHelper.LoadNugetConfigFile();
            preferences_changed_this_frame = true;
          }

          if (preferences_changed_this_frame) {
            NugetHelper.NugetConfigFile.Save(filepath : NugetHelper.NugetConfigFilePath);
          }
        }
      }

      if (GUI.changed) {
        EditorPrefs.SetString("NugetProjectBasePath", _Base_Path);
      }
    }
  }
}