using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Porto.Apple;

// Bounded plist codec. Data and identifiers never appear in exception messages.
internal static class ApplePlist
{
    internal const int MaximumLength=4*1024*1024;
    internal static Dictionary<string,object> Decode(byte[] data)
    {
        try{return DecodeCore(data);}
        catch(Exception ex) when(ex is XmlException or FormatException or DecoderFallbackException or OverflowException or ArgumentException)
        {throw Bad();}
    }
    private static Dictionary<string,object> DecodeCore(byte[] data)
    {
        if(data.Length==0||data.Length>MaximumLength)throw Bad();
        object root;
        if(data.AsSpan().StartsWith("bplist00"u8))root=new BinaryReader(data).Read();
        else
        {
            using var input=new MemoryStream(data,false);
            using var reader=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Ignore,XmlResolver=null,MaxCharactersInDocument=MaximumLength,MaxCharactersFromEntities=0});
            var doc=XDocument.Load(reader);var elements=doc.Root?.Elements().ToArray();
            if(doc.Root?.Name!="plist"||elements?.Length!=1)throw Bad();
            int nodes=0;root=XmlValue(elements[0],0,ref nodes);
        }
        return root as Dictionary<string,object>??throw Bad();
    }
    internal static byte[] Encode(Dictionary<string,object> value)
    {
        var doc=new XDocument(new XDeclaration("1.0","UTF-8",null),new XElement("plist",new XAttribute("version","1.0"),XmlElement(value)));
        byte[] data=Encoding.UTF8.GetBytes(doc.ToString(SaveOptions.DisableFormatting));if(data.Length>MaximumLength)throw Bad();return data;
    }
    private static object XmlValue(XElement node,int depth,ref int count)
    {
        if(depth>32||++count>100000||node.Name.Namespace!=XNamespace.None)throw Bad();
        switch(node.Name.LocalName)
        {
            case "dict":
                var entries=node.Elements().ToArray();if(entries.Length%2!=0)throw Bad();var dictionary=new Dictionary<string,object>(StringComparer.Ordinal);
                for(int i=0;i<entries.Length;i+=2)
                    if(entries[i].Name!="key"||entries[i].HasElements||!dictionary.TryAdd(entries[i].Value,XmlValue(entries[i+1],depth+1,ref count)))throw Bad();
                return dictionary;
            case "array":var values=new List<object>();foreach(var item in node.Elements())values.Add(XmlValue(item,depth+1,ref count));return values;
            case "string":if(node.HasElements)throw Bad();return node.Value;
            case "integer":if(node.HasElements||!ulong.TryParse(node.Value,NumberStyles.None,CultureInfo.InvariantCulture,out var number))throw Bad();return number;
            case "data":if(node.HasElements)throw Bad();try{return Convert.FromBase64String(node.Value);}catch(FormatException){throw Bad();}
            case "true":if(node.HasElements||node.Value.Length!=0)throw Bad();return true;
            case "false":if(node.HasElements||node.Value.Length!=0)throw Bad();return false;
            default:throw Bad();
        }
    }
    private static XElement XmlElement(object value)=>value switch
    {
        Dictionary<string,object> dictionary=>new XElement("dict",dictionary.SelectMany(pair=>new[]{new XElement("key",pair.Key),XmlElement(pair.Value)})),
        string text=>new XElement("string",text),
        ulong number=>new XElement("integer",number.ToString(CultureInfo.InvariantCulture)),
        uint number=>new XElement("integer",number.ToString(CultureInfo.InvariantCulture)),
        bool flag=>new XElement(flag?"true":"false"),
        byte[] bytes=>new XElement("data",Convert.ToBase64String(bytes)),
        _=>throw Bad()
    };
    internal static string Text(Dictionary<string,object> dictionary,string key)=>dictionary.TryGetValue(key,out var value)&&value is string text?text:throw Bad();
    internal static ulong Integer(Dictionary<string,object> dictionary,string key)=>dictionary.TryGetValue(key,out var value)&&value is ulong integer?integer:throw Bad();
    internal static Dictionary<string,object> Dictionary(object value)=>value as Dictionary<string,object>??throw Bad();
    internal static IOException Bad()=>new("Risposta Apple non riconosciuta o fuori dai limiti del protocollo.");

    private sealed class BinaryReader(byte[] data)
    {
        private int offsetSize,referenceSize,count,root,table;
        private readonly HashSet<int> active=[];
        private readonly Dictionary<int,object> cache=[];
        private int visited;
        private long materialized;
        internal object Read()
        {
            if(data.Length<40)throw Bad();int trailer=data.Length-32;
            offsetSize=data[trailer+6];referenceSize=data[trailer+7];
            count=Checked(ReadUnsigned(trailer+8,8));root=Checked(ReadUnsigned(trailer+16,8));table=Checked(ReadUnsigned(trailer+24,8));
            if(offsetSize is <1 or >8||referenceSize is <1 or >8||count<1||count>100000||root>=count||table<8||(long)table+(long)count*offsetSize>trailer)throw Bad();
            return Object(root,0);
        }
        private object Object(int id,int depth)
        {
            if(++visited>100000)throw Bad();
            if(cache.TryGetValue(id,out var existing))return existing;
            object value=ReadObject(id,depth);cache.Add(id,value);return value;
        }
        private object ReadObject(int id,int depth)
        {
            if(id<0||id>=count||depth>32||!active.Add(id))throw Bad();
            try
            {
                int offset=Checked(ReadUnsigned(table+id*offsetSize,offsetSize));if(offset<8||offset>=table)throw Bad();
                int marker=data[offset++],type=marker>>4,length=marker&15;
                if(type==0)return length switch{8=>false,9=>true,_=>throw Bad()};
                if(type==1){int width=1<<length;if(width>8)throw Bad();Range(offset,width);return ReadUnsigned(offset,width);}
                if(type is not(4 or 5 or 6 or 10 or 13))throw Bad();
                if(length==15)
                {
                    Range(offset,1);int tag=data[offset++];if((tag>>4)!=1||(tag&15)>3)throw Bad();int width=1<<(tag&15);
                    Range(offset,width);length=Checked(ReadUnsigned(offset,width));offset+=width;
                }
                if(length>MaximumLength)throw Bad();
                if(type==4){Range(offset,length);Budget(length);return data.AsSpan(offset,length).ToArray();}
                if(type==5){Range(offset,length);Budget((long)length*2);return Encoding.ASCII.GetString(data,offset,length);}
                if(type==6){int bytes=checked(length*2);Range(offset,bytes);Budget(bytes);return new UnicodeEncoding(true,false,true).GetString(data,offset,bytes);}
                if(length>100000)throw Bad();Range(offset,checked(length*referenceSize*(type==13?2:1)));
                if(type==10){var array=new List<object>();for(int i=0;i<length;i++)array.Add(Object(Checked(ReadUnsigned(offset+i*referenceSize,referenceSize)),depth+1));return array;}
                var dictionary=new Dictionary<string,object>(StringComparer.Ordinal);
                for(int i=0;i<length;i++)
                {
                    var key=Object(Checked(ReadUnsigned(offset+i*referenceSize,referenceSize)),depth+1) as string??throw Bad();
                    var value=Object(Checked(ReadUnsigned(offset+(length+i)*referenceSize,referenceSize)),depth+1);
                    if(!dictionary.TryAdd(key,value))throw Bad();
                }
                return dictionary;
            }
            finally{active.Remove(id);}
        }
        private ulong ReadUnsigned(int offset,int size)
        {
            if(size is <1 or >8||offset<0||(long)offset+size>data.Length)throw Bad();
            ulong value=0;for(int i=0;i<size;i++)value=(value<<8)|data[offset+i];return value;
        }
        private void Range(int offset,int size){if(size<0||offset<8||(long)offset+size>table)throw Bad();}
        private void Budget(long bytes){materialized+=bytes;if(materialized>16*1024*1024)throw Bad();}
        private static int Checked(ulong value)=>value<=int.MaxValue?(int)value:throw Bad();
    }
}
