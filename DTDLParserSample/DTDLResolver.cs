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


        public string GetModelContent(string dtmiPath)
        {
            var jsonModel = string.Empty;
            var modelFolder = _modelFolder;
            var filePath = Path.Join(modelFolder, dtmiPath);
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
                //return Task.FromResult(jsonModel);
            }

            return jsonModel;
        }

        public IReadOnlyDictionary<Dtmi, DTEntityInfo> ParseModel(string dtmi, string modelFolder)
        {
            _modelFolder = modelFolder;
            string modelContent = string.Empty;
            IReadOnlyDictionary<Dtmi, DTEntityInfo>? parseResult = null;

            // for now, just single model
            List<string> modelFileList = new List<string>();

            string dtmiPath = DtmiToPath(dtmi);

            if (!string.IsNullOrEmpty(dtmiPath))
            {
                modelContent = GetModelContent(dtmiPath);

                if (!string.IsNullOrEmpty(modelContent))
                {
                    modelFileList.Add(modelContent);
                }

                //
                // Create Model Parser
                //
                try
                {
                    //
                    // Create Model Parser
                    //
                    ModelParser parser = new ModelParser(
                        new ParsingOptions()
                        {
                            AllowUndefinedExtensions = WhenToAllow.Always,
                            DtmiResolver = Resolver
                        }
                    );

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
                    //foreach (var err in e.Errors)
                    //{
                    //    Logging.LogError($"{err.Message}");
                    //}

                }
            }
            return parseResult;
        }


        public IEnumerable<string> Resolver(IReadOnlyCollection<Dtmi> dtmis)
        //        public async IAsyncEnumerable<string> ParserDtmiResolverAsync(IReadOnlyCollection<Dtmi> dtmis, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Dictionary<Dtmi, string> modelDefinitions = new();
            List<string> models = new();

            foreach (var dtmi in dtmis)
            {
                Logging.LogSuccess($"Parsing DTDL in {_modelFolder} for {dtmi}");
                var dtmiPath = DtmiToPath(dtmi.AbsoluteUri);
                string dtmiContent = GetModelContent(dtmiPath);
                modelDefinitions.Add(dtmi, dtmiContent);
                models.Add(modelDefinitions[dtmi]);
                
            }
            return models;
        }


        static private bool IsValidDtmi(string dtmi)
        {
            // Regex defined at https://github.com/Azure/digital-twin-model-identifier#validation-regular-expressions
            Regex rx = new Regex(@"^dtmi:[A-Za-z](?:[A-Za-z0-9_]*[A-Za-z0-9])?(?::[A-Za-z](?:[A-Za-z0-9_]*[A-Za-z0-9])?)*;[1-9][0-9]{0,8}$");
            return rx.IsMatch(dtmi);
        }

        static private string DtmiToPath(string dtmi)
        {
            if (IsValidDtmi(dtmi))
            {
                return $"{dtmi.ToLowerInvariant().Replace(":", "\\").Replace(";", "-")}.json";
            }
            return string.Empty;
        }
    }
}
