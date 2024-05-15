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
using Microsoft.EntityFrameworkCore.Metadata;
using Newtonsoft.Json;
using System.Runtime.Serialization.Formatters;

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

        static bool HasMinMax(DTEntityInfo DtEntity)
        {
            bool ret = ((DtEntity.UndefinedTypes.Count != 0) && (DtEntity.UndefinedTypes.Contains("MinMax")));

            return ret;
        }

        static (float min, float max) GetMinMax(DTEntityInfo DtEntity)
        {
            var f = DtEntity.UndefinedProperties.Where(x => x.Key == "minValue").FirstOrDefault();
            var min = f.Value.GetSingle();

            f = DtEntity.UndefinedProperties.Where(x => x.Key == "maxValue").FirstOrDefault();
            var max = f.Value.GetSingle();

            return (min, max);
        }

        static void ProcessJsonElement(DTEntityInfo DtEntity, string JsonElementName, JsonElement Je)
        {
            try
            {
                switch (Je.ValueKind)
                {
                    case JsonValueKind.Object:
                        {
                            foreach (JsonProperty jsonProp in Je.EnumerateObject())
                            {
                                if (DtEntity is DTPropertyInfo)
                                {
                                    DTPropertyInfo dtPropInfo = DtEntity as DTPropertyInfo;
                                    ProcessJsonElement(dtPropInfo.Schema, jsonProp.Name, jsonProp.Value);
                                }
                                else if (DtEntity is DTObjectInfo)
                                {
                                    DTObjectInfo dtObjInfo = DtEntity as DTObjectInfo;

                                    Logging.LogOutPutHighLight(string.Format("{0, 16} : Name  = {1}", "Field", JsonElementName));

                                    var dtFieldInfo = dtObjInfo.Fields.First(f => f.Name == JsonElementName);
                                    ProcessJsonElement(dtFieldInfo, jsonProp.Name, jsonProp.Value);
                                }
                            }
                        }

                        break;

                    case JsonValueKind.Number:

                        var val = Je.GetSingle();

                        switch (DtEntity.EntityKind)
                        {
                            case DTEntityKind.Property:
                                {
                                    Logging.LogOutPutHighLight(string.Format("{0, 16} : Name  = {1}", "Property", JsonElementName));
                                    Logging.LogOutPutHighLight(string.Format("{0, 16} : Value = {1}", "", val));
                                    DTPropertyInfo dtPropInfo = DtEntity as DTPropertyInfo;

                                    if (HasMinMax(dtPropInfo))
                                    {
                                        var minmax = GetMinMax(dtPropInfo);

                                        if (val > minmax.max || val < minmax.min)
                                        {
                                            Logging.LogError(string.Format("{0, 16} : Invalid : Value {1} outside of min {2} - max {3} range", "", val, minmax.min, minmax.max));
                                        }
                                        else
                                        {
                                            Logging.LogSuccess(string.Format("{0, 16} : Valid : Value {1} within min {2} - max {3} range", "", val, minmax.min, minmax.max));
                                        }
                                    }
                                }

                                break;

                            case DTEntityKind.Object:
                                {
                                    Logging.LogOutPutHighLight(string.Format("{0, 16} : Name  = {1}", "Field", JsonElementName));
                                    Logging.LogOutPutHighLight(string.Format("{0, 16} : Value = {1}", "", val));

                                    DTObjectInfo dtObjInfo = DtEntity as DTObjectInfo;
                                    DTFieldInfo dtFieldInfo = dtObjInfo.Fields.FirstOrDefault(x => x.Name == JsonElementName);

                                    if (HasMinMax(dtFieldInfo))
                                    {
                                        var minmax = GetMinMax(dtFieldInfo);

                                        if (val > minmax.max || val < minmax.min)
                                        {
                                            Logging.LogError(string.Format("{0, 16} : Invalid : Value {1} outside of min {2} - max {3} range", "", val, minmax.min, minmax.max));
                                        }
                                        else
                                        {
                                            Logging.LogSuccess(string.Format("{0, 16} : Valid : Value {1} within min {2} - max {3} range", "", val, minmax.min, minmax.max));
                                        }
                                    }
                                    else if (HasMinMax(dtObjInfo))
                                    {
                                        float min = 0;
                                        float max = 0;

                                        var isArray = dtObjInfo.UndefinedProperties.FirstOrDefault(x => x.Key == "MinMaxArray");

                                        if (isArray.Key == null)
                                        {
                                            var minmax = GetMinMax(dtObjInfo);
                                            min = minmax.min;
                                            max = minmax.max;
                                        }
                                        else
                                        {
                                            var array = isArray.Value;
                                            foreach (var jsonProp in array.EnumerateArray())
                                            {
                                                var name = jsonProp.EnumerateObject().FirstOrDefault(s => s.Name == "name");

                                                if (name.Value.GetString().Equals(JsonElementName))
                                                {
                                                    var minValue = jsonProp.EnumerateObject().FirstOrDefault(s => s.Name == "minValue");
                                                    var maxValue = jsonProp.EnumerateObject().FirstOrDefault(s => s.Name == "maxValue");

                                                    min = minValue.Value.GetSingle();
                                                    max = maxValue.Value.GetSingle();
                                                    break;
                                                }
                                            }
                                        }

                                        if (val > max || val < min)
                                        {
                                            Logging.LogError(string.Format("{0, 16} : Invalid : Value {1} outside of min {2} - max {3} range", "", val, min, max));
                                        }
                                        else
                                        {
                                            Logging.LogSuccess(string.Format("{0, 16} : Valid : Value {1} within min {2} - max {3} range", "", val, min, max));
                                        }

                                    }
                                }
                                break;

                            case DTEntityKind.Field:
                                {
                                    DTFieldInfo dtFieldInfo = DtEntity as DTFieldInfo;

                                    if (dtFieldInfo.Schema.EntityKind == DTEntityKind.Object)
                                    {
                                        DTObjectInfo dtObjInfo = dtFieldInfo.Schema as DTObjectInfo;

                                        ProcessJsonElement(dtObjInfo, JsonElementName, Je);
                                    }
                                }
                                break;
                        }
                        break;
                    default:
                        break;
                }

            }
            catch (Exception e)
            {
                Logging.LogError($"Error accessing '{JsonElementName}': Exiting... \n{e.Message}");
                return;
            }
        }

        static void ProcessCoType(DTEntityInfo DtEntity, int Indent = 0)
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

        static void ProcessDtEnum(DTEnumInfo DtEnum, int Indent = 0)
        {
            foreach (var enumValue in DtEnum.EnumValues)
            {
                Logging.LogOutPut(string.Format("{0, 16} : Name  = {1}", "", enumValue.Name));
                Logging.LogOutPut(string.Format("{0, 16} : Value = {1}", "", enumValue.EnumValue));
            }
        }

        static void ProcessDtField(DTFieldInfo DtField, int Indent = 0)
        {
            Logging.LogOutPut("----------------", IndentLevel:Indent);
            Logging.LogOutPut(string.Format("{0, 16} : Name = {1}", "Field", DtField.Name), IndentLevel: Indent);
            Logging.LogOutPut(string.Format("{0, 16} : Display Name = {1}", "", (DtField.DisplayName.Count > 0 ? DtField.DisplayName.FirstOrDefault().Value : "<No Display Name>")), IndentLevel: Indent);
            Logging.LogOutPut(string.Format("{0, 16} : Description = {1}", "", (DtField.Description.Count > 0 ? DtField.Description.FirstOrDefault().Value : "<No Description>")), IndentLevel: Indent);
            Logging.LogOutPut(string.Format("{0, 16} : Schema = {1}", "", DtField.Schema.EntityKind), IndentLevel: Indent);
            if (!String.IsNullOrEmpty(DtField.Comment))
            {
                Logging.LogOutPut(string.Format("{0, 16} : Comment = {1}", "", DtField.Comment), IndentLevel: Indent);
            }

            ProcessCoType(DtField);

            if (DtField.Schema != null)
            {
                ProcessDtSchema(DtField.Schema, Indent + 1);
            }
        }

        static void ProcessDtSchema(DTSchemaInfo DtSchema, int Indent = 0)
        {
            try
            {
                if (!String.IsNullOrEmpty(DtSchema.Comment))
                {
                    Logging.LogOutPut(string.Format("{0, 16} : Comment = {1}", "", DtSchema.Comment), IndentLevel:Indent);
                }

                switch (DtSchema.EntityKind)
                {
                    case DTEntityKind.Object:
                        {
                            DTObjectInfo dtVal = (DTObjectInfo)DtSchema;

                            ProcessCoType(DtSchema);

                            foreach (var field in dtVal.Fields)
                            {
                                ProcessDtField(field, Indent + 1);
                            }
                        }
                        break;

                    case DTEntityKind.Array:
                        {
                            DTArrayInfo dtVal = (DTArrayInfo)DtSchema;
                            ProcessDtSchema(dtVal.ElementSchema, Indent + 1);
                        }
                        break;

                    case DTEntityKind.Enum:
                        {
                            DTEnumInfo dtVal = (DTEnumInfo)DtSchema;
                            ProcessDtEnum(dtVal, Indent + 1);

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

                    Logging.LogOutPut($"Reading simulated data : '{fileInfo.FullName}'");

                    if (!fileInfo.Exists)
                    {
                        Logging.LogError($"{fileInfo.FullName} does not exist: Exiting...");
                        return;
                    }

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

                    Logging.LogSuccess($"Found DTDL ID '{modelId}' in '{fileInfo.FullName}'");
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

            Logging.LogOutPut($"Parsing DTDL in '{dinfo.FullName}' for '{modelId}'");
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
                            ProcessDtSchema(dtVal.Schema, 0);
                        }
                        break;
                    case DTEntityKind.Telemetry:
                        {
                            DTTelemetryInfo dtVal = (DTTelemetryInfo)val;
                            Logging.LogOutPut(string.Format("{0, 16} : {1}", "Name", dtVal.Name));
                            Logging.LogOutPut(string.Format("{0, 16} : {1}", "Schema", dtVal.Schema));
                            ProcessDtSchema(dtVal.Schema, 0);
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
                                ProcessDtSchema(dtVal.Request.Schema, 0);
                            }
                        }
                        break;
                }

                ProcessCoType(val);
            }

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

                Logging.LogOutPutHighLight("----------------");
                Logging.LogOutPutHighLight(string.Format("{0, 16} : {1}", "Name", element.Name));

                DTPropertyInfo propInfo = modelData.Where(r => r.Value.EntityKind == DTEntityKind.Property)
                    .Select(x => x.Value as DTPropertyInfo)
                    .FirstOrDefault(x => x.Name == element.Name);

                if (propInfo != null)
                {
                    switch (element.Value.ValueKind)
                    {
                        case JsonValueKind.Object:
                            ProcessJsonElement(propInfo, element.Name, element.Value);
                            break;

                        case JsonValueKind.Number:
                            ProcessJsonElement(propInfo, element.Name, element.Value);
                            break;

                        default:
                            Logging.LogWarn($"Unsupported JSON Value Kind : {element.Value.ValueKind}");
                            break;
                    }
                }
                else
                {
                    Logging.LogWarn($"Failed to find model for {element.Name}");
                }
            }
        }
    }
}
