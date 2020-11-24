namespace NuGetForUnity.Editor {
  /// <summary>
  ///   Represents a grouping of items by framework type and version.
  ///   This could be a group of package dependencies, or a group of package references.
  /// </summary>
  public class NugetFrameworkGroup {
    public NugetFrameworkGroup() {
      this.TargetFramework = "";
      this.Dependencies = new System.Collections.Generic.List<NugetPackageIdentifier>();
    }

    /// <summary>
    ///   Gets or sets the framework and version that this group targets.
    /// </summary>
    public string TargetFramework { get; set; }

    /// <summary>
    ///   Gets or sets the list of package dependencies that belong to this group.
    /// </summary>
    public System.Collections.Generic.List<NugetPackageIdentifier> Dependencies { get; set; }
  }
}