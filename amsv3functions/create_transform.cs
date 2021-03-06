//
// Azure Media Services REST API v3 - Functions
//
// create_transform - This function creates Transform in AMS account.
//
//  Input:
//      {
//          "transformName":  "Name of the Transform",
//          "builtInStandardEncoderPreset":
//          {
//              "presetName": "string"  // string (default: AdaptiveStreaming)
//          }
//          "videoAnalyzerPreset":
//          {
//              "audioInsightsOnly": true|false,    // boolean: Whether to only extract audio insights when processing a video file
//              "audioLanguage": "en-US"           // string: The language for the audio payload in the input using the BCP-47 format of 'language tag-region' (e.g: 'en-US').
//
//              // The list of supported languages are:
//              // 'en-US', 'en-GB', 'es-ES', 'es-MX', 'fr-FR', 'it-IT', 'ja-JP', 'pt-BR', 'zh-CN'.
//          }
//      }
//  Output:
//      {
//          "transformId":  "Id of the Transform"
//      }
//

using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.Management.Media;
using Microsoft.Azure.Management.Media.Models;

using Newtonsoft.Json;


namespace amsv3functions
{
    public static class create_transform
    {
        private static Dictionary<string, EncoderNamedPreset> encoderPreset = new Dictionary<string, EncoderNamedPreset>()
        {
            { "AdaptiveStreaming", EncoderNamedPreset.AdaptiveStreaming },
            { "H264MultipleBitrate1080p", EncoderNamedPreset.H264MultipleBitrate1080p },
            { "H264MultipleBitrate720p", EncoderNamedPreset.H264MultipleBitrate720p },
            { "H264MultipleBitrateSD", EncoderNamedPreset.H264MultipleBitrateSD },
            { "AACGoodQualityAudio", EncoderNamedPreset.AACGoodQualityAudio }
        };

        [FunctionName("create_transform")]
        public static async Task<object> Run([HttpTrigger(WebHookType = "genericJson")]HttpRequestMessage req, TraceWriter log)
        {
            log.Info($"AMS v3 Function - create_transform was triggered!");

            string jsonContent = await req.Content.ReadAsStringAsync();
            dynamic data = JsonConvert.DeserializeObject(jsonContent);

            // Validate input objects
            if (data.transformName == null)
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass transformName in the input object" });
            if (data.builtInStandardEncoderPreset == null && data.videoAnalyzerPreset == null)
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass preset in the input object" });
            string transformName = data.transformName;

            MediaServicesConfigWrapper amsconfig = new MediaServicesConfigWrapper();
            string transformId = null;

            try
            {
                IAzureMediaServicesClient client = CreateMediaServicesClient(amsconfig);
                // Does a Transform already exist with the desired name? Assume that an existing Transform with the desired name
                // also uses the same recipe or Preset for processing content.
                Transform transform = client.Transforms.Get(amsconfig.ResourceGroup, amsconfig.AccountName, transformName);

                if (transform == null)
                {
                    // You need to specify what you want it to produce as an output
                    var transformOutputList = new List<TransformOutput>();

                    // BuiltInStandardEncoderPreset
                    if (data.builtInStandardEncoderPreset != null)
                    {
                        EncoderNamedPreset preset = EncoderNamedPreset.AdaptiveStreaming;

                        if (data.builtInStandardEncoderPreset.presetName != null)
                        {
                            string presetName = data.builtInStandardEncoderPreset.presetName;
                            if (encoderPreset.ContainsKey(presetName))
                                preset = encoderPreset[presetName];
                        }

                        TransformOutput encoderTransform = new TransformOutput
                        {
                            // The preset for the Transform is set to one of Media Services built-in sample presets.
                            // You can  customize the encoding settings by changing this to use "StandardEncoderPreset" class.
                            Preset = new BuiltInStandardEncoderPreset()
                            {
                                // This sample uses the built-in encoding preset for Adaptive Bitrate Streaming.
                                PresetName = preset
                            }
                        };
                        transformOutputList.Add(encoderTransform);
                    }

                    // VideoAnalyzerPreset
                    if (data.builtInStandardEncoderPreset != null)
                    {
                        bool audioInsightsOnly = false;
                        string audioLanguage = "en-US";

                        if (data.videoAnalyzerPreset.audioInsightsOnly != null)
                            audioInsightsOnly = data.videoAnalyzerPreset.audioInsightsOnly;
                        if (data.videoAnalyzerPreset.audioLanguage != null)
                            audioLanguage = data.videoAnalyzerPreset.audioLanguage;

                        TransformOutput encoderTransform = new TransformOutput
                        {
                            // The preset for the Transform is set to one of Media Services built-in sample presets.
                            // You can  customize the encoding settings by changing this to use "StandardEncoderPreset" class.
                            Preset = new VideoAnalyzerPreset(audioLanguage, audioInsightsOnly)
                        };
                        transformOutputList.Add(encoderTransform);
                    }

                    // You need to specify what you want it to produce as an output
                    TransformOutput[] output = transformOutputList.ToArray();

                    // Create the Transform with the output defined above
                    transform = client.Transforms.CreateOrUpdate(amsconfig.ResourceGroup, amsconfig.AccountName, transformName, output);
                    transformId = transform.Id;
                }
            }
            catch (ApiErrorException e)
            {
                log.Info($"ERROR: AMS API call failed with error code: {e.Body.Error.Code} and message: {e.Body.Error.Message}");
                return req.CreateResponse(HttpStatusCode.BadRequest, new
                {
                    error = "AMS API call error: " + e.Message
                });
            }


            return req.CreateResponse(HttpStatusCode.OK, new
            {
                transformId = transformId
            });
        }

        private static IAzureMediaServicesClient CreateMediaServicesClient(MediaServicesConfigWrapper config)
        {
            ArmClientCredentials credentials = new ArmClientCredentials(config.serviceClientCredentialsConfig);

            return new AzureMediaServicesClient(config.serviceClientCredentialsConfig.ArmEndpoint, credentials)
            {
                SubscriptionId = config.SubscriptionId,
            };
        }
    }
}
