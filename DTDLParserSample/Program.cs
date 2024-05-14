﻿using CommandLine;
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

        private static object? TranslateValue(object? Value) => Value switch
        {
            JsonElement element => element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.GetSingle(),
                _ => Value,
            },
            _ => Value,
        };

        private static void CheckJsonElement(JsonElement Value)
        {
            switch (Value.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (JsonProperty jsonProp in Value.EnumerateObject())
                    {
                        if (jsonProp.Value.ValueKind != JsonValueKind.Object)
                        {
                            Logging.LogOutPutNoCR(string.Format("{0, 16} : {1} = ", "", jsonProp.Name));
                        }
                        CheckJsonElement(jsonProp.Value);
                    }

                    break;

                case JsonValueKind.Array:
                    var index = 0;

                    foreach (JsonElement arrayElement in Value.EnumerateArray())
                    {
                        Logging.LogOutPut(string.Format("{0, 16} : {1} = ", "Array", index++));
                        CheckJsonElement(arrayElement);
                    }
                    break;

                case JsonValueKind.Number:
                    Logging.LogOutPut(string.Format("{0, 16} : Type = {1} = ", Value.GetSingle(), Value.ValueKind));
                    break;
                case JsonValueKind.String:
                    Logging.LogOutPut(string.Format("{0, 16} : Type = {1} = ", Value.GetString(), Value.ValueKind));
                    break;
                case JsonValueKind.True:
                case JsonValueKind.False:
                case JsonValueKind.Null:
                    break;

                default:
                    throw new FormatException($"Error: unsupported JSON token [{Value.ValueKind}]");
            }
        }

        private static void CheckJsonElement(string ElementName, JsonElement JsonElement, object DtInfo)
        {
            switch (JsonElement.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (JsonProperty jsonProp in JsonElement.EnumerateObject())
                    {
                        if (jsonProp.Value.ValueKind != JsonValueKind.Object)
                        {
                            Logging.LogOutPutNoCR(string.Format("{0, 16} : {1} = ", "", jsonProp.Name));
                        }

                        DTPropertyInfo? propInfo = DtInfo as DTPropertyInfo;
                        DTObjectInfo? obInfo = propInfo.Schema as DTObjectInfo;

                        DTFieldInfo? fieldInfo = obInfo.Fields.FirstOrDefault(x => x.Name == jsonProp.Name);
                        CheckJsonElement(jsonProp.Name, jsonProp.Value, fieldInfo);
                    }

                    break;

                case JsonValueKind.Array:
                    var index = 0;

                    foreach (JsonElement arrayElement in JsonElement.EnumerateArray())
                    {
                        Logging.LogOutPut(string.Format("{0, 16} : {1} = ", "Array", index++));
                        CheckJsonElement(arrayElement);
                    }
                    break;

                case JsonValueKind.Number:
                    {
                        var value = JsonElement.GetSingle();
                        Logging.LogOutPut($"{value} (type = {JsonElement.ValueKind})");

                        DTFieldInfo fieldInfo = DtInfo as DTFieldInfo;

                        if (fieldInfo != null &&  fieldInfo.UndefinedTypes.Contains("MinMax"))
                        {
                            var f = fieldInfo.UndefinedProperties.Where(x => x.Key == "minValue").FirstOrDefault();
                            var min = f.Value.GetSingle();

                            f = fieldInfo.UndefinedProperties.Where(x => x.Key == "maxValue").FirstOrDefault();
                            var max = f.Value.GetSingle();

                            if (value > max || value < min)
                            {
                                Logging.LogError(string.Format("{0, 16} : Value {1} outside of min {2} - max {3} range", "Error", value, min, max));
                            }
                            else
                            {
                                Logging.LogSuccess(string.Format("{0, 16} : Value {1} outside of min {2} - max {3} range", "", value, min, max));
                            }
                        }
                    }

                    break;
                case JsonValueKind.String:
                    Logging.LogOutPut($"{JsonElement.GetString()} (type = {JsonElement.ValueKind})");
                    break;
                case JsonValueKind.True:
                case JsonValueKind.False:
                case JsonValueKind.Null:
                    break;

                default:
                    throw new FormatException($"Error: unsupported JSON token [{JsonElement.ValueKind}]");
            }
        }

        static void ProcessCoType(DTEntityInfo DtEntity)
        {
            if (DtEntity.UndefinedTypes.Count > 0)
            {
                foreach (var undefinedType in DtEntity.UndefinedTypes)
                {
                    Logging.LogOutPutHighLight(string.Format("{0, 16} : {1}", "CoType Type", undefinedType));
                }
            }
            if (DtEntity.UndefinedProperties.Count > 0)
            {
                foreach (var undefinedProperty in DtEntity.UndefinedProperties)
                {
                    Logging.LogOutPutHighLight(string.Format("{0, 16} : {1} = {2}", "CoType Property", undefinedProperty.Key, undefinedProperty.Value));
                }
            }
        }

        static void ProcessDtEnum(DTEnumInfo DtEnum)
        {
            foreach (var enumValue in DtEnum.EnumValues)
            {
                Logging.LogOutPut(string.Format("{0, 16} : Name  = {1}", "", enumValue.Name));
                Logging.LogOutPut(string.Format("{0, 16} : Value = {1}", "", enumValue.EnumValue));
            }
        }

        static void ProcessDtField(DTFieldInfo DtField)
        {
            Logging.LogOutPut("----------------");
            Logging.LogOutPut(string.Format("{0, 16} : Name = {1}", "Field", DtField.Name));
            Logging.LogOutPut(string.Format("{0, 16} : Display Name = {1}", "", (DtField.DisplayName.Count > 0 ? DtField.DisplayName.FirstOrDefault().Value : "<No Display Name>")));
            Logging.LogOutPut(string.Format("{0, 16} : Description = {1}", "", (DtField.Description.Count > 0 ? DtField.Description.FirstOrDefault().Value : "<No Description>")));
            Logging.LogOutPut(string.Format("{0, 16} : Schema = {1}", "", DtField.Schema.EntityKind));
            if (!String.IsNullOrEmpty(DtField.Comment))
            {
                Logging.LogOutPut(string.Format("{0, 16} : Comment = {1}", "", DtField.Comment));
            }

            ProcessCoType(DtField);

            if (DtField.Schema != null)
            {
                ProcessDtSchema(DtField.Schema);
            }
        }

        static void ProcessDtSchema(DTSchemaInfo DtSchema)
        {
            try
            {
                if (!String.IsNullOrEmpty(DtSchema.Comment))
                {
                    Logging.LogOutPut(string.Format("{0, 16} : Comment = {1}", "", DtSchema.Comment));
                }

                switch (DtSchema.EntityKind)
                {
                    case DTEntityKind.Object:
                        {
                            DTObjectInfo dtVal = (DTObjectInfo)DtSchema;

                            ProcessCoType(DtSchema);

                            foreach (var field in dtVal.Fields)
                            {
                                ProcessDtField(field);
                            }
                        }
                        break;

                    case DTEntityKind.Array:
                        {
                            DTArrayInfo dtVal = (DTArrayInfo)DtSchema;
                            ProcessDtSchema(dtVal.ElementSchema);
                        }
                        break;

                    case DTEntityKind.Enum:
                        {
                            DTEnumInfo dtVal = (DTEnumInfo)DtSchema;
                            ProcessDtEnum(dtVal);

                        }
                        break;

                    case DTEntityKind.Integer:
                    case DTEntityKind.Double:
                    case DTEntityKind.String:
                    case DTEntityKind.Date:
                    case DTEntityKind.DateTime:
                    case DTEntityKind.Boolean:
                        break;
                    default:
                        Logging.LogWarn(string.Format("Skipping {0}", DtSchema.EntityKind.ToString()));
                        break;
                }
            }
            catch (Exception e)
            {
                Logging.LogError($"Error Processing DTSchemaInfo in ProcessDtSchema()\n{e.Message}");
                return;
            }
}

        static void Main(string[] Args)
        {
            CommandLine.Parser.Default.ParseArguments<Options>(Args)
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
        // main body of the program
        //
        static void RunOptions(Options Opts)
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
            // Model ID from the payload
            //
            string modelId = String.Empty;

            //
            // Parse JSON file into JsonDocument as we do not know structure of JSON
            //
            JsonDocument? jsonDoc = null;

            try
            {
                //
                // Check to see if input data is specified.  This is to simulate input property payload.
                //
                if (Opts.InputFile != string.Empty)
                {
                    var fileInfo = new FileInfo(Opts.InputFile);

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
                Logging.LogError($"Error accessing the input file '{Opts.InputFile}': Exiting... \n{e.Message}");
                return;
            }

            //
            // Parse model(s)
            //
            if (String.IsNullOrEmpty(modelId))
            {
                Logging.LogError($"Model ID not specified: Exiting...");
                return;
            }    


            //
            // Make sure Model Folder is valid
            //
            DirectoryInfo? dinfo = null;

            try
            {
                if (Opts.ModelFolder != string.Empty)
                {
                    dinfo = new DirectoryInfo(Opts.ModelFolder);
                }

                if (dinfo != null && dinfo.Exists == false)
                {
                    Logging.LogError($"Specified directory '{Opts.ModelFolder}' does not exist: Exiting...");
                    return;
                }

            }
            catch (Exception e)
            {
                Logging.LogError($"Error accessing the target directory '{Opts.ModelFolder}': Exiting... \n{e.Message}");
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

            var interfaces = modelData.Where(r => r.Value.EntityKind == DTEntityKind.Property || r.Value.EntityKind == DTEntityKind.Telemetry || r.Value.EntityKind == DTEntityKind.Command).ToList();

            foreach (var item in interfaces)
            {
                var val = item.Value;
                Logging.LogOutPut("=================================================");
                Logging.LogOutPutHighLight(string.Format("{0, 16} : {1}", "Interface Type", val.EntityKind));
                Logging.LogOutPut(string.Format("{0, 16} : {1}", "Display Name", (val.DisplayName.Count > 0 ? val.DisplayName.FirstOrDefault().Value : "<No Display Name>")));
                Logging.LogOutPut(string.Format("{0, 16} : {1}", "Description", (val.Description.Count > 0 ? val.Description.FirstOrDefault().Value : "<No Description>")));

                if (!String.IsNullOrEmpty(val.Comment))
                {
                    Logging.LogOutPut(string.Format("{0, 16} : {1}", "Comment", val.Comment));
                }

                switch (val.EntityKind)
                {
                    case DTEntityKind.Property:
                        {
                            DTPropertyInfo dtVal = (DTPropertyInfo)val;
                            Logging.LogOutPut(string.Format("{0, 16} : {1}", "Name", dtVal.Name));
                            Logging.LogOutPut(string.Format("{0, 16} : {1}", "Schema", dtVal.Schema));
                            ProcessDtSchema(dtVal.Schema);
                        }
                        break;
                    case DTEntityKind.Telemetry:
                        {
                            DTTelemetryInfo dtVal = (DTTelemetryInfo)val;
                            Logging.LogOutPut(string.Format("{0, 16} : {1}", "Name", dtVal.Name));
                            Logging.LogOutPut(string.Format("{0, 16} : {1}", "Schema", dtVal.Schema));
                            ProcessDtSchema(dtVal.Schema);
                        }
                        break;
                    case DTEntityKind.Command:
                        {
                            DTCommandInfo dtVal = (DTCommandInfo)val;
                            Logging.LogOutPut(string.Format("{0, 16} : {1}", "Name", dtVal.Name));

                            if (dtVal.Request != null)
                            {
                                Logging.LogOutPut(string.Format("{0, 16} : {1}", "Request Name", dtVal.Request.Name));
                                Logging.LogOutPut(string.Format("{0, 16} : {1}", "Schema", dtVal.Request.Schema));
                                ProcessDtSchema(dtVal.Request.Schema);
                            }
                        }
                        break;
                }

                ProcessCoType(val);
            }

#if old
            var properties = modelData.Where(r => r.Value.EntityKind == DTEntityKind.Property).ToList();

            foreach (var property in properties)
            {
                DTPropertyInfo propInfo = property.Value as DTPropertyInfo;

                Logging.LogOutPut("=================================================");
                Logging.LogOutPutHighLight(string.Format("{0, 16} : {1}", "Interface Type", "Property"));
                Logging.LogOutPut(string.Format("{0, 16} : {1}", "Display Name", (propInfo.DisplayName.Count > 0 ? propInfo.DisplayName.FirstOrDefault().Value : "<No Display Name>")));
                Logging.LogOutPut(string.Format("{0, 16} : {1}", "Name", propInfo.Name));
                Logging.LogOutPut(string.Format("{0, 16} : {1}", "Type", propInfo.Schema));

                switch (propInfo.Schema)
                {
                    case DTObjectInfo objInfo:

                        if (objInfo.UndefinedTypes.Count > 0)
                        {
                            foreach (var undefinedType in objInfo.UndefinedTypes)
                            {
                                Logging.LogOutPut(string.Format("{0, 16} : {1}", "Undefined Type", undefinedType));
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
                                    Logging.LogOutPut(string.Format("{0, 16} : {1} = {2}", "Undefined Prop", undefinedProperty.Key, undefinedProperty.Value));
                                }


                            }
                        }

                        foreach (DTFieldInfo field in objInfo.Fields)
                        {
                            Logging.LogOutPut($"           Field : Name = {(field.DisplayName.Count > 0 ? field.DisplayName.FirstOrDefault().Value : field.Name)}");

                            foreach (var undefinedProperty in field.UndefinedProperties)
                            {
                                Logging.LogOutPut(string.Format("{0, 16} : {1} = {2}", "Undefined Prop", undefinedProperty.Key, undefinedProperty.Value));
                            }
                        }

                        break;

                    case DTIntegerInfo intInfo:

                        if (intInfo.UndefinedTypes.Count > 0)
                        {
                            foreach (var undefinedType in intInfo.UndefinedTypes)
                            {
                                Logging.LogOutPut(string.Format("{0, 16} : {1}", "Undefined Types", undefinedType));
                            }
                        }

                        break;

                    default:

                        if (propInfo.UndefinedTypes.Count > 0)
                        {
                            foreach (var undefinedType in propInfo.UndefinedTypes)
                            {
                                Logging.LogOutPut(string.Format("{0, 16} : {1}", "Undefined Types", undefinedType));
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
                        Logging.LogOutPut(string.Format("{0, 16} : {1} = {2}", "Undefined Prop", undefinedProperty.Key, value));
                    }
                }
            }

            var telemetries = modelData.Where(r => r.Value.EntityKind == DTEntityKind.Telemetry).ToList();
            foreach (var telemetry in telemetries)
            {
                Logging.LogOutPut("=================================================");
                Logging.LogOutPut(string.Format("{0, 16} : {1}", "Interface Type", "Telemetry"));

                DTTelemetryInfo telemetryInfo = telemetry.Value as DTTelemetryInfo;

                Logging.LogOutPut(string.Format("{0, 16} : {1}", "Display Name", (telemetryInfo.DisplayName.Count > 0 ? telemetryInfo.DisplayName.FirstOrDefault().Value : "<No Display Name>")));
                Logging.LogOutPut(string.Format("{0, 16} : {1}", "Name", telemetryInfo.Name));
                Logging.LogOutPut(string.Format("{0, 16} : {1}", "Type", telemetryInfo.Schema));
            }

            var commands = modelData.Where(r => r.Value.EntityKind == DTEntityKind.Command).ToList();
            foreach (var command in commands)
            {
                Logging.LogOutPut("=================================================");
                Logging.LogOutPut(string.Format("{0, 16} : {1}", "Interface Type", "Commmand"));

                DTCommandInfo commandInfo = command.Value as DTCommandInfo;

                Logging.LogOutPut(string.Format("{0, 16} : {1}", "Display Name", (commandInfo.DisplayName.Count > 0 ? commandInfo.DisplayName.FirstOrDefault().Value : "<No Display Name>")));
                Logging.LogOutPut(string.Format("{0, 16} : {1}", "Command Name", commandInfo.Name));
            }
#endif
            //
            // Process simulated payload against DTDL's MinMax settings
            //
            Logging.LogOutPutHighLight("=================================================");
            Logging.LogOutPutHighLight(string.Format("{0, 16}", "Input Data"));
            Logging.LogOutPutHighLight(string.Format("{0, 16}", "Property"));

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
                    Logging.LogOutPut(string.Format("{0, 16} : {1}", "Type", je.ValueKind.ToString()));
                    CheckJsonElement(element.Name, je, propInfo);
                }
            }
        }
    }
}
