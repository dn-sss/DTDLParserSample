using CommandLine;
using System.Reflection;
using DTDLParser;
using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using DTDLParser.Models;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Xml.Linq;

namespace DTDLParserSample
{
    internal class Program
    {
        public class Options
        {
            [Option('e', "extension", Default = "json", SetName = "normal", HelpText = "File extension of files to be processed.")]
            public string? Extension { get; set; }

            [Option('i', "input", Default = "input.json", SetName = "normal", HelpText = "Simulated Input Data.")]
            public required string InputFile { get; set; }

            [Option('d', "model folder", Default = ".", SetName = "normal", HelpText = "Directory to search model files.")]
            public required string ModelFolder { get; set; }

            [Option('r', "recursive", Default = false, SetName = "normal", HelpText = "Search given directory (option -d) only (false) or subdirectories too (true)")]
            public bool Recursive { get; set; }
        }

        private static object? TranslateValue(object? value) => value switch
        {
            JsonElement element => element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.GetSingle(),
                _ => value,
            },
            _ => value,
        };

        //private static void PrintUndefinedType()
        //{

        //}

        //private static void PrintUndefinedProperty()
        //{

        //}

        private static void CheckJsonElement(JsonElement value)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (JsonProperty jsonProp in value.EnumerateObject())
                    {
                        if (jsonProp.Value.ValueKind != JsonValueKind.Object)
                        {
                            Logging.LogOutPutNoCR($"                 : {jsonProp.Name} = ");
                        }
                        CheckJsonElement(jsonProp.Value);
                    }

                    break;

                case JsonValueKind.Array:
                    var index = 0;

                    foreach (JsonElement arrayElement in value.EnumerateArray())
                    {
                        Logging.LogOutPut($"           Array : {index++}");
                        CheckJsonElement(arrayElement);
                    }
                    break;

                case JsonValueKind.Number:
                    Logging.LogOutPut($"{value.GetSingle()} (type = {value.ValueKind})");
                    break;
                case JsonValueKind.String:
                    Logging.LogOutPut($"{value.GetString()} (type = {value.ValueKind})");
                    break;
                case JsonValueKind.True:
                case JsonValueKind.False:
                case JsonValueKind.Null:
                    break;

