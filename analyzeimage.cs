using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace oneclickai
{
    public static class analyzeimage
    {
        // Specify the features to return
        private static readonly List<VisualFeatureTypes> features =
            new List<VisualFeatureTypes>()
        {
            VisualFeatureTypes.Categories, VisualFeatureTypes.Description,
            VisualFeatureTypes.Faces, VisualFeatureTypes.ImageType,
            VisualFeatureTypes.Tags
        };

        [FunctionName("analyzeimage")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            string cognitive_service_key = Environment.GetEnvironmentVariable("cognitive_service_key");
            string cognitive_service_endpoint = Environment.GetEnvironmentVariable("cognitive_service_endpoint");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            string imageURL = data.imageurl;

            dynamic result = new JObject();

            var credentials = new ApiKeyServiceClientCredentials(cognitive_service_key);

            ComputerVisionClient computerVision = new ComputerVisionClient(credentials,
                new System.Net.Http.DelegatingHandler[] { });

            // Specify the Azure region
            computerVision.Endpoint = cognitive_service_endpoint;

            // Analyzing image from remote URL
            var t1 = AnalyzeRemoteAsync(computerVision, imageURL, log);


            return imageURL != null
                ? (ActionResult)new OkObjectResult($"{result.ToString()}")
                : new BadRequestObjectResult("{ \"error\": \"Please pass the text input for the text analytics operations\"");
        }
        private static async Task AnalyzeRemoteAsync(
            ComputerVisionClient computerVision, string imageUrl, ILogger log)
        {
            if (!Uri.IsWellFormedUriString(imageUrl, UriKind.Absolute))
            {
                log.LogError(
                    "\nInvalid remoteImageUrl:\n{0} \n", imageUrl);
                return;
            }

            ImageAnalysis analysis =
                await computerVision.AnalyzeImageAsync(imageUrl, features);
            DisplayResults(analysis, imageUrl, log);
        }

        // Display the most relevant caption for the image
        private static void DisplayResults(ImageAnalysis analysis, string imageUri, ILogger log)
        {
            if (analysis.Description.Captions.Count != 0)
            {
                log.LogInformation(analysis.Description.Captions[0].Text + "\n");
            }
            else
            {
                log.LogInformation("No description generated.");
            }
        }
    }
}
