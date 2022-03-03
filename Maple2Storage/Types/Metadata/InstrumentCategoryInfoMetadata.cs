using System.Xml.Serialization;

namespace Maple2Storage.Types.Metadata;

[XmlType]
public class InstrumentCategoryInfoMetadata
{
    [XmlElement(Order = 1)]
    public byte CategoryId;
    [XmlElement(Order = 2)]
    public byte GmId;
    [XmlElement(Order = 3)]
    public string Octave;
    [XmlElement(Order = 4)]
    public byte PercussionId;

    public override string ToString()
    {
        return $"InstrumentCategoryInfo(CategoryId:{CategoryId},GMId:{GmId},Octave:{Octave},PercussionId{PercussionId})";
    }
}
