namespace NuGetForUnity.Editor {
  using System;
  using System.Collections.Generic;
  using System.Diagnostics;
  using System.Linq;
  using System.Text;
  using System.Text.RegularExpressions;
  using UnityEditor;
  using UnityEngine;
  using UnityEngine.Networking;

  /// <summary>
  /// Represents the NuGet Package Manager Window in the Unity Editor.
  /// </summary>
  public class NugetWindow : EditorWindow {
    /// <summary>
    /// True when the NugetWindow has initialized. This is used to skip time-consuming reloading operations when the assembly is reloaded.
    /// </summary>
    [SerializeField]
    bool hasRefreshed = false;

    /// <summary>
    /// The current position of the scroll bar in the GUI.
    /// </summary>
    Vector2 scrollPosition;

    /// <summary>
    /// The list of NugetPackages available to install.
    /// </summary>
    [SerializeField]
    List<NugetPackage> availablePackages = new List<NugetPackage>();

    /// <summary>
    /// The list of package updates available, based on the already installed packages.
    /// </summary>
    [SerializeField]
    List<NugetPackage> updatePackages = new List<NugetPackage>();

    /// <summary>
    /// The filtered list of package updates available.
    /// </summary>
    List<NugetPackage> filteredUpdatePackages = new List<NugetPackage>();

    /// <summary>
    /// True to show all old package versions.  False to only show the latest version.
    /// </summary>
    bool showAllOnlineVersions;

    /// <summary>
    /// True to show beta and alpha package versions.  False to only show stable versions.
    /// </summary>
    bool showOnlinePrerelease;

    /// <summary>
    /// True to show all old package versions.  False to only show the latest version.
    /// </summary>
    bool showAllUpdateVersions;

    /// <summary>
    /// True to show beta and alpha package versions.  False to only show stable versions.
    /// </summary>
    bool showPrereleaseUpdates;

    /// <summary>
    /// The search term to search the online packages for.
    /// </summary>
    string onlineSearchTerm = "Search";

    /// <summary>
    /// The search term to search the installed packages for.
    /// </summary>
    string installedSearchTerm = "Search";

    /// <summary>
    /// The search term in progress while it is being typed into the search box.
    /// We wait until the Enter key or Search button is pressed before searching in order
    /// to match the way that the Online and Updates searches work.
    /// </summary>
    string installedSearchTermEditBox = "Search";

    /// <summary>
    /// The search term to search the update packages for.
    /// </summary>
    string updatesSearchTerm = "Search";

    /// <summary>
    /// The number of packages to get from the request to the server.
    /// </summary>
    int numberToGet = 15;

    /// <summary>
    /// The number of packages to skip when requesting a list of packages from the server.  This is used to get a new group of packages.
    /// </summary>
    [SerializeField]
    int numberToSkip;

    /// <summary>
    /// The currently selected tab in the window.
    /// </summary>
    int currentTab;

    /// <summary>
    /// The titles of the tabs in the window.
    /// </summary>
    readonly string[] tabTitles = {"Online", "Installed", "Updates"};

    /// <summary>
    /// The default icon to display for packages.
    /// </summary>
    [SerializeField]
    Texture2D defaultIcon;

    /// <summary>
    /// Used to keep track of which packages the user has opened the clone window on.
    /// </summary>
    HashSet<NugetPackage> openCloneWindows = new HashSet<NugetPackage>();

    IEnumerable<NugetPackage> FilteredInstalledPackages {
      get {
        if (this.installedSearchTerm == "Search")
          return NugetHelper.InstalledPackages;

        return NugetHelper.InstalledPackages
                          .Where(x => x._Id.ToLower().Contains(value : this.installedSearchTerm)
                                      || x.Title.ToLower().Contains(value : this.installedSearchTerm))
                          .ToList();
      }
    }

    /// <summary>
    /// Opens the NuGet Package Manager Window.
    /// </summary>
    [MenuItem("NuGet/Open NuGet Official Site", false, 0)]
    protected static void OpenOfficialSite() { Application.OpenURL("https://www.nuget.org/packages"); }

    /// <summary>
    /// Opens the NuGet Package Manager Window.
    /// </summary>
    [MenuItem("NuGet/Manage NuGet Packages", false, 1)]
    protected static void DisplayNugetWindow() { GetWindow<NugetWindow>(); }

    /// <summary>
    /// Restores all packages defined in packages.config
    /// </summary>
    [MenuItem("NuGet/Restore Packages", false, 2)]
    protected static void RestorePackages() { NugetHelper.Restore(); }

