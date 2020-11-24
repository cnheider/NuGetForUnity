namespace NuGetForUnity.Editor {

  /// <inheritdoc />
  /// <summary>
  ///   Represents the NuGet Package Manager Window in the Unity Editor.
  /// </summary>
  public class NugetWindow : UnityEditor.EditorWindow {
    /// <summary>
    ///   True when the NugetWindow has initialized. This is used to skip time-consuming reloading operations when
    ///   the assembly is reloaded.
    /// </summary>
    [UnityEngine.SerializeField]
    bool hasRefreshed = false;

    /// <summary>
    ///   The list of NugetPackages available to install.
    /// </summary>
    [UnityEngine.SerializeField]
    System.Collections.Generic.List<NugetPackage> availablePackages =
        new System.Collections.Generic.List<NugetPackage>();

    /// <summary>
    ///   The list of package updates available, based on the already installed packages.
    /// </summary>
    [UnityEngine.SerializeField]
    System.Collections.Generic.List<NugetPackage> updatePackages =
        new System.Collections.Generic.List<NugetPackage>();

    /// <summary>
    ///   The number of packages to skip when requesting a list of packages from the server.  This is used to get a
    ///   new group of packages.
    /// </summary>
    [UnityEngine.SerializeField]
    int numberToSkip;

    /// <summary>
    ///   The default icon to display for packages.
    /// </summary>
    [UnityEngine.SerializeField]
    UnityEngine.Texture2D defaultIcon;

    /// <summary>
    ///   The titles of the tabs in the window.
    /// </summary>
    readonly string[] _tab_titles = {"Online", "Installed", "Updates"};

    /// <summary>
    ///   The currently selected tab in the window.
    /// </summary>
    int _current_tab;

    /// <summary>
    ///   The filtered list of package updates available.
    /// </summary>
    System.Collections.Generic.List<NugetPackage> _filtered_update_packages =
        new System.Collections.Generic.List<NugetPackage>();

    System.Collections.Generic.Dictionary<string, bool> _foldouts =
        new System.Collections.Generic.Dictionary<string, bool>();

    /// <summary>
    ///   The search term to search the installed packages for.
    /// </summary>
    string _installed_search_term = "Search";

    /// <summary>
    ///   The search term in progress while it is being typed into the search box.
    ///   We wait until the Enter key or Search button is pressed before searching in order
    ///   to match the way that the Online and Updates searches work.
    /// </summary>
    string _installed_search_term_edit_box = "Search";

    /// <summary>
    ///   The number of packages to get from the request to the server.
    /// </summary>
    int _number_to_get = 15;

    /// <summary>
    ///   The search term to search the online packages for.
    /// </summary>
    string _online_search_term = "Search";

    /// <summary>
    ///   Used to keep track of which packages the user has opened the clone window on.
    /// </summary>
    System.Collections.Generic.HashSet<NugetPackage> _open_clone_windows =
        new System.Collections.Generic.HashSet<NugetPackage>();

    /// <summary>
    ///   The current position of the scroll bar in the GUI.
    /// </summary>
    UnityEngine.Vector2 _scroll_position;

    /// <summary>
    ///   True to show all old package versions.  False to only show the latest version.
    /// </summary>
    bool _show_all_online_versions;

    /// <summary>
    ///   True to show all old package versions.  False to only show the latest version.
    /// </summary>
    bool _show_all_update_versions;

    /// <summary>
    ///   True to show beta and alpha package versions.  False to only show stable versions.
    /// </summary>
    bool _show_online_prerelease;

    /// <summary>
    ///   True to show beta and alpha package versions.  False to only show stable versions.
    /// </summary>
    bool _show_prerelease_updates;

    /// <summary>
    ///   The search term to search the update packages for.
    /// </summary>
    string _updates_search_term = "Search";

    System.Collections.Generic.IEnumerable<NugetPackage> FilteredInstalledPackages {
      get {
        if (this._installed_search_term == "Search") {
          return NugetHelper.InstalledPackages;
        }

        return System.Linq.Enumerable.ToList(source : System.Linq.Enumerable.Where(source : NugetHelper.InstalledPackages,
                                                                                   x => x._Id.ToLower()
                                                                                         .Contains(value : this._installed_search_term)
                                                                                        || x.Title.ToLower()
                                                                                            .Contains(value : this._installed_search_term)));
      }
    }

    /// <summary>
    ///   Called when enabling the window.
    /// </summary>
    void OnEnable() { this.Refresh(false); }

    /// <summary>
    ///   Automatically called by Unity to draw the GUI.
    /// </summary>
    protected void OnGUI() {
      var selected_tab =
          UnityEngine.GUILayout.Toolbar(selected : this._current_tab, texts : this._tab_titles);

      if (selected_tab != this._current_tab) {
        this.OnTabChanged();
      }

      this._current_tab = selected_tab;

      switch (this._current_tab) {
        case 0:
          this.DrawOnline();
          break;
        case 1:
          this.DrawInstalled();
          break;
        case 2:
          this.DrawUpdates();
          break;
      }
    }

    /// <summary>
    ///   Opens the NuGet Package Manager Window.
    /// </summary>
    [UnityEditor.MenuItem("NuGet/Open NuGet Official Site", false, 0)]
    protected static void OpenOfficialSite() {
      UnityEngine.Application.OpenURL("https://www.nuget.org/packages");
    }

    /// <summary>
    ///   Opens the NuGet Package Manager Window.
    /// </summary>
    [UnityEditor.MenuItem("NuGet/Manage NuGet Packages", false, 1)]
    protected static void DisplayNugetWindow() { GetWindow<NugetWindow>(); }

    /// <summary>
    ///   Restores all packages defined in packages.config
    /// </summary>
    [UnityEditor.MenuItem("NuGet/Restore Packages", false, 2)]
    protected static void RestorePackages() { NugetHelper.Restore(); }

    /// <summary>
    ///   Displays the version number of NuGetForUnity.
    /// </summary>
    [UnityEditor.MenuItem(itemName : "NuGet/Version " + NugetConstants._NuGetForUnityVersion, false, 10)]
    protected static void DisplayVersion() {
      // open the preferences window
      #if UNITY_2018_1_OR_NEWER
      UnityEditor.SettingsService.OpenUserPreferences("Preferences/NuGet For Unity");
      #else
            var assembly = System.Reflection.Assembly.GetAssembly(typeof(EditorWindow));
            var preferencesWindow = assembly.GetType("UnityEditor.PreferencesWindow");
            var preferencesWindowSection =
 assembly.GetType("UnityEditor.PreferencesWindow+Section"); // access nested class via + instead of .     

            EditorWindow preferencesEditorWindow =
 EditorWindow.GetWindowWithRect(preferencesWindow, new Rect(100f, 100f, 500f, 400f), true, "Unity Preferences");

            //preferencesEditorWindow.m_Parent.window.m_DontSaveToLayout = true; //<-- Unity's implementation also does this

            // Get the flag to see if custom sections have already been added
            var m_RefreshCustomPreferences =
 preferencesWindow.GetField("m_RefreshCustomPreferences", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            bool refesh = (bool)m_RefreshCustomPreferences.GetValue(preferencesEditorWindow);

            if (refesh)
            {
                // Invoke the AddCustomSections to load all user-specified preferences sections.  This normally isn't done until OnGUI, but we need to call it now to set the proper index
                var addCustomSections =
 preferencesWindow.GetMethod("AddCustomSections", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                addCustomSections.Invoke(preferencesEditorWindow, null);

                // Unity is dumb and doesn't set the flag for having loaded the custom sections INSIDE the AddCustomSections method!  So we must call it manually.
                m_RefreshCustomPreferences.SetValue(preferencesEditorWindow, false);
            }

            // get the List<PreferencesWindow.Section> m_Sections.Count
            var m_Sections =
 preferencesWindow.GetField("m_Sections", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            object list = m_Sections.GetValue(preferencesEditorWindow);
            var sectionList = typeof(List<>).MakeGenericType(new Type[] { preferencesWindowSection });
            var getCount = sectionList.GetProperty("Count").GetGetMethod(true);
            int count = (int)getCount.Invoke(list, null);
            //Debug.LogFormat("Count = {0}", count);

            // Figure out the index of the NuGet for Unity preferences
            var getItem = sectionList.GetMethod("get_Item");
            int nugetIndex = 0;
            for (int i = 0; i < count; i++)
            {
                var section = getItem.Invoke(list, new object[] { i });
                GUIContent content =
 (GUIContent)section.GetType().GetField("content", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).GetValue(section);
                if (content != null && content.text == "NuGet For Unity")
                {
                    nugetIndex = i;
                    break;
                }
            }
            //Debug.LogFormat("NuGet index = {0}", nugetIndex);

            // set the selected section index
            var selectedSectionIndex =
 preferencesWindow.GetProperty("selectedSectionIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var selectedSectionIndexSetter = selectedSectionIndex.GetSetMethod(true);
            selectedSectionIndexSetter.Invoke(preferencesEditorWindow, new object[] { nugetIndex });
            //var selectedSectionIndexGetter = selectedSectionIndex.GetGetMethod(true);
            //object index = selectedSectionIndexGetter.Invoke(preferencesEditorWindow, null);
            //Debug.LogFormat("Selected Index = {0}", index);
      #endif
    }

    /// <summary>
    ///   Checks/launches the Releases page to update NuGetForUnity with a new version.
    /// </summary>
    [UnityEditor.MenuItem("NuGet/Check for Updates...", false, 10)]
    protected static void CheckForUpdates() {
      const string url = "https://github.com/GlitchEnzo/NuGetForUnity/releases";
      #if UNITY_2017_1_OR_NEWER // UnityWebRequest is not available in Unity 5.2, which is the currently the earliest version supported by NuGetForUnity.
      using (var request = UnityEngine.Networking.UnityWebRequest.Get(uri : url)) {
        request.SendWebRequest();
        #else
            using (WWW request = new WWW(url))
            {
        #endif

        NugetHelper.LogVerbose("HTTP GET {0}", url);
        while (!request.isDone) {
          UnityEditor.EditorUtility.DisplayProgressBar("Checking updates", null, 0.0f);
        }

        UnityEditor.EditorUtility.ClearProgressBar();

        string latest_version = null;
        string latest_version_download_url = null;

        string response = null;
        #if UNITY_2017_1_OR_NEWER
        if (!request.isNetworkError && !request.isHttpError) {
          response = request.downloadHandler.text;
        }
        #else
                if (request.error == null)
                {
                    response = request.text;
                }
        #endif

        if (response != null) {
          latest_version =
              GetLatestVersonFromReleasesHtml(response : response, url : out latest_version_download_url);
        }

        if (latest_version == null) {
          UnityEditor.EditorUtility.DisplayDialog("Unable to Determine Updates",
                                                  message :
                                                  string.Format("Couldn't find release information at {0}.",
                                                                arg0 : url),
                                                  "OK");
          return;
        }

        var current =
            new NugetPackageIdentifier("NuGetForUnity", version : NugetConstants._NuGetForUnityVersion);
        var latest = new NugetPackageIdentifier("NuGetForUnity", version : latest_version);
        if (current >= latest) {
          UnityEditor.EditorUtility.DisplayDialog("No Updates Available",
                                                  message :
                                                  string
                                                      .Format("Your version of NuGetForUnity is up to date.\nVersion {0}.",
                                                              arg0 : NugetConstants._NuGetForUnityVersion),
                                                  "OK");
          return;
        }


        switch (UnityEditor.EditorUtility.DisplayDialogComplex("Update Available",
                                                               message :
                                                               string
                                                                   .Format("Current Version: {0}\nLatest Version: {1}",
                                                                     arg0 : NugetConstants
                                                                         ._NuGetForUnityVersion,
                                                                     arg1 : latest_version),
                                                               "Install Latest",
                                                               "Open Releases Page",
                                                               "Cancel")) {        // New version is available. Give user options for installing it.
          case 0:
            UnityEngine.Application.OpenURL(url : latest_version_download_url);
            break;
          case 1:
            UnityEngine.Application.OpenURL(url : url);
            break;
          case 2: break;
        }
      }
    }

    static string GetLatestVersonFromReleasesHtml(string response, out string url) {
      var href_regex =
          new
              System.Text.RegularExpressions.Regex(@"<a href=""(?<url>.*NuGetForUnity\.(?<version>\d+\.\d+\.\d+)\.unitypackage)""");
      var match = href_regex.Match(input : response);
      if (!match.Success) {
        url = null;
        return null;
      }

      url = "https://github.com/" + match.Groups["url"].Value;
      return match.Groups["version"].Value;
    }

    void Refresh(bool force_full_refresh) {
      var stopwatch = new System.Diagnostics.Stopwatch();
      stopwatch.Start();

      try {
        if (force_full_refresh) {
          NugetHelper.ClearCachedCredentials();
        }

        // reload the nuget.config file, in case it was changed after Unity opened, but before the manager window opened (now)
        NugetHelper.LoadNugetConfigFile();

        // if we are entering playmode, don't do anything
        if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) {
          return;
        }

        NugetHelper.LogVerbose(format : this.hasRefreshed
                                            ? "NugetWindow reloading config"
                                            : "NugetWindow reloading config and updating packages");

        // set the window title
        this.titleContent = new UnityEngine.GUIContent("NuGet");

        if (!this.hasRefreshed || force_full_refresh) {
          // reset the number to skip
          this.numberToSkip = 0;

          // TODO: Do we even need to load ALL of the data, or can we just get the Online tab packages?

          UnityEditor.EditorUtility.DisplayProgressBar("Opening NuGet",
                                                       "Fetching packages from server...",
                                                       0.3f);
          this.UpdateOnlinePackages();

          UnityEditor.EditorUtility.DisplayProgressBar("Opening NuGet",
                                                       "Getting installed packages...",
                                                       0.6f);
          NugetHelper.UpdateInstalledPackages();

          UnityEditor.EditorUtility.DisplayProgressBar("Opening NuGet", "Getting available updates...", 0.9f);
          this.UpdateUpdatePackages();

          // load the default icon from the Resources folder
          this.defaultIcon =
              (UnityEngine.Texture2D)UnityEngine.Resources.Load("defaultIcon",
                                                                systemTypeInstance :
                                                                typeof(UnityEngine.Texture2D));
        }

        this.hasRefreshed = true;
      } catch (System.Exception e) {
        UnityEngine.Debug.LogErrorFormat("{0}", e);
      } finally {
        UnityEditor.EditorUtility.ClearProgressBar();

        NugetHelper.LogVerbose("NugetWindow reloading took {0} ms", stopwatch.ElapsedMilliseconds);
      }
    }

    /// <summary>
    ///   Updates the list of available packages by running a search with the server using the currently set
    ///   parameters (# to get, # to skip, etc).
    /// </summary>
    void UpdateOnlinePackages() {
      this.availablePackages =
          NugetHelper.Search(search_term :
                             this._online_search_term != "Search" ? this._online_search_term : string.Empty,
                             include_all_versions : this._show_all_online_versions,
                             include_prerelease : this._show_online_prerelease,
                             number_to_get : this._number_to_get,
                             number_to_skip : this.numberToSkip);
    }

    /// <summary>
    ///   Updates the list of update packages.
    /// </summary>
    void UpdateUpdatePackages() {
      // get any available updates for the installed packages
      this.updatePackages = NugetHelper.GetUpdates(packages_to_update : NugetHelper.InstalledPackages,
                                                   include_prerelease : this._show_prerelease_updates,
                                                   include_all_versions : this._show_all_update_versions);
      this._filtered_update_packages = this.updatePackages;

      if (this._updates_search_term != "Search") {
        this._filtered_update_packages = System.Linq.Enumerable.ToList(source : System.Linq.Enumerable.Where(source : this.updatePackages,
                                                                                                             x => x._Id.ToLower()
                                                                                                                   .Contains(value : this
                                                                                                                                 ._updates_search_term)
                                                                                                                  || x.Title.ToLower()
                                                                                                                      .Contains(value : this
                                                                                                                                    ._updates_search_term)));
      }
    }

    /// <summary>
    ///   From here: http://forum.unity3d.com/threads/changing-the-background-color-for-beginhorizontal.66015/
    /// </summary>
    /// <param name="width"></param>
    /// <param name="height"></param>
    /// <param name="col"></param>
    /// <returns></returns>
    UnityEngine.Texture2D MakeTex(int width, int height, UnityEngine.Color col) {
      var pix = new UnityEngine.Color[width * height];

      for (var i = 0; i < pix.Length; i++) {
        pix[i] = col;
      }

      var result = new UnityEngine.Texture2D(width : width, height : height);
      result.SetPixels(colors : pix);
      result.Apply();

      return result;
    }

    void OnTabChanged() { this._open_clone_windows.Clear(); }

    /// <summary>
    ///   Creates a GUI style with a contrasting background color based upon if the Unity Editor is the free (light)
    ///   skin or the Pro (dark) skin.
    /// </summary>
    /// <returns>A GUI style with the appropriate background color set.</returns>
    UnityEngine.GUIStyle GetContrastStyle() {
      var style = new UnityEngine.GUIStyle();
      var background_color = UnityEditor.EditorGUIUtility.isProSkin
                                 ? new UnityEngine.Color(0.3f, 0.3f, 0.3f)
                                 : new UnityEngine.Color(0.6f, 0.6f, 0.6f);
      style.normal.background = this.MakeTex(16, 16, col : background_color);
      return style;
    }

    /// <summary>
    ///   Creates a GUI style with a background color the same as the editor's current background color.
    /// </summary>
    /// <returns>A GUI style with the appropriate background color set.</returns>
    UnityEngine.GUIStyle GetBackgroundStyle() {
      var style = new UnityEngine.GUIStyle();
      var background_color = UnityEditor.EditorGUIUtility.isProSkin
                                 ? new UnityEngine.Color32(56,
                                                           56,
                                                           56,
                                                           255)
                                 : new UnityEngine.Color32(194,
                                                           194,
                                                           194,
                                                           255);
      style.normal.background = this.MakeTex(16, 16, col : background_color);
      return style;
    }

    /// <summary>
    ///   Draws the list of installed packages that have updates available.
    /// </summary>
    void DrawUpdates() {
      this.DrawUpdatesHeader();

      // display all of the installed packages
      this._scroll_position =
          UnityEditor.EditorGUILayout.BeginScrollView(scrollPosition : this._scroll_position);
      UnityEditor.EditorGUILayout.BeginVertical();

      var style = this.GetContrastStyle();

      if (this._filtered_update_packages != null && this._filtered_update_packages.Count > 0) {
        this.DrawPackages(packages : this._filtered_update_packages);
      } else {
        UnityEditor.EditorStyles.label.fontStyle = UnityEngine.FontStyle.Bold;
        UnityEditor.EditorStyles.label.fontSize = 14;
        UnityEditor.EditorGUILayout.LabelField("There are no updates available!",
                                               UnityEngine.GUILayout.Height(20));
        UnityEditor.EditorStyles.label.fontSize = 10;
        UnityEditor.EditorStyles.label.fontStyle = UnityEngine.FontStyle.Normal;
      }

      UnityEditor.EditorGUILayout.EndVertical();
      UnityEditor.EditorGUILayout.EndScrollView();
    }

    /// <summary>
    ///   Draws the list of installed packages.
    /// </summary>
    void DrawInstalled() {
      this.DrawInstalledHeader();

      // display all of the installed packages
      this._scroll_position =
          UnityEditor.EditorGUILayout.BeginScrollView(scrollPosition : this._scroll_position);
      UnityEditor.EditorGUILayout.BeginVertical();

      var filtered_installed_packages = System.Linq.Enumerable.ToList(source : this.FilteredInstalledPackages);
      if (filtered_installed_packages != null && filtered_installed_packages.Count > 0) {
        this.DrawPackages(packages : filtered_installed_packages);
      } else {
        UnityEditor.EditorStyles.label.fontStyle = UnityEngine.FontStyle.Bold;
        UnityEditor.EditorStyles.label.fontSize = 14;
        UnityEditor.EditorGUILayout.LabelField("There are no packages installed!",
                                               UnityEngine.GUILayout.Height(20));
        UnityEditor.EditorStyles.label.fontSize = 10;
        UnityEditor.EditorStyles.label.fontStyle = UnityEngine.FontStyle.Normal;
      }

      UnityEditor.EditorGUILayout.EndVertical();
      UnityEditor.EditorGUILayout.EndScrollView();
    }

    /// <summary>
    ///   Draws the current list of available online packages.
    /// </summary>
    void DrawOnline() {
      this.DrawOnlineHeader();

      // display all of the packages
      this._scroll_position =
          UnityEditor.EditorGUILayout.BeginScrollView(scrollPosition : this._scroll_position);
      UnityEditor.EditorGUILayout.BeginVertical();

      if (this.availablePackages != null) {
        this.DrawPackages(packages : this.availablePackages);
      }

      var show_more_style = new UnityEngine.GUIStyle();
      if (UnityEngine.Application.HasProLicense()) {
        show_more_style.normal.background =
            this.MakeTex(20, 20, col : new UnityEngine.Color(0.05f, 0.05f, 0.05f));
      } else {
        show_more_style.normal.background =
            this.MakeTex(20, 20, col : new UnityEngine.Color(0.4f, 0.4f, 0.4f));
      }

      UnityEditor.EditorGUILayout.BeginVertical(style : show_more_style);
      // allow the user to dislay more results
      if (UnityEngine.GUILayout.Button("Show More", UnityEngine.GUILayout.Width(120))) {
        this.numberToSkip += this._number_to_get;
        this.availablePackages.AddRange(collection : NugetHelper.Search(search_term :
                                                                        this._online_search_term != "Search"
                                                                            ? this._online_search_term
                                                                            : string.Empty,
                                                                        include_all_versions :
                                                                        this._show_all_online_versions,
                                                                        include_prerelease : this
                                                                            ._show_online_prerelease,
                                                                        number_to_get : this._number_to_get,
                                                                        number_to_skip : this.numberToSkip));
      }

      UnityEditor.EditorGUILayout.EndVertical();

      UnityEditor.EditorGUILayout.EndVertical();
      UnityEditor.EditorGUILayout.EndScrollView();
    }

    void DrawPackages(System.Collections.Generic.List<NugetPackage> packages) {
      var background_style = this.GetBackgroundStyle();
      var contrast_style = this.GetContrastStyle();

      for (var i = 0; i < packages.Count; i++) {
        UnityEditor.EditorGUILayout.BeginVertical(style : background_style);
        this.DrawPackage(package : packages[index : i],
                         package_style : background_style,
                         contrast_style : contrast_style);
        UnityEditor.EditorGUILayout.EndVertical();

        // swap styles
        var temp_style = background_style;
        background_style = contrast_style;
        contrast_style = temp_style;
      }
    }

    /// <summary>
    ///   Draws the header which allows filtering the online list of packages.
    /// </summary>
    void DrawOnlineHeader() {
      var header_style = new UnityEngine.GUIStyle();
      if (UnityEngine.Application.HasProLicense()) {
        header_style.normal.background =
            this.MakeTex(20, 20, col : new UnityEngine.Color(0.05f, 0.05f, 0.05f));
      } else {
        header_style.normal.background = this.MakeTex(20, 20, col : new UnityEngine.Color(0.4f, 0.4f, 0.4f));
      }

      UnityEditor.EditorGUILayout.BeginVertical(style : header_style);
      {
        UnityEditor.EditorGUILayout.BeginHorizontal();
        {
          var show_all_versions_temp =
              UnityEditor.EditorGUILayout.Toggle("Show All Versions", value : this._show_all_online_versions);
          if (show_all_versions_temp != this._show_all_online_versions) {
            this._show_all_online_versions = show_all_versions_temp;
            this.UpdateOnlinePackages();
          }

          if (UnityEngine.GUILayout.Button("Refresh", UnityEngine.GUILayout.Width(60))) {
            this.Refresh(true);
          }
        }
        UnityEditor.EditorGUILayout.EndHorizontal();

        var show_prerelease_temp =
            UnityEditor.EditorGUILayout.Toggle("Show Prerelease", value : this._show_online_prerelease);
        if (show_prerelease_temp != this._show_online_prerelease) {
          this._show_online_prerelease = show_prerelease_temp;
          this.UpdateOnlinePackages();
        }

        var enter_pressed = UnityEngine.Event.current.Equals(obj : UnityEngine.Event.KeyboardEvent("return"));

        UnityEditor.EditorGUILayout.BeginHorizontal();
        {
          var old_font_size = UnityEngine.GUI.skin.textField.fontSize;
          UnityEngine.GUI.skin.textField.fontSize = 25;
          this._online_search_term =
              UnityEditor.EditorGUILayout.TextField(text : this._online_search_term,
                                                    UnityEngine.GUILayout.Height(30));

          if (UnityEngine.GUILayout.Button("Search",
                                           UnityEngine.GUILayout.Width(100),
                                           UnityEngine.GUILayout.Height(28))) {
            // the search button emulates the Enter key
            enter_pressed = true;
          }

          UnityEngine.GUI.skin.textField.fontSize = old_font_size;
        }
        UnityEditor.EditorGUILayout.EndHorizontal();

        // search only if the enter key is pressed
        if (enter_pressed) {
          // reset the number to skip
          this.numberToSkip = 0;
          this.UpdateOnlinePackages();
        }
      }
      UnityEditor.EditorGUILayout.EndVertical();
    }

    /// <summary>
    ///   Draws the header which allows filtering the installed list of packages.
    /// </summary>
    void DrawInstalledHeader() {
      var header_style = new UnityEngine.GUIStyle();
      if (UnityEngine.Application.HasProLicense()) {
        header_style.normal.background =
            this.MakeTex(20, 20, col : new UnityEngine.Color(0.05f, 0.05f, 0.05f));
      } else {
        header_style.normal.background = this.MakeTex(20, 20, col : new UnityEngine.Color(0.4f, 0.4f, 0.4f));
      }

      UnityEditor.EditorGUILayout.BeginVertical(style : header_style);
      {
        var enter_pressed = UnityEngine.Event.current.Equals(obj : UnityEngine.Event.KeyboardEvent("return"));

        UnityEditor.EditorGUILayout.BeginHorizontal();
        {
          var old_font_size = UnityEngine.GUI.skin.textField.fontSize;
          UnityEngine.GUI.skin.textField.fontSize = 25;
          this._installed_search_term_edit_box =
              UnityEditor.EditorGUILayout.TextField(text : this._installed_search_term_edit_box,
                                                    UnityEngine.GUILayout.Height(30));

          if (UnityEngine.GUILayout.Button("Search",
                                           UnityEngine.GUILayout.Width(100),
                                           UnityEngine.GUILayout.Height(28))) {
            // the search button emulates the Enter key
            enter_pressed = true;
          }

          UnityEngine.GUI.skin.textField.fontSize = old_font_size;
        }
        UnityEditor.EditorGUILayout.EndHorizontal();

        // search only if the enter key is pressed
        if (enter_pressed) {
          this._installed_search_term = this._installed_search_term_edit_box;
        }
      }
      UnityEditor.EditorGUILayout.EndVertical();
    }

    /// <summary>
    ///   Draws the header for the Updates tab.
    /// </summary>
    void DrawUpdatesHeader() {
      var header_style = new UnityEngine.GUIStyle();
      if (UnityEngine.Application.HasProLicense()) {
        header_style.normal.background =
            this.MakeTex(20, 20, col : new UnityEngine.Color(0.05f, 0.05f, 0.05f));
      } else {
        header_style.normal.background = this.MakeTex(20, 20, col : new UnityEngine.Color(0.4f, 0.4f, 0.4f));
      }

      UnityEditor.EditorGUILayout.BeginVertical(style : header_style);
      {
        UnityEditor.EditorGUILayout.BeginHorizontal();
        {
          var show_all_versions_temp =
              UnityEditor.EditorGUILayout.Toggle("Show All Versions", value : this._show_all_update_versions);
          if (show_all_versions_temp != this._show_all_update_versions) {
            this._show_all_update_versions = show_all_versions_temp;
            this.UpdateUpdatePackages();
          }

          if (UnityEngine.GUILayout.Button("Install All Updates", UnityEngine.GUILayout.Width(150))) {
            NugetHelper.UpdateAll(updates : this.updatePackages,
                                  packages_to_update : NugetHelper.InstalledPackages);
            NugetHelper.UpdateInstalledPackages();
            this.UpdateUpdatePackages();
          }

          if (UnityEngine.GUILayout.Button("Refresh", UnityEngine.GUILayout.Width(60))) {
            this.Refresh(true);
          }
        }
        UnityEditor.EditorGUILayout.EndHorizontal();

        var show_prerelease_temp =
            UnityEditor.EditorGUILayout.Toggle("Show Prerelease", value : this._show_prerelease_updates);
        if (show_prerelease_temp != this._show_prerelease_updates) {
          this._show_prerelease_updates = show_prerelease_temp;
          this.UpdateUpdatePackages();
        }

        var enter_pressed = UnityEngine.Event.current.Equals(obj : UnityEngine.Event.KeyboardEvent("return"));

        UnityEditor.EditorGUILayout.BeginHorizontal();
        {
          var old_font_size = UnityEngine.GUI.skin.textField.fontSize;
          UnityEngine.GUI.skin.textField.fontSize = 25;
          this._updates_search_term =
              UnityEditor.EditorGUILayout.TextField(text : this._updates_search_term,
                                                    UnityEngine.GUILayout.Height(30));

          if (UnityEngine.GUILayout.Button("Search",
                                           UnityEngine.GUILayout.Width(100),
                                           UnityEngine.GUILayout.Height(28))) {
            // the search button emulates the Enter key
            enter_pressed = true;
          }

          UnityEngine.GUI.skin.textField.fontSize = old_font_size;
        }
        UnityEditor.EditorGUILayout.EndHorizontal();

        // search only if the enter key is pressed
        if (enter_pressed) {
          if (this._updates_search_term != "Search") {
            this._filtered_update_packages = System.Linq.Enumerable.ToList(source : System.Linq.Enumerable.Where(source : this.updatePackages,
                                                                                                                 x => x._Id.ToLower()
                                                                                                                       .Contains(value : this
                                                                                                                                     ._updates_search_term)
                                                                                                                      || x.Title.ToLower()
                                                                                                                          .Contains(value : this
                                                                                                                                        ._updates_search_term)));
          }
        }
      }
      UnityEditor.EditorGUILayout.EndVertical();
    }

    /// <summary>
    ///   Draws the given <see cref="NugetPackage" />.
    /// </summary>
    /// <param name="package">The <see cref="NugetPackage" /> to draw.</param>
    void DrawPackage(NugetPackage package,
                     UnityEngine.GUIStyle package_style,
                     UnityEngine.GUIStyle contrast_style) {
      var installed_packages = NugetHelper.InstalledPackages;
      var installed = System.Linq.Enumerable.FirstOrDefault(source : installed_packages, p => p._Id == package._Id);

      UnityEditor.EditorGUILayout.BeginHorizontal();
      {
        // The Unity GUI system (in the Editor) is terrible.  This probably requires some explanation.
        // Every time you use a Horizontal block, Unity appears to divide the space evenly.
        // (i.e. 2 components have half of the window width, 3 components have a third of the window width, etc)
        // GUILayoutUtility.GetRect is SUPPOSED to return a rect with the given height and width, but in the GUI layout.  It doesn't.
        // We have to use GUILayoutUtility to get SOME rect properties, but then manually calculate others.
        UnityEditor.EditorGUILayout.BeginHorizontal();
        {
          const int icon_size = 32;
          var padding = UnityEditor.EditorStyles.label.padding.vertical;
          var rect = UnityEngine.GUILayoutUtility.GetRect(width : icon_size, height : icon_size);
          // only use GetRect's Y position.  It doesn't correctly set the width, height or X position.

          rect.x = padding;
          rect.y += padding;
          rect.width = icon_size;
          rect.height = icon_size;

          if (package.Icon != null) {
            UnityEngine.GUI.DrawTexture(position : rect,
                                        image : package.Icon,
                                        scaleMode : UnityEngine.ScaleMode.StretchToFill);
          } else {
            UnityEngine.GUI.DrawTexture(position : rect,
                                        image : this.defaultIcon,
                                        scaleMode : UnityEngine.ScaleMode.StretchToFill);
          }

          rect.x = icon_size + 2 * padding;
          rect.width = this.position.width / 2 - (icon_size + padding);
          rect.y -= padding; // This will leave the text aligned with the top of the image

          UnityEditor.EditorStyles.label.fontStyle = UnityEngine.FontStyle.Bold;
          UnityEditor.EditorStyles.label.fontSize = 16;

          var id_size =
              UnityEditor.EditorStyles.label.CalcSize(content :
                                                      new UnityEngine.GUIContent(text : package._Id));
          rect.y += icon_size / 2 - id_size.y / 2 + padding;
          UnityEngine.GUI.Label(position : rect, text : package._Id, style : UnityEditor.EditorStyles.label);
          rect.x += id_size.x;

          UnityEditor.EditorStyles.label.fontSize = 10;
          UnityEditor.EditorStyles.label.fontStyle = UnityEngine.FontStyle.Normal;

          var version_size =
              UnityEditor.EditorStyles.label.CalcSize(content : new UnityEngine.GUIContent(text : package
                                                          ._Version));
          rect.y += id_size.y - version_size.y - padding / 2;

          if (!string.IsNullOrEmpty(value : package.Authors)) {
            var author_label = string.Format("by {0}", arg0 : package.Authors);
            var size =
                UnityEditor.EditorStyles.label.CalcSize(content : new
                                                            UnityEngine.GUIContent(text : author_label));
            UnityEngine.GUI.Label(position : rect,
                                  text : author_label,
                                  style : UnityEditor.EditorStyles.label);
            rect.x += size.x;
          }

          if (package.DownloadCount > 0) {
            var download_label = string.Format("{0} downloads", arg0 : package.DownloadCount.ToString("#,#"));
            var size =
                UnityEditor.EditorStyles.label.CalcSize(content : new
                                                            UnityEngine.GUIContent(text : download_label));
            UnityEngine.GUI.Label(position : rect,
                                  text : download_label,
                                  style : UnityEditor.EditorStyles.label);
            rect.x += size.x;
          }
        }

        UnityEngine.GUILayout.FlexibleSpace();
        if (installed != null && installed._Version != package._Version) {
          UnityEngine.GUILayout.Label(text : string.Format("Current Version {0}", arg0 : installed._Version));
        }

        UnityEngine.GUILayout.Label(text : string.Format("Version {0}", arg0 : package._Version));

        if (System.Linq.Enumerable.Contains(source : installed_packages, value : package)) {
          // This specific version is installed
          if (UnityEngine.GUILayout.Button("Uninstall")) {
            // TODO: Perhaps use a "mark as dirty" system instead of updating all of the data all the time? 
            NugetHelper.Uninstall(package : package);
            NugetHelper.UpdateInstalledPackages();
            this.UpdateUpdatePackages();
          }
        } else {
          if (installed != null) {
            if (installed < package) {
              // An older version is installed
              if (UnityEngine.GUILayout.Button("Update")) {
                NugetHelper.Update(current_version : installed, new_version : package);
                NugetHelper.UpdateInstalledPackages();
                this.UpdateUpdatePackages();
              }
            } else if (installed > package) {
              // A newer version is installed
              if (UnityEngine.GUILayout.Button("Downgrade")) {
                NugetHelper.Update(current_version : installed, new_version : package);
                NugetHelper.UpdateInstalledPackages();
                this.UpdateUpdatePackages();
              }
            }
          } else {
            if (UnityEngine.GUILayout.Button("Install")) {
              NugetHelper.InstallIdentifier(package : package);
              UnityEditor.AssetDatabase.Refresh();
              NugetHelper.UpdateInstalledPackages();
              this.UpdateUpdatePackages();
            }
          }
        }

        UnityEditor.EditorGUILayout.EndHorizontal();
      }
      UnityEditor.EditorGUILayout.EndHorizontal();

      UnityEditor.EditorGUILayout.Space();
      UnityEditor.EditorGUILayout.BeginHorizontal();
      {
        UnityEditor.EditorGUILayout.BeginVertical();
        {
          // Show the package details
          UnityEditor.EditorStyles.label.wordWrap = true;
          UnityEditor.EditorStyles.label.fontStyle = UnityEngine.FontStyle.Normal;

          var summary = package.Summary;
          if (string.IsNullOrEmpty(value : summary)) {
            summary = package.Description;
          }

          if (!package.Title.Equals(value : package._Id,
                                    comparisonType : System.StringComparison.InvariantCultureIgnoreCase)) {
            summary = string.Format("{0} - {1}", arg0 : package.Title, arg1 : summary);
          }

          if (summary.Length >= 240) {
            summary = string.Format("{0}...", arg0 : summary.Substring(0, 237));
          }

          UnityEditor.EditorGUILayout.LabelField(label : summary);

          bool details_foldout;
          var details_foldout_id = string.Format("{0}.{1}", arg0 : package._Id, "Details");
          if (!this._foldouts.TryGetValue(key : details_foldout_id, value : out details_foldout)) {
            this._foldouts[key : details_foldout_id] = details_foldout;
          }

          details_foldout = UnityEditor.EditorGUILayout.Foldout(foldout : details_foldout, "Details");
          this._foldouts[key : details_foldout_id] = details_foldout;

          if (details_foldout) {
            UnityEditor.EditorGUI.indentLevel++;
            if (!string.IsNullOrEmpty(value : package.Description)) {
              UnityEditor.EditorGUILayout.LabelField("Description",
                                                     style : UnityEditor.EditorStyles.boldLabel);
              UnityEditor.EditorGUILayout.LabelField(label : package.Description);
            }

            if (!string.IsNullOrEmpty(value : package.ReleaseNotes)) {
              UnityEditor.EditorGUILayout.LabelField("Release Notes",
                                                     style : UnityEditor.EditorStyles.boldLabel);
              UnityEditor.EditorGUILayout.LabelField(label : package.ReleaseNotes);
            }

            // Show project URL link
            if (!string.IsNullOrEmpty(value : package.ProjectUrl)) {
              UnityEditor.EditorGUILayout.LabelField("Project Url",
                                                     style : UnityEditor.EditorStyles.boldLabel);
              GUILayoutLink(url : package.ProjectUrl);
              UnityEngine.GUILayout.Space(4f);
            }

            // Show the dependencies
            if (package.Dependencies.Count > 0) {
              UnityEditor.EditorStyles.label.wordWrap = true;
              UnityEditor.EditorStyles.label.fontStyle = UnityEngine.FontStyle.Italic;
              var builder = new System.Text.StringBuilder();

              var framework_group =
                  NugetHelper.GetBestDependencyFrameworkGroupForCurrentSettings(package : package);
              foreach (var dependency in framework_group.Dependencies) {
                builder.Append(value : string.Format(" {0} {1};",
                                                     arg0 : dependency._Id,
                                                     arg1 : dependency._Version));
              }

              UnityEditor.EditorGUILayout.Space();
              UnityEditor.EditorGUILayout.LabelField(label : string.Format("Depends on:{0}", arg0 : builder));
              UnityEditor.EditorStyles.label.fontStyle = UnityEngine.FontStyle.Normal;
            }

            // Create the style for putting a box around the 'Clone' button
            var clone_button_box_style = new UnityEngine.GUIStyle("box");
            clone_button_box_style.stretchWidth = false;
            clone_button_box_style.margin.top = 0;
            clone_button_box_style.margin.bottom = 0;
            clone_button_box_style.padding.bottom = 4;

            var normal_button_box_style = new UnityEngine.GUIStyle(other : clone_button_box_style);
            normal_button_box_style.normal.background = package_style.normal.background;

            var show_clone_window = this._open_clone_windows.Contains(item : package);
            clone_button_box_style.normal.background =
                show_clone_window ? contrast_style.normal.background : package_style.normal.background;

            // Create a simillar style for the 'Clone' window
            var clone_window_style = new UnityEngine.GUIStyle(other : clone_button_box_style);
            clone_window_style.padding = new UnityEngine.RectOffset(6,
                                                                    6,
                                                                    2,
                                                                    6);

            // Show button bar
            UnityEditor.EditorGUILayout.BeginHorizontal();
            {
              if (package.RepositoryType == RepositoryType.Git
                  || package.RepositoryType == RepositoryType.TfsGit) {
                if (!string.IsNullOrEmpty(value : package.RepositoryUrl)) {
                  UnityEditor.EditorGUILayout.BeginHorizontal(style : clone_button_box_style);
                  {
                    var clone_button_style = new UnityEngine.GUIStyle(other : UnityEngine.GUI.skin.button);
                    clone_button_style.normal =
                        show_clone_window ? clone_button_style.active : clone_button_style.normal;
                    if (UnityEngine.GUILayout.Button("Clone",
                                                     style : clone_button_style,
                                                     UnityEngine.GUILayout.ExpandWidth(false))) {
                      show_clone_window = !show_clone_window;
                    }

                    if (show_clone_window) {
                      this._open_clone_windows.Add(item : package);
                    } else {
                      this._open_clone_windows.Remove(item : package);
                    }
                  }
                  UnityEditor.EditorGUILayout.EndHorizontal();
                }
              }

              if (!string.IsNullOrEmpty(value : package.LicenseUrl)
                  && package.LicenseUrl != "http://your_license_url_here") {
                // Creaete a box around the license button to keep it alligned with Clone button
                UnityEditor.EditorGUILayout.BeginHorizontal(style : normal_button_box_style);
                // Show the license button
                if (UnityEngine.GUILayout.Button("View License", UnityEngine.GUILayout.ExpandWidth(false))) {
                  UnityEngine.Application.OpenURL(url : package.LicenseUrl);
                }

                UnityEditor.EditorGUILayout.EndHorizontal();
              }
            }
            UnityEditor.EditorGUILayout.EndHorizontal();

            if (show_clone_window) {
              UnityEditor.EditorGUILayout.BeginVertical(style : clone_window_style);
              {
                // Clone latest label
                UnityEditor.EditorGUILayout.BeginHorizontal();
                UnityEngine.GUILayout.Space(20f);
                UnityEditor.EditorGUILayout.LabelField("clone latest");
                UnityEditor.EditorGUILayout.EndHorizontal();

                // Clone latest row
                UnityEditor.EditorGUILayout.BeginHorizontal();
                {
                  if (UnityEngine.GUILayout.Button("Copy", UnityEngine.GUILayout.ExpandWidth(false))) {
                    UnityEngine.GUI.FocusControl(name : package._Id + package._Version + "repoUrl");
                    UnityEngine.GUIUtility.systemCopyBuffer = package.RepositoryUrl;
                  }

                  UnityEngine.GUI.SetNextControlName(name : package._Id + package._Version + "repoUrl");
                  UnityEditor.EditorGUILayout.TextField(text : package.RepositoryUrl);
                }
                UnityEditor.EditorGUILayout.EndHorizontal();

                // Clone @ commit label
                UnityEngine.GUILayout.Space(4f);
                UnityEditor.EditorGUILayout.BeginHorizontal();
                UnityEngine.GUILayout.Space(20f);
                UnityEditor.EditorGUILayout.LabelField("clone @ commit");
                UnityEditor.EditorGUILayout.EndHorizontal();

                // Clone @ commit row
                UnityEditor.EditorGUILayout.BeginHorizontal();
                {
                  // Create the three commands a user will need to run to get the repo @ the commit. Intentionally leave off the last newline for better UI appearance
                  var commands = string.Format("git clone {0} {1} --no-checkout{2}cd {1}{2}git checkout {3}",
                                               package.RepositoryUrl,
                                               package._Id,
                                               System.Environment.NewLine,
                                               package.RepositoryCommit);

                  if (UnityEngine.GUILayout.Button("Copy", UnityEngine.GUILayout.ExpandWidth(false))) {
                    UnityEngine.GUI.FocusControl(name : package._Id + package._Version + "commands");

                    // Add a newline so the last command will execute when pasted to the CL
                    UnityEngine.GUIUtility.systemCopyBuffer = commands + System.Environment.NewLine;
                  }

                  UnityEditor.EditorGUILayout.BeginVertical();
                  UnityEngine.GUI.SetNextControlName(name : package._Id + package._Version + "commands");
                  UnityEditor.EditorGUILayout.TextArea(text : commands);
                  UnityEditor.EditorGUILayout.EndVertical();
                }
                UnityEditor.EditorGUILayout.EndHorizontal();
              }
              UnityEditor.EditorGUILayout.EndVertical();
            }

            UnityEditor.EditorGUI.indentLevel--;
          }

          UnityEditor.EditorGUILayout.Separator();
          UnityEditor.EditorGUILayout.Separator();
        }
        UnityEditor.EditorGUILayout.EndVertical();
      }
      UnityEditor.EditorGUILayout.EndHorizontal();
    }

    public static void GUILayoutLink(string url) {
      var hyper_link_style = new UnityEngine.GUIStyle(other : UnityEngine.GUI.skin.label);
      hyper_link_style.stretchWidth = false;
      hyper_link_style.richText = true;

      var color_format_string = "<color=#add8e6ff>{0}</color>";

      var underline = new string('_', count : url.Length);

      var formatted_url = string.Format(format : color_format_string, arg0 : url);
      var formatted_underline = string.Format(format : color_format_string, arg0 : underline);
      var url_rect =
          UnityEngine.GUILayoutUtility.GetRect(content : new UnityEngine.GUIContent(text : url),
                                               style : hyper_link_style);

      // Update rect for indentation
      {
        var indented_url_rect = UnityEditor.EditorGUI.IndentedRect(source : url_rect);
        var delta = indented_url_rect.x - url_rect.x;
        indented_url_rect.width += delta;
        url_rect = indented_url_rect;
      }

      UnityEngine.GUI.Label(position : url_rect, text : formatted_url, style : hyper_link_style);
      UnityEngine.GUI.Label(position : url_rect, text : formatted_underline, style : hyper_link_style);

      UnityEditor.EditorGUIUtility.AddCursorRect(position : url_rect, mouse : UnityEditor.MouseCursor.Link);
      if (url_rect.Contains(point : UnityEngine.Event.current.mousePosition)) {
        if (UnityEngine.Event.current.type == UnityEngine.EventType.MouseUp) {
          UnityEngine.Application.OpenURL(url : url);
        }
      }
    }
  }
}