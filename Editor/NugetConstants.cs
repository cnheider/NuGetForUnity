namespace NuGetForUnity.Editor {
  public static class NugetConstants {
    /// <summary>
    ///   The path to the nuget.config file.
    /// </summary>
    public const string _Nuget_Config_File_Path = "nuget.config";

    /// <summary>
    ///   The path to the packages.config file.
    /// </summary>
    public const string _Packages_Config_File_Path = "packages.config";

    /// <summary>
    ///   The path where to put created (packed) and downloaded (not installed yet) .nupkg files.
    /// </summary>
    //public static readonly string PackOutputDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Path.Combine("NuGet", "Cache"));
    public const string _Pack_Output_Directory = "Libraries";

    public const string _Tools_Packages_Folder = "Libraries";

    /// <summary>
    ///   The amount of time, in milliseconds, before the nuget.exe process times out and is killed.
    /// </summary>
    public const int _Time_Out = 60000;

    /// <summary>
    ///   The current version of NuGet for Unity.
    /// </summary>
    public const string _NuGetForUnityVersion = "3.0.1";

    public const string _Default_Nuget_Config_Contents = @"<?xml version=""1.0"" encoding=""utf-8""?>
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
  }
}