    /// <summary>
    /// Displays the version number of NuGetForUnity.
    /// </summary>
    [MenuItem(itemName : "NuGet/Version " + NugetPreferences._NuGetForUnityVersion, false, 10)]
    protected static void DisplayVersion() {
      // open the preferences window
      #if UNITY_2018_1_OR_NEWER
      SettingsService.OpenUserPreferences("Preferences/NuGet For Unity");
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
    /// Checks/launches the Releases page to update NuGetForUnity with a new version.
    /// </summary>
    [MenuItem("NuGet/Check for Updates...", false, 10)]
    protected static void CheckForUpdates() {
      const string url = "https://github.com/GlitchEnzo/NuGetForUnity/releases";
      #if UNITY_2017_1_OR_NEWER // UnityWebRequest is not available in Unity 5.2, which is the currently the earliest version supported by NuGetForUnity.
      using (var request = UnityWebRequest.Get(uri : url)) {
        request.SendWebRequest();
        #else
            using (WWW request = new WWW(url))
            {
        #endif

        NugetHelper.LogVerbose("HTTP GET {0}", url);
        while (!request.isDone) {
          EditorUtility.DisplayProgressBar("Checking updates", null, 0.0f);
        }

        EditorUtility.ClearProgressBar();

        string latestVersion = null;
        string latestVersionDownloadUrl = null;

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
          latestVersion =
              GetLatestVersonFromReleasesHtml(response : response, url : out latestVersionDownloadUrl);
        }

        if (latestVersion == null) {
          EditorUtility.DisplayDialog("Unable to Determine Updates",
                                      message : string.Format("Couldn't find release information at {0}.",
                                                              arg0 : url),
                                      "OK");
          return;
        }

        var current =
            new NugetPackageIdentifier("NuGetForUnity", version : NugetPreferences._NuGetForUnityVersion);
        var latest = new NugetPackageIdentifier("NuGetForUnity", version : latestVersion);
        if (current >= latest) {
          EditorUtility.DisplayDialog("No Updates Available",
                                      message :
                                      string
                                          .Format("Your version of NuGetForUnity is up to date.\nVersion {0}.",
                                                  arg0 : NugetPreferences._NuGetForUnityVersion),
                                      "OK");
          return;
        }

        // New version is available. Give user options for installing it.
        switch (EditorUtility.DisplayDialogComplex("Update Available",
                                                   message :
                                                   string.Format("Current Version: {0}\nLatest Version: {1}",
                                                                 arg0 : NugetPreferences._NuGetForUnityVersion,
                                                                 arg1 : latestVersion),
                                                   "Install Latest",
                                                   "Open Releases Page",
                                                   "Cancel")) {
          case 0:
            Application.OpenURL(url : latestVersionDownloadUrl);
            break;
          case 1:
            Application.OpenURL(url : url);
            break;
          case 2: break;
        }
      }
    }

    static string GetLatestVersonFromReleasesHtml(string response, out string url) {
      var hrefRegex =
          new Regex(@"<a href=""(?<url>.*NuGetForUnity\.(?<version>\d+\.\d+\.\d+)\.unitypackage)""");
      var match = hrefRegex.Match(input : response);
      if (!match.Success) {
        url = null;
        return null;
      }

      url = "https://github.com/" + match.Groups["url"].Value;
      return match.Groups["version"].Value;
    }

    /// <summary>
    /// Called when enabling the window.
    /// </summary>
    void OnEnable() { this.Refresh(false); }

    void Refresh(bool forceFullRefresh) {
      var stopwatch = new Stopwatch();
      stopwatch.Start();

      try {
        if (forceFullRefresh) {
          NugetHelper.ClearCachedCredentials();
        }

        // reload the NuGet.config file, in case it was changed after Unity opened, but before the manager window opened (now)
        NugetHelper.LoadNugetConfigFile();

        // if we are entering playmode, don't do anything
        if (EditorApplication.isPlayingOrWillChangePlaymode) {
          return;
        }

        NugetHelper.LogVerbose(format : this.hasRefreshed
                                            ? "NugetWindow reloading config"
                                            : "NugetWindow reloading config and updating packages");

        // set the window title
        this.titleContent = new GUIContent("NuGet");

        if (!this.hasRefreshed || forceFullRefresh) {
          // reset the number to skip
          this.numberToSkip = 0;

          // TODO: Do we even need to load ALL of the data, or can we just get the Online tab packages?

          EditorUtility.DisplayProgressBar("Opening NuGet", "Fetching packages from server...", 0.3f);
          this.UpdateOnlinePackages();

          EditorUtility.DisplayProgressBar("Opening NuGet", "Getting installed packages...", 0.6f);
          NugetHelper.UpdateInstalledPackages();

          EditorUtility.DisplayProgressBar("Opening NuGet", "Getting available updates...", 0.9f);
          this.UpdateUpdatePackages();

          // load the default icon from the Resources folder
          this.defaultIcon = (Texture2D)Resources.Load("defaultIcon", systemTypeInstance : typeof(Texture2D));
        }

        this.hasRefreshed = true;
      } catch (Exception e) {
        UnityEngine.Debug.LogErrorFormat("{0}", e.ToString());
      } finally {
        EditorUtility.ClearProgressBar();

        NugetHelper.LogVerbose("NugetWindow reloading took {0} ms", stopwatch.ElapsedMilliseconds);
      }
    }

    /// <summary>
    /// Updates the list of available packages by running a search with the server using the currently set parameters (# to get, # to skip, etc).
    /// </summary>
    void UpdateOnlinePackages() {
      this.availablePackages =
          NugetHelper.Search(search_term :
                             this.onlineSearchTerm != "Search" ? this.onlineSearchTerm : string.Empty,
                             include_all_versions : this.showAllOnlineVersions,
                             include_prerelease : this.showOnlinePrerelease,
                             number_to_get : this.numberToGet,
                             number_to_skip : this.numberToSkip);
    }

    /// <summary>
    /// Updates the list of update packages.
    /// </summary>
    void UpdateUpdatePackages() {
      // get any available updates for the installed packages
      this.updatePackages = NugetHelper.GetUpdates(packages_to_update : NugetHelper.InstalledPackages,
                                                   include_prerelease : this.showPrereleaseUpdates,
                                                   include_all_versions : this.showAllUpdateVersions);
      this.filteredUpdatePackages = this.updatePackages;

      if (this.updatesSearchTerm != "Search") {
        this.filteredUpdatePackages = this.updatePackages
                                          .Where(x => x._Id.ToLower().Contains(value : this.updatesSearchTerm)
                                                      || x.Title.ToLower()
                                                          .Contains(value : this.updatesSearchTerm)).ToList();
      }
    }

    /// <summary>
    /// From here: http://forum.unity3d.com/threads/changing-the-background-color-for-beginhorizontal.66015/
    /// </summary>
    /// <param name="width"></param>
    /// <param name="height"></param>
    /// <param name="col"></param>
    /// <returns></returns>
    Texture2D MakeTex(int width, int height, Color col) {
      var pix = new Color[width * height];

      for (var i = 0; i < pix.Length; i++)
        pix[i] = col;

      var result = new Texture2D(width : width, height : height);
      result.SetPixels(colors : pix);
      result.Apply();

      return result;
    }

    /// <summary>
    /// Automatically called by Unity to draw the GUI.
    /// </summary>
    protected void OnGUI() {
      var selectedTab = GUILayout.Toolbar(selected : this.currentTab, texts : this.tabTitles);

      if (selectedTab != this.currentTab) this.OnTabChanged();

      this.currentTab = selectedTab;

      switch (this.currentTab) {
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

    void OnTabChanged() { this.openCloneWindows.Clear(); }

    /// <summary>
    /// Creates a GUI style with a contrasting background color based upon if the Unity Editor is the free (light) skin or the Pro (dark) skin.
    /// </summary>
    /// <returns>A GUI style with the appropriate background color set.</returns>
    GUIStyle GetContrastStyle() {
      var style = new GUIStyle();
      var backgroundColor =
          EditorGUIUtility.isProSkin ? new Color(0.3f, 0.3f, 0.3f) : new Color(0.6f, 0.6f, 0.6f);
      style.normal.background = this.MakeTex(16, 16, col : backgroundColor);
      return style;
    }

    /// <summary>
    /// Creates a GUI style with a background color the same as the editor's current background color.
    /// </summary>
    /// <returns>A GUI style with the appropriate background color set.</returns>
    GUIStyle GetBackgroundStyle() {
      var style = new GUIStyle();
      var backgroundColor = EditorGUIUtility.isProSkin
                                ? new Color32(56,
                                              56,
                                              56,
                                              255)
                                : new Color32(194,
                                              194,
                                              194,
                                              255);
      style.normal.background = this.MakeTex(16, 16, col : backgroundColor);
      return style;
    }

    /// <summary>
    /// Draws the list of installed packages that have updates available.
    /// </summary>
    void DrawUpdates() {
      this.DrawUpdatesHeader();

      // display all of the installed packages
      this.scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition : this.scrollPosition);
      EditorGUILayout.BeginVertical();

      var style = this.GetContrastStyle();

      if (this.filteredUpdatePackages != null && this.filteredUpdatePackages.Count > 0) {
        this.DrawPackages(packages : this.filteredUpdatePackages);
      } else {
        EditorStyles.label.fontStyle = FontStyle.Bold;
        EditorStyles.label.fontSize = 14;
        EditorGUILayout.LabelField("There are no updates available!", GUILayout.Height(20));
        EditorStyles.label.fontSize = 10;
        EditorStyles.label.fontStyle = FontStyle.Normal;
      }

      EditorGUILayout.EndVertical();
      EditorGUILayout.EndScrollView();
    }

    /// <summary>
    /// Draws the list of installed packages.
    /// </summary>
    void DrawInstalled() {
      this.DrawInstalledHeader();

      // display all of the installed packages
      this.scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition : this.scrollPosition);
      EditorGUILayout.BeginVertical();

      var filteredInstalledPackages = this.FilteredInstalledPackages.ToList();
      if (filteredInstalledPackages != null && filteredInstalledPackages.Count > 0) {
        this.DrawPackages(packages : filteredInstalledPackages);
      } else {
        EditorStyles.label.fontStyle = FontStyle.Bold;
        EditorStyles.label.fontSize = 14;
        EditorGUILayout.LabelField("There are no packages installed!", GUILayout.Height(20));
        EditorStyles.label.fontSize = 10;
        EditorStyles.label.fontStyle = FontStyle.Normal;
      }

      EditorGUILayout.EndVertical();
      EditorGUILayout.EndScrollView();
    }

    /// <summary>
    /// Draws the current list of available online packages.
    /// </summary>
    void DrawOnline() {
      this.DrawOnlineHeader();

      // display all of the packages
      this.scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition : this.scrollPosition);
      EditorGUILayout.BeginVertical();

      if (this.availablePackages != null) {
        this.DrawPackages(packages : this.availablePackages);
      }

      var showMoreStyle = new GUIStyle();
      if (Application.HasProLicense()) {
        showMoreStyle.normal.background = this.MakeTex(20, 20, col : new Color(0.05f, 0.05f, 0.05f));
      } else {
        showMoreStyle.normal.background = this.MakeTex(20, 20, col : new Color(0.4f, 0.4f, 0.4f));
      }

      EditorGUILayout.BeginVertical(style : showMoreStyle);
      // allow the user to dislay more results
      if (GUILayout.Button("Show More", GUILayout.Width(120))) {
        this.numberToSkip += this.numberToGet;
        this.availablePackages.AddRange(collection : NugetHelper.Search(search_term :
                                                                        this.onlineSearchTerm != "Search"
                                                                            ? this.onlineSearchTerm
                                                                            : string.Empty,
                                                                        include_all_versions :
                                                                        this.showAllOnlineVersions,
                                                                        include_prerelease : this
                                                                            .showOnlinePrerelease,
                                                                        number_to_get : this.numberToGet,
                                                                        number_to_skip : this.numberToSkip));
      }

      EditorGUILayout.EndVertical();

      EditorGUILayout.EndVertical();
      EditorGUILayout.EndScrollView();
    }

