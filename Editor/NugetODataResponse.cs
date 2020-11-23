namespace NuGetForUnity.Editor {
  using System.Collections.Generic;
  using System.Xml.Linq;

  /// <summary>
  /// Provides helper methods for parsing a NuGet server OData response.
  /// OData is a superset of the Atom API.
  /// </summary>
  public static class NugetODataResponse {
    const string _atom_namespace = "http://www.w3.org/2005/Atom";

    const string _data_services_namespace = "http://schemas.microsoft.com/ado/2007/08/dataservices";

    const string _meta_data_namespace = "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata";

    /// <summary>
    /// Gets the string value of a NuGet metadata property from the given properties element and property name.
    /// </summary>
    /// <param name="properties">The properties element.</param>
    /// <param name="name">The name of the property to get.</param>
    /// <returns>The string value of the property.</returns>
    static string GetProperty(this XElement properties, string name) {
      return (string)properties.Element(name : XName.Get(localName : name,
                                                         namespaceName : _data_services_namespace))
             ?? string.Empty;
    }

    /// <summary>
    /// Gets the <see cref="XElement"/> within the Atom namespace with the given name.
    /// </summary>
    /// <param name="element">The element containing the Atom element.</param>
    /// <param name="name">The name of the Atom element</param>
    /// <returns>The Atom element.</returns>
    static XElement GetAtomElement(this XElement element, string name) {
      return element.Element(name : XName.Get(localName : name, namespaceName : _atom_namespace));
    }

    /// <summary>
    /// Parses the given <see cref="XDocument"/> and returns the list of <see cref="NugetPackage"/>s contained within.
    /// </summary>
    /// <param name="document">The <see cref="XDocument"/> that is the OData XML response from the NuGet server.</param>
    /// <returns>The list of <see cref="NugetPackage"/>s read from the given XML.</returns>
    public static List<NugetPackage> Parse(XDocument document) {
      var packages = new List<NugetPackage>();

      var package_entries = document.Root.Elements(name : XName.Get("entry", namespaceName : _atom_namespace));
      foreach (var entry in package_entries) {
        var package = new NugetPackage();
        package._Id = entry.GetAtomElement("title").Value;
        package.DownloadUrl = entry.GetAtomElement("content").Attribute("src").Value;

        var entry_properties =
            entry.Element(name : XName.Get("properties", namespaceName : _meta_data_namespace));
        package.Title = entry_properties.GetProperty("Title");
        package._Version = entry_properties.GetProperty("Version");
        package.Description = entry_properties.GetProperty("Description");
        package.Summary = entry_properties.GetProperty("Summary");
        package.ReleaseNotes = entry_properties.GetProperty("ReleaseNotes");
        package.LicenseUrl = entry_properties.GetProperty("LicenseUrl");
        package.ProjectUrl = entry_properties.GetProperty("ProjectUrl");
        package.Authors = entry_properties.GetProperty("Authors");
        package.DownloadCount = int.Parse(s : entry_properties.GetProperty("DownloadCount"));

        var icon_url = entry_properties.GetProperty("IconUrl");
        if (!string.IsNullOrEmpty(value : icon_url)) {
          package.Icon = NugetHelper.DownloadImage(url : icon_url);
        }

        // if there is no title, just use the ID as the title
        if (string.IsNullOrEmpty(value : package.Title)) {
          package.Title = package._Id;
        }

        // Get dependencies
        var raw_dependencies = entry_properties.GetProperty("Dependencies");
        if (!string.IsNullOrEmpty(value : raw_dependencies)) {
          var dependency_groups = new Dictionary<string, NugetFrameworkGroup>();

          var dependencies = raw_dependencies.Split('|');
          foreach (var dependency_string in dependencies) {
            var details = dependency_string.Split(':');

            var framework = string.Empty;
            if (details.Length > 2) {
              framework = details[2];
            }

            NugetFrameworkGroup group;
            if (!dependency_groups.TryGetValue(key : framework, value : out group)) {
              group = new NugetFrameworkGroup();
              group.TargetFramework = framework;
              dependency_groups.Add(key : framework, value : group);
            }

            var dependency = new NugetPackageIdentifier(id : details[0], version : details[1]);
            // some packages (ex: FSharp.Data - 2.1.0) have inproper "semi-empty" dependencies such as:
            // "Zlib.Portable:1.10.0:portable-net40+sl50+wp80+win80|::net40"
            // so we need to only add valid dependencies and skip invalid ones
            if (!string.IsNullOrEmpty(value : dependency._Id)
                && !string.IsNullOrEmpty(value : dependency._Version)) {
              group.Dependencies.Add(item : dependency);
            }
          }

          foreach (var group in dependency_groups.Values) {
            package.Dependencies.Add(item : group);
          }
        }

        packages.Add(item : package);
      }

      return packages;
    }
  }
}