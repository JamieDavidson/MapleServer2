using Maple2.File.IO;
using Maple2Storage.Types;

namespace GameDataParser.Files;

public class MetadataResources
{
    public readonly M2dReader XmlReader;
    public readonly M2dReader ExportedReader;

    public MetadataResources()
    {
        string xmlPath = $"{Paths.ResourcesInputDirectory}/Xml.m2d";
        string exportedPath = $"{Paths.ResourcesInputDirectory}/Exported.m2d";

        XmlReader = new(xmlPath);
        ExportedReader = new(exportedPath);
    }
}