    void DrawPackages(List<NugetPackage> packages) {
      var backgroundStyle = this.GetBackgroundStyle();
      var contrastStyle = this.GetContrastStyle();

      for (var i = 0; i < packages.Count; i++) {
        EditorGUILayout.BeginVertical(style : backgroundStyle);
        this.DrawPackage(package : packages[index : i],
                         packageStyle : backgroundStyle,
                         contrastStyle : contrastStyle);
        EditorGUILayout.EndVertical();

        // swap styles
        var tempStyle = backgroundStyle;
        backgroundStyle = contrastStyle;
        contrastStyle = tempStyle;
      }
    }

    /// <summary>
    /// Draws the header which allows filtering the online list of packages.
    /// </summary>
    void DrawOnlineHeader() {
      var headerStyle = new GUIStyle();
      if (Application.HasProLicense()) {
        headerStyle.normal.background = this.MakeTex(20, 20, col : new Color(0.05f, 0.05f, 0.05f));
      } else {
        headerStyle.normal.background = this.MakeTex(20, 20, col : new Color(0.4f, 0.4f, 0.4f));
      }

      EditorGUILayout.BeginVertical(style : headerStyle);
      {
        EditorGUILayout.BeginHorizontal();
        {
          var showAllVersionsTemp =
              EditorGUILayout.Toggle("Show All Versions", value : this.showAllOnlineVersions);
          if (showAllVersionsTemp != this.showAllOnlineVersions) {
            this.showAllOnlineVersions = showAllVersionsTemp;
            this.UpdateOnlinePackages();
          }

          if (GUILayout.Button("Refresh", GUILayout.Width(60))) {
            this.Refresh(true);
          }
        }
        EditorGUILayout.EndHorizontal();

        var showPrereleaseTemp = EditorGUILayout.Toggle("Show Prerelease", value : this.showOnlinePrerelease);
        if (showPrereleaseTemp != this.showOnlinePrerelease) {
          this.showOnlinePrerelease = showPrereleaseTemp;
          this.UpdateOnlinePackages();
        }

        var enterPressed = Event.current.Equals(obj : Event.KeyboardEvent("return"));

        EditorGUILayout.BeginHorizontal();
        {
          var oldFontSize = GUI.skin.textField.fontSize;
          GUI.skin.textField.fontSize = 25;
          this.onlineSearchTerm =
              EditorGUILayout.TextField(text : this.onlineSearchTerm, GUILayout.Height(30));

          if (GUILayout.Button("Search", GUILayout.Width(100), GUILayout.Height(28))) {
            // the search button emulates the Enter key
            enterPressed = true;
          }

          GUI.skin.textField.fontSize = oldFontSize;
        }
        EditorGUILayout.EndHorizontal();

        // search only if the enter key is pressed
        if (enterPressed) {
          // reset the number to skip
          this.numberToSkip = 0;
          this.UpdateOnlinePackages();
        }
      }
      EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// Draws the header which allows filtering the installed list of packages.
    /// </summary>
    void DrawInstalledHeader() {
      var headerStyle = new GUIStyle();
      if (Application.HasProLicense()) {
        headerStyle.normal.background = this.MakeTex(20, 20, col : new Color(0.05f, 0.05f, 0.05f));
      } else {
        headerStyle.normal.background = this.MakeTex(20, 20, col : new Color(0.4f, 0.4f, 0.4f));
      }

      EditorGUILayout.BeginVertical(style : headerStyle);
      {
        var enterPressed = Event.current.Equals(obj : Event.KeyboardEvent("return"));

        EditorGUILayout.BeginHorizontal();
        {
          var oldFontSize = GUI.skin.textField.fontSize;
          GUI.skin.textField.fontSize = 25;
          this.installedSearchTermEditBox =
              EditorGUILayout.TextField(text : this.installedSearchTermEditBox, GUILayout.Height(30));

          if (GUILayout.Button("Search", GUILayout.Width(100), GUILayout.Height(28))) {
            // the search button emulates the Enter key
            enterPressed = true;
          }

          GUI.skin.textField.fontSize = oldFontSize;
        }
        EditorGUILayout.EndHorizontal();

        // search only if the enter key is pressed
        if (enterPressed) {
          this.installedSearchTerm = this.installedSearchTermEditBox;
        }
      }
      EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// Draws the header for the Updates tab.
    /// </summary>
    void DrawUpdatesHeader() {
      var headerStyle = new GUIStyle();
      if (Application.HasProLicense()) {
        headerStyle.normal.background = this.MakeTex(20, 20, col : new Color(0.05f, 0.05f, 0.05f));
      } else {
        headerStyle.normal.background = this.MakeTex(20, 20, col : new Color(0.4f, 0.4f, 0.4f));
      }

      EditorGUILayout.BeginVertical(style : headerStyle);
      {
        EditorGUILayout.BeginHorizontal();
        {
          var showAllVersionsTemp =
              EditorGUILayout.Toggle("Show All Versions", value : this.showAllUpdateVersions);
          if (showAllVersionsTemp != this.showAllUpdateVersions) {
            this.showAllUpdateVersions = showAllVersionsTemp;
            this.UpdateUpdatePackages();
          }

          if (GUILayout.Button("Install All Updates", GUILayout.Width(150))) {
            NugetHelper.UpdateAll(updates : this.updatePackages,
                                  packages_to_update : NugetHelper.InstalledPackages);
            NugetHelper.UpdateInstalledPackages();
            this.UpdateUpdatePackages();
          }

          if (GUILayout.Button("Refresh", GUILayout.Width(60))) {
            this.Refresh(true);
          }
        }
        EditorGUILayout.EndHorizontal();

        var showPrereleaseTemp =
            EditorGUILayout.Toggle("Show Prerelease", value : this.showPrereleaseUpdates);
        if (showPrereleaseTemp != this.showPrereleaseUpdates) {
          this.showPrereleaseUpdates = showPrereleaseTemp;
          this.UpdateUpdatePackages();
        }

        var enterPressed = Event.current.Equals(obj : Event.KeyboardEvent("return"));

        EditorGUILayout.BeginHorizontal();
        {
          var oldFontSize = GUI.skin.textField.fontSize;
          GUI.skin.textField.fontSize = 25;
          this.updatesSearchTerm =
              EditorGUILayout.TextField(text : this.updatesSearchTerm, GUILayout.Height(30));

          if (GUILayout.Button("Search", GUILayout.Width(100), GUILayout.Height(28))) {
            // the search button emulates the Enter key
            enterPressed = true;
          }

          GUI.skin.textField.fontSize = oldFontSize;
        }
        EditorGUILayout.EndHorizontal();

        // search only if the enter key is pressed
        if (enterPressed) {
          if (this.updatesSearchTerm != "Search") {
            this.filteredUpdatePackages = this.updatePackages
                                              .Where(x => x._Id.ToLower()
                                                           .Contains(value : this.updatesSearchTerm)
                                                          || x.Title.ToLower()
                                                              .Contains(value : this.updatesSearchTerm))
                                              .ToList();
          }
        }
      }
      EditorGUILayout.EndVertical();
    }

    Dictionary<string, bool> foldouts = new Dictionary<string, bool>();

    /// <summary>
    /// Draws the given <see cref="NugetPackage"/>.
    /// </summary>
    /// <param name="package">The <see cref="NugetPackage"/> to draw.</param>
    void DrawPackage(NugetPackage package, GUIStyle packageStyle, GUIStyle contrastStyle) {
      var installedPackages = NugetHelper.InstalledPackages;
      var installed = installedPackages.FirstOrDefault(p => p._Id == package._Id);

      EditorGUILayout.BeginHorizontal();
      {
        // The Unity GUI system (in the Editor) is terrible.  This probably requires some explanation.
        // Every time you use a Horizontal block, Unity appears to divide the space evenly.
        // (i.e. 2 components have half of the window width, 3 components have a third of the window width, etc)
        // GUILayoutUtility.GetRect is SUPPOSED to return a rect with the given height and width, but in the GUI layout.  It doesn't.
        // We have to use GUILayoutUtility to get SOME rect properties, but then manually calculate others.
        EditorGUILayout.BeginHorizontal();
        {
          const int iconSize = 32;
          var padding = EditorStyles.label.padding.vertical;
          var rect = GUILayoutUtility.GetRect(width : iconSize, height : iconSize);
          // only use GetRect's Y position.  It doesn't correctly set the width, height or X position.

          rect.x = padding;
          rect.y += padding;
          rect.width = iconSize;
          rect.height = iconSize;

          if (package.Icon != null) {
            GUI.DrawTexture(position : rect, image : package.Icon, scaleMode : ScaleMode.StretchToFill);
          } else {
            GUI.DrawTexture(position : rect, image : this.defaultIcon, scaleMode : ScaleMode.StretchToFill);
          }

          rect.x = iconSize + 2 * padding;
          rect.width = this.position.width / 2 - (iconSize + padding);
          rect.y -= padding; // This will leave the text aligned with the top of the image

          EditorStyles.label.fontStyle = FontStyle.Bold;
          EditorStyles.label.fontSize = 16;

          var idSize = EditorStyles.label.CalcSize(content : new GUIContent(text : package._Id));
          rect.y += (iconSize / 2 - idSize.y / 2) + padding;
          GUI.Label(position : rect, text : package._Id, style : EditorStyles.label);
          rect.x += idSize.x;

          EditorStyles.label.fontSize = 10;
          EditorStyles.label.fontStyle = FontStyle.Normal;

          var versionSize = EditorStyles.label.CalcSize(content : new GUIContent(text : package._Version));
          rect.y += (idSize.y - versionSize.y - padding / 2);

          if (!string.IsNullOrEmpty(value : package.Authors)) {
            var authorLabel = string.Format("by {0}", arg0 : package.Authors);
            var size = EditorStyles.label.CalcSize(content : new GUIContent(text : authorLabel));
            GUI.Label(position : rect, text : authorLabel, style : EditorStyles.label);
            rect.x += size.x;
          }

          if (package.DownloadCount > 0) {
            var downloadLabel = string.Format("{0} downloads", arg0 : package.DownloadCount.ToString("#,#"));
            var size = EditorStyles.label.CalcSize(content : new GUIContent(text : downloadLabel));
            GUI.Label(position : rect, text : downloadLabel, style : EditorStyles.label);
            rect.x += size.x;
          }
        }

        GUILayout.FlexibleSpace();
        if (installed != null && installed._Version != package._Version) {
          GUILayout.Label(text : string.Format("Current Version {0}", arg0 : installed._Version));
        }

        GUILayout.Label(text : string.Format("Version {0}", arg0 : package._Version));

        if (installedPackages.Contains(value : package)) {
          // This specific version is installed
          if (GUILayout.Button("Uninstall")) {
            // TODO: Perhaps use a "mark as dirty" system instead of updating all of the data all the time? 
            NugetHelper.Uninstall(package : package);
            NugetHelper.UpdateInstalledPackages();
            this.UpdateUpdatePackages();
          }
        } else {
          if (installed != null) {
            if (installed < package) {
              // An older version is installed
              if (GUILayout.Button("Update")) {
                NugetHelper.Update(current_version : installed, new_version : package);
                NugetHelper.UpdateInstalledPackages();
                this.UpdateUpdatePackages();
              }
            } else if (installed > package) {
              // A newer version is installed
              if (GUILayout.Button("Downgrade")) {
                NugetHelper.Update(current_version : installed, new_version : package);
                NugetHelper.UpdateInstalledPackages();
                this.UpdateUpdatePackages();
              }
            }
          } else {
            if (GUILayout.Button("Install")) {
              NugetHelper.InstallIdentifier(package : package);
              AssetDatabase.Refresh();
              NugetHelper.UpdateInstalledPackages();
              this.UpdateUpdatePackages();
            }
          }
        }

        EditorGUILayout.EndHorizontal();
      }
      EditorGUILayout.EndHorizontal();

      EditorGUILayout.Space();
      EditorGUILayout.BeginHorizontal();
      {
        EditorGUILayout.BeginVertical();
        {
          // Show the package details
          EditorStyles.label.wordWrap = true;
          EditorStyles.label.fontStyle = FontStyle.Normal;

          var summary = package.Summary;
          if (string.IsNullOrEmpty(value : summary)) {
            summary = package.Description;
          }

          if (!package.Title.Equals(value : package._Id,
                                    comparisonType : StringComparison.InvariantCultureIgnoreCase)) {
            summary = string.Format("{0} - {1}", arg0 : package.Title, arg1 : summary);
          }

          if (summary.Length >= 240) {
            summary = string.Format("{0}...", arg0 : summary.Substring(0, 237));
          }

          EditorGUILayout.LabelField(label : summary);

          bool detailsFoldout;
          var detailsFoldoutId = string.Format("{0}.{1}", arg0 : package._Id, "Details");
          if (!this.foldouts.TryGetValue(key : detailsFoldoutId, value : out detailsFoldout)) {
            this.foldouts[key : detailsFoldoutId] = detailsFoldout;
          }

          detailsFoldout = EditorGUILayout.Foldout(foldout : detailsFoldout, "Details");
          this.foldouts[key : detailsFoldoutId] = detailsFoldout;

          if (detailsFoldout) {
            EditorGUI.indentLevel++;
            if (!string.IsNullOrEmpty(value : package.Description)) {
              EditorGUILayout.LabelField("Description", style : EditorStyles.boldLabel);
              EditorGUILayout.LabelField(label : package.Description);
            }

            if (!string.IsNullOrEmpty(value : package.ReleaseNotes)) {
              EditorGUILayout.LabelField("Release Notes", style : EditorStyles.boldLabel);
              EditorGUILayout.LabelField(label : package.ReleaseNotes);
            }

            // Show project URL link
            if (!string.IsNullOrEmpty(value : package.ProjectUrl)) {
              EditorGUILayout.LabelField("Project Url", style : EditorStyles.boldLabel);
              GUILayoutLink(url : package.ProjectUrl);
              GUILayout.Space(4f);
            }

            // Show the dependencies
            if (package.Dependencies.Count > 0) {
              EditorStyles.label.wordWrap = true;
              EditorStyles.label.fontStyle = FontStyle.Italic;
              var builder = new StringBuilder();

              var frameworkGroup =
                  NugetHelper.GetBestDependencyFrameworkGroupForCurrentSettings(package : package);
              foreach (var dependency in frameworkGroup.Dependencies) {
                builder.Append(value : string.Format(" {0} {1};",
                                                     arg0 : dependency._Id,
                                                     arg1 : dependency._Version));
              }

              EditorGUILayout.Space();
              EditorGUILayout.LabelField(label : string.Format("Depends on:{0}", arg0 : builder.ToString()));
              EditorStyles.label.fontStyle = FontStyle.Normal;
            }

            // Create the style for putting a box around the 'Clone' button
            var cloneButtonBoxStyle = new GUIStyle("box");
            cloneButtonBoxStyle.stretchWidth = false;
            cloneButtonBoxStyle.margin.top = 0;
            cloneButtonBoxStyle.margin.bottom = 0;
            cloneButtonBoxStyle.padding.bottom = 4;

            var normalButtonBoxStyle = new GUIStyle(other : cloneButtonBoxStyle);
            normalButtonBoxStyle.normal.background = packageStyle.normal.background;

            var showCloneWindow = this.openCloneWindows.Contains(item : package);
            cloneButtonBoxStyle.normal.background =
                showCloneWindow ? contrastStyle.normal.background : packageStyle.normal.background;

            // Create a simillar style for the 'Clone' window
            var cloneWindowStyle = new GUIStyle(other : cloneButtonBoxStyle);
            cloneWindowStyle.padding = new RectOffset(6,
                                                      6,
                                                      2,
                                                      6);

            // Show button bar
            EditorGUILayout.BeginHorizontal();
            {
              if (package.RepositoryType == RepositoryType.Git
                  || package.RepositoryType == RepositoryType.TfsGit) {
                if (!string.IsNullOrEmpty(value : package.RepositoryUrl)) {
                  EditorGUILayout.BeginHorizontal(style : cloneButtonBoxStyle);
                  {
                    var cloneButtonStyle = new GUIStyle(other : GUI.skin.button);
                    cloneButtonStyle.normal =
                        showCloneWindow ? cloneButtonStyle.active : cloneButtonStyle.normal;
                    if (GUILayout.Button("Clone", style : cloneButtonStyle, GUILayout.ExpandWidth(false))) {
                      showCloneWindow = !showCloneWindow;
                    }

                    if (showCloneWindow)
                      this.openCloneWindows.Add(item : package);
                    else
                      this.openCloneWindows.Remove(item : package);
                  }
                  EditorGUILayout.EndHorizontal();
                }
              }

              if (!string.IsNullOrEmpty(value : package.LicenseUrl)
                  && package.LicenseUrl != "http://your_license_url_here") {
                // Creaete a box around the license button to keep it alligned with Clone button
                EditorGUILayout.BeginHorizontal(style : normalButtonBoxStyle);
                // Show the license button
                if (GUILayout.Button("View License", GUILayout.ExpandWidth(false))) {
                  Application.OpenURL(url : package.LicenseUrl);
                }

                EditorGUILayout.EndHorizontal();
              }
            }
            EditorGUILayout.EndHorizontal();

            if (showCloneWindow) {
              EditorGUILayout.BeginVertical(style : cloneWindowStyle);
              {
                // Clone latest label
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(20f);
                EditorGUILayout.LabelField("clone latest");
                EditorGUILayout.EndHorizontal();

                // Clone latest row
                EditorGUILayout.BeginHorizontal();
                {
                  if (GUILayout.Button("Copy", GUILayout.ExpandWidth(false))) {
                    GUI.FocusControl(name : package._Id + package._Version + "repoUrl");
                    GUIUtility.systemCopyBuffer = package.RepositoryUrl;
                  }

                  GUI.SetNextControlName(name : package._Id + package._Version + "repoUrl");
                  EditorGUILayout.TextField(text : package.RepositoryUrl);
                }
                EditorGUILayout.EndHorizontal();

                // Clone @ commit label
                GUILayout.Space(4f);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(20f);
                EditorGUILayout.LabelField("clone @ commit");
                EditorGUILayout.EndHorizontal();

                // Clone @ commit row
                EditorGUILayout.BeginHorizontal();
                {
                  // Create the three commands a user will need to run to get the repo @ the commit. Intentionally leave off the last newline for better UI appearance
                  var commands = string.Format("git clone {0} {1} --no-checkout{2}cd {1}{2}git checkout {3}",
                                               package.RepositoryUrl,
                                               package._Id,
                                               Environment.NewLine,
                                               package.RepositoryCommit);

                  if (GUILayout.Button("Copy", GUILayout.ExpandWidth(false))) {
                    GUI.FocusControl(name : package._Id + package._Version + "commands");

                    // Add a newline so the last command will execute when pasted to the CL
                    GUIUtility.systemCopyBuffer = (commands + Environment.NewLine);
                  }

                  EditorGUILayout.BeginVertical();
                  GUI.SetNextControlName(name : package._Id + package._Version + "commands");
                  EditorGUILayout.TextArea(text : commands);
                  EditorGUILayout.EndVertical();
                }
                EditorGUILayout.EndHorizontal();
              }
              EditorGUILayout.EndVertical();
            }

            EditorGUI.indentLevel--;
          }

          EditorGUILayout.Separator();
          EditorGUILayout.Separator();
        }
        EditorGUILayout.EndVertical();
      }
      EditorGUILayout.EndHorizontal();
    }

    public static void GUILayoutLink(string url) {
      var hyperLinkStyle = new GUIStyle(other : GUI.skin.label);
      hyperLinkStyle.stretchWidth = false;
      hyperLinkStyle.richText = true;

      var colorFormatString = "<color=#add8e6ff>{0}</color>";

      var underline = new string('_', count : url.Length);

      var formattedUrl = string.Format(format : colorFormatString, arg0 : url);
      var formattedUnderline = string.Format(format : colorFormatString, arg0 : underline);
      var urlRect = GUILayoutUtility.GetRect(content : new GUIContent(text : url), style : hyperLinkStyle);

      // Update rect for indentation
      {
        var indentedUrlRect = EditorGUI.IndentedRect(source : urlRect);
        var delta = indentedUrlRect.x - urlRect.x;
        indentedUrlRect.width += delta;
        urlRect = indentedUrlRect;
      }

      GUI.Label(position : urlRect, text : formattedUrl, style : hyperLinkStyle);
      GUI.Label(position : urlRect, text : formattedUnderline, style : hyperLinkStyle);

      EditorGUIUtility.AddCursorRect(position : urlRect, mouse : MouseCursor.Link);
      if (urlRect.Contains(point : Event.current.mousePosition)) {
        if (Event.current.type == EventType.MouseUp)
          Application.OpenURL(url : url);
      }
    }
  }
}