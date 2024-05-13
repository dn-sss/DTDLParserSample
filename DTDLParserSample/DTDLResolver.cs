using DTDLParser.Models;
using DTDLParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using CommandLine;

namespace DTDLParserSample
{
    internal class DTDLResolver
    {

        public string _modelFolder = string.Empty;
        public DTDLResolver()
        {
        }

        //
        // Reads the contents of Model Definition JSON File 
        //
        public string GetModelContent(string DtmiPath)
        {
            var jsonModel = string.Empty;
            var modelFolder = _modelFolder;
            var filePath = Path.Join(modelFolder, DtmiPath);
            var fileInfo = new FileInfo(filePath);

            try
            {
                if (fileInfo.Exists)
                {
                    Logging.LogSuccess($"Reading {fileInfo.FullName}");
                    StreamReader r = new StreamReader(fileInfo.FullName);
                    jsonModel = r.ReadToEnd();
                    r.Close();
                }
            }
            catch (Exception e)
            {
                Logging.LogError($"Error accessing the target directory '{modelFolder}': \n{e.Message}");
                return jsonModel;
            }

            return jsonModel;
        }

        //
        // Parses DTDL file
        //
        public IReadOnlyDictionary<Dtmi, DTEntityInfo> ParseModel(string Dtmi, string ModelFolder)
        {
            _modelFolder = ModelFolder;
            string modelContent = string.Empty;
            IReadOnlyDictionary<Dtmi, DTEntityInfo>? parseResult = null;

            // for now, just single model
            List<string> modelFileList = new List<string>();

            // Convert to a local path based on Model ID
            string dtmiPath = DtmiToPath(Dtmi);

            if (!string.IsNullOrEmpty(dtmiPath))
            {
                // Read model contents
                modelContent = GetModelContent(dtmiPath);

                if (!string.IsNullOrEmpty(modelContent))
                {
                    modelFileList.Add(modelContent);
                }

                try
                {
                    //
                    // Create Model Parser
                    // For adding custom types, AllowUndefinedExtensions must be specified 'Always'
                    // A function 'Resolver' will be called when model contents require additional parsing
                    //
                    ModelParser parser = new ModelParser(
                        new ParsingOptions()
                        {
                            AllowUndefinedExtensions = WhenToAllow.Always,
                            DtmiResolver = Resolver
                        }
                    );

                    //
                    // Parse DTDL
                    //
                    parseResult = parser.Parse(modelFileList);

                }
                catch (ParsingException pe)
                {
                    Logging.LogError($"*** Error parsing models");
                    foreach (ParsingError err in pe.Errors)
                    {
                        Logging.LogError($"{err.Message}");
                        Logging.LogError($"Primary ID: {err.PrimaryID}");
                        Logging.LogError($"Secondary ID: {err.SecondaryID}");
                        Logging.LogError($"Property: {err.Property}\n");
                    }

                }
                catch (Exception e)
                {
                    Logging.LogError($"Error ParseModel(): {e.Message}");
                }
            }
            return parseResult;
        }

        //
        // A callback function for additional resolution.
        //
        public IEnumerable<string> Resolver(IReadOnlyCollection<Dtmi> Dtmis)
        {
            Dictionary<Dtmi, string> modelDefinitions = new();
            List<string> models = new();

            foreach (var dtmi in Dtmis)
            {
                Logging.LogSuccess($"Parsing DTDL in {_modelFolder} for {dtmi}");
                var dtmiPath = DtmiToPath(dtmi.AbsoluteUri);
                string dtmiContent = GetModelContent(dtmiPath);
                modelDefinitions.Add(dtmi, dtmiContent);
                models.Add(modelDefinitions[dtmi]);
                
            }
            return models;
        }


        static private bool IsValidDtmi(string Dtmi)
        {
            // Regex defined at https://github.com/Azure/digital-twin-model-identifier#validation-regular-expressions
            Regex rx = new Regex(@"^dtmi:[A-Za-z](?:[A-Za-z0-9_]*[A-Za-z0-9])?(?::[A-Za-z](?:[A-Za-z0-9_]*[A-Za-z0-9])?)*;[1-9][0-9]{0,8}$");
            return rx.IsMatch(Dtmi);
        }

        static private string DtmiToPath(string Dtmi)
        {
            if (IsValidDtmi(Dtmi))
            {
                return $"{Dtmi.ToLowerInvariant().Replace(":", "\\").Replace(";", "-")}.json";
            }
            return string.Empty;
        }
    }
}
