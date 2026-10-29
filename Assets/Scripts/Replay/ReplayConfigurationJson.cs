using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

/// <summary>Capture configuration fields without Unity value types' computed recursive properties; preserve null branches.</summary>
public static class ReplayConfigurationJson
{
    private sealed class PublicFields : DefaultContractResolver
    {
        protected override IList<JsonProperty> CreateProperties(Type type,MemberSerialization serialization) =>
            base.CreateProperties(type,serialization).Where(property=>type.GetField(property.UnderlyingName,BindingFlags.Instance|BindingFlags.Public)!=null).ToList();
    }
    private static readonly IContractResolver Fields=new PublicFields();
    public static JObject Capture(object configuration) => JObject.FromObject(configuration,JsonSerializer.Create(new JsonSerializerSettings {
        ContractResolver=Fields,NullValueHandling=NullValueHandling.Include,ReferenceLoopHandling=ReferenceLoopHandling.Error }));
}