                default:
                    throw new FormatException($"Error: unsupported JSON token [{value.ValueKind}]");
            }
        }

        private static void CheckJsonElement(string elementName, JsonElement jsonElement, object dtInfo)
        {
            switch (jsonElement.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (JsonProperty jsonProp in jsonElement.EnumerateObject())
                    {
                        if (jsonProp.Value.ValueKind != JsonValueKind.Object)
                        {
                            Logging.LogOutPutNoCR($"                 : {jsonProp.Name} = ");
                        }

                        DTPropertyInfo propInfo = dtInfo as DTPropertyInfo;
                        DTObjectInfo objInfo = propInfo.Schema as DTObjectInfo;

                        DTFieldInfo fieldInfo = objInfo.Fields.Where(x => x.Name == jsonProp.Name).FirstOrDefault();
                        CheckJsonElement(jsonProp.Name, jsonProp.Value, fieldInfo);
                    }

                    break;

                case JsonValueKind.Array:
                    var index = 0;

                    foreach (JsonElement arrayElement in jsonElement.EnumerateArray())
                    {
                        Logging.LogOutPut($"           Array : {index++}");
                        CheckJsonElement(arrayElement);
                    }
                    break;

                case JsonValueKind.Number:
                    {
                        var value = jsonElement.GetSingle();
                        Logging.LogOutPut($"{value} (type = {jsonElement.ValueKind})");

                        DTFieldInfo fieldInfo = dtInfo as DTFieldInfo;

                        if (fieldInfo != null &&  fieldInfo.UndefinedTypes.Contains("MinMax"))
                        {
                            var f = fieldInfo.UndefinedProperties.Where(x => x.Key == "minValue").FirstOrDefault();
                            var min = f.Value.GetSingle();

                            f = fieldInfo.UndefinedProperties.Where(x => x.Key == "maxValue").FirstOrDefault();
                            var max = f.Value.GetSingle();

                            if (value > max || value < min)
                            {
                                Logging.LogError ($"           Error : Value {value} outside of min ({min}) - max {max} range");
                            }
                            else
                            {
                                Logging.LogSuccess($"                 : Value {value} inside of min ({min}) - max {max} range");
                            }
                        }
                    }

                    break;
                case JsonValueKind.String:
                    Logging.LogOutPut($"{jsonElement.GetString()} (type = {jsonElement.ValueKind})");
                    break;
                case JsonValueKind.True:
                case JsonValueKind.False:
                case JsonValueKind.Null:
                    break;

                default:
                    throw new FormatException($"Error: unsupported JSON token [{jsonElement.ValueKind}]");
            }
        }

        static void Main(string[] args)
        {
            CommandLine.Parser.Default.ParseArguments<Options>(args)
              .WithParsed(RunOptions)
              .WithNotParsed(HandleParseError);
        }

        //
        // Handle Command Line parse error
        //
        static void HandleParseError(IEnumerable<Error> errs)
        {
            Logging.LogError($"Invalid command line.");
            foreach (Error e in errs)
            {
                Logging.LogError($"{e.Tag}: {e.ToString()}");
            }

        }

        //
        // Main body of the code
        //
        static async void RunOptions(Options opts)
        {
            //
            // Display Parser version for reference
            //
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            string dtdlParserVersion = "<unknown>";
            foreach (Assembly a in assemblies)
            {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                if (a.GetName().Name.EndsWith("DTDLParser"))
                {
                    dtdlParserVersion = a.GetName().Version.ToString();
                }
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            }

            Logging.LogSuccess($"DTDL Parser (dtdl parser library version {dtdlParserVersion}");

            //
            // Process input JSON file with simulated data payload
            //
            
            //
            // Model ID from the payload
            //
            string modelId = String.Empty;

            //
            // Parse JSON file into JsonDocument as we do not know structure of JSON
            //
            JsonDocument? jsonDoc = null;

            try
            {
                if (opts.InputFile != string.Empty)
                {
                    var fileInfo = new FileInfo(opts.InputFile);

                    if (!fileInfo.Exists)
                    {
                        Logging.LogError($"{fileInfo.FullName} does not exist: Exiting...");
                        return;
                    }

                    Logging.LogSuccess($"Reading simulated data : '{fileInfo.FullName}'");

                    //
                    // Read file contents
                    //
                    StreamReader r = new StreamReader(fileInfo.FullName);
                    string inputData = r.ReadToEnd();
                    r.Close();

                    //
                    // Make sure it is a valid JSON file
                    //
                    try
                    {
                        //
                        // Parse JSON into JsonDocument
                        //
                        jsonDoc = JsonDocument.Parse(inputData);

                    }
                    catch (Exception e)
                    {
                        Logging.LogError($"Invalid json found in file {fileInfo.FullName}.\nJson parser error: Exiting... \n{e.Message}");
                        return;
                    }

                    //
                    // Make sure the payload contains Model ID
                    //
                    if (jsonDoc == null || !jsonDoc.RootElement.TryGetProperty("$modelId", out var modelElement))
                    {
                        Logging.LogError($"'{fileInfo.FullName}' missing $modelId: Exiting...");
                        return;
                    }

#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                    modelId = modelElement.GetString();
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.

                    if (string.IsNullOrEmpty(modelId))
                    {
                        Logging.LogError($"Model ID empty or null '{modelId}': Exiting...");
                        return;
                    }

                    Logging.LogSuccess($"Found DTDL ID in '{fileInfo.FullName}' : '{modelId}'");
                }
            }
            catch (Exception e)
            {
                Logging.LogError($"Error accessing the input file '{opts.InputFile}': Exiting... \n{e.Message}");
                return;
            }



            //
            // Parse model(s)
            //

            //
            // Make sure Model Folder is valie
            //

            DirectoryInfo? dinfo = null;

            try
            {
                if (opts.ModelFolder != string.Empty)
                {
                    dinfo = new DirectoryInfo(opts.ModelFolder);
                }

                if (dinfo != null && dinfo.Exists == false)
                {
                    Logging.LogError($"Specified directory '{opts.ModelFolder}' does not exist: Exiting...");
                    return;
                }

            }
            catch (Exception e)
            {
                Logging.LogError($"Error accessing the target directory '{opts.ModelFolder}': Exiting... \n{e.Message}");
                return;
            }


            //
            // Prepare to parse DTDL Model
            //
            DTDLResolver resolver = new DTDLResolver();

            Logging.LogSuccess($"Parsing DTDL in '{dinfo.FullName}' for '{modelId}'");
            var modelData = resolver.ParseModel(modelId, dinfo.FullName);

            if (modelData == null)
            {
                Logging.LogError($"Model Data empty: Exiting...");
                return;
            }


            var properties = modelData.Where(r => r.Value.EntityKind == DTEntityKind.Property).ToList();
            foreach ( var property in properties)
            {
                DTPropertyInfo propInfo = property.Value as DTPropertyInfo;

                Logging.LogOutPut("=================================================");
                Logging.LogOutPut($"        Property");
                Logging.LogOutPut($"    Display Name : {(propInfo.DisplayName.Count > 0 ? propInfo.DisplayName.FirstOrDefault().Value : "No Display Name")}");
                Logging.LogOutPut($"            Name : {propInfo.Name}");
                Logging.LogOutPut($"            Type : {propInfo.Schema}");

                switch (propInfo.Schema)
                {
                    case DTObjectInfo objInfo:

                        if (objInfo.UndefinedTypes.Count > 0)
                        {
                            foreach (var undefinedType in objInfo.UndefinedTypes)
                            {
                                Logging.LogOutPut($"  Undefined Type : {undefinedType}");
                            }
                        }

                        if (objInfo.UndefinedProperties.Count > 0)
                        {
                            foreach (var undefinedProperty in objInfo.UndefinedProperties)
                            {
                                var value = TranslateValue(undefinedProperty.Value);

                                if (value is JsonElement je && je.ValueKind == JsonValueKind.Array)
                                {
                                    CheckJsonElement(je);
                                }
                                else
                                {
                                    Logging.LogOutPut($"  Undefined Prop : {undefinedProperty.Key} = {value}");
                                }


                            }
                        }

                        foreach (DTFieldInfo field in objInfo.Fields)
                        {
                            Logging.LogOutPut($"           Field : Name = {(field.DisplayName.Count > 0 ? field.DisplayName.FirstOrDefault().Value : field.Name)}");

                            foreach (var undefinedProperty in field.UndefinedProperties)
                            {
                                Logging.LogOutPut($"  Undefined Prop : {undefinedProperty.Key} = {undefinedProperty.Value}");
                            }
                        }

                        break;

                    case DTIntegerInfo intInfo:

                        if (intInfo.UndefinedTypes.Count > 0)
                        {
                            foreach (var undefinedType in intInfo.UndefinedTypes)
                            {
                                Logging.LogOutPut($"    Undefined Types (Object) : {undefinedType}");
                            }
                        }

                        break;

                    default:

                        if (propInfo.UndefinedTypes.Count > 0)
                        {
                            foreach (var undefinedType in propInfo.UndefinedTypes)
                            {
                                Logging.LogOutPut($"    Undefined Types : {undefinedType}");
                            }
                        }

                        break;
                        //throw new ArgumentOutOfRangeException(nameof(propInfo.Schema), "Only schemas of type DTObjectInfocan is supported.");
                }


                if (propInfo.UndefinedProperties.Count > 0)
                {
                    foreach (var undefinedProperty in propInfo.UndefinedProperties)
                    {
                        var value = TranslateValue(undefinedProperty.Value);
                        Logging.LogOutPut($" Undefined Prop  : {undefinedProperty.Key} = {value}");
                    }
                }
            }

            var telemetries = modelData.Where(r => r.Value.EntityKind == DTEntityKind.Telemetry).ToList();
            foreach (var telemetry in telemetries)
            {

                Logging.LogOutPut("=================================================");
                Logging.LogOutPut($"       Telemetry");

                DTTelemetryInfo telemetryInfo = telemetry.Value as DTTelemetryInfo;

                Logging.LogOutPut($"    Display Name : {(telemetryInfo.DisplayName.Count > 0 ? telemetryInfo.DisplayName.FirstOrDefault().Value : "No Display Name")}");
                Logging.LogOutPut($"            Name : {telemetryInfo.Name}");
                Logging.LogOutPut($"            Type : {telemetryInfo.Schema}");
            }

            var commands = modelData.Where(r => r.Value.EntityKind == DTEntityKind.Command).ToList();
            foreach (var command in commands)
            {
                Logging.LogOutPut("=================================================");
                Logging.LogOutPut($"         Commman");

                DTCommandInfo commandInfo = command.Value as DTCommandInfo;

                Logging.LogOutPut($"    Display Name : {(commandInfo.DisplayName.Count > 0 ? commandInfo.DisplayName.FirstOrDefault().Value : "No Display Name")}");
                Logging.LogOutPut($"            Name : {commandInfo.Name}");

                Logging.LogOutPut($"Command Name   : {commandInfo.Name}");
            }


            Logging.LogOutPut("=================================================");
            Logging.LogOutPut($"      Input Data");
            Logging.LogOutPut($"        Property");

            foreach (var element in jsonDoc.RootElement.EnumerateObject())
            {
                if (element.Name.StartsWith("$"))
                {
                    continue;
                }

                var value = TranslateValue(element.Value);

                Logging.LogOutPut($"            Name : {element.Name}");

                DTPropertyInfo propInfo = modelData.Where(r => r.Value.EntityKind == DTEntityKind.Property)
                    .Select(x => x.Value as DTPropertyInfo)
                    .Where(x => x.Writable == true)
                    .FirstOrDefault(x => x.Name == element.Name);

                if ((propInfo != null) && (value is JsonElement je))
                {
                    Logging.LogOutPut($"            Type : {je.ValueKind.ToString()}");
                    CheckJsonElement(element.Name, je, propInfo);
                }
            }
        }
    }
}
