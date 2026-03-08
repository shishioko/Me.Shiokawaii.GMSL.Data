using System.Text.Json;
using Microsoft.CodeAnalysis;

namespace Me.Shiokawaii.GMSL.Data.Generator
{
    [Generator(LanguageNames.CSharp)]
    public sealed class TypeGenerator : IIncrementalGenerator
    {
        private record TypeInfo
        {
            public Func<JsonElement[], string> TypeName { get; init; } = (JsonElement[] options) => "object";
            public Func<string, string, JsonElement[], string> Serialization { get; init; } = (string stream, string variable, JsonElement[] options) => "";
            public Func<string, string, JsonElement[], string> Deserialization { get; init; } = (string stream, string variable, JsonElement[] options) => "";

            public TypeInfo()
            {
                
            }
        }
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            List<string> usings =
            [
                "System.Net",
                "Me.Shiokawaii.IO"
            ];
            
            IncrementalValuesProvider<(string name, string content)> files = 
                context.AdditionalTextsProvider
                       .Where(file => Path.GetFileName(file.Path) == "protocol.json")
                       .Select((text, cancellationToken) => (Path.GetFileNameWithoutExtension(text.Path), text.GetText(cancellationToken)!.ToString()));

            context.RegisterSourceOutput(files, (SourceProductionContext spc, (string name, string content) file) =>
            {
                JsonElement json = JsonDocument.Parse(file.content).RootElement.GetProperty("types");
                
                Dictionary<string, TypeInfo> knownTypes = new Dictionary<string, TypeInfo>();

                foreach (JsonProperty type in json.EnumerateObject())
                {
                    knownTypes.Add(type.Name, ConstructType(type.Name, type.Value, knownTypes, spc));
                }
            });

        }
        private static TypeInfo ConstructType(string name, JsonElement description, IDictionary<string, TypeInfo> knownTypes, SourceProductionContext spc)
        {
            if (description.ValueKind == JsonValueKind.String)
            {
                string descriptionString = description.GetString()!;
                if (descriptionString == "native")
                {
                    switch (name)
                    {
                        case "varint":
                        {
                            return new()
                            {
                                TypeName = (JsonElement[] options) => "int",
                                Serialization = (string stream, string variable, JsonElement[] options) =>
                                    $$"""
                                      {{stream}}.WriteS32V({{variable}});
                                      """,
                                Deserialization = (string stream, string variable, JsonElement[] options) =>
                                    $$"""
                                      {{variable}} = {{stream}}.ReadS32V();
                                      """,
                            };
                        }
                        case "varlong":
                        {
                            return new()
                            {
                                TypeName = (JsonElement[] options) => "long",
                                Serialization = (string stream, string variable, JsonElement[] options) =>
                                    $$"""
                                      {{stream}}.WriteS64({{variable}});
                                      """,
                                Deserialization = (string stream, string variable, JsonElement[] options) =>
                                    $$"""
                                      {{variable}} = {{stream}}.ReadS64V();
                                      """,
                            };
                        }
                        case "pstring":
                        {
                            return new()
                            {
                                TypeName = (JsonElement[] options) => "string",
                                Serialization = (string stream, string variable, JsonElement[] options) =>
                                {
                                    TypeInfo countType = knownTypes[options[0].GetProperty("countType").GetString() ?? throw new InvalidOperationException("Missing options!")];
                                    return $$"""
                                             {
                                                 byte[] buffer = Encoding.UTF8.GetBytes({{variable}});
                                                 {{countType.Serialization(stream, "buffer.Length", options[1..])}}
                                                 {{stream}}.WriteU8A(buffer);
                                             }
                                             """;
                                },
                                Deserialization = (string stream, string variable, JsonElement[] options) =>
                                {
                                    TypeInfo countType = knownTypes[options[0].GetProperty("countType").GetString() ?? throw new InvalidOperationException("Missing options!")];
                                    return $$"""
                                             {
                                                 {{countType.TypeName(options[1..])}} size = 0;
                                                 {{countType.Deserialization(stream, "size", options[1..])}}
                                                 byte[] buffer = {{stream}}.ReadU8A(size);
                                                 {{variable}} = Encoding.UTF8.GetString(buffer);
                                             }
                                             """;
                                },
                            };
                        }
                        case "buffer":
                        {
                            return new()
                            {
                                TypeName = (JsonElement[] options) => "byte[]",
                                Serialization = (string stream, string variable, JsonElement[] options) =>
                                {
                                    TypeInfo countType = knownTypes[options[0].GetProperty("countType").GetString() ?? throw new InvalidOperationException("Missing options!")];
                                    return $$"""
                                             {
                                                 {{countType.Serialization(stream, $"{variable}.Length", options[1..])}}
                                                 {{stream}}.WriteU8A({{variable}});
                                             }
                                             """;
                                },
                                Deserialization = (string stream, string variable, JsonElement[] options) =>
                                {
                                    TypeInfo countType = knownTypes[options[0].GetProperty("countType").GetString() ?? throw new InvalidOperationException("Missing options!")];
                                    return $$"""
                                             {
                                                 {{countType.TypeName(options[1..])}} size = 0;
                                                 {{countType.Deserialization(stream, "size", options[1..])}}
                                                 {{variable}} = {{stream}}.ReadU8A(size);
                                             }
                                             """;
                                },
                            };
                        }
                        case "u8":
                        {
                            return new()
                            {
                                TypeName = (JsonElement[] options) => "byte",
                                Serialization = (string stream, string variable, JsonElement[] options) =>
                                    $$"""
                                      {{stream}}.WriteU8({{variable}});
                                      """,
                                Deserialization = (string stream, string variable, JsonElement[] options) =>
                                    $$"""
                                      {{variable}} = {{stream}}.ReadU8();
                                      """,
                            };
                        }
                        case "u16":
                        {
                            return new()
                            {
                                TypeName = (JsonElement[] options) => "ushort",
                                Serialization = (string stream, string variable, JsonElement[] options) =>
                                    $$"""
                                      {{stream}}.WriteU16({{variable}});
                                      """,
                                Deserialization = (string stream, string variable, JsonElement[] options) =>
                                    $$"""
                                      {{variable}} = {{stream}}.ReadU16();
                                      """,
                            };
                        }
                        case "u32":
                        {
                            return new()
                            {
                                TypeName = (JsonElement[] options) => "uint",
                                Serialization = (string stream, string variable, JsonElement[] options) =>
                                    $$"""
                                      {{stream}}.WriteU32({{variable}});
                                      """,
                                Deserialization = (string stream, string variable, JsonElement[] options) =>
                                    $$"""
                                      {{variable}} = {{stream}}.ReadU32();
                                      """,
                            };
                        }
                        case "u64":
                        {
                            return new()
                            {
                                TypeName = (JsonElement[] options) => "ulong",
                                Serialization = (string stream, string variable, JsonElement[] options) =>
                                    $$"""
                                      {{stream}}.WriteU64({{variable}});
                                      """,
                                Deserialization = (string stream, string variable, JsonElement[] options) =>
                                    $$"""
                                      {{variable}} = {{stream}}.ReadU64();
                                      """,
                            };
                        }
                        case "i8":
                        {
                            return new()
                            {
                                TypeName = (JsonElement[] options) => "sbyte",
                                Serialization = (string stream, string variable, JsonElement[] options) =>
                                    $$"""
                                      {{stream}}.WriteS8({{variable}});
                                      """,
                                Deserialization = (string stream, string variable, JsonElement[] options) =>
                                    $$"""
                                      {{variable}} = {{stream}}.ReadS8();
                                      """,
                            };
                        }
                        case "i16":
                        {
                            return new()
                            {
                                TypeName = (JsonElement[] options) => "short",
                                Serialization = (string stream, string variable, JsonElement[] options) =>
                                    $$"""
                                      {{stream}}.WriteS16({{variable}});
                                      """,
                                Deserialization = (string stream, string variable, JsonElement[] options) =>
                                    $$"""
                                      {{variable}} = {{stream}}.ReadS16();
                                      """,
                            };
                        }
                        case "i32":
                        {
                            return new()
                            {
                                TypeName = (JsonElement[] options) => "int",
                                Serialization = (string stream, string variable, JsonElement[] options) =>
                                    $$"""
                                      {{stream}}.WriteS32({{variable}});
                                      """,
                                Deserialization = (string stream, string variable, JsonElement[] options) =>
                                    $$"""
                                      {{variable}} = {{stream}}.ReadS32();
                                      """,
                            };
                        }
                        case "i64":
                        {
                            return new()
                            {
                                TypeName = (JsonElement[] options) => "long",
                                Serialization = (string stream, string variable, JsonElement[] options) =>
                                    $$"""
                                      {{stream}}.WriteS64({{variable}});
                                      """,
                                Deserialization = (string stream, string variable, JsonElement[] options) =>
                                    $$"""
                                      {{variable}} = {{stream}}.ReadS64();
                                      """,
                            };
                        }
                        case "bool":
                        {
                            return new()
                            {
                                TypeName = (JsonElement[] options) => "bool",
                                Serialization = (string stream, string variable, JsonElement[] options) =>
                                    $$"""
                                      {{stream}}.WriteBool({{variable}});
                                      """,
                                Deserialization = (string stream, string variable, JsonElement[] options) =>
                                    $$"""
                                      {{variable}} = {{stream}}.ReadBool();
                                      """,
                            };
                        }
                        case "f32":
                        {
                            return new()
                            {
                                TypeName = (JsonElement[] options) => "float",
                                Serialization = (string stream, string variable, JsonElement[] options) =>
                                    $$"""
                                      {{stream}}.WriteF32({{variable}});
                                      """,
                                Deserialization = (string stream, string variable, JsonElement[] options) =>
                                    $$"""
                                      {{variable}} = {{stream}}.ReadF32();
                                      """,
                            };
                        }
                        case "f64":
                        {
                            return new()
                            {
                                TypeName = (JsonElement[] options) => "double",
                                Serialization = (string stream, string variable, JsonElement[] options) =>
                                    $$"""
                                      {{stream}}.WriteF64({{variable}});
                                      """,
                                Deserialization = (string stream, string variable, JsonElement[] options) =>
                                    $$"""
                                      {{variable}} = {{stream}}.ReadF64();
                                      """,
                            };
                        }
                        case "UUID":
                        {
                            return new()
                            {
                                TypeName = (JsonElement[] options) => "Guid",
                                Serialization = (string stream, string variable, JsonElement[] options) =>
                                    $$"""
                                      {{stream}}.WriteGuid({{variable}});
                                      """,
                                Deserialization = (string stream, string variable, JsonElement[] options) =>
                                    $$"""
                                      {{variable}} = {{stream}}.ReadGuid();
                                      """,
                            };
                        }
                        case "option":
                        {
                            return new()
                            {
                                TypeName = (JsonElement[] options) =>
                                {
                                    TypeInfo wrappedType = knownTypes[options[0].GetString() ?? throw new InvalidOperationException("Missing options!")];
                                    return $"Nullable<{wrappedType.TypeName}>";
                                },
                                Serialization = (string stream, string variable, JsonElement[] options) =>
                                {
                                    TypeInfo wrappedType = knownTypes[options[0].GetString() ?? throw new InvalidOperationException("Missing options!")];
                                    return $$"""
                                             {
                                                 {{stream}}.WriteBool({{variable}}.HasValue);   
                                                 if ({{variable}}.HasValue) 
                                                 {
                                                     {{wrappedType.Serialization(stream, $"{variable}.Length", options[1..])}}
                                                 }
                                             }
                                             """;
                                },
                                Deserialization = (string stream, string variable, JsonElement[] options) =>
                                {
                                    TypeInfo wrappedType = knownTypes[options[0].GetString() ?? throw new InvalidOperationException("Missing options!")];
                                    return $$"""
                                             {
                                                 bool hasValue = {{stream}}.ReadBool();
                                                 if (hasValue) 
                                                 {
                                                     {{wrappedType.Deserialization(stream, variable, null)}}
                                                 }
                                                 else
                                                 {
                                                     {{variable}} = null;
                                                 }
                                             }
                                             """;
                                },
                            };
                        }
                        default:
                        {
                            throw new NotImplementedException(name);
                        }
                    }
                }
                else if (knownTypes.TryGetValue(descriptionString, out TypeInfo typeInfo)) return typeInfo;
                else throw new InvalidDataException($"Unknown type: \"{name}\"!");
            }
            else
            {
                throw new NotImplementedException(description.GetRawText().ReplaceLineEndings(""));
            }
        }
    }
